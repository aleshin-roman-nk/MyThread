using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Application.Features.Projects;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JChrono.Tests;

public sealed class ProjectDeletionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleting_project_removes_children_preserves_subject_and_siblings_and_never_reuses_code(bool currentBelongsToDeletedProject)
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        var other = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Оставить")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Удалить")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        int currentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var parent = (await db.Subjects.FindAsync(subject.Id))!;
            var target = (await db.Projects.FindAsync(project.Id))!;
            var sibling = (await db.Projects.FindAsync(other.Id))!;
            target.SetStatus(ProjectStatus.Closed);
            var task = new ProjectTask(target, "Задача", "0001", "Описание");
            task.Update(task.Name, task.Body, ProjectTaskStatus.Closed);
            db.AddRange(task, new ProjectTask(sibling, "Оставить", "0001", "Описание"));
            await db.SaveChangesAsync();
            var date = new DateOnly(2026, 11, 18);
            db.AddRange(Note.ForProject(target.Id, date, TimeOnly.MinValue, "Удалить", Note.DefaultRating), Note.ForProject(sibling.Id, date, TimeOnly.MinValue, "Оставить", Note.DefaultRating), Note.ForSubject(parent.Id, date, TimeOnly.MinValue, "Заметка предмета", Note.DefaultRating));
            var start = DateTimeOffset.UtcNow.AddHours(-1);
            foreach (var session in new[] { new Session(parent, start), new Session(parent, start, project: target), new Session(parent, start, project: target, task: task) })
            {
                session.Close(start.AddMinutes(30)); db.Add(session);
            }
            var current = new Session(parent, DateTimeOffset.UtcNow, project: currentBelongsToDeletedProject ? target : sibling);
            db.Add(current);
            await db.SaveChangesAsync();
            currentId = current.Id;
        }
        using var request = DeleteRequest(project.Id, project.Code);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Equal(subject.Id, (await db.Subjects.SingleAsync()).Id);
            Assert.Equal(other.Id, (await db.Projects.SingleAsync()).Id);
            Assert.Equal(other.Id, (await db.ProjectTasks.SingleAsync()).ProjectId);
            Assert.Equal(other.Id, (await db.Notes.Where(n => n.ProjectId != null).SingleAsync()).ProjectId);
            Assert.Equal("Заметка предмета", (await db.Notes.Where(n => n.SubjectId != null).SingleAsync()).Text);
            var sessions = await db.Sessions.ToListAsync();
            Assert.Equal(currentBelongsToDeletedProject ? 1 : 2, sessions.Count);
            Assert.All(sessions, session => Assert.NotEqual(project.Id, session.ProjectId));
        }
        var currentResponse = await client.GetAsync("/api/sessions/current");
        if (currentBelongsToDeletedProject)
        {
            Assert.Equal(HttpStatusCode.NoContent, currentResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, other.Id))).StatusCode);
        }
        else Assert.Equal(currentId, (await currentResponse.Content.ReadFromJsonAsync<SessionDto>())!.Id);
        var next = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Новый")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("0003", next.Code);
    }

    [Fact]
    public async Task Project_deletion_requires_exact_confirmation_and_rolls_back_children_when_deletion_fails()
    {
        using var factory = new NotesFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        int id;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Проект", "0001");
            var task = new ProjectTask(project, "Задача", "0001", "Описание");
            db.AddRange(subject, project, task);
            await db.SaveChangesAsync(); id = project.Id;
            db.AddRange(Note.ForProject(project.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Заметка", Note.DefaultRating),
                new Session(subject, DateTimeOffset.UtcNow, project: project, task: task));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER Test_PreventProjectDeletion BEFORE DELETE ON Projects BEGIN SELECT RAISE(ABORT, 'Test failure'); END;");
        }
        foreach (var code in new[] { "", "0002", " 0001 ", "1" })
        {
            using var request = DeleteRequest(id, code);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }
        using var missing = DeleteRequest(int.MaxValue, "0001");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missing)).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DeleteProject>();
            await Assert.ThrowsAsync<SqliteException>(() => handler.Execute(id, new DeleteProjectRequest("0001"), CancellationToken.None));
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Single(await db.Projects.ToListAsync());
            Assert.Single(await db.ProjectTasks.ToListAsync());
            Assert.Single(await db.Notes.Where(n => n.ProjectId != null).ToListAsync());
            Assert.Single(await db.Sessions.ToListAsync());
        }
    }

    private static HttpRequestMessage DeleteRequest(int id, string code) => new(HttpMethod.Delete, $"/api/projects/{id}")
    {
        Content = JsonContent.Create(new DeleteProjectRequest(code))
    };
}

using System.Net;
using System.Net.Http.Json;
using DailyNotes.Application;
using DailyNotes.Domain;
using DailyNotes.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DailyNotes.Tests;

public sealed class SubjectDeletionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleting_subject_removes_its_entire_tree_and_preserves_other_subject(bool currentBelongsToDeletedSubject)
    {
        using var factory = new NotesFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        int subjectId, otherId, currentId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Удаляемый", "DEL");
            var other = new Subject("Сохраняемый", "KEEP");
            var project = new Project(subject, "Архивный проект", "0001");
            project.SetStatus(ProjectStatus.Closed);
            var task = new ProjectTask(project, "Архивная задача", "0001", "Описание");
            task.Update(task.Name, task.Body, ProjectTaskStatus.Closed);
            var otherProject = new Project(other, "Другой проект", "0001");
            var otherTask = new ProjectTask(otherProject, "Другая задача", "0001", "Описание");
            db.AddRange(subject, other, project, task, otherProject, otherTask);
            await db.SaveChangesAsync();
            subjectId = subject.Id; otherId = other.Id;
            db.AddRange(Note.ForProject(project.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Удалить заметку проекта", Note.DefaultRating),
                Note.ForProject(otherProject.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Оставить заметку проекта", Note.DefaultRating));
            var start = DateTimeOffset.UtcNow.AddHours(-2);
            var sessions = new[] {
                new Session(subject, start, "Предмет"),
                new Session(subject, start, "Проект", project),
                new Session(subject, start, "Задача", project, task),
                new Session(other, start, "Другая сессия", otherProject, otherTask)
            };
            foreach (var session in sessions) session.Close(start.AddHours(1));
            var current = currentBelongsToDeletedSubject
                ? new Session(subject, DateTimeOffset.UtcNow, project: project, task: task)
                : new Session(other, DateTimeOffset.UtcNow, project: otherProject, task: otherTask);
            db.AddRange(sessions);
            db.Add(current);
            db.AddRange(Note.ForSubject(subject.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Удалить", Note.DefaultRating),
                Note.ForSubject(other.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Оставить", Note.DefaultRating));
            await db.SaveChangesAsync();
            currentId = current.Id;
        }

        using var request = DeleteRequest(subjectId, "DEL");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/subjects/{subjectId}")).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Equal(otherId, (await db.Subjects.SingleAsync()).Id);
            Assert.Equal(otherId, (await db.Projects.SingleAsync()).SubjectId);
            Assert.Single(await db.ProjectTasks.ToListAsync());
            Assert.Equal("Оставить заметку проекта", (await db.Notes.Where(n => n.ProjectId != null).SingleAsync()).Text);
            Assert.Equal(otherId, (await db.Notes.Where(n => n.SubjectId != null).SingleAsync()).SubjectId);
            var sessions = await db.Sessions.ToListAsync();
            Assert.Equal(currentBelongsToDeletedSubject ? 1 : 2, sessions.Count);
            Assert.All(sessions, session => Assert.Equal(otherId, session.SubjectId));
        }
        var currentResponse = await client.GetAsync("/api/sessions/current");
        if (currentBelongsToDeletedSubject)
        {
            Assert.Equal(HttpStatusCode.NoContent, currentResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(otherId))).StatusCode);
        }
        else
            Assert.Equal(currentId, (await currentResponse.Content.ReadFromJsonAsync<SessionDto>())!.Id);
    }

    [Fact]
    public async Task Deletion_requires_exact_confirmation_and_unknown_subject_returns_not_found()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Предмет", "DEL")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        foreach (var confirmation in new[] { "", "del", " DEL ", "OTHER" })
        {
            using var request = DeleteRequest(subject.Id, confirmation);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
            Assert.Equal(subject, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"));
        }
        using var missing = DeleteRequest(int.MaxValue, "DEL");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missing)).StatusCode);
    }

    [Fact]
    public async Task Failure_during_cascade_rolls_back_all_prior_deletes()
    {
        using var factory = new NotesFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        int id;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Откат", "BACK");
            var project = new Project(subject, "Проект", "0001");
            var task = new ProjectTask(project, "Задача", "0001", "Описание");
            db.AddRange(subject, project, task);
            await db.SaveChangesAsync();
            id = subject.Id;
            db.Add(Note.ForProject(project.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Заметка проекта", Note.DefaultRating));
            db.AddRange(Note.ForSubject(id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Заметка", Note.DefaultRating),
                new Session(subject, DateTimeOffset.UtcNow, project: project, task: task));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER Test_PreventProjectDeletion BEFORE DELETE ON Projects BEGIN SELECT RAISE(ABORT, 'Test failure'); END;");
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<INotesRepository>();
            await Assert.ThrowsAsync<SqliteException>(() => repository.DeleteSubject(id, "BACK", CancellationToken.None));
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Single(await db.Subjects.ToListAsync());
            Assert.Single(await db.Projects.ToListAsync());
            Assert.Single(await db.ProjectTasks.ToListAsync());
            Assert.Single(await db.Notes.Where(n => n.SubjectId != null).ToListAsync());
            Assert.Single(await db.Notes.Where(n => n.ProjectId != null).ToListAsync());
            Assert.Single(await db.Sessions.ToListAsync());
        }
    }

    private static HttpRequestMessage DeleteRequest(int id, string confirmation) => new(HttpMethod.Delete, $"/api/subjects/{id}")
    {
        Content = JsonContent.Create(new DeleteSubjectRequest(confirmation))
    };
}

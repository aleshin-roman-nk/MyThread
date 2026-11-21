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

public sealed class TaskDeletionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleting_task_removes_only_its_sessions_preserves_parents_and_does_not_reuse_its_code(bool currentBelongsToDeletedTask)
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Практика")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var url = $"/api/projects/{project.Id}/tasks";
        var other = (await (await client.PostAsJsonAsync(url, new CreateProjectTaskRequest("Оставить", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var task = (await (await client.PostAsJsonAsync(url, new CreateProjectTaskRequest("Удалить", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        int currentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var parent = (await db.Subjects.FindAsync(subject.Id))!;
            var parentProject = (await db.Projects.FindAsync(project.Id))!;
            var target = (await db.ProjectTasks.FindAsync(task.Id))!;
            var sibling = (await db.ProjectTasks.FindAsync(other.Id))!;
            target.Update(target.Name, target.Body, ProjectTaskStatus.Closed);
            var date = new DateOnly(2026, 11, 18);
            db.AddRange(Note.ForSubject(parent.Id, date, TimeOnly.MinValue, "Заметка предмета", Note.DefaultRating), Note.ForProject(parentProject.Id, date, TimeOnly.MinValue, "Заметка проекта", Note.DefaultRating));
            var start = DateTimeOffset.UtcNow.AddHours(-1);
            foreach (var session in new[] { new Session(parent, start), new Session(parent, start, project: parentProject),
                new Session(parent, start, project: parentProject, task: target), new Session(parent, start, project: parentProject, task: sibling) })
            {
                session.Close(start.AddMinutes(30)); db.Add(session);
            }
            var current = new Session(parent, DateTimeOffset.UtcNow, project: parentProject, task: currentBelongsToDeletedTask ? target : sibling);
            db.Add(current); await db.SaveChangesAsync(); currentId = current.Id;
        }
        using var request = DeleteRequest(project.Id, task.Id, task.Code);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Equal(subject.Id, (await db.Subjects.SingleAsync()).Id);
            Assert.Equal(project.Id, (await db.Projects.SingleAsync()).Id);
            Assert.Equal(other.Id, (await db.ProjectTasks.SingleAsync()).Id);
            Assert.Single(await db.Notes.Where(n => n.SubjectId != null).ToListAsync());
            Assert.Single(await db.Notes.Where(n => n.ProjectId != null).ToListAsync());
            var sessions = await db.Sessions.ToListAsync();
            Assert.Equal(currentBelongsToDeletedTask ? 3 : 4, sessions.Count);
            Assert.All(sessions, session => Assert.NotEqual(task.Id, session.TaskId));
        }
        var currentResponse = await client.GetAsync("/api/sessions/current");
        if (currentBelongsToDeletedTask)
        {
            Assert.Equal(HttpStatusCode.NoContent, currentResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, project.Id, other.Id))).StatusCode);
        }
        else Assert.Equal(currentId, (await currentResponse.Content.ReadFromJsonAsync<SessionDto>())!.Id);
        var next = (await (await client.PostAsJsonAsync(url, new CreateProjectTaskRequest("Новая", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        Assert.Equal("0003", next.Code);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(DeleteRequest(project.Id, task.Id, task.Code))).StatusCode);
    }

    [Fact]
    public async Task Task_deletion_validates_parent_and_confirmation_and_rolls_back_sessions_on_failure()
    {
        using var factory = new NotesFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        int projectId, taskId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Проект", "0001");
            var task = new ProjectTask(project, "Задача", "0001", "Описание");
            db.AddRange(subject, project, task); await db.SaveChangesAsync();
            projectId = project.Id; taskId = task.Id;
            db.Add(Note.ForTask(task.Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Заметка задачи", Note.DefaultRating));
            db.Add(new Session(subject, DateTimeOffset.UtcNow, project: project, task: task));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER Test_PreventTaskDeletion BEFORE DELETE ON ProjectTasks BEGIN SELECT RAISE(ABORT, 'Test failure'); END;");
        }
        foreach (var code in new[] { "", "1", "0002", " 0001 " })
        {
            using var request = DeleteRequest(projectId, taskId, code);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }
        using var wrongProject = DeleteRequest(int.MaxValue, taskId, "0001");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(wrongProject)).StatusCode);
        using var missing = DeleteRequest(projectId, int.MaxValue, "0001");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missing)).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<SqliteException>(() => scope.ServiceProvider.GetRequiredService<INotesRepository>()
                .DeleteProjectTask(projectId, taskId, "0001", CancellationToken.None));
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            Assert.Single(await db.ProjectTasks.ToListAsync());
            Assert.Single(await db.Notes.Where(n => n.TaskId != null).ToListAsync());
            Assert.Single(await db.Sessions.ToListAsync());
        }
    }

    private static HttpRequestMessage DeleteRequest(int projectId, int id, string code) => new(HttpMethod.Delete, $"/api/projects/{projectId}/tasks/{id}")
    {
        Content = JsonContent.Create(new DeleteTaskRequest(code))
    };
}

using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JChrono.Tests;

public sealed class TaskNoteTests
{
    [Fact]
    public async Task Task_notes_support_crud_validate_input_and_reject_other_tasks_and_projects()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        int subjectId, projectId, taskId, siblingId, otherProjectId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Проект", "0001");
            var other = new Project(subject, "Другой проект", "0002");
            var task = new ProjectTask(project, "Задача", "0001", "Описание");
            var sibling = new ProjectTask(project, "Другая задача", "0002", "Описание");
            db.AddRange(subject, project, other, task, sibling); await db.SaveChangesAsync();
            subjectId = subject.Id; projectId = project.Id; taskId = task.Id; siblingId = sibling.Id; otherProjectId = other.Id;
        }
        var url = $"/api/projects/{projectId}/tasks/{taskId}/notes";
        var siblingUrl = $"/api/projects/{projectId}/tasks/{siblingId}/notes";
        var wrongProjectUrl = $"/api/projects/{otherProjectId}/tasks/{taskId}/notes";
        var date = new DateOnly(2026, 11, 19);
        Assert.Empty((await client.GetFromJsonAsync<List<TaskNoteDto>>(url))!);
        var created = await client.PostAsJsonAsync(url, new SaveTaskNoteRequest(date, "  Идея задачи  "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(url, created.Headers.Location?.OriginalString);
        var note = (await created.Content.ReadFromJsonAsync<TaskNoteDto>())!;
        Assert.Equal(taskId, note.TaskId);
        Assert.Equal("Идея задачи", note.Text);
        Assert.Equal(TimeOnly.MinValue, note.Time);
        Assert.Equal(Note.DefaultRating, note.Rating);
        using var reconnected = factory.CreateClient();
        Assert.Equal(note, Assert.Single((await reconnected.GetFromJsonAsync<List<TaskNoteDto>>(created.Headers.Location))!));
        Assert.Empty((await client.GetFromJsonAsync<List<TaskNoteDto>>(siblingUrl))!);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectNoteDto>>($"/api/projects/{projectId}/notes"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<NoteDto>>($"/api/subjects/{subjectId}/notes"))!);
        foreach (var request in new[] { new SaveTaskNoteRequest(default, "Текст"), new SaveTaskNoteRequest(date, " \n "), new SaveTaskNoteRequest(date, new string('я', 4001)) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, request)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(url + "/" + note.Id, request)).StatusCode);
            Assert.Equal(note, Assert.Single((await client.GetFromJsonAsync<List<TaskNoteDto>>(url))!));
        }
        var time = new TimeOnly(14, 30);
        const int rating = 5;
        using var metadataUpdate = await client.PutAsJsonAsync(url + "/" + note.Id,
            new SaveTaskNoteRequest(date, note.Text, time, rating));
        Assert.Equal(HttpStatusCode.OK, metadataUpdate.StatusCode);
        var valid = new SaveTaskNoteRequest(date.AddDays(-1), new string('я', 4000));
        var edited = await client.PutAsJsonAsync(url + "/" + note.Id, valid);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var updated = (await edited.Content.ReadFromJsonAsync<TaskNoteDto>())!;
        Assert.Equal(valid.Date, updated.Date); Assert.Equal(valid.Text, updated.Text);
        Assert.Equal(time, updated.Time);
        Assert.Equal(rating, updated.Rating);
        foreach (var wrongUrl in new[] { siblingUrl, wrongProjectUrl })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(wrongUrl + "/" + note.Id, valid)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(wrongUrl + "/" + note.Id)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(wrongProjectUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(wrongProjectUrl, valid)).StatusCode);
        var latest = (await (await client.PostAsJsonAsync(url, new SaveTaskNoteRequest(date, "Свежая"))).Content.ReadFromJsonAsync<TaskNoteDto>())!;
        Assert.Equal(new[] { latest.Id, note.Id }, (await client.GetFromJsonAsync<List<TaskNoteDto>>(url))!.Select(n => n.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url + "/" + note.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url + "/" + note.Id)).StatusCode);
        Assert.Equal(latest, Assert.Single((await reconnected.GetFromJsonAsync<List<TaskNoteDto>>(url))!));
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("project")]
    [InlineData("task")]
    public async Task Cascading_deletion_removes_task_notes_and_preserves_unrelated_notes(string level)
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        int subjectId, projectId, taskId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Удаляемый", "DEL");
            var otherSubject = new Subject("Оставить", "KEEP");
            var project = new Project(subject, "Проект", "0001");
            var otherProject = new Project(otherSubject, "Оставить", "0001");
            var task = new ProjectTask(project, "Задача", "0001", "Описание");
            var otherTask = new ProjectTask(otherProject, "Оставить", "0001", "Описание");
            db.AddRange(task, otherTask); await db.SaveChangesAsync();
            subjectId = subject.Id; projectId = project.Id; taskId = task.Id;
            db.AddRange(Note.ForTask(task.Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Удалить", Note.DefaultRating), Note.ForTask(otherTask.Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Оставить", Note.DefaultRating));
            await db.SaveChangesAsync();
        }
        var url = level switch { "subject" => $"/api/subjects/{subjectId}", "project" => $"/api/projects/{projectId}", _ => $"/api/projects/{projectId}/tasks/{taskId}" };
        using var request = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = level == "subject" ? JsonContent.Create(new DeleteSubjectRequest("DEL")) : JsonContent.Create(new DeleteTaskRequest("0001"))
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal("Оставить", (await check.ServiceProvider.GetRequiredService<NotesDbContext>().Notes.Where(n => n.TaskId != null).SingleAsync()).Text);
    }

    [Fact]
    public async Task Existing_database_gains_task_notes_and_repeat_upgrade_preserves_all_notes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Проект", "0001");
            db.Add(new ProjectTask(project, "Задача", "0001", "Описание"));
            await db.SaveChangesAsync();
            db.AddRange(Note.ForSubject(subject.Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Предмет", Note.DefaultRating), Note.ForProject(project.Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Проект", Note.DefaultRating));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE SubjectNotes AS SELECT Id, SubjectId, Date, Text FROM Note WHERE SubjectId IS NOT NULL; CREATE TABLE ProjectNotes AS SELECT Id, ProjectId, Date, Text FROM Note WHERE ProjectId IS NOT NULL; DELETE FROM Note;");
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            db.Add(Note.ForTask((await db.ProjectTasks.SingleAsync()).Id, new DateOnly(2026, 11, 19), TimeOnly.MinValue, "Задача", Note.DefaultRating));
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            Assert.Equal("Предмет", (await db.Notes.Where(n => n.SubjectId != null).SingleAsync()).Text);
            Assert.Equal("Проект", (await db.Notes.Where(n => n.ProjectId != null).SingleAsync()).Text);
            Assert.Equal("Задача", (await db.Notes.SingleAsync(n => n.TaskId != null)).Text);
        }
    }
}

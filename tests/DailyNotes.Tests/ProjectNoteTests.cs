using System.Net;
using System.Net.Http.Json;
using DailyNotes.Application;
using DailyNotes.Domain;
using DailyNotes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DailyNotes.Tests;

public sealed class ProjectNoteTests
{
    [Fact]
    public async Task Project_notes_support_crud_validate_text_and_date_and_remain_scoped_to_the_project()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Практика")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var other = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Другой")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var url = $"/api/projects/{project.Id}/notes";
        var otherUrl = $"/api/projects/{other.Id}/notes";
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectNoteDto>>(url))!);
        var date = new DateOnly(2026, 11, 18);
        var created = await client.PostAsJsonAsync(url, new SaveProjectNoteRequest(date, "  Идея проекта  "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = (await created.Content.ReadFromJsonAsync<ProjectNoteDto>())!;
        Assert.Equal(project.Id, note.ProjectId);
        Assert.Equal("Идея проекта", note.Text);
        using var reconnected = factory.CreateClient();
        Assert.Equal(note, Assert.Single((await reconnected.GetFromJsonAsync<List<ProjectNoteDto>>(url))!));
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectNoteDto>>(otherUrl))!);
        Assert.Empty((await client.GetFromJsonAsync<List<NoteDto>>($"/api/subjects/{subject.Id}/notes"))!);

        foreach (var request in new[] { new SaveProjectNoteRequest(default, "Текст"), new SaveProjectNoteRequest(date, "  "), new SaveProjectNoteRequest(date, new string('я', 4001)) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, request)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(url + "/" + note.Id, request)).StatusCode);
            Assert.Equal(note, Assert.Single((await client.GetFromJsonAsync<List<ProjectNoteDto>>(url))!));
        }
        var updatedResponse = await client.PutAsJsonAsync(url + "/" + note.Id, new SaveProjectNoteRequest(date.AddDays(-1), new string('я', 4000)));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<ProjectNoteDto>())!;
        Assert.Equal(date.AddDays(-1), updated.Date);
        Assert.Equal(4000, updated.Text.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(otherUrl + "/" + note.Id, new SaveProjectNoteRequest(date, "Подмена"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(otherUrl + "/" + note.Id)).StatusCode);
        Assert.Equal(updated, Assert.Single((await client.GetFromJsonAsync<List<ProjectNoteDto>>(url))!));
        var latest = (await (await client.PostAsJsonAsync(url, new SaveProjectNoteRequest(date, "Свежая заметка")))
            .Content.ReadFromJsonAsync<ProjectNoteDto>())!;
        Assert.Equal(new[] { latest.Id, note.Id }, (await client.GetFromJsonAsync<List<ProjectNoteDto>>(url))!.Select(n => n.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url + "/" + note.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url + "/" + note.Id)).StatusCode);
        Assert.Equal(latest, Assert.Single((await reconnected.GetFromJsonAsync<List<ProjectNoteDto>>(url))!));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/999999/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/projects/999999/notes", new SaveProjectNoteRequest(date, "Текст"))).StatusCode);
    }

    [Fact]
    public async Task Existing_database_gains_project_notes_without_losing_data_and_upgrade_is_repeatable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var subject = new Subject("Разработка", "DEV");
            db.Add(new Project(subject, "Проект", "0001"));
            await db.SaveChangesAsync();
            db.Add(Note.ForSubject(subject.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Заметка предмета", Note.DefaultRating));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE SubjectNotes AS SELECT Id, SubjectId, Date, Text FROM Note; DELETE FROM Note;");
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var project = await db.Projects.SingleAsync();
            db.Add(Note.ForProject(project.Id, new DateOnly(2026, 11, 18), TimeOnly.MinValue, "Заметка проекта", Note.DefaultRating));
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            Assert.Equal("Заметка предмета", (await db.Notes.Where(n => n.SubjectId != null).SingleAsync()).Text);
            Assert.Equal("Заметка проекта", (await db.Notes.Where(n => n.ProjectId != null).SingleAsync()).Text);
            Assert.Equal((await db.Projects.SingleAsync()).Id, (await db.Notes.SingleAsync(n => n.ProjectId != null)).ProjectId);
        }
    }
}

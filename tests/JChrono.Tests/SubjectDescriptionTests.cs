using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JChrono.Tests;

public sealed class SubjectDescriptionTests
{
    [Fact]
    public async Task Markdown_description_is_saved_returned_updated_and_cleared()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        const string markdown = "# План\n> **Изучать** каждый день\n---\n1. Практика";
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Английский", "ENG", "  " + markdown + "  "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var subject = (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
        Assert.Equal(markdown, subject.Description);
        Assert.Equal(new Uri(client.BaseAddress!, $"/api/subjects/{subject.Id}"), response.Headers.Location);
        using var reconnected = factory.CreateClient();
        Assert.Equal(markdown, (await reconnected.GetFromJsonAsync<SubjectDto>(response.Headers.Location))!.Description);
        Assert.Equal(markdown, Assert.Single((await client.GetFromJsonAsync<List<SubjectDto>>("/api/subjects"))!).Description);
        var description = new string('x', Subject.MaxDescriptionLength);
        response = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("Английский", "ENG", description));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(description, (await response.Content.ReadFromJsonAsync<SubjectDto>())!.Description);
        response = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("Правка", "ENG", description + "x"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = (await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"))!;
        Assert.Equal(description, unchanged.Description);
        Assert.Equal("Английский", unchanged.Name);
        response = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("Английский", "ENG", "   "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", (await response.Content.ReadFromJsonAsync<SubjectDto>())!.Description);
        response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Ошибка", "BAD", description + "x"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<SubjectDto>>("/api/subjects"))!);
    }

    [Fact]
    public async Task Legacy_subject_gets_empty_description_and_repeat_upgrade_preserves_edits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Subjects.Add(new Subject("Предмет", "S"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects DROP COLUMN Description;");
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var subject = await db.Subjects.SingleAsync();
            Assert.Equal("", subject.Description);
            subject.Update(subject.Name, subject.ShortName, "## Описание");
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var subject = await db.Subjects.SingleAsync();
            Assert.Equal("## Описание", subject.Description);
            Assert.Equal("Предмет", subject.Name);
            Assert.Equal("S", subject.ShortName);
        }
    }
}

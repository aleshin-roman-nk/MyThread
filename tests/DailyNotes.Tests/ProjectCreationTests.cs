using System.Net;
using System.Net.Http.Json;
using DailyNotes.Application;
using DailyNotes.Domain;
using DailyNotes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DailyNotes.Tests;

public sealed class ProjectCreationTests
{
    [Fact]
    public async Task Creation_time_is_assigned_by_server_persisted_and_preserved_when_project_is_edited()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new { name = "Проект", createdAt = "2000-01-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.NotNull(project.CreatedAt);
        Assert.InRange(project.CreatedAt.Value, before, DateTimeOffset.UtcNow);
        using var reconnected = factory.CreateClient();
        Assert.Equal(project.CreatedAt, (await reconnected.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.CreatedAt);
        Assert.Equal(project.CreatedAt, Assert.Single((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!).CreatedAt);
        response = await client.PutAsJsonAsync($"/api/projects/{project.Id}", new { name = "Правка", status = 1, description = "Описание", createdAt = "2001-01-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(project.CreatedAt, (await response.Content.ReadFromJsonAsync<ProjectDto>())!.CreatedAt);
    }

    [Fact]
    public async Task Upgrade_keeps_legacy_creation_time_unknown_and_preserves_dates_of_new_projects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(new Project(new Subject("Разработка", "DEV"), "Старый проект", "0001"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects DROP COLUMN CreatedAt;");
        }
        DateTimeOffset? createdAt;
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var old = await db.Projects.SingleAsync();
            Assert.Null(old.CreatedAt);
            old.Update("Правка старого", ProjectStatus.Deferred);
            var project = new Project(await db.Subjects.SingleAsync(), "Новый проект", "0002");
            db.Add(project); await db.SaveChangesAsync(); createdAt = project.CreatedAt;
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            Assert.Null((await db.Projects.SingleAsync(p => p.Code == "0001")).CreatedAt);
            Assert.NotNull(createdAt);
            Assert.Equal(createdAt, (await db.Projects.SingleAsync(p => p.Code == "0002")).CreatedAt);
        }
    }
}

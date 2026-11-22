using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JChrono.Tests;

public sealed class TaskCreationTests
{
    [Fact]
    public async Task Task_creation_time_is_assigned_by_server_and_preserved_during_edits()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = (await (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV")))
            .Content.ReadFromJsonAsync<SubjectDto>())!;
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Проект")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var url = $"/api/projects/{project.Id}/tasks";
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync(url, new { name = "Задача", body = "Описание", createdAt = "2000-01-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = (await response.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        Assert.NotNull(task.CreatedAt);
        Assert.InRange(task.CreatedAt.Value, before, DateTimeOffset.UtcNow);
        using var reconnected = factory.CreateClient();
        Assert.Equal(task.CreatedAt, Assert.Single((await reconnected.GetFromJsonAsync<List<ProjectTaskDto>>(url))!).CreatedAt);
        Assert.Equal(task.CreatedAt, Assert.Single((await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.Tasks).CreatedAt);
        response = await client.PutAsJsonAsync(url + "/" + task.Id, new { name = "Правка", body = "Новое описание", status = 1, createdAt = "2001-01-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(task.CreatedAt, (await response.Content.ReadFromJsonAsync<ProjectTaskDto>())!.CreatedAt);
    }

    [Fact]
    public async Task Upgrade_keeps_old_task_dates_unknown_and_new_task_dates_unchanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var project = new Project(new Subject("Разработка", "DEV"), "Проект", "0001");
            db.Add(new ProjectTask(project, "Старая задача", "0001", "Описание"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE ProjectTasks DROP COLUMN CreatedAt;");
        }
        DateTimeOffset? createdAt;
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var old = await db.ProjectTasks.SingleAsync();
            Assert.Null(old.CreatedAt);
            old.Update("Правка", old.Body, ProjectTaskStatus.Deferred);
            var task = new ProjectTask(await db.Projects.SingleAsync(), "Новая задача", "0002", "Описание");
            db.Add(task); await db.SaveChangesAsync(); createdAt = task.CreatedAt;
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            Assert.Null((await db.ProjectTasks.SingleAsync(t => t.Code == "0001")).CreatedAt);
            Assert.NotNull(createdAt);
            Assert.Equal(createdAt, (await db.ProjectTasks.SingleAsync(t => t.Code == "0002")).CreatedAt);
        }
    }
}

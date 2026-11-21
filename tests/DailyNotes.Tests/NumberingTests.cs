using System.Net;
using System.Net.Http.Json;
using DailyNotes.Application;
using DailyNotes.Domain;
using DailyNotes.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DailyNotes.Tests;

public sealed class NumberingTests
{
    [Fact]
    public async Task Api_numbers_are_scoped_to_parent_and_cannot_be_set_or_changed_by_client()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var subject = await Subject(client, "Разработка");
        var other = await Subject(client, "Английский");
        var first = await Project(client, subject.Id);
        var second = await Project(client, subject.Id);
        Assert.Equal("0001", first.Code);
        Assert.Equal("0002", second.Code);
        Assert.Equal("0001", (await Project(client, other.Id)).Code);
        var task = await Task(client, first.Id);
        Assert.Equal("0001", task.Code);
        Assert.Equal("0002", (await Task(client, first.Id)).Code);
        Assert.Equal("0001", (await Task(client, second.Id)).Code);
        var updated = await client.PutAsJsonAsync($"/api/projects/{first.Id}", new { name = "Новое имя", code = "CUSTOM", status = 2, description = "Описание" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(first.Code, (await updated.Content.ReadFromJsonAsync<ProjectDto>())!.Code);
        var updatedTask = await client.PutAsJsonAsync($"/api/projects/{first.Id}/tasks/{task.Id}", new { name = "Новое имя", code = "CUSTOM", body = "Описание", status = 2 });
        Assert.Equal(HttpStatusCode.OK, updatedTask.StatusCode);
        Assert.Equal(task.Code, (await updatedTask.Content.ReadFromJsonAsync<ProjectTaskDto>())!.Code);
        Assert.Equal("0003", (await Project(client, subject.Id)).Code);
        Assert.Equal("0003", (await Task(client, first.Id)).Code);
    }

    [Fact]
    public async Task Concurrent_requests_receive_distinct_consecutive_project_and_task_numbers()
    {
        using var factory = new NotesFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        var subject = await Subject(client, "Разработка");
        var projects = await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Project(client, subject.Id)));
        Assert.Equal(Enumerable.Range(1, 8).Select(number => GeneratedCode.Format(number)), projects.Select(p => p.Code).Order());
        var tasks = await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task(client, projects[0].Id)));
        Assert.Equal(Enumerable.Range(1, 8).Select(number => GeneratedCode.Format(number)), tasks.Select(t => t.Code).Order());
    }

    [Fact]
    public async Task Deleting_highest_numbers_and_reinitializing_database_does_not_reuse_them()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            db.Subjects.Add(new Subject("Разработка", "DEV"));
            await db.SaveChangesAsync();
            var repository = new NotesRepository(db);
            var first = await repository.CreateProject(1, "Первый", "", default);
            var second = await repository.CreateProject(1, "Ошибочный", "", default);
            db.Projects.Remove(second);
            await db.SaveChangesAsync();
            await repository.CreateProjectTask(first.Id, "Первая", "Описание", default);
            var task = await repository.CreateProjectTask(first.Id, "Ошибочная", "Описание", default);
            db.ProjectTasks.Remove(task);
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var repository = new NotesRepository(db);
            var next = await repository.CreateProject(1, "Следующий", "", default);
            Assert.Equal("0003", next.Code);
            Assert.Equal("0003", (await repository.CreateProjectTask(1, "Следующая", "Описание", default)).Code);
            Assert.Equal(3, (await db.Subjects.SingleAsync()).LastProjectNumber);
            Assert.Equal(3, (await db.Projects.SingleAsync(p => p.Id == 1)).LastTaskNumber);
        }
    }

    [Fact]
    public async Task Invalid_creation_rolls_back_reserved_number_and_codes_grow_past_four_digits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            db.Subjects.Add(new Subject("Разработка", "DEV"));
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => new NotesRepository(db).CreateProject(1, " ", "", default));
        }
        await using (var db = new NotesDbContext(options))
        {
            Assert.Equal(0, (await db.Subjects.SingleAsync()).LastProjectNumber);
            var project = await new NotesRepository(db).CreateProject(1, "Проект", "", default);
            Assert.Equal("0001", project.Code);
            await Assert.ThrowsAsync<ArgumentException>(() => new NotesRepository(db).CreateProjectTask(project.Id, "Задача", " ", default));
        }
        await using (var db = new NotesDbContext(options))
        {
            Assert.Equal(0, (await db.Projects.SingleAsync()).LastTaskNumber);
            Assert.Equal("0001", (await new NotesRepository(db).CreateProjectTask(1, "Задача", "Описание", default)).Code);
            await db.Database.ExecuteSqlRawAsync("UPDATE Subjects SET LastProjectNumber = 9999; UPDATE Projects SET LastTaskNumber = 9999;");
            Assert.Equal("10000", (await new NotesRepository(db).CreateProject(1, "Большой номер", "", default)).Code);
            Assert.Equal("10000", (await new NotesRepository(db).CreateProjectTask(1, "Большой номер", "Описание", default)).Code);
            await db.Database.ExecuteSqlRawAsync("UPDATE Subjects SET LastProjectNumber = 99999999; UPDATE Projects SET LastTaskNumber = 99999999;");
            await Assert.ThrowsAsync<ConflictException>(() => new NotesRepository(db).CreateProject(1, "Предел", "", default));
            await Assert.ThrowsAsync<ConflictException>(() => new NotesRepository(db).CreateProjectTask(1, "Предел", "Описание", default));
            Assert.Equal(2, await db.Projects.CountAsync());
            Assert.Equal(2, await db.ProjectTasks.CountAsync());
        }
    }

    [Fact]
    public async Task Upgrade_seeds_parent_counters_from_numeric_codes_and_preserves_legacy_codes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Старый проект", "0042");
            db.ProjectTasks.Add(new ProjectTask(project, "Старая задача", "0012", "Описание"));
            db.Projects.Add(new Project(subject, "Именованный", "Conspect"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects DROP COLUMN LastProjectNumber; ALTER TABLE Projects DROP COLUMN LastTaskNumber;");
        }
        await using var check = new NotesDbContext(options);
        await DatabaseInitializer.Initialize(check);
        await DatabaseInitializer.Initialize(check);
        var old = await check.Projects.SingleAsync(p => p.Code == "0042");
        Assert.Equal(12, old.LastTaskNumber);
        Assert.Equal("0012", (await check.ProjectTasks.SingleAsync()).Code);
        Assert.True(await check.Projects.AnyAsync(p => p.Code == "Conspect"));
        var repository = new NotesRepository(check);
        Assert.Equal("0043", (await repository.CreateProject(old.SubjectId, "Новый", "", default)).Code);
        Assert.Equal("0013", (await repository.CreateProjectTask(old.Id, "Новая", "Описание", default)).Code);
    }

    private static async Task<SubjectDto> Subject(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(name, "TEST"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
    }

    private static async Task<ProjectDto> Project(HttpClient client, int subjectId)
    {
        var response = await client.PostAsJsonAsync($"/api/subjects/{subjectId}/projects", new { name = "Проект", code = "CUSTOM" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private static async Task<ProjectTaskDto> Task(HttpClient client, int projectId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Задача", code = "CUSTOM", body = "Описание" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
    }
}

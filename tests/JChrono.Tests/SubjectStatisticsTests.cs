using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JChrono.Tests;

public sealed class SubjectStatisticsTests
{
    [Fact]
    public async Task Subject_cards_sum_closed_intervals_and_count_projects_and_tasks_by_status()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        int id, otherId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var subject = new Subject("Разработка", "DEV");
            var other = new Subject("Пустой предмет", "EMPTY");
            var project = new Project(subject, "Проект", "0001");
            var archived = new Project(subject, "Архив", "0002");
            archived.SetStatus(ProjectStatus.Closed);
            db.AddRange(subject, other, project, archived);
            await db.SaveChangesAsync();
            id = subject.Id; otherId = other.Id;
            var active = new ProjectTask(project, "В работе", "0001", "Описание");
            var deferred = new ProjectTask(project, "Отложено", "0002", "Описание");
            deferred.Update(deferred.Name, deferred.Body, ProjectTaskStatus.Deferred);
            var closed = new ProjectTask(project, "Закрыто", "0003", "Описание");
            closed.Update(closed.Name, closed.Body, ProjectTaskStatus.Closed);
            db.AddRange(active, deferred, closed);
            await db.SaveChangesAsync();
            var start = new DateTimeOffset(2026, 11, 17, 23, 0, 0, TimeSpan.FromHours(3));
            var direct = new Session(subject, start);
            direct.Close(start.AddMinutes(30));
            var projectSession = new Session(subject, start, project: archived);
            projectSession.Close(start.AddHours(25));
            var taskSession = new Session(subject, start, project: project, task: closed);
            taskSession.Close(start.AddMinutes(45));
            db.AddRange(direct, projectSession, taskSession, new Session(subject, start.AddDays(-3)));
            await db.SaveChangesAsync();
        }
        var subjects = (await client.GetFromJsonAsync<List<SubjectDto>>("/api/subjects"))!;
        var result = subjects.Single(s => s.Id == id);
        Assert.Equal(26.25, result.ClosedSessionHours);
        Assert.Equal(2, result.ProjectCount);
        Assert.Equal(1, result.InProgressTaskCount);
        Assert.Equal(1, result.DeferredTaskCount);
        Assert.Equal(result, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{id}"));
        var empty = subjects.Single(s => s.Id == otherId);
        Assert.Equal(0, empty.ClosedSessionHours);
        Assert.Equal(0, empty.ProjectCount);
        Assert.Equal(0, empty.InProgressTaskCount);
        Assert.Equal(0, empty.DeferredTaskCount);
    }
}

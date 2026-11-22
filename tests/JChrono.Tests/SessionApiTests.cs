using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JChrono.Tests;

public sealed class SessionApiTests : IDisposable
{
    private readonly NotesFactory factory = new();
    private readonly WebApplicationFactory<Program> app;
    private readonly HttpClient client;
    private readonly TestClock clock = new();

    public SessionApiTests()
    {
        app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        client = app.CreateClient();
    }

    [Fact]
    public async Task Session_lifecycle_uses_server_time_keeps_history_and_supports_text_only_edits()
    {
        var subject = await Subject("Разработка");
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, Text: " Начало "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var opened = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(clock.Now, opened.StartTime);
        Assert.Equal(SessionStatus.Open, opened.Status);
        Assert.Null(opened.EndTime);
        Assert.Null(opened.Duration);
        Assert.Equal("Начало", opened.Text);
        Assert.Equal(new Uri(client.BaseAddress!, $"/api/sessions/{opened.Id}"), response.Headers.Location);
        Assert.Equal(opened, await client.GetFromJsonAsync<SessionDto>(response.Headers.Location));
        Assert.Equal(opened, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/sessions/{opened.Id}/text", new UpdateSessionTextRequest("В процессе"))).StatusCode);
        clock.Now = clock.Now.AddHours(27);
        var closedResponse = await client.PostAsync($"/api/sessions/{opened.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        var closed = (await closedResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(SessionStatus.Closed, closed.Status);
        Assert.Equal(clock.Now, closed.EndTime);
        Assert.Equal(TimeSpan.FromHours(27), closed.Duration);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/sessions/{opened.Id}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/sessions/{opened.Id}/text", new { text = "Итог", status = 0, startTime = clock.Now, endTime = (DateTimeOffset?)null })).StatusCode);
        var edited = (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{opened.Id}"))!;
        Assert.Equal(closed with { Text = "Итог" }, edited);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id))).StatusCode);
        Assert.Equal(edited, await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{opened.Id}"));
    }

    [Fact]
    public async Task Session_creation_validates_links_and_text_without_leaving_an_open_session()
    {
        var subject = await Subject("Разработка");
        var other = await Subject("Английский");
        var projectResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Конспекты"));
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        var secondResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Практика"));
        var second = (await secondResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        var taskResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("SQL", "Практика"));
        var task = (await taskResponse.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        foreach (var request in new[] { new CreateSessionRequest(other.Id, project.Id), new CreateSessionRequest(other.Id, TaskId: task.Id),
            new CreateSessionRequest(subject.Id, second.Id, task.Id), new CreateSessionRequest(subject.Id, Text: new string('a', 1001)) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sessions", request)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
        }
        foreach (var request in new[] { new CreateSessionRequest(999), new CreateSessionRequest(subject.Id, 999), new CreateSessionRequest(subject.Id, TaskId: 999) })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/sessions", request)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, TaskId: task.Id, Text: new string('я', 1000)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(project.Id, session.ProjectId);
        Assert.Equal(task.Id, session.TaskId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sessions/{session.Id}/text", new UpdateSessionTextRequest(new string('a', 1001)))).StatusCode);
        Assert.Equal(session, await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sessions/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/sessions/999/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/sessions/999/text", new UpdateSessionTextRequest("Текст"))).StatusCode);
    }

    [Fact]
    public async Task Simultaneous_creation_requests_only_create_one_current_session()
    {
        var subject = await Subject("Разработка");
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var created = (await results.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(created, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
    }

    [Fact]
    public async Task Concurrent_creation_and_reopening_leave_just_one_open_session()
    {
        const string url = "/api/sessions";
        var subject = await Subject("Разработка");
        using var created = await client.PostAsJsonAsync(url, new CreateSessionRequest(subject.Id));
        created.EnsureSuccessStatusCode();
        var original = (await created.Content.ReadFromJsonAsync<SessionDto>())!;
        clock.Now = clock.Now.AddHours(1);
        using var closed = await client.PostAsync($"{url}/{original.Id}/close", null);
        closed.EnsureSuccessStatusCode();
        var history = (await closed.Content.ReadFromJsonAsync<SessionDto>())!;

        var create = client.PostAsJsonAsync(url, new CreateSessionRequest(subject.Id));
        var reopen = client.PutAsJsonAsync($"{url}/{original.Id}", new UpdateSessionRequest(original.StartTime, null, "Открыта повторно"));
        using var createResponse = await create;
        using var reopenResponse = await reopen;

        var creationWon = createResponse.StatusCode == HttpStatusCode.Created;
        Assert.Equal(creationWon ? (HttpStatusCode.Created, HttpStatusCode.Conflict) : (HttpStatusCode.Conflict, HttpStatusCode.OK),
            (createResponse.StatusCode, reopenResponse.StatusCode));
        var winner = await (creationWon ? createResponse : reopenResponse).Content.ReadFromJsonAsync<SessionDto>();
        var sessions = (await client.GetFromJsonAsync<SessionDto[]>($"/api/subjects/{subject.Id}/sessions"))!;
        Assert.Equal(winner, Assert.Single(sessions, session => session.EndTime is null));
        Assert.Equal(winner, await client.GetFromJsonAsync<SessionDto>($"{url}/current"));
        if (creationWon)
            Assert.Equal(history, Assert.Single(sessions, session => session.Id == history.Id));
        else
            Assert.Single(sessions);
    }

    [Fact]
    public async Task Closed_session_times_can_be_edited_and_clearing_end_time_reopens_it()
    {
        var subject = await Subject("Разработка");
        var createdResponse = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var session = (await createdResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        clock.Now = clock.Now.AddHours(1);
        await client.PostAsync($"/api/sessions/{session.Id}/close", null);
        var request = new UpdateSessionRequest(session.StartTime.AddHours(-2), clock.Now.AddHours(2), "Правка закрытой сессии");
        var response = await client.PutAsJsonAsync($"/api/sessions/{session.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(request.StartTime, edited.StartTime);
        Assert.Equal(request.EndTime, edited.EndTime);
        Assert.Equal(TimeSpan.FromHours(5), edited.Duration);
        Assert.Equal(SessionStatus.Closed, edited.Status);
        Assert.Equal(edited, await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}"));
        response = await client.PutAsJsonAsync($"/api/sessions/{session.Id}", request with { EndTime = null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reopened = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Null(reopened.EndTime);
        Assert.Null(reopened.Duration);
        Assert.Equal(SessionStatus.Open, reopened.Status);
        Assert.Equal(reopened, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        response = await client.PutAsJsonAsync($"/api/sessions/{session.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
    }

    [Fact]
    public async Task Invalid_session_edit_and_reopening_while_another_session_is_open_do_not_save_partial_changes()
    {
        var subject = await Subject("Разработка");
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        var request = new UpdateSessionRequest(session.StartTime, session.StartTime.AddHours(1), "Текст");
        foreach (var invalid in new[] { request with { StartTime = null }, request with { EndTime = session.StartTime.AddMinutes(-1) },
            request with { Text = new string('a', 1001) } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sessions/{session.Id}", invalid)).StatusCode);
            Assert.Equal(session, await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}"));
        }
        clock.Now = clock.Now.AddHours(1);
        var closedResponse = await client.PostAsync($"/api/sessions/{session.Id}/close", null);
        var closed = (await closedResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        var otherResponse = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var other = (await otherResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/sessions/{session.Id}", request with { EndTime = null, Text = "Не сохранять" })).StatusCode);
        Assert.Equal(closed, await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}"));
        Assert.Equal(other, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/sessions/999", request)).StatusCode);
    }

    [Fact]
    public async Task Subject_sessions_are_restored_filtered_and_sorted_from_earliest_start_to_latest()
    {
        var subject = await Subject("Разработка");
        var other = await Subject("Английский");
        Assert.Empty((await client.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!);
        var firstResponse = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var first = (await firstResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        clock.Now = clock.Now.AddHours(1);
        await client.PostAsync($"/api/sessions/{first.Id}/close", null);
        var otherResponse = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(other.Id));
        var foreign = (await otherResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        await client.PostAsync($"/api/sessions/{foreign.Id}/close", null);
        clock.Now = clock.Now.AddHours(1);
        var lastResponse = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var last = (await lastResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        using var reconnected = app.CreateClient();
        var sessions = (await reconnected.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!;
        Assert.Equal(new[] { first.Id, last.Id }, sessions.Select(s => s.Id));
        Assert.All(sessions, session => Assert.Equal(subject.Id, session.SubjectId));
        Assert.Equal(last, await reconnected.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Null(sessions[1].EndTime);
        await client.PutAsJsonAsync($"/api/sessions/{first.Id}", new UpdateSessionRequest(last.StartTime.AddHours(1), last.StartTime.AddHours(2), "Позже"));
        sessions = (await reconnected.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!;
        Assert.Equal(new[] { last.Id, first.Id }, sessions.Select(s => s.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/subjects/999/sessions")).StatusCode);
    }

    [Fact]
    public async Task Deleting_closed_session_preserves_current_session_and_its_subject_project_and_task()
    {
        var subject = await Subject("Разработка");
        var other = await Subject("Английский");
        var projectResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Проект"));
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        var taskResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Задача", "Описание"));
        var task = (await taskResponse.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, TaskId: task.Id));
        var closed = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        await client.PostAsync($"/api/sessions/{closed.Id}/close", null);
        response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(other.Id));
        var current = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sessions/{closed.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sessions/{closed.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!);
        Assert.Equal(current, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Equal(subject.Id, (await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"))!.Id);
        Assert.Equal(task, Assert.Single((await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.Tasks));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/sessions/{closed.Id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_open_session_frees_current_session_and_allows_starting_another()
    {
        var subject = await Subject("Разработка");
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id));
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/sessions/999")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id))).StatusCode);
    }

    [Fact]
    public async Task Project_sessions_link_both_project_and_subject_and_filter_out_other_project_and_subject_only_sessions()
    {
        var subject = await Subject("Разработка");
        var projectResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Проект"));
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        var otherResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Другой проект"));
        var other = (await otherResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Empty((await client.GetFromJsonAsync<List<SessionDto>>($"/api/projects/{project.Id}/sessions"))!);
        foreach (var request in new[] { new CreateSessionRequest(subject.Id), new CreateSessionRequest(subject.Id, other.Id) })
        {
            var created = await client.PostAsJsonAsync("/api/sessions", request);
            var session = (await created.Content.ReadFromJsonAsync<SessionDto>())!;
            await client.PostAsync($"/api/sessions/{session.Id}/close", null);
        }
        var response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, project.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var direct = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(subject.Id, direct.SubjectId);
        Assert.Equal(project.Id, direct.ProjectId);
        Assert.Null(direct.TaskId);
        Assert.Null(direct.EndTime);
        Assert.Equal(clock.Now, direct.StartTime);
        clock.Now = clock.Now.AddHours(1);
        await client.PostAsync($"/api/sessions/{direct.Id}/close", null);
        var taskResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Задача", "Описание"));
        var task = (await taskResponse.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        response = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, TaskId: task.Id));
        var taskSession = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        using var reconnected = app.CreateClient();
        var sessions = (await reconnected.GetFromJsonAsync<List<SessionDto>>($"/api/projects/{project.Id}/sessions"))!;
        Assert.Equal(new[] { direct.Id, taskSession.Id }, sessions.Select(s => s.Id));
        Assert.All(sessions, session => { Assert.Equal(project.Id, session.ProjectId); Assert.Equal(subject.Id, session.SubjectId); });
        Assert.Equal(taskSession, await reconnected.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Single((await client.GetFromJsonAsync<List<SessionDto>>($"/api/projects/{other.Id}/sessions"))!);
        Assert.Equal(4, (await client.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/999/sessions")).StatusCode);
    }

    [Fact]
    public async Task Task_sessions_keep_all_parent_links_support_lifecycle_and_exclude_other_scopes()
    {
        var subject = await Subject("Разработка");
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Проект")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var otherProject = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Другой проект")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var task = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Задача", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var otherTask = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Другая задача", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var url = $"/api/projects/{project.Id}/tasks/{task.Id}/sessions";
        Assert.Empty((await client.GetFromJsonAsync<List<SessionDto>>(url))!);
        foreach (var request in new[] { new CreateSessionRequest(subject.Id), new CreateSessionRequest(subject.Id, project.Id),
            new CreateSessionRequest(subject.Id, project.Id, otherTask.Id), new CreateSessionRequest(subject.Id, otherProject.Id) })
        {
            var session = (await (await client.PostAsJsonAsync("/api/sessions", request)).Content.ReadFromJsonAsync<SessionDto>())!;
            await client.PostAsync($"/api/sessions/{session.Id}/close", null);
        }
        var created = await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, project.Id, task.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(subject.Id, first.SubjectId);
        Assert.Equal(project.Id, first.ProjectId);
        Assert.Equal(task.Id, first.TaskId);
        Assert.Equal(clock.Now, first.StartTime);
        Assert.Null(first.EndTime);
        using var reconnected = app.CreateClient();
        Assert.Equal(first, Assert.Single((await reconnected.GetFromJsonAsync<List<SessionDto>>(url))!));
        Assert.Equal(first, await reconnected.GetFromJsonAsync<SessionDto>("/api/sessions/current"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, project.Id, otherTask.Id))).StatusCode);

        clock.Now = clock.Now.AddHours(1);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/sessions/{first.Id}/close", null)).StatusCode);
        var earlier = first.StartTime.AddHours(-1);
        var edited = await client.PutAsJsonAsync($"/api/sessions/{first.Id}", new UpdateSessionRequest(earlier, clock.Now, "Итог"));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var saved = (await edited.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(first with { StartTime = earlier, EndTime = clock.Now, Text = "Итог", Status = SessionStatus.Closed, Duration = TimeSpan.FromHours(2) }, saved);
        var second = (await (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, TaskId: task.Id)))
            .Content.ReadFromJsonAsync<SessionDto>())!;
        var sessions = (await reconnected.GetFromJsonAsync<List<SessionDto>>(url))!;
        Assert.Equal(new[] { saved, second }, sessions);
        Assert.Contains(second, (await client.GetFromJsonAsync<List<SessionDto>>($"/api/projects/{project.Id}/sessions"))!);
        Assert.Contains(second, (await client.GetFromJsonAsync<List<SessionDto>>($"/api/subjects/{subject.Id}/sessions"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{otherProject.Id}/tasks/{task.Id}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{project.Id}/tasks/999999/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sessions/{second.Id}")).StatusCode);
        Assert.Equal(saved, Assert.Single((await client.GetFromJsonAsync<List<SessionDto>>(url))!));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/sessions/current")).StatusCode);
    }

    [Fact]
    public async Task Day_sessions_include_all_scopes_and_statuses_with_full_codes_and_local_day_boundaries()
    {
        var subject = await Subject("Разработка");
        var other = await Subject("Английский");
        var project = (await (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Практика")))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var task = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("SQL", "Описание")))
            .Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var start = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.FromHours(3));
        var end = start.AddDays(1);
        var included = new List<SessionDto>();
        var entries = new[] {
            (new CreateSessionRequest(subject.Id), start.AddTicks(-1), false),
            (new CreateSessionRequest(subject.Id, project.Id), start, true),
            (new CreateSessionRequest(subject.Id, project.Id, task.Id), start.AddHours(2), true),
            (new CreateSessionRequest(other.Id), start.AddHours(4), true),
            (new CreateSessionRequest(subject.Id), end.AddTicks(-1), true),
            (new CreateSessionRequest(subject.Id), end, false)
        };
        foreach (var (request, time, include) in entries)
        {
            var session = (await (await client.PostAsJsonAsync("/api/sessions", request)).Content.ReadFromJsonAsync<SessionDto>())!;
            await client.PostAsync($"/api/sessions/{session.Id}/close", null);
            var response = await client.PutAsJsonAsync($"/api/sessions/{session.Id}", new UpdateSessionRequest(time, time.AddHours(1), "Итог"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (include) included.Add((await response.Content.ReadFromJsonAsync<SessionDto>())!);
        }
        clock.Now = start.AddHours(6).ToUniversalTime();
        var open = (await (await client.PostAsJsonAsync("/api/sessions", new CreateSessionRequest(subject.Id, TaskId: task.Id)))
            .Content.ReadFromJsonAsync<SessionDto>())!;
        included.Insert(3, open);
        var url = $"/api/sessions?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}";
        using var reconnected = app.CreateClient();
        var sessions = (await reconnected.GetFromJsonAsync<List<DaySessionDto>>(url))!;
        Assert.Equal(included, sessions.Select(s => s.Session));
        Assert.Equal(new[] { project.FullCode, task.FullCode, other.ShortName, task.FullCode, subject.ShortName }, sessions.Select(s => s.FullCode));
        Assert.Equal(new[] { project.Name, task.Name, other.Name, task.Name, subject.Name }, sessions.Select(s => s.TargetName));
        Assert.Single(sessions, s => s.Session.EndTime is null);
        Assert.Equal(open, await client.GetFromJsonAsync<SessionDto>("/api/sessions/current"));

        var journalPath = $"/api/subjects/{subject.Id}/sessions/journal";
        var journal = (await reconnected.GetFromJsonAsync<List<DaySessionDto>>(journalPath + url[url.IndexOf('?')..]))!;
        Assert.Equal(new[] { subject.ShortName, project.FullCode, task.FullCode, task.FullCode, subject.ShortName }, journal.Select(s => s.FullCode));
        Assert.All(journal, row => Assert.Equal(subject.Id, row.Session.SubjectId));
        Assert.Equal(start.AddTicks(-1), journal[0].Session.StartTime);
        Assert.Equal(end.AddTicks(-1), journal[^1].Session.StartTime);
        Assert.Equal(open, Assert.Single(journal, row => row.Session.EndTime is null).Session);
        var nextDay = await reconnected.GetFromJsonAsync<List<DaySessionDto>>(
            journalPath + "?startTime=2026-11-18T21:00:00Z&endTime=2026-11-19T21:00:00Z");
        Assert.Equal(2, nextDay!.Count);
        Assert.All(nextDay, row => Assert.NotNull(row.Session.EndTime));

        await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest(subject.Name, "NEW"));
        sessions = (await client.GetFromJsonAsync<List<DaySessionDto>>(url))!;
        Assert.Equal($"NEW \\ {project.Code} \\ {task.Code}", sessions[1].FullCode);
        Assert.Empty((await client.GetFromJsonAsync<List<DaySessionDto>>("/api/sessions?startTime=2020-01-01T00:00:00Z&endTime=2020-01-02T00:00:00Z"))!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?startTime=2026-11-18T00:00:00Z")]
    [InlineData("?startTime=2026-11-18T00:00:00Z&endTime=2026-11-18T00:00:00Z")]
    [InlineData("?startTime=2026-11-18T00:00:00Z&endTime=2026-11-17T00:00:00Z")]
    [InlineData("?startTime=invalid&endTime=2026-11-18T00:00:00Z")]
    public async Task Day_sessions_reject_missing_or_invalid_time_bounds(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/sessions" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/sessions/useful-work" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/sessions/closed-times" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/subjects/1/sessions/journal" + query)).StatusCode);
    }

    [Fact]
    public async Task Subject_journal_distinguishes_a_missing_subject_from_an_empty_journal()
    {
        const string query = "?startTime=2026-11-18T00:00:00Z&endTime=2026-11-19T00:00:00Z";
        using var missing = await client.GetAsync("/api/subjects/1/sessions/journal" + query);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var subject = await Subject("Разработка");
        var journal = await client.GetFromJsonAsync<DaySessionDto[]>($"/api/subjects/{subject.Id}/sessions/journal{query}");
        Assert.Empty(journal!);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)]
    public async Task Useful_work_sums_only_closed_session_parts_within_the_requested_local_day(int dayHours)
    {
        var subject = await Subject("Разработка");
        var other = await Subject("Английский");
        var start = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.FromHours(3));
        var end = start.AddHours(dayHours);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var first = (await db.Subjects.FindAsync(subject.Id))!;
            var second = (await db.Subjects.FindAsync(other.Id))!;
            var intervals = new[] {
                (start.AddHours(-1), start.AddMinutes(30)),
                (start.AddHours(2), start.AddHours(3).AddMinutes(15)),
                (end.AddHours(-1), end.AddHours(2)),
                (start.AddDays(-1), end.AddDays(1)),
                (end.AddHours(1), end.AddHours(2)),
                (start.AddHours(-1), start),
                (end, end.AddHours(1)),
                (start.AddHours(1), start.AddHours(1))
            };
            foreach (var (from, to) in intervals)
            {
                var session = new Session(first, from);
                session.Close(to);
                db.Add(session);
            }
            var otherSession = new Session(second, start.AddHours(4));
            otherSession.Close(start.AddHours(4).AddMinutes(15));
            db.AddRange(otherSession, new Session(first, start));
            await db.SaveChangesAsync();
        }
        var url = $"/api/sessions/useful-work?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}";
        var result = (await client.GetFromJsonAsync<UsefulWorkDto>(url))!;
        Assert.Equal(TimeSpan.FromHours(dayHours + 3).TotalSeconds, result.TotalSeconds);

        var closedTimes = (await client.GetFromJsonAsync<List<ClosedSessionTimeDto>>(url.Replace("useful-work", "closed-times")))!;
        Assert.Equal(6, closedTimes.Count);
        Assert.Contains(new ClosedSessionTimeDto(subject.Id, start, start.AddMinutes(30)), closedTimes);
        Assert.Contains(new ClosedSessionTimeDto(subject.Id, end.AddHours(-1), end), closedTimes);
        Assert.Contains(new ClosedSessionTimeDto(subject.Id, start, end), closedTimes);
        Assert.Equal(new ClosedSessionTimeDto(other.Id, start.AddHours(4), start.AddHours(4).AddMinutes(15)),
            Assert.Single(closedTimes, session => session.SubjectId == other.Id));
        result = (await client.GetFromJsonAsync<UsefulWorkDto>("/api/sessions/useful-work?startTime=2020-01-01T00:00:00Z&endTime=2020-01-02T00:00:00Z"))!;
        Assert.Equal(0, result.TotalSeconds);
    }

    private async Task<SubjectDto> Subject(string name)
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(name, "TEST"));
        return (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
    }

    public void Dispose() { client.Dispose(); app.Dispose(); factory.Dispose(); }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 17, 23, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

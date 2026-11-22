using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using Xunit;

namespace JChrono.Tests;

public sealed class PlanningApiTests : IDisposable
{
    private const string Url = "/api/planning/activities";
    private readonly NotesFactory factory = new();
    private readonly HttpClient client;

    public PlanningApiTests() => client = factory.CreateClient();

    [Fact]
    public async Task Activities_are_sorted_by_start_and_can_be_moved_and_deleted()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var late = new SavePlannedActivityRequest(date, new TimeOnly(23, 45), 90, "Чтение", subject.Id, [], []);
        var reading = await Create<PlannedActivityDto>(Url, late);
        await Create<PlannedActivityDto>(Url, late with { StartTime = new TimeOnly(10, 0), Description = "Пробежка" });
        var activities = await GetDay(date);
        Assert.Equal(["Пробежка", "Чтение"], activities.Select(x => x.Description));
        Assert.Equal(new DateTime(2026, 11, 22, 1, 15, 0), activities[1].End);

        using var invalid = await client.PutAsJsonAsync($"{Url}/{reading.Id}", late with { DurationMinutes = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var nextDay = date.AddDays(1);
        using var updated = await client.PutAsJsonAsync($"{Url}/{reading.Id}", late with { Date = nextDay });
        updated.EnsureSuccessStatusCode();
        Assert.Equal(reading.Id, Assert.Single(await GetDay(nextDay)).Id);
        Assert.Equal("Пробежка", Assert.Single(await GetDay(date)).Description);

        using var deleted = await client.DeleteAsync($"{Url}/{reading.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await GetDay(nextDay));
    }

    [Fact]
    public async Task Overlapping_activities_are_rejected_and_touching_boundaries_are_allowed()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(9, 0), 60, "Занятие", subject.Id, [], []);
        var morning = await Create<PlannedActivityDto>(Url, request);

        foreach (var (start, duration) in new[]
        {
            (new TimeOnly(8, 0), 90),
            (new TimeOnly(8, 0), 120),
            (new TimeOnly(8, 0), 180),
            (new TimeOnly(9, 30), 30),
            (new TimeOnly(9, 0), 60)
        })
        {
            using var response = await client.PostAsJsonAsync(Url, request with { StartTime = start, DurationMinutes = duration });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        await Create<PlannedActivityDto>(Url, request with { StartTime = new TimeOnly(8, 0) });
        var next = await Create<PlannedActivityDto>(Url, request with { StartTime = new TimeOnly(10, 0) });
        using var unchangedTime = await client.PutAsJsonAsync($"{Url}/{morning.Id}", request with { Description = "Уточнение" });
        Assert.Equal(HttpStatusCode.OK, unchangedTime.StatusCode);

        using var conflictingTime = await client.PutAsJsonAsync($"{Url}/{morning.Id}", request with { DurationMinutes = 61 });
        Assert.Equal(HttpStatusCode.Conflict, conflictingTime.StatusCode);
        var saved = await GetDay(date);
        Assert.Equal(60, saved.Single(x => x.Id == morning.Id).DurationMinutes);
        Assert.Equal(new TimeOnly(10, 0), saved.Single(x => x.Id == next.Id).StartTime);
    }

    [Fact]
    public async Task Activities_are_checked_for_overlap_across_midnight()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var evening = new SavePlannedActivityRequest(date, new TimeOnly(23, 30), 90, "Вечернее занятие", subject.Id, [], []);
        var midnight = evening with { Date = date.AddDays(1), StartTime = new TimeOnly(0, 30) };
        var nextDay = await Create<PlannedActivityDto>(Url, midnight);
        using var conflictingEvening = await client.PostAsJsonAsync(Url, evening);
        Assert.Equal(HttpStatusCode.Conflict, conflictingEvening.StatusCode);

        using var removed = await client.DeleteAsync($"{Url}/{nextDay.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await Create<PlannedActivityDto>(Url, evening);
        using var conflictingMidnight = await client.PostAsJsonAsync(Url, midnight);
        Assert.Equal(HttpStatusCode.Conflict, conflictingMidnight.StatusCode);
        await Create<PlannedActivityDto>(Url, midnight with { StartTime = new TimeOnly(1, 0) });
    }

    [Fact]
    public async Task Concurrent_requests_cannot_reserve_overlapping_time()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(9, 0), 60, "Занятие", subject.Id, [], []);
        var first = client.PostAsJsonAsync(Url, request);
        var second = client.PostAsJsonAsync(Url, request with { StartTime = new TimeOnly(9, 30) });
        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Order());
        Assert.Single(await GetDay(date));
    }

    [Fact]
    public async Task Concurrent_creation_and_update_cannot_reserve_the_same_interval()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var original = new SavePlannedActivityRequest(date, new TimeOnly(9, 0), 60, "Занятие", subject.Id, [], []);
        var activity = await Create<PlannedActivityDto>(Url, original);
        var target = original with { StartTime = new TimeOnly(11, 0) };

        var create = client.PostAsJsonAsync(Url, target);
        var update = client.PutAsJsonAsync($"{Url}/{activity.Id}", target);
        using var created = await create;
        using var updated = await update;

        var creationWon = created.StatusCode == HttpStatusCode.Created;
        Assert.Equal(creationWon ? (HttpStatusCode.Created, HttpStatusCode.Conflict) : (HttpStatusCode.Conflict, HttpStatusCode.OK),
            (created.StatusCode, updated.StatusCode));
        var activities = await GetDay(date);
        Assert.Equal(creationWon ? 2 : 1, activities.Length);
        Assert.Single(activities, item => item.StartTime == target.StartTime);
        Assert.Equal(creationWon ? original.StartTime : target.StartTime,
            activities.Single(item => item.Id == activity.Id).StartTime);
    }

    [Fact]
    public async Task References_are_unique_and_belong_to_the_selected_subject()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Разработка", "DEV"));
        var other = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Английский", "ENG"));
        var project = await Create<ProjectDto>($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Планировщик"));
        var task = await Create<ProjectTaskDto>($"/api/projects/{project.Id}/tasks", new { name = "Календарь", body = "Сетка дней" });
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(10, 0), 60,
            "Работа над планировщиком", subject.Id, [project.Id], [task.Id]);

        foreach (var invalid in new[]
        {
            request with { SubjectId = null },
            request with { SubjectId = other.Id },
            request with { ProjectIds = [project.Id, project.Id] },
            request with { TaskIds = [task.Id, task.Id] }
        })
        {
            using var response = await client.PostAsJsonAsync(Url, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Empty(await GetDay(date));

        var activity = await Create<PlannedActivityDto>(Url, request);
        var saved = Assert.Single(await GetDay(date));
        Assert.Equal(new PlannedActivitySubjectDto(subject.Id, subject.Name, subject.ShortName), saved.Subject);
        Assert.Contains(new PlannedActivityReferenceDto(project.Id, null, project.Name), saved.References);
        Assert.Contains(new PlannedActivityReferenceDto(project.Id, task.Id, task.Name, project.Name), saved.References);

        using var updated = await client.PutAsJsonAsync($"{Url}/{activity.Id}", request with { ProjectIds = [] });
        updated.EnsureSuccessStatusCode();
        Assert.Equal(task.Id, Assert.Single(Assert.Single(await GetDay(date)).References).TaskId);

        using var deleteSubject = new HttpRequestMessage(HttpMethod.Delete, $"/api/subjects/{subject.Id}")
        {
            Content = JsonContent.Create(new { confirmationShortName = subject.ShortName })
        };
        using var deleted = await client.SendAsync(deleteSubject);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var afterContextRemoval = Assert.Single(await GetDay(date));
        Assert.Null(afterContextRemoval.Subject);
        Assert.Empty(afterContextRemoval.References);
        Assert.Equal(request.Description, afterContextRemoval.Description);
    }

    [Fact]
    public async Task Moving_an_activity_rejects_an_occupied_interval_and_leaves_the_neighbor_in_place()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(9, 0), 60, "Первое", subject.Id, [], []);
        var first = await Create<PlannedActivityDto>(Url, request);
        var second = await Create<PlannedActivityDto>(Url, request with { StartTime = new TimeOnly(10, 0), Description = "Второе" });
        var url = $"{Url}/{first.Id}/position";
        var position = new MovePlannedActivity(date.ToDateTime(first.StartTime), 60, date.ToDateTime(new TimeOnly(10, 30)));

        using var occupied = await client.PutAsJsonAsync(url, position);
        Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
        Assert.Equal(new[] { first, second }.Select(activity => activity.StartTime),
            (await GetDay(date)).Select(activity => activity.StartTime));

        position = position with { Start = date.ToDateTime(new TimeOnly(11, 0)) };
        using var available = await client.PutAsJsonAsync(url, position);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(11, 0)],
            (await GetDay(date)).Select(activity => activity.StartTime));

        using var stale = await client.PutAsJsonAsync(url, position);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Concurrent_moves_cannot_reserve_the_same_interval()
    {
        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Планирование", "PLAN"));
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(9, 0), 60, "Занятие", subject.Id, [], []);
        var first = await Create<PlannedActivityDto>(Url, request);
        var second = await Create<PlannedActivityDto>(Url, request with { StartTime = new TimeOnly(10, 0) });
        var target = new TimeOnly(11, 0);

        var firstMove = MoveToTarget(first);
        var secondMove = MoveToTarget(second);
        using var firstResponse = await firstMove;
        using var secondResponse = await secondMove;

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Order());
        var activities = await GetDay(date);
        Assert.Equal(2, activities.Length);
        var moved = Assert.Single(activities, activity => activity.StartTime == target);
        Assert.Equal(firstResponse.StatusCode == HttpStatusCode.OK ? first.Id : second.Id, moved.Id);
        var original = moved.Id == first.Id ? second : first;
        Assert.Equal(original.StartTime, activities.Single(activity => activity.Id == original.Id).StartTime);

        Task<HttpResponseMessage> MoveToTarget(PlannedActivityDto activity) =>
            client.PutAsJsonAsync($"{Url}/{activity.Id}/position",
                new MovePlannedActivity(date.ToDateTime(activity.StartTime), activity.DurationMinutes, date.ToDateTime(target)));
    }

    [Fact]
    public async Task New_activities_require_a_subject_even_when_they_have_a_description()
    {
        var date = new DateOnly(2026, 11, 21);
        var request = new SavePlannedActivityRequest(date, new TimeOnly(10, 0), 60, "Чтение", null, [], []);

        using var noSubjects = await client.PostAsJsonAsync(Url, request);
        Assert.Equal(HttpStatusCode.BadRequest, noSubjects.StatusCode);

        var subject = await Create<SubjectDto>("/api/subjects", new CreateSubjectRequest("Литература", "LIT"));
        using var noSelection = await client.PostAsJsonAsync(Url, request);
        Assert.Equal(HttpStatusCode.BadRequest, noSelection.StatusCode);
        Assert.Empty(await GetDay(date));

        var activity = await Create<PlannedActivityDto>(Url, request with { SubjectId = subject.Id, Description = null });
        Assert.Equal(subject.Id, activity.Subject?.Id);
        Assert.Equal("", activity.Description);
    }

    private async Task<PlannedActivityDto[]> GetDay(DateOnly date) =>
        (await client.GetFromJsonAsync<PlannedActivityDto[]>($"{Url}?from={date:yyyy-MM-dd}&through={date:yyyy-MM-dd}"))!;

    private async Task<T> Create<T>(string url, object request)
    {
        using var response = await client.PostAsJsonAsync(url, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }
}

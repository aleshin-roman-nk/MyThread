using System.Net;
using System.Net.Http.Json;
using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DailyNotes.Tests;

public sealed class NotesFactory : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"daily-notes-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Notes", $"Data Source={database};Pooling=False");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                File.Delete(database + suffix);
        }
    }
}

public sealed class ApiTests : IDisposable
{
    private readonly NotesFactory factory = new();
    private readonly HttpClient client;
    public ApiTests() => client = factory.CreateClient();

    [Fact]
    public async Task Diary_notes_are_saved_without_a_subject_and_returned_in_calendar_order()
    {
        const string url = "/api/diary/notes";
        var day = new DateOnly(2026, 11, 19);
        var morning = new SaveDiaryNoteRequest(day, new TimeOnly(8, 30), "Утро", 4);
        foreach (var note in new[]
        {
            morning with { Time = new TimeOnly(17, 10), Text = "Вечер" },
            morning with { Date = day.AddDays(1), Text = "Следующий день" },
            morning,
            morning with { Date = day.AddDays(-19), Text = "Прошлый месяц" }
        })
        {
            using var response = await client.PostAsJsonAsync(url, note);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var invalid = await client.PostAsJsonAsync(url, morning with { Rating = 6 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var daily = await client.GetFromJsonAsync<DiaryNoteDto[]>($"{url}?from=2026-11-19&through=2026-11-19");
        Assert.Equal(["Утро", "Вечер"], daily!.Select(note => note.Text));
        Assert.Equal(4, daily![0].Rating);
        var monthly = await client.GetFromJsonAsync<DiaryNoteDto[]>($"{url}?from=2026-11-01&through=2026-11-30");
        Assert.Equal(["Утро", "Вечер", "Следующий день"], monthly!.Select(note => note.Text));
        Assert.Empty((await client.GetFromJsonAsync<NoteDto[]>("/api/notes?date=2026-11-19"))!);
    }

    [Fact]
    public async Task Editing_moves_a_diary_note_to_its_new_date_and_deleting_removes_it()
    {
        const string url = "/api/diary/notes";
        const string originalDay = "?from=2026-11-19&through=2026-11-19";
        const string newDay = "?from=2026-12-01&through=2026-12-01";
        var request = new SaveDiaryNoteRequest(new DateOnly(2026, 11, 19), new TimeOnly(8, 30), "Заметка", 4);
        using var created = await client.PostAsJsonAsync(url, request);
        created.EnsureSuccessStatusCode();
        var note = Assert.IsType<DiaryNoteDto>(await created.Content.ReadFromJsonAsync<DiaryNoteDto>());
        var itemUrl = $"{url}/{note.Id}";
        var edited = new SaveDiaryNoteRequest(new DateOnly(2026, 12, 1), new TimeOnly(19, 15, 20), "Обновлено", 5);

        using var invalid = await client.PutAsJsonAsync(itemUrl, edited with { Rating = 6 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(note, Assert.Single((await client.GetFromJsonAsync<DiaryNoteDto[]>(url + originalDay))!));

        using var updated = await client.PutAsJsonAsync(itemUrl, edited);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var expected = new DiaryNoteDto(note.Id, edited.Date, new TimeOnly(19, 15, 20), edited.Text, edited.Rating);
        Assert.Equal(expected, await updated.Content.ReadFromJsonAsync<DiaryNoteDto>());
        Assert.Empty((await client.GetFromJsonAsync<DiaryNoteDto[]>(url + originalDay))!);
        Assert.Equal(expected, Assert.Single((await client.GetFromJsonAsync<DiaryNoteDto[]>(url + newDay))!));

        using var deleted = await client.DeleteAsync(itemUrl);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<DiaryNoteDto[]>(url + newDay))!);
        using var missing = await client.PutAsJsonAsync(itemUrl, edited);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var alreadyDeleted = await client.DeleteAsync(itemUrl);
        Assert.Equal(HttpStatusCode.NotFound, alreadyDeleted.StatusCode);
    }

    [Fact]
    public async Task Notes_are_filtered_by_calendar_date_and_subject()
    {
        var math = await CreateSubject("Математика");
        var angular = await CreateSubject("Angular");
        var date = new DateOnly(2026, 11, 17);
        await CreateNote(math.Id, date, "Первая");
        await CreateNote(angular.Id, date, "Вторая");
        await CreateNote(math.Id, date.AddDays(-1), "Вчерашняя");

        var day = await client.GetFromJsonAsync<List<NoteDto>>("/api/notes?date=2026-11-17");
        Assert.Equal(2, day!.Count);
        Assert.All(day, note => Assert.Equal(date, note.Date));

        var bySubject = await client.GetFromJsonAsync<List<NoteDto>>($"/api/subjects/{math.Id}/notes");
        Assert.Equal(2, bySubject!.Count);
        Assert.All(bySubject, note => Assert.Equal(math.Id, note.SubjectId));
        Assert.Equal(date, bySubject[0].Date);
        var subjects = await client.GetFromJsonAsync<List<SubjectDto>>("/api/subjects");
        Assert.Equal(2, subjects!.Single(s => s.Id == math.Id).NoteCount);
    }

    [Fact]
    public async Task Note_changes_and_deletion_are_persisted_across_requests()
    {
        var subject = await CreateSubject("C#");
        var other = await CreateSubject("SQL");
        var note = await CreateNote(subject.Id, new(2026, 11, 17), "Исходный текст");
        var response = await client.PutAsJsonAsync($"/api/notes/{note.Id}",
            new SaveNoteRequest(other.Id, new(2026, 11, 18), "Изменённый текст"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var oldDay = await client.GetFromJsonAsync<List<NoteDto>>("/api/notes?date=2026-11-17");
        Assert.Empty(oldDay!);
        var newDay = await client.GetFromJsonAsync<List<NoteDto>>("/api/notes?date=2026-11-18");
        Assert.Equal("Изменённый текст", Assert.Single(newDay!).Text);
        Assert.Equal(other.Id, newDay![0].SubjectId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<NoteDto>>("/api/notes?date=2026-11-18"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_subjects_ignore_case_and_surrounding_whitespace()
    {
        var subject = await CreateSubject("  Математика  ");
        Assert.Equal("Математика", subject.Name);
        var duplicate = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("математика", "MATH"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_subject_is_rejected(string name)
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(name, "TEST"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_subject_cannot_receive_notes()
    {
        var response = await client.PostAsJsonAsync("/api/notes", new SaveNoteRequest(999, new(2026, 11, 17), "Текст"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/subjects/999/notes")).StatusCode);
    }

    [Fact]
    public async Task Blank_or_oversize_note_is_rejected()
    {
        var subject = await CreateSubject("Тест");
        foreach (var text in new[] { "   ", new string('a', Note.MaxTextLength + 1) })
        {
            var response = await client.PostAsJsonAsync("/api/notes",
                new SaveNoteRequest(subject.Id, new(2026, 11, 17), text));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Invalid_date_and_unknown_api_route_return_errors_instead_of_spa()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notes?date=not-a-date")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notes")).StatusCode);
        var response = await client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Projects_and_tasks_persist_with_parent_links_and_full_codes()
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", " DEV "));
        var subject = (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
        Assert.Equal("DEV", subject.ShortName);
        var other = await CreateSubject("Другой предмет");
        var project = await CreateProject(subject.Id, "Конспекты");
        Assert.Equal("DEV \\ 0001", project.FullCode);
        Assert.Equal(subject.Id, project.SubjectId);
        var taskResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateProjectTaskRequest("Исследование и практика в SQL", " Изучить JOIN "));
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var task = (await taskResponse.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        Assert.Equal("DEV \\ 0001 \\ 0001", task.FullCode);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal("Изучить JOIN", task.Body);
        var projects = await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects");
        Assert.Equal(task, Assert.Single(Assert.Single(projects!).Tasks));
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{other.Id}/projects"))!);
        Assert.Single((await client.GetFromJsonAsync<List<ProjectTaskDto>>(taskResponse.Headers.Location))!);

        var renamed = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}/short-name", new SetSubjectShortNameRequest("CODE"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        projects = await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects");
        Assert.Equal("CODE \\ 0001", projects![0].FullCode);
        Assert.Equal("CODE \\ 0001 \\ 0001", projects[0].Tasks[0].FullCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789")]
    [InlineData("A\\B")]
    [InlineData("A/B")]
    public async Task Invalid_subject_short_name_is_rejected(string shortName)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Предмет", shortName))).StatusCode);
        var subject = await CreateSubject("Предмет");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}/short-name", new SetSubjectShortNameRequest(shortName))).StatusCode);
    }

    [Fact]
    public async Task Eight_character_short_name_is_accepted_and_unknown_parents_return_not_found()
    {
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Предмет", "12345678"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/subjects/999/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/subjects/999/projects", new CreateProjectRequest("Имя"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/999/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/projects/999/tasks", new CreateProjectTaskRequest("Имя", "Текст"))).StatusCode);
    }

    [Fact]
    public async Task Invalid_project_and_task_fields_are_rejected_without_saving()
    {
        var subject = await CreateSubject("Предмет");
        foreach (var request in new[] {
            new CreateProjectRequest(" "), new CreateProjectRequest(new string('a', 201)),
            new CreateProjectRequest("Имя", new string('a', 4001)) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", request)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!);
        var project = await CreateProject(subject.Id, "Проект");
        var valid = new CreateProjectTaskRequest("Название", "Описание");
        foreach (var request in new[] { valid with { Name = " " }, valid with { Name = new string('a', 201) },
            valid with { Body = " " }, valid with { Body = new string('a', ProjectTask.MaxBodyLength + 1) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", request)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks"))!);
    }

    [Fact]
    public async Task Subject_settings_persist_and_update_note_names_and_full_codes()
    {
        var subject = await CreateSubject("Исходное имя");
        await CreateNote(subject.Id, new(2026, 11, 17), "Идея");
        var project = await CreateProject(subject.Id, "Конспекты");
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Практика SQL", "Описание"));
        var response = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("  Разработка  ", " DEV "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
        Assert.Equal(new SubjectDto(subject.Id, "Разработка", 1, "DEV", ProjectCount: 1, InProgressTaskCount: 1), updated);
        Assert.Equal(updated, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"));
        Assert.Equal(updated, Assert.Single((await client.GetFromJsonAsync<List<SubjectDto>>("/api/subjects"))!));
        var notes = await client.GetFromJsonAsync<List<NoteDto>>($"/api/subjects/{subject.Id}/notes");
        Assert.Equal("Разработка", Assert.Single(notes!).SubjectName);
        var projects = await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects");
        Assert.Equal("DEV \\ 0001", Assert.Single(projects!).FullCode);
        Assert.Equal("DEV \\ 0001 \\ 0001", Assert.Single(projects![0].Tasks).FullCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("разработка", "DEV"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(" РАЗРАБОТКА ", "OTHER"))).StatusCode);
    }

    [Fact]
    public async Task Duplicate_subject_rename_is_rejected_without_changing_either_field()
    {
        var subject = await CreateSubject("Предмет");
        await CreateSubject("Другое имя");
        var response = await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest(" другое имя ", "NEW"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(subject, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"));
    }

    [Fact]
    public async Task Invalid_subject_settings_are_rejected_without_partial_updates()
    {
        var subject = await CreateSubject("Предмет");
        foreach (var request in new[] {
            new UpdateSubjectRequest(" ", "NEW"), new UpdateSubjectRequest(new string('a', 101), "NEW"),
            new UpdateSubjectRequest("Новое имя", " "), new UpdateSubjectRequest("Новое имя", "123456789"),
            new UpdateSubjectRequest("Новое имя", "A\\B"), new UpdateSubjectRequest("Новое имя", "A/B") })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", request)).StatusCode);
            Assert.Equal(subject, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/subjects/999", new UpdateSubjectRequest("Имя", "CODE"))).StatusCode);
    }

    [Fact]
    public async Task Project_status_is_persisted_filtered_and_can_be_restored_to_work()
    {
        var subject = await CreateSubject("Предмет");
        var other = await CreateSubject("Другой предмет");
        var active = await CreateProject(subject.Id, "В работе");
        var deferred = await CreateProject(subject.Id, "Отложенный");
        var closed = await CreateProject(subject.Id, "Закрытый");
        await CreateProject(other.Id, "Чужой");
        Assert.Equal(ProjectStatus.InProgress, active.Status);
        foreach (var (project, status) in new[] { (deferred, ProjectStatus.Deferred), (closed, ProjectStatus.Closed) })
        {
            var response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/status", new SetProjectStatusRequest(status));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
            Assert.Equal(status, updated.Status);
            Assert.Equal(project.FullCode, updated.FullCode);
            Assert.Equal(project.SubjectId, updated.SubjectId);
        }
        var all = (await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!;
        Assert.Equal(3, all.Count);
        foreach (var (id, status) in new[] { (active.Id, ProjectStatus.InProgress), (deferred.Id, ProjectStatus.Deferred), (closed.Id, ProjectStatus.Closed) })
        {
            var filtered = await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects?status={status}");
            Assert.Equal(id, Assert.Single(filtered!).Id);
            Assert.Equal(status, filtered![0].Status);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{closed.Id}/status", new SetProjectStatusRequest(ProjectStatus.InProgress))).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects?status=InProgress"))!.Count);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects?status=Closed"))!);
    }

    [Fact]
    public async Task Invalid_project_status_is_rejected_and_tasks_survive_status_changes()
    {
        var subject = await CreateSubject("Предмет");
        var project = await CreateProject(subject.Id, "Проект");
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Задача", "Описание"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/projects/{project.Id}/status", new { status = 999 })).StatusCode);
        var projects = (await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!;
        Assert.Equal(ProjectStatus.InProgress, projects[0].Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/subjects/{subject.Id}/projects?status=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/subjects/{subject.Id}/projects?status=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/projects/999/status", new SetProjectStatusRequest(ProjectStatus.Closed))).StatusCode);
        var response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/status", new SetProjectStatusRequest(ProjectStatus.Closed));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("0001", Assert.Single(updated.Tasks).Code);
        Assert.Single((await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks"))!);
    }

    [Fact]
    public async Task Project_details_include_only_its_tasks_and_remain_available_when_closed()
    {
        var subject = await CreateSubject("Разработка");
        var project = await CreateProject(subject.Id, "Конспекты");
        var other = await CreateProject(subject.Id, "Другой проект");
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new CreateProjectTaskRequest("Практика SQL", "Изучить JOIN"));
        await client.PostAsJsonAsync($"/api/projects/{other.Id}/tasks", new CreateProjectTaskRequest("Чужая задача", "Другой текст"));
        var details = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(project.Id, details.Id);
        Assert.Equal(subject.Id, details.SubjectId);
        Assert.Equal("Конспекты", details.Name);
        Assert.Equal("TEST \\ 0001", details.FullCode);
        Assert.Equal(ProjectStatus.InProgress, details.Status);
        var task = Assert.Single(details.Tasks);
        Assert.Equal("TEST \\ 0001 \\ 0001", task.FullCode);
        Assert.Equal("Изучить JOIN", task.Body);
        await client.PutAsJsonAsync($"/api/projects/{project.Id}/status", new SetProjectStatusRequest(ProjectStatus.Closed));
        details = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(ProjectStatus.Closed, details.Status);
        Assert.Equal(task, Assert.Single(details.Tasks));
        var missing = await client.GetAsync("/api/projects/999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Task_edits_persist_all_fields_and_tasks_are_filtered_by_status()
    {
        var subject = await CreateSubject("Предмет");
        var project = await CreateProject(subject.Id, "Проект");
        var active = await CreateProjectTask(project.Id, "Активная");
        var deferred = await CreateProjectTask(project.Id, "Отложенная");
        var closed = await CreateProjectTask(project.Id, "Закрытая");
        Assert.Equal(ProjectTaskStatus.InProgress, active.Status);
        var request = new UpdateProjectTaskRequest(" Новое название ", " Новое описание ", ProjectTaskStatus.Deferred);
        var response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/tasks/{deferred.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        Assert.Equal("Новое название", updated.Name);
        Assert.Equal(deferred.Code, updated.Code);
        Assert.Equal("Новое описание", updated.Body);
        Assert.Equal("TEST \\ 0001 \\ 0002", updated.FullCode);
        Assert.Equal(ProjectTaskStatus.Deferred, updated.Status);
        Assert.Equal(deferred.Id, updated.Id);
        Assert.Equal(project.Id, updated.ProjectId);
        await client.PutAsJsonAsync($"/api/projects/{project.Id}/tasks/{closed.Id}", new UpdateProjectTaskRequest(closed.Name, closed.Body, ProjectTaskStatus.Closed));
        var details = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(3, details.Tasks.Count);
        Assert.Equal(updated, details.Tasks.Single(task => task.Id == deferred.Id));
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks"))!.Count);
        foreach (var (id, status) in new[] { (active.Id, ProjectTaskStatus.InProgress), (deferred.Id, ProjectTaskStatus.Deferred), (closed.Id, ProjectTaskStatus.Closed) })
        {
            var filtered = await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks?status={status}");
            Assert.Equal(id, Assert.Single(filtered!).Id);
            Assert.Equal(status, filtered![0].Status);
        }
        response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/tasks/{deferred.Id}", request with { Status = ProjectTaskStatus.InProgress });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks?status=Deferred"))!);
    }

    [Fact]
    public async Task Invalid_task_edits_and_status_filters_are_rejected_without_saving()
    {
        var subject = await CreateSubject("Предмет");
        var project = await CreateProject(subject.Id, "Проект");
        var task = await CreateProjectTask(project.Id, "Задача");
        var valid = new UpdateProjectTaskRequest("Новое имя", "Новое описание", ProjectTaskStatus.Deferred);
        foreach (var request in new[] { valid with { Name = " " }, valid with { Name = new string('a', 201) },
            valid with { Body = " " }, valid with { Body = new string('a', ProjectTask.MaxBodyLength + 1) },
            valid with { Status = (ProjectTaskStatus)999 } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/projects/{project.Id}/tasks/{task.Id}", request)).StatusCode);
            Assert.Equal(task, Assert.Single((await client.GetFromJsonAsync<List<ProjectTaskDto>>($"/api/projects/{project.Id}/tasks"))!));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/projects/{project.Id}/tasks?status=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/projects/{project.Id}/tasks?status=unknown")).StatusCode);
    }

    [Fact]
    public async Task Project_settings_persist_and_keep_codes_unchanged()
    {
        var subject = await CreateSubject("Предмет");
        var project = await CreateProject(subject.Id, "Проект");
        var task = await CreateProjectTask(project.Id, "Задача");
        var response = await client.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest(" Новое имя ", ProjectStatus.Deferred));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("Новое имя", updated.Name);
        Assert.Equal(project.Code, updated.Code);
        Assert.Equal("TEST \\ 0001", updated.FullCode);
        Assert.Equal(ProjectStatus.Deferred, updated.Status);
        Assert.Equal(subject.Id, updated.SubjectId);
        Assert.Equal(task with { FullCode = "TEST \\ 0001 \\ 0001" }, Assert.Single(updated.Tasks));
        var persisted = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(updated.Name, persisted.Name);
        Assert.Equal(updated.Code, persisted.Code);
        Assert.Equal(updated.Status, persisted.Status);
        Assert.Equal(updated.Tasks, persisted.Tasks);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects?status=InProgress"))!);
        var otherSubject = await CreateSubject("Другой предмет");
        await CreateProject(otherSubject.Id, "Другой");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest("Имя", ProjectStatus.Closed))).StatusCode);
    }

    [Fact]
    public async Task Invalid_project_settings_are_rejected_without_partial_updates()
    {
        var subject = await CreateSubject("Предмет");
        var project = await CreateProject(subject.Id, "Проект");
        await CreateProject(subject.Id, "Другой");
        var valid = new UpdateProjectRequest("Изменено", ProjectStatus.Closed);
        foreach (var request in new[] { valid with { Name = " " }, valid with { Name = new string('a', 201) },
            valid with { Status = (ProjectStatus)999 } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/projects/{project.Id}", request)).StatusCode);
        }
        var persisted = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(project.Name, persisted.Name);
        Assert.Equal(project.Code, persisted.Code);
        Assert.Equal(project.Status, persisted.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/projects/999", valid)).StatusCode);
    }

    [Fact]
    public async Task Subject_codes_accept_eight_characters_and_reject_nine_on_create_and_edit()
    {
        var subjectResponse = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Предмет", "12345678"));
        Assert.Equal(HttpStatusCode.Created, subjectResponse.StatusCode);
        var subject = (await subjectResponse.Content.ReadFromJsonAsync<SubjectDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Другой", "123456789"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest("Изменено", "123456789"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}/short-name", new SetSubjectShortNameRequest("123456789"))).StatusCode);
        Assert.Equal(subject, await client.GetFromJsonAsync<SubjectDto>($"/api/subjects/{subject.Id}"));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/subjects/{subject.Id}", new UpdateSubjectRequest(subject.Name, "ABCDEFGH"))).StatusCode);

    }

    [Fact]
    public async Task Project_description_is_created_edited_cleared_and_preserved_by_status_changes()
    {
        var subject = await CreateSubject("Предмет");
        var response = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects",
            new CreateProjectRequest("Проект", " Описание\nВторая строка "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal("Описание\nВторая строка", project.Description);
        Assert.Equal(project.Description, (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.Description);
        Assert.Equal(project.Description, Assert.Single((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!).Description);
        response = await client.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest(project.Name, project.Status, new string('я', 4000)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4000, (await response.Content.ReadFromJsonAsync<ProjectDto>())!.Description.Length);
        response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/status", new SetProjectStatusRequest(ProjectStatus.Deferred));
        Assert.Equal(new string('я', 4000), (await response.Content.ReadFromJsonAsync<ProjectDto>())!.Description);
        response = await client.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest(project.Name, ProjectStatus.Deferred, ""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.Description);
    }

    [Fact]
    public async Task Overlong_project_description_is_rejected_on_create_and_edit_without_partial_saving()
    {
        var subject = await CreateSubject("Предмет");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects",
            new CreateProjectRequest("Проект", new string('a', 4001)))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ProjectDto>>($"/api/subjects/{subject.Id}/projects"))!);
        var project = await CreateProject(subject.Id, "Проект");
        Assert.Equal("", project.Description);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/projects/{project.Id}",
            new UpdateProjectRequest("Новое имя", ProjectStatus.Closed, new string('a', 4001)))).StatusCode);
        var unchanged = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        Assert.Equal(project.Name, unchanged.Name);
        Assert.Equal(project.Code, unchanged.Code);
        Assert.Equal(project.Status, unchanged.Status);
        Assert.Equal("", unchanged.Description);
    }

    private async Task<ProjectTaskDto> CreateProjectTask(int projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new CreateProjectTaskRequest(name, "Описание"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
    }

    private async Task<ProjectDto> CreateProject(int subjectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/subjects/{subjectId}/projects", new CreateProjectRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private async Task<SubjectDto> CreateSubject(string name)
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(name, "TEST"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubjectDto>())!;
    }

    private async Task<NoteDto> CreateNote(int subjectId, DateOnly date, string text)
    {
        var response = await client.PostAsJsonAsync("/api/notes", new SaveNoteRequest(subjectId, date, text));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    public void Dispose() { client.Dispose(); factory.Dispose(); }
}


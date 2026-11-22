using System.Net;
using System.Net.Http.Json;
using JChrono.Application;
using JChrono.Domain;
using Xunit;

namespace JChrono.Tests;

public sealed class DesktopApiTests
{
    private const string Stickers = "/api/desktop/stickers";

    [Fact]
    public async Task All_note_kinds_can_be_pinned_and_keep_their_live_content()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        var date = new DateOnly(2026, 11, 21);
        var time = new TimeOnly(14, 25);
        var subject = await Post<CreateSubjectRequest, SubjectDto>("/api/subjects", new("Разработка", "DEV"));
        var project = await Post<CreateProjectRequest, ProjectDto>($"/api/subjects/{subject.Id}/projects", new("Практика"));
        var task = await Post<CreateProjectTaskRequest, ProjectTaskDto>($"/api/projects/{project.Id}/tasks", new("Задача", "Описание"));
        const string diaryUrl = "/api/diary/notes";
        var diary = await Post<SaveDiaryNoteRequest, DiaryNoteDto>(diaryUrl, new(date, time, "Дневник", 4));
        var subjectNote = await Post<SaveNoteRequest, NoteDto>("/api/notes", new(subject.Id, date, "Предмет", time, 4));
        var projectUrl = $"/api/projects/{project.Id}/notes";
        var projectNote = await Post<SaveProjectNoteRequest, ProjectNoteDto>(projectUrl, new(date, "Проект", time, 4));
        var taskUrl = $"/api/projects/{project.Id}/tasks/{task.Id}/notes";
        var taskNote = await Post<SaveTaskNoteRequest, TaskNoteDto>(taskUrl, new(date, "Задача", time, 4));
        var notes = new[] { (diary.Id, "diary"), (subjectNote.Id, "subject"), (projectNote.Id, "project"), (taskNote.Id, "task") };
        Assert.Equal(4, notes.Select(note => note.Id).Distinct().Count());

        foreach (var (id, kind) in notes)
        {
            var pinUrl = $"{Stickers}/notes/{id}";
            using var pinnedResponse = await client.PutAsync(pinUrl, null);
            Assert.Equal(HttpStatusCode.OK, pinnedResponse.StatusCode);
            var pinned = (await pinnedResponse.Content.ReadFromJsonAsync<StickerDto>())!;
            Assert.Null(pinned.Text);
            Assert.Null(pinned.Task);
            var note = Assert.IsType<DesktopNoteDto>(pinned.Note);
            Assert.Equal((id, kind, time, 4), (note.Id, note.Kind, note.Time, note.Rating));
            using var move = await client.PutAsJsonAsync($"{Stickers}/{pinned.Id}/position", new MoveStickerRequest(0.1, 0.8));
            move.EnsureSuccessStatusCode();
            using var repeated = await client.PutAsync(pinUrl, null);
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Equal(pinned with { X = 0.1, Y = 0.8 }, await repeated.Content.ReadFromJsonAsync<StickerDto>());
            using var deleteAsText = await client.DeleteAsync($"{Stickers}/{pinned.Id}/text");
            Assert.Equal(HttpStatusCode.NoContent, deleteAsText.StatusCode);
            Assert.Equal(new TaskPinStatusDto(true), await client.GetFromJsonAsync<TaskPinStatusDto>(pinUrl));
        }

        using var edit = await client.PutAsJsonAsync($"{taskUrl}/{taskNote.Id}", new SaveTaskNoteRequest(date, "Новый текст", time, 5));
        edit.EnsureSuccessStatusCode();
        var stickers = (await client.GetFromJsonAsync<StickerDto[]>(Stickers))!;
        var updated = Assert.Single(stickers, sticker => sticker.Note?.Id == taskNote.Id).Note!;
        Assert.Equal(("Новый текст", 5, project.Id, task.Id), (updated.Text, updated.Rating, updated.ProjectId, updated.TaskId));
        Assert.Equal(notes.Select(note => note.Id), stickers.Select(sticker => sticker.Note!.Id));
        var taskNotePinUrl = $"{Stickers}/notes/{taskNote.Id}";
        using var unpin = await client.DeleteAsync(taskNotePinUrl);
        using var repeatedUnpin = await client.DeleteAsync(taskNotePinUrl);
        Assert.Equal(HttpStatusCode.NoContent, unpin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedUnpin.StatusCode);
        Assert.Equal(new TaskPinStatusDto(false), await client.GetFromJsonAsync<TaskPinStatusDto>(taskNotePinUrl));
        Assert.Equal(taskNote.Id, Assert.Single((await client.GetFromJsonAsync<TaskNoteDto[]>(taskUrl))!).Id);

        using var wrongScope = await client.DeleteAsync($"{diaryUrl}/{projectNote.Id}");
        Assert.Equal(HttpStatusCode.NotFound, wrongScope.StatusCode);
        using var delete = await client.DeleteAsync($"{projectUrl}/{projectNote.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(new TaskPinStatusDto(false), await client.GetFromJsonAsync<TaskPinStatusDto>($"{Stickers}/notes/{projectNote.Id}"));
        using var missing = await client.PutAsync($"{Stickers}/notes/{projectNote.Id}", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        async Task<TResponse> Post<TRequest, TResponse>(string url, TRequest value)
        {
            using var response = await client.PostAsJsonAsync(url, value);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<TResponse>())!;
        }
    }

    [Fact]
    public async Task Text_sticker_can_be_edited_and_keeps_its_last_valid_position()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        using var empty = await client.PostAsJsonAsync($"{Stickers}/text", new SaveTextStickerRequest("  "));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        using var response = await client.PostAsJsonAsync($"{Stickers}/text", new SaveTextStickerRequest("  Заметка  "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sticker = (await response.Content.ReadFromJsonAsync<StickerDto>())!;
        Assert.Equal("Заметка", sticker.Text);
        Assert.Null(sticker.Task);
        Assert.Null(sticker.X);
        Assert.Null(sticker.Y);

        var positionUrl = $"{Stickers}/{sticker.Id}/position";
        var positioned = sticker with { X = 0.25, Y = 0.75 };
        using var moved = await client.PutAsJsonAsync(positionUrl, new MoveStickerRequest(0.25, 0.75));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(positioned, await moved.Content.ReadFromJsonAsync<StickerDto>());
        foreach (var position in new[]
        {
            new MoveStickerRequest(1.01, 0.5),
            new MoveStickerRequest(0.5, -0.01),
            new MoveStickerRequest(null, 0.5),
            new MoveStickerRequest(0.5, null)
        })
        {
            using var invalid = await client.PutAsJsonAsync(positionUrl, position);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(positioned, Assert.Single((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!));
        }

        var textUrl = $"{Stickers}/{sticker.Id}/text";
        const string markdown = "## Заголовок\n\n- Первый пункт";
        var expected = positioned with { Text = markdown };
        using var edit = await client.PutAsJsonAsync(textUrl, new SaveTextStickerRequest(markdown));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(expected, await edit.Content.ReadFromJsonAsync<StickerDto>());
        using var invalidEdit = await client.PutAsJsonAsync(textUrl, new SaveTextStickerRequest("  "));
        Assert.Equal(HttpStatusCode.BadRequest, invalidEdit.StatusCode);

        var saved = Assert.Single((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public async Task Deleting_a_text_sticker_persists_and_can_be_retried()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"{Stickers}/text", new SaveTextStickerRequest("Заметка"));
        response.EnsureSuccessStatusCode();
        var sticker = (await response.Content.ReadFromJsonAsync<StickerDto>())!;

        var url = $"{Stickers}/{sticker.Id}/text";
        using var deleted = await client.DeleteAsync(url);
        using var retried = await client.DeleteAsync(url);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);
    }

    [Fact]
    public async Task Task_pin_can_be_toggled_and_follows_task_renames_and_deletion()
    {
        using var factory = new NotesFactory();
        using var client = factory.CreateClient();
        using var subjectResponse = await client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest("Разработка", "DEV"));
        var subject = (await subjectResponse.Content.ReadFromJsonAsync<SubjectDto>())!;
        using var projectResponse = await client.PostAsJsonAsync($"/api/subjects/{subject.Id}/projects", new CreateProjectRequest("Практика"));
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;
        var tasks = $"/api/projects/{project.Id}/tasks";
        using var taskResponse = await client.PostAsJsonAsync(tasks, new CreateProjectTaskRequest("Задача", "Описание"));
        var task = (await taskResponse.Content.ReadFromJsonAsync<ProjectTaskDto>())!;
        var pinUrl = $"{Stickers}/tasks/{task.Id}";
        Assert.Equal(new TaskPinStatusDto(false), await client.GetFromJsonAsync<TaskPinStatusDto>(pinUrl));
        using var pinnedResponse = await client.PutAsync(pinUrl, null);
        Assert.Equal(HttpStatusCode.OK, pinnedResponse.StatusCode);
        var pinned = (await pinnedResponse.Content.ReadFromJsonAsync<StickerDto>())!;
        using var taskEdit = await client.PutAsJsonAsync($"{Stickers}/{pinned.Id}/text", new SaveTextStickerRequest("Текст"));
        Assert.Equal(HttpStatusCode.BadRequest, taskEdit.StatusCode);
        Assert.Null(pinned.Text);
        Assert.Equal(new DesktopTaskDto(task.Id, project.Id, subject.Id, task.Code, task.FullCode, task.Name), pinned.Task);
        using var move = await client.PutAsJsonAsync($"{Stickers}/{pinned.Id}/position", new MoveStickerRequest(0.2, 0.7));
        move.EnsureSuccessStatusCode();
        using var repeatedResponse = await client.PutAsync(pinUrl, null);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(pinned with { X = 0.2, Y = 0.7 }, await repeatedResponse.Content.ReadFromJsonAsync<StickerDto>());
        using var deleteAsText = await client.DeleteAsync($"{Stickers}/{pinned.Id}/text");
        Assert.Equal(HttpStatusCode.NoContent, deleteAsText.StatusCode);
        Assert.Equal(new TaskPinStatusDto(true), await client.GetFromJsonAsync<TaskPinStatusDto>(pinUrl));
        Assert.Single((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);

        using var rename = await client.PutAsJsonAsync($"{tasks}/{task.Id}",
            new UpdateProjectTaskRequest("Новое название", task.Body, ProjectTaskStatus.InProgress));
        rename.EnsureSuccessStatusCode();
        var updated = Assert.Single((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);
        Assert.Equal("Новое название", updated.Task!.Name);

        using var unpinned = await client.DeleteAsync(pinUrl);
        using var repeatedUnpin = await client.DeleteAsync(pinUrl);
        Assert.Equal(HttpStatusCode.NoContent, unpinned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedUnpin.StatusCode);
        Assert.Equal(new TaskPinStatusDto(false), await client.GetFromJsonAsync<TaskPinStatusDto>(pinUrl));
        Assert.Empty((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);
        using var repinned = await client.PutAsync(pinUrl, null);
        repinned.EnsureSuccessStatusCode();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"{tasks}/{task.Id}")
        {
            Content = JsonContent.Create(new DeleteTaskRequest(task.Code))
        };
        using var deleted = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<StickerDto[]>(Stickers))!);
        using var missing = await client.PutAsync(pinUrl, null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}

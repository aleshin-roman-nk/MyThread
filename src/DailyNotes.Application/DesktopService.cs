using DailyNotes.Domain;

namespace DailyNotes.Application;

public record DesktopTaskDto(int Id, int ProjectId, int SubjectId, string Code, string FullCode, string Name);
public record DesktopNoteDto(int Id, string Kind, string Source, DateOnly Date, TimeOnly Time, string Text, int Rating,
    int? SubjectId, int? ProjectId, int? TaskId);
public record StickerDto(int Id, string? Text, DesktopTaskDto? Task, double? X, double? Y, DesktopNoteDto? Note = null);
public record TaskPinStatusDto(bool Pinned);
public record SaveTextStickerRequest(string? Text);
public record MoveStickerRequest(double? X, double? Y);

public interface IDesktopRepository
{
    Task<IReadOnlyList<Sticker>> GetStickers(CancellationToken ct);
    Task<Sticker?> FindSticker(int id, CancellationToken ct);
    Task AddSticker(Sticker sticker, CancellationToken ct);
    Task DeleteTextSticker(int id, CancellationToken ct);
    Task<bool> IsTaskPinned(int taskId, CancellationToken ct);
    Task<Sticker?> PinTask(int taskId, CancellationToken ct);
    Task UnpinTask(int taskId, CancellationToken ct);
    Task<bool> IsNotePinned(int noteId, CancellationToken ct);
    Task<Sticker?> PinNote(int noteId, CancellationToken ct);
    Task UnpinNote(int noteId, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class DesktopService(IDesktopRepository repository)
{
    public async Task<IReadOnlyList<StickerDto>> GetStickers(CancellationToken ct) =>
        (await repository.GetStickers(ct)).Select(ToDto).ToList();

    public async Task<StickerDto> CreateText(SaveTextStickerRequest request, CancellationToken ct)
    {
        var sticker = new Sticker(request.Text);
        await repository.AddSticker(sticker, ct);
        await repository.SaveChanges(ct);
        return ToDto(sticker);
    }

    public async Task<StickerDto> UpdateText(int id, SaveTextStickerRequest request, CancellationToken ct)
    {
        var sticker = await FindSticker(id, ct);
        sticker.UpdateText(request.Text);
        await repository.SaveChanges(ct);
        return ToDto(sticker);
    }

    public async Task<TaskPinStatusDto> GetTaskPinStatus(int taskId, CancellationToken ct) =>
        new(await repository.IsTaskPinned(taskId, ct));

    public Task DeleteText(int id, CancellationToken ct) => repository.DeleteTextSticker(id, ct);

    public async Task<StickerDto> PinTask(int taskId, CancellationToken ct) =>
        ToDto(await repository.PinTask(taskId, ct) ?? throw new NotFoundException("Задача не найдена."));

    public Task UnpinTask(int taskId, CancellationToken ct) => repository.UnpinTask(taskId, ct);

    public async Task<TaskPinStatusDto> GetNotePinStatus(int noteId, CancellationToken ct) =>
        new(await repository.IsNotePinned(noteId, ct));

    public async Task<StickerDto> PinNote(int noteId, CancellationToken ct) =>
        ToDto(await repository.PinNote(noteId, ct) ?? throw new NotFoundException("Заметка не найдена."));

    public Task UnpinNote(int noteId, CancellationToken ct) => repository.UnpinNote(noteId, ct);

    public async Task<StickerDto> Move(int id, MoveStickerRequest request, CancellationToken ct)
    {
        var sticker = await FindSticker(id, ct);
        sticker.Move(request.X, request.Y);
        await repository.SaveChanges(ct);
        return ToDto(sticker);
    }

    private async Task<Sticker> FindSticker(int id, CancellationToken ct) =>
        await repository.FindSticker(id, ct) ?? throw new NotFoundException("Стикер не найден.");

    private static StickerDto ToDto(Sticker sticker) => new(sticker.Id, sticker.Text,
        sticker.Task is { } task ? new(task.Id, task.ProjectId, task.Project.SubjectId, task.Code, task.FullCode, task.Name) : null,
        sticker.X, sticker.Y, sticker.Note is { } note ? ToDto(note) : null);

    private static DesktopNoteDto ToDto(Note note) => new(note.Id, note.Kind.ToString().ToLowerInvariant(),
        note.Task?.Name ?? note.Project?.Name ?? note.Subject?.Name ?? "Мой дневник",
        note.Date, note.Time, note.Text, note.Rating, note.SubjectId, note.ProjectId ?? note.Task?.ProjectId, note.TaskId);
}

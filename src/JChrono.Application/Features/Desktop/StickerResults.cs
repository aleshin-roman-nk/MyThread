using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

internal static class StickerResults
{
    public static StickerDto ToDto(Sticker sticker) => new(sticker.Id, sticker.Text,
        sticker.Task is { } task ? new(task.Id, task.ProjectId, task.Project.SubjectId, task.Code, task.FullCode, task.Name) : null,
        sticker.X, sticker.Y, sticker.Note is { } note ? ToDto(note) : null);

    private static DesktopNoteDto ToDto(Note note) => new(note.Id, note.Kind.ToString().ToLowerInvariant(),
        note.Task?.Name ?? note.Project?.Name ?? note.Subject?.Name ?? "Мой дневник",
        note.Date, note.Time, note.Text, note.Rating, note.SubjectId, note.ProjectId ?? note.Task?.ProjectId, note.TaskId);
}

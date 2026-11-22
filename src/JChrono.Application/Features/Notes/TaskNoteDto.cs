using JChrono.Domain;

namespace JChrono.Application;

public sealed record TaskNoteDto(int Id, int TaskId, DateOnly Date, string Text, TimeOnly Time, int Rating)
{
    internal static TaskNoteDto FromNote(Note note, int taskId) =>
        new(note.Id, taskId, note.Date, note.Text, note.Time, note.Rating);
}

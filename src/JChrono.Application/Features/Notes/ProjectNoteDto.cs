using JChrono.Domain;

namespace JChrono.Application;

public sealed record ProjectNoteDto(int Id, int ProjectId, DateOnly Date, string Text, TimeOnly Time, int Rating)
{
    internal static ProjectNoteDto FromNote(Note note, int projectId) =>
        new(note.Id, projectId, note.Date, note.Text, note.Time, note.Rating);
}

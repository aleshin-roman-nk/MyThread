using JChrono.Domain;

namespace JChrono.Application;

public sealed record DiaryNoteDto(int Id, DateOnly Date, TimeOnly Time, string Text, int Rating)
{
    internal static DiaryNoteDto FromNote(Note note) => new(note.Id, note.Date, note.Time, note.Text, note.Rating);
}

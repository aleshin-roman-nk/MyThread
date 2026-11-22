using JChrono.Domain;

namespace JChrono.Application;

public sealed record NoteDto(int Id, int SubjectId, string SubjectName, DateOnly Date, string Text, TimeOnly Time, int Rating)
{
    internal static NoteDto FromNote(Note note, SubjectDto subject) =>
        new(note.Id, subject.Id, subject.Name, note.Date, note.Text, note.Time, note.Rating);
}

using JChrono.Domain;

namespace JChrono.Infrastructure.Features.Diary;

internal static class DiaryNoteQueries
{
    public static IQueryable<Note> DiaryNotes(this NotesDbContext db) =>
        db.Notes.Where(note => note.SubjectId == null && note.ProjectId == null && note.TaskId == null);
}

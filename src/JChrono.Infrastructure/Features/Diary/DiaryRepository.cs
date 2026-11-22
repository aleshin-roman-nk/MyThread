using JChrono.Application;
using JChrono.Application.Features.Diary;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Diary;

public sealed class DiaryRepository(NotesDbContext db) : ICreateDiaryNoteStore, IUpdateDiaryNoteStore, IDeleteDiaryNoteStore
{
    public Task<Note?> FindNote(int id, CancellationToken ct) =>
        db.DiaryNotes().SingleOrDefaultAsync(note => note.Id == id, ct);

    public Task AddNote(Note note, CancellationToken ct)
    {
        db.Notes.Add(note);
        return SaveChanges(ct);
    }

    public async Task<bool> DeleteNote(int id, CancellationToken ct) =>
        await db.DiaryNotes().Where(note => note.Id == id).ExecuteDeleteAsync(ct) != 0;

    public async Task SaveChanges(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotFoundException("Заметка дневника не найдена.");
        }
    }
}

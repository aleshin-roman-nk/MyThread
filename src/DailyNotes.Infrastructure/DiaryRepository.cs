using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyNotes.Infrastructure;

public sealed class DiaryRepository(NotesDbContext db) : IDiaryRepository
{
    private IQueryable<Note> DiaryNotes => db.Notes.Where(note =>
        note.SubjectId == null && note.ProjectId == null && note.TaskId == null);

    public async Task<IReadOnlyList<DiaryNoteDto>> GetNotes(DateOnly from, DateOnly through, CancellationToken ct) =>
        await DiaryNotes.AsNoTracking()
            .Where(note => note.Date >= from && note.Date <= through)
            .OrderBy(note => note.Date).ThenBy(note => note.Time).ThenBy(note => note.Id)
            .Select(note => new DiaryNoteDto(note.Id, note.Date, note.Time, note.Text, note.Rating))
            .ToListAsync(ct);

    public Task<Note?> FindNote(int id, CancellationToken ct) =>
        DiaryNotes.SingleOrDefaultAsync(note => note.Id == id, ct);

    public Task AddNote(Note note, CancellationToken ct)
    {
        db.Notes.Add(note);
        return SaveNote(ct);
    }

    public Task UpdateNote(Note note, CancellationToken ct) => SaveNote(ct);

    public async Task<bool> DeleteNote(int id, CancellationToken ct) =>
        await DiaryNotes.Where(note => note.Id == id).ExecuteDeleteAsync(ct) != 0;

    private async Task SaveNote(CancellationToken ct)
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

using JChrono.Application;
using JChrono.Application.Features.Diary;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Diary;

public sealed class ListDiaryNotesQuery(NotesDbContext db) : IDiaryNotesQuery
{
    public async Task<IReadOnlyList<DiaryNoteDto>> List(DateOnly from, DateOnly through, CancellationToken ct) =>
        await db.DiaryNotes().AsNoTracking()
            .Where(note => note.Date >= from && note.Date <= through)
            .OrderBy(note => note.Date).ThenBy(note => note.Time).ThenBy(note => note.Id)
            .Select(note => new DiaryNoteDto(note.Id, note.Date, note.Time, note.Text, note.Rating))
            .ToListAsync(ct);
}

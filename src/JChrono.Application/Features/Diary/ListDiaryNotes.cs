namespace JChrono.Application.Features.Diary;

public interface IDiaryNotesQuery
{
    Task<IReadOnlyList<DiaryNoteDto>> List(DateOnly from, DateOnly through, CancellationToken ct);
}

public sealed class ListDiaryNotes(IDiaryNotesQuery query)
{
    public Task<IReadOnlyList<DiaryNoteDto>> Execute(DateOnly from, DateOnly through, CancellationToken ct)
    {
        if (from == default || through < from || through.DayNumber - from.DayNumber >= 31)
            throw new ArgumentException("Укажите период от 1 до 31 дня.");

        return query.List(from, through, ct);
    }
}

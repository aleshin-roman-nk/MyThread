namespace JChrono.Application.Features.Notes;

public interface INotesByDateQuery
{
    Task<IReadOnlyList<NoteDto>> ListByDate(DateOnly date, CancellationToken ct);
}

public sealed class GetNotes(INotesByDateQuery query)
{
    public Task<IReadOnlyList<NoteDto>> Execute(DateOnly date, CancellationToken ct) =>
        query.ListByDate(date, ct);
}

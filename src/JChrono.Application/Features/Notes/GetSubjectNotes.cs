namespace JChrono.Application.Features.Notes;

public interface ISubjectNotesQuery
{
    Task<bool> SubjectExists(int subjectId, CancellationToken ct);
    Task<IReadOnlyList<NoteDto>> ListBySubject(int subjectId, CancellationToken ct);
}

public sealed class GetSubjectNotes(ISubjectNotesQuery query)
{
    public async Task<IReadOnlyList<NoteDto>> Execute(int subjectId, CancellationToken ct)
    {
        if (!await query.SubjectExists(subjectId, ct))
            throw new NotFoundException("Предмет не найден.");

        return await query.ListBySubject(subjectId, ct);
    }
}

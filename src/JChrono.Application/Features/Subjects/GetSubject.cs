namespace JChrono.Application.Features.Subjects;

public interface ISubjectQuery
{
    Task<SubjectDto?> Find(int id, CancellationToken ct);
}

public sealed class GetSubject(ISubjectQuery query)
{
    public async Task<SubjectDto> Execute(int id, CancellationToken ct) =>
        await query.Find(id, ct) ?? throw new NotFoundException("Предмет не найден.");
}

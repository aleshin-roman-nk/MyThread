namespace JChrono.Application.Features.Subjects;

public interface ISubjectsQuery
{
    Task<IReadOnlyList<SubjectDto>> List(CancellationToken ct);
}

public sealed class GetSubjects(ISubjectsQuery query)
{
    public Task<IReadOnlyList<SubjectDto>> Execute(CancellationToken ct) => query.List(ct);
}

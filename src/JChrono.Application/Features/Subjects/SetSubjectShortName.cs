using JChrono.Domain;

namespace JChrono.Application.Features.Subjects;

public interface ISetSubjectShortNameStore
{
    Task<Subject?> FindSubject(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class SetSubjectShortName(ISetSubjectShortNameStore store, ISubjectQuery query)
{
    public async Task<SubjectDto> Execute(int id, SetSubjectShortNameRequest request, CancellationToken ct)
    {
        var subject = await store.FindSubject(id, ct) ?? throw new NotFoundException("Предмет не найден.");
        subject.SetShortName(request.ShortName);
        await store.SaveChanges(ct);
        return await query.Find(id, ct) ?? throw new NotFoundException("Предмет не найден.");
    }
}

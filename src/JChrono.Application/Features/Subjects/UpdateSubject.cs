using JChrono.Domain;

namespace JChrono.Application.Features.Subjects;

public interface IUpdateSubjectStore
{
    Task<Subject?> FindSubject(int id, CancellationToken ct);
    Task<bool> SubjectNameExists(string normalizedName, CancellationToken ct, int? excludingId);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateSubject(IUpdateSubjectStore store, ISubjectQuery query)
{
    public async Task<SubjectDto> Execute(int id, UpdateSubjectRequest request, CancellationToken ct)
    {
        var subject = await store.FindSubject(id, ct) ?? throw new NotFoundException("Предмет не найден.");
        subject.Update(request.Name, request.ShortName, request.Description);
        if (await store.SubjectNameExists(subject.NormalizedName, ct, id))
            throw new ConflictException("Предмет с таким названием уже существует.");

        await store.SaveChanges(ct);
        return await query.Find(id, ct) ?? throw new NotFoundException("Предмет не найден.");
    }
}

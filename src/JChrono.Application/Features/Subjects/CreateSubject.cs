using JChrono.Domain;

namespace JChrono.Application.Features.Subjects;

public interface ICreateSubjectStore
{
    Task<bool> SubjectNameExists(string normalizedName, CancellationToken ct, int? excludingId = null);
    Task AddSubject(Subject subject, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateSubject(ICreateSubjectStore store)
{
    public async Task<SubjectDto> Execute(CreateSubjectRequest request, CancellationToken ct)
    {
        var subject = new Subject(request.Name, request.ShortName, request.Description);
        if (await store.SubjectNameExists(subject.NormalizedName, ct))
            throw new ConflictException("Предмет с таким названием уже существует.");

        await store.AddSubject(subject, ct);
        await store.SaveChanges(ct);
        return new(subject.Id, subject.Name, 0, subject.ShortName, Description: subject.Description);
    }
}

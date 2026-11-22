using JChrono.Domain;

namespace JChrono.Application.Features.Planning;

public interface ICreatePlannedActivityStore
{
    Task<PlannedActivityDto> Create(PlannedActivity activity, CancellationToken ct);
}

public sealed class CreatePlannedActivity(ICreatePlannedActivityStore store)
{
    public Task<PlannedActivityDto> Execute(SavePlannedActivityRequest request, CancellationToken ct)
    {
        if (request.SubjectId is null)
            throw new ArgumentException("Выберите предмет для нового занятия.");

        return store.Create(request.ToActivity(), ct);
    }
}

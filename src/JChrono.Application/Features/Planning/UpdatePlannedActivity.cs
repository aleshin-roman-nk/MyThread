using JChrono.Domain;

namespace JChrono.Application.Features.Planning;

public interface IUpdatePlannedActivityStore
{
    Task<PlannedActivityDto> Update(int id, PlannedActivity activity, CancellationToken ct);
}

public sealed class UpdatePlannedActivity(IUpdatePlannedActivityStore store)
{
    public Task<PlannedActivityDto> Execute(int id, SavePlannedActivityRequest request, CancellationToken ct) =>
        store.Update(id, request.ToActivity(), ct);
}

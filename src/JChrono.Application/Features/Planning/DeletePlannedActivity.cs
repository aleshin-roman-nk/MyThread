namespace JChrono.Application.Features.Planning;

public interface IDeletePlannedActivityStore
{
    Task<bool> Delete(int id, CancellationToken ct);
}

public sealed class DeletePlannedActivity(IDeletePlannedActivityStore store)
{
    public async Task Execute(int id, CancellationToken ct)
    {
        if (!await store.Delete(id, ct))
            throw new NotFoundException("Занятие не найдено.");
    }
}

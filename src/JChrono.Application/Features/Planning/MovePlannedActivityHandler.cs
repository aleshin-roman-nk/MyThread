namespace JChrono.Application.Features.Planning;

public interface IMovePlannedActivityStore
{
    Task<PlannedActivityDto> Move(int id, MovePlannedActivity position, CancellationToken ct);
}

public sealed class MovePlannedActivityHandler(IMovePlannedActivityStore store)
{
    public Task<PlannedActivityDto> Execute(int id, MovePlannedActivity position, CancellationToken ct)
    {
        if (id <= 0 || position.DurationMinutes <= 0 ||
            position.OriginalStart.Kind != DateTimeKind.Unspecified || position.Start.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException("Укажите занятие и его местное время.");

        return store.Move(id, position, ct);
    }
}

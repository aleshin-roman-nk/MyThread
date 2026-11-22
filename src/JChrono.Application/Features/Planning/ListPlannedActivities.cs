namespace JChrono.Application.Features.Planning;

public interface IPlannedActivitiesQuery
{
    Task<IReadOnlyList<PlannedActivityDto>> List(DateOnly from, DateOnly through, CancellationToken ct);
}

public sealed class ListPlannedActivities(IPlannedActivitiesQuery query)
{
    public Task<IReadOnlyList<PlannedActivityDto>> Execute(DateOnly from, DateOnly through, CancellationToken ct)
    {
        if (through < from || through.DayNumber - from.DayNumber >= 42)
            throw new ArgumentException("Укажите период от 1 до 42 дней.");

        return query.List(from, through, ct);
    }
}

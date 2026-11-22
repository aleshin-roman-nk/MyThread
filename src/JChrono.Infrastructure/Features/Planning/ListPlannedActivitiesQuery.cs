using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Planning;

public sealed class ListPlannedActivitiesQuery(NotesDbContext db) : IPlannedActivitiesQuery
{
    public async Task<IReadOnlyList<PlannedActivityDto>> List(DateOnly from, DateOnly through, CancellationToken ct)
    {
        var activities = await db.ReadPlannedActivities().Where(x => x.Date >= from && x.Date <= through)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToListAsync(ct);
        return activities.Select(PlannedActivityQueries.ToDto).ToList();
    }
}

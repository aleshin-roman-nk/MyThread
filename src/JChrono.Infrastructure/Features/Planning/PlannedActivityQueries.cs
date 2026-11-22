using JChrono.Application;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Planning;

internal static class PlannedActivityQueries
{
    public static IQueryable<PlannedActivity> ReadPlannedActivities(this NotesDbContext db) =>
        db.PlannedActivities.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.References).ThenInclude(x => x.Project)
            .Include(x => x.References).ThenInclude(x => x.Task).ThenInclude(x => x!.Project);

    public static PlannedActivityDto ToDto(PlannedActivity activity) =>
        new(activity.Id, activity.Date, activity.StartTime, activity.DurationMinutes, activity.End, activity.Description,
            activity.Subject is { } subject ? new(subject.Id, subject.Name, subject.ShortName) : null,
            activity.References.OrderBy(x => x.Id).Select(reference =>
                reference.Task is { } task
                    ? new PlannedActivityReferenceDto(task.ProjectId, task.Id, task.Name, task.Project.Name)
                    : new PlannedActivityReferenceDto(reference.ProjectId!.Value, null, reference.Project!.Name)).ToList());

    public static async Task EnsureTimeIsAvailable(
        this NotesDbContext db, PlannedActivity activity, int? excludedId, CancellationToken ct)
    {
        var start = activity.Date.ToDateTime(activity.StartTime);
        var end = activity.End;
        var endDate = DateOnly.FromDateTime(end);
        var startSeconds = (start.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;
        var endSeconds = (end.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;

        // The caller's write transaction serializes this check with other reservations.
        var overlaps = await db.PlannedActivities.FromSql($"""
            SELECT * FROM PlannedActivities
            WHERE Date <= {endDate}
                AND unixepoch(Date || ' ' || StartTime) < {endSeconds}
                AND unixepoch(Date || ' ' || StartTime) + DurationMinutes * 60 > {startSeconds}
            """).AnyAsync(x => x.Id != excludedId, ct);
        if (overlaps)
            throw new ConflictException("Это время пересекается с другим занятием. Выберите свободное время.");
    }
}

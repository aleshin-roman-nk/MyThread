using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyNotes.Infrastructure;

public sealed class PlanningRepository(NotesDbContext db) : IPlanningRepository
{
    public async Task<IReadOnlyList<PlannedActivityDto>> Get(DateOnly from, DateOnly through, CancellationToken ct)
    {
        var activities = await ReadActivities().Where(x => x.Date >= from && x.Date <= through)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToListAsync(ct);
        return activities.Select(ToDto).ToList();
    }

    public async Task<PlannedActivityDto> Save(int? id, PlannedActivity activity, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var projectIds = activity.References.Where(x => x.ProjectId.HasValue).Select(x => x.ProjectId!.Value).ToArray();
        var taskIds = activity.References.Where(x => x.TaskId.HasValue).Select(x => x.TaskId!.Value).ToArray();

        if (activity.SubjectId is int subjectId)
        {
            if (!await db.Subjects.AnyAsync(x => x.Id == subjectId, ct))
                throw new ArgumentException("Выбранный предмет не найден.");
            if (projectIds.Length > 0 && await db.Projects.CountAsync(
                x => x.SubjectId == subjectId && projectIds.Contains(x.Id), ct) != projectIds.Length)
                throw new ArgumentException("Все выбранные проекты должны относиться к предмету.");
            if (taskIds.Length > 0 && await db.ProjectTasks.CountAsync(
                x => x.Project.SubjectId == subjectId && taskIds.Contains(x.Id), ct) != taskIds.Length)
                throw new ArgumentException("Все выбранные задачи должны относиться к предмету.");
        }

        var current = id is int activityId
            ? await db.PlannedActivities.Include(x => x.References).SingleOrDefaultAsync(x => x.Id == activityId, ct)
                ?? throw new NotFoundException("Занятие не найдено.")
            : null;
        await EnsureTimeIsAvailable(activity, id, ct);

        if (current is not null)
        {
            current.Update(activity.Date, activity.StartTime, activity.DurationMinutes, activity.Description,
                activity.SubjectId, projectIds, taskIds);
            activity = current;
        }
        else
        {
            db.PlannedActivities.Add(activity);
        }

        await db.SaveChangesAsync(ct);
        var saved = await ReadActivities().SingleAsync(x => x.Id == activity.Id, ct);
        await transaction.CommitAsync(ct);
        return ToDto(saved);
    }

    public async Task<bool> Delete(int id, CancellationToken ct) =>
        await db.PlannedActivities.Where(x => x.Id == id).ExecuteDeleteAsync(ct) != 0;

    private async Task EnsureTimeIsAvailable(PlannedActivity activity, int? id, CancellationToken ct)
    {
        var start = activity.Date.ToDateTime(activity.StartTime);
        var end = activity.End;
        var endDate = DateOnly.FromDateTime(end);
        var startSeconds = (start.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;
        var endSeconds = (end.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;

        // The save transaction serializes writers so another request cannot reserve the same time.
        var overlaps = await db.PlannedActivities.FromSql($"""
            SELECT * FROM PlannedActivities
            WHERE Date <= {endDate}
                AND unixepoch(Date || ' ' || StartTime) < {endSeconds}
                AND unixepoch(Date || ' ' || StartTime) + DurationMinutes * 60 > {startSeconds}
            """).AnyAsync(x => x.Id != id, ct);
        if (overlaps)
            throw new ConflictException("Это время пересекается с другим занятием. Выберите свободное время.");
    }

    private IQueryable<PlannedActivity> ReadActivities() =>
        db.PlannedActivities.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.References).ThenInclude(x => x.Project)
            .Include(x => x.References).ThenInclude(x => x.Task).ThenInclude(x => x!.Project);

    private static PlannedActivityDto ToDto(PlannedActivity activity) =>
        new(activity.Id, activity.Date, activity.StartTime, activity.DurationMinutes, activity.End, activity.Description,
            activity.Subject is { } subject ? new(subject.Id, subject.Name, subject.ShortName) : null,
            activity.References.OrderBy(x => x.Id).Select(reference =>
                reference.Task is { } task
                    ? new PlannedActivityReferenceDto(task.ProjectId, task.Id, task.Name)
                    : new PlannedActivityReferenceDto(reference.ProjectId!.Value, null, reference.Project!.Name)).ToList());
}

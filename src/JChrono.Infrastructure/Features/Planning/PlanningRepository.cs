using JChrono.Application;
using JChrono.Application.Features.Planning;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Planning;

public sealed class PlanningRepository(NotesDbContext db)
    : ICreatePlannedActivityStore, IUpdatePlannedActivityStore, IDeletePlannedActivityStore
{
    public Task<PlannedActivityDto> Create(PlannedActivity activity, CancellationToken ct) =>
        Save(null, activity, ct);

    public Task<PlannedActivityDto> Update(int id, PlannedActivity activity, CancellationToken ct) =>
        Save(id, activity, ct);

    public async Task<bool> Delete(int id, CancellationToken ct) =>
        await db.PlannedActivities.Where(x => x.Id == id).ExecuteDeleteAsync(ct) != 0;

    private async Task<PlannedActivityDto> Save(int? id, PlannedActivity activity, CancellationToken ct)
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
        await db.EnsureTimeIsAvailable(activity, id, ct);

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
        var saved = await db.ReadPlannedActivities().SingleAsync(x => x.Id == activity.Id, ct);
        await transaction.CommitAsync(ct);
        return PlannedActivityQueries.ToDto(saved);
    }
}

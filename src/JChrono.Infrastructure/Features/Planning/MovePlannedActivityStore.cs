using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Planning;

public sealed class MovePlannedActivityStore(NotesDbContext db) : IMovePlannedActivityStore
{
    public async Task<PlannedActivityDto> Move(int id, MovePlannedActivity position, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var activity = await db.PlannedActivities.SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Занятие не найдено.");
        if (activity.Date.ToDateTime(activity.StartTime) != position.OriginalStart ||
            activity.DurationMinutes != position.DurationMinutes)
            throw new ConflictException("Расписание изменилось. Повторите перенос после обновления.");

        activity.Reschedule(position.Start);
        await db.EnsureTimeIsAvailable(activity, id, ct);
        await db.SaveChangesAsync(ct);
        var saved = await db.ReadPlannedActivities().SingleAsync(item => item.Id == id, ct);
        await transaction.CommitAsync(ct);
        return PlannedActivityQueries.ToDto(saved);
    }
}

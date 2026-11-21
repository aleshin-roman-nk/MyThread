using DailyNotes.Domain;

namespace DailyNotes.Application;

public sealed record PlannedActivitySubjectDto(int Id, string Name, string ShortName);
public sealed record PlannedActivityReferenceDto(int ProjectId, int? TaskId, string Name);
public sealed record PlannedActivityDto(int Id, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTime End, string Description, PlannedActivitySubjectDto? Subject, IReadOnlyList<PlannedActivityReferenceDto> References);
public sealed record SavePlannedActivityRequest(DateOnly? Date, TimeOnly? StartTime, int DurationMinutes,
    string? Description, int? SubjectId, int[]? ProjectIds, int[]? TaskIds);

public interface IPlanningRepository
{
    Task<IReadOnlyList<PlannedActivityDto>> Get(DateOnly from, DateOnly through, CancellationToken ct);
    Task<PlannedActivityDto> Save(int? id, PlannedActivity activity, CancellationToken ct);
    Task<bool> Delete(int id, CancellationToken ct);
}

public sealed class PlanningService(IPlanningRepository repository)
{
    public Task<IReadOnlyList<PlannedActivityDto>> Get(DateOnly from, DateOnly through, CancellationToken ct)
    {
        if (through < from || through.DayNumber - from.DayNumber >= 42)
            throw new ArgumentException("Укажите период от 1 до 42 дней.");
        return repository.Get(from, through, ct);
    }

    public Task<PlannedActivityDto> Save(int? id, SavePlannedActivityRequest request, CancellationToken ct) =>
        repository.Save(id, new PlannedActivity(
            request.Date ?? throw new ArgumentException("Укажите дату занятия."),
            request.StartTime ?? throw new ArgumentException("Укажите время начала занятия."),
            request.DurationMinutes, request.Description, request.SubjectId,
            request.ProjectIds ?? [], request.TaskIds ?? []), ct);

    public async Task Delete(int id, CancellationToken ct)
    {
        if (!await repository.Delete(id, ct))
            throw new NotFoundException("Занятие не найдено.");
    }
}

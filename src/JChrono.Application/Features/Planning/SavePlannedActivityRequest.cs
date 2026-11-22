using JChrono.Domain;

namespace JChrono.Application;

public sealed record SavePlannedActivityRequest(DateOnly? Date, TimeOnly? StartTime, int DurationMinutes,
    string? Description, int? SubjectId, int[]? ProjectIds, int[]? TaskIds)
{
    internal PlannedActivity ToActivity() => new(
        Date ?? throw new ArgumentException("Укажите дату занятия."),
        StartTime ?? throw new ArgumentException("Укажите время начала занятия."),
        DurationMinutes, Description, SubjectId, ProjectIds ?? [], TaskIds ?? []);
}

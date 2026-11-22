namespace JChrono.Application;

public sealed record PlannedActivitySubjectDto(int Id, string Name, string ShortName);
public sealed record PlannedActivityReferenceDto(int ProjectId, int? TaskId, string Name, string? ProjectName = null);
public sealed record PlannedActivityDto(int Id, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTime End, string Description, PlannedActivitySubjectDto? Subject, IReadOnlyList<PlannedActivityReferenceDto> References);

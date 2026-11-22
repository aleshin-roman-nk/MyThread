using JChrono.Domain;

namespace JChrono.Application;

public sealed record ProjectTaskDto(int Id, int ProjectId, string Name, string Code, string FullCode, string Body, ProjectTaskStatus Status, DateTimeOffset? CreatedAt = null);
public sealed record SessionDto(int Id, int SubjectId, int? ProjectId, int? TaskId,
    DateTimeOffset StartTime, DateTimeOffset? EndTime, SessionStatus Status, string Text, TimeSpan? Duration);
public sealed record DaySessionDto(SessionDto Session, string FullCode, string TargetName);
public sealed record ClosedSessionTimeDto(int SubjectId, DateTimeOffset StartTime, DateTimeOffset EndTime);
public sealed record UsefulWorkDto(double TotalSeconds);

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);


using DailyNotes.Domain;

namespace DailyNotes.Application;

public sealed record SubjectDto(int Id, string Name, int NoteCount, string ShortName,
    double ClosedSessionHours = 0, int ProjectCount = 0, int InProgressTaskCount = 0, int DeferredTaskCount = 0, string Description = "");
public sealed record ProjectTaskDto(int Id, int ProjectId, string Name, string Code, string FullCode, string Body, ProjectTaskStatus Status, DateTimeOffset? CreatedAt = null);
public sealed record UpdateProjectTaskRequest(string Name, string Body, ProjectTaskStatus Status);
public sealed record ProjectDto(int Id, int SubjectId, string Name, string Code, string FullCode, IReadOnlyList<ProjectTaskDto> Tasks, ProjectStatus Status, string Description, DateTimeOffset? CreatedAt = null);
public sealed record SetProjectStatusRequest(ProjectStatus Status);
public sealed record CreateProjectRequest(string Name, string Description = "");
public sealed record UpdateProjectRequest(string Name, ProjectStatus Status, string Description = "");
public sealed record CreateProjectTaskRequest(string Name, string Body);
public sealed record SetSubjectShortNameRequest(string ShortName);
public sealed record UpdateSubjectRequest(string Name, string ShortName, string Description = "");
public sealed record DeleteSubjectRequest(string ConfirmationShortName);
public sealed record DeleteProjectRequest(string ConfirmationCode);
public sealed record DeleteTaskRequest(string ConfirmationCode);
public sealed record NoteDto(int Id, int SubjectId, string SubjectName, DateOnly Date, string Text, TimeOnly Time, int Rating);
public sealed record SaveNoteRequest(int SubjectId, DateOnly Date, string Text, TimeOnly? Time = null, int? Rating = null);
public sealed record ProjectNoteDto(int Id, int ProjectId, DateOnly Date, string Text, TimeOnly Time, int Rating);
public sealed record SaveProjectNoteRequest(DateOnly Date, string Text, TimeOnly? Time = null, int? Rating = null);
public sealed record TaskNoteDto(int Id, int TaskId, DateOnly Date, string Text, TimeOnly Time, int Rating);
public sealed record SaveTaskNoteRequest(DateOnly Date, string Text, TimeOnly? Time = null, int? Rating = null);
public sealed record CreateSubjectRequest(string Name, string ShortName, string Description = "");
public sealed record SessionDto(int Id, int SubjectId, int? ProjectId, int? TaskId,
    DateTimeOffset StartTime, DateTimeOffset? EndTime, SessionStatus Status, string Text, TimeSpan? Duration);
public sealed record DaySessionDto(SessionDto Session, string FullCode, string TargetName);
public sealed record UsefulWorkDto(double TotalSeconds);
public sealed record CreateSessionRequest(int SubjectId, int? ProjectId = null, int? TaskId = null, string Text = "");
public sealed record UpdateSessionTextRequest(string Text);
public sealed record UpdateSessionRequest(DateTimeOffset? StartTime, DateTimeOffset? EndTime, string Text);

public interface INotesRepository
{
    Task<IReadOnlyList<TaskNoteDto>> GetTaskNotes(int taskId, CancellationToken ct);
    Task<Note?> FindTaskNote(int projectId, int taskId, int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectNoteDto>> GetProjectNotes(int projectId, CancellationToken ct);
    Task<Note?> FindProjectNote(int projectId, int id, CancellationToken ct);
    Task<Session?> FindSession(int id, CancellationToken ct);
    Task<Session?> FindCurrentSession(CancellationToken ct);
    Task<IReadOnlyList<Session>> GetSubjectSessions(int subjectId, CancellationToken ct);
    Task<IReadOnlyList<Session>> GetProjectSessions(int projectId, CancellationToken ct);
    Task<IReadOnlyList<Session>> GetTaskSessions(int taskId, CancellationToken ct);
    Task<IReadOnlyList<Session>> GetSessions(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct);
    Task<IReadOnlyList<Session>> GetClosedSessionsOverlapping(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct);
    Task<ProjectTask?> FindSessionTask(int id, CancellationToken ct);
    Task AddSession(Session session, CancellationToken ct);
    void RemoveSession(Session session);
    Task<IReadOnlyList<SubjectDto>> GetSubjects(CancellationToken ct);
    Task<SubjectDto?> GetSubject(int id, CancellationToken ct);
    Task<bool> SubjectNameExists(string normalizedName, CancellationToken ct, int? excludingId = null);
    Task AddSubject(Subject subject, CancellationToken ct);
    Task DeleteSubject(int id, string confirmationShortName, CancellationToken ct);
    Task DeleteProject(int id, string confirmationCode, CancellationToken ct);
    Task DeleteProjectTask(int projectId, int id, string confirmationCode, CancellationToken ct);
    Task<Subject?> FindSubject(int id, CancellationToken ct);
    Task<Project?> FindProject(int id, CancellationToken ct);
    Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetProjects(int subjectId, CancellationToken ct, ProjectStatus? status = null);
    Task<Project> CreateProject(int subjectId, string name, string description, CancellationToken ct);
    Task<ProjectTask> CreateProjectTask(int projectId, string name, string body, CancellationToken ct);
    Task<IReadOnlyList<NoteDto>> GetNotes(DateOnly? date, int? subjectId, CancellationToken ct);
    Task<Note?> FindSubjectNote(int id, CancellationToken ct);
    Task AddNote(Note note, CancellationToken ct);
    void RemoveNote(Note note);
    Task SaveChanges(CancellationToken ct);
}

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);


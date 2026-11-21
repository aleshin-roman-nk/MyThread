using DailyNotes.Domain;

namespace DailyNotes.Application;

public sealed class SessionsService(INotesRepository repository, TimeProvider clock)
{
    public async Task<UsefulWorkDto> GetUsefulWork(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        ValidateDayBounds(startTime, endTime);
        var start = startTime!.Value;
        var end = endTime!.Value;
        var sessions = await repository.GetClosedSessionsOverlapping(start, end, ct);
        var seconds = sessions.Sum(session =>
        {
            var clippedStart = session.StartTime < start ? start : session.StartTime;
            var clippedEnd = session.EndTime!.Value > end ? end : session.EndTime.Value;
            return (clippedEnd - clippedStart).TotalSeconds;
        });
        return new UsefulWorkDto(seconds);
    }

    private static void ValidateDayBounds(DateTimeOffset? startTime, DateTimeOffset? endTime)
    {
        if (!startTime.HasValue || !endTime.HasValue || endTime <= startTime)
            throw new ArgumentException("Укажите начало и конец выбранного дня; конец должен быть позже начала.");
    }

    public async Task<IReadOnlyList<DaySessionDto>> GetDaySessions(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        ValidateDayBounds(startTime, endTime);
        return (await repository.GetSessions(startTime!.Value, endTime!.Value, ct))
            .OrderBy(s => s.StartTime).ThenBy(s => s.Id)
            .Select(s => new DaySessionDto(ToDto(s), s.Task?.FullCode ?? s.Project?.FullCode ?? s.Subject.ShortName,
                s.Task?.Name ?? s.Project?.Name ?? s.Subject.Name)).ToList();
    }

    public async Task<SessionDto?> GetCurrent(CancellationToken ct) =>
        await repository.FindCurrentSession(ct) is { } session ? ToDto(session) : null;

    public async Task<SessionDto> Get(int id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task Delete(int id, CancellationToken ct)
    {
        repository.RemoveSession(await Find(id, ct));
        await repository.SaveChanges(ct);
    }

    public async Task<IReadOnlyList<SessionDto>> GetSubjectSessions(int subjectId, CancellationToken ct)
    {
        if (await repository.FindSubject(subjectId, ct) is null) throw new NotFoundException("Предмет не найден.");
        return (await repository.GetSubjectSessions(subjectId, ct)).OrderBy(s => s.StartTime).ThenBy(s => s.Id).Select(ToDto).ToList();
    }

    public async Task<SessionDto> Create(CreateSessionRequest request, CancellationToken ct)
    {
        if (await repository.FindCurrentSession(ct) is not null)
            throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");
        var subject = await repository.FindSubject(request.SubjectId, ct) ?? throw new NotFoundException("Предмет не найден.");
        var project = request.ProjectId.HasValue
            ? await repository.FindProject(request.ProjectId.Value, ct) ?? throw new NotFoundException("Проект не найден.") : null;
        var task = request.TaskId.HasValue
            ? await repository.FindSessionTask(request.TaskId.Value, ct) ?? throw new NotFoundException("Задача не найдена.") : null;
        var session = new Session(subject, clock.GetUtcNow(), request.Text, project, task);
        await repository.AddSession(session, ct);
        await repository.SaveChanges(ct);
        return ToDto(session);
    }

    public async Task<IReadOnlyList<SessionDto>> GetProjectSessions(int projectId, CancellationToken ct)
    {
        if (await repository.FindProject(projectId, ct) is null) throw new NotFoundException("Проект не найден.");
        return (await repository.GetProjectSessions(projectId, ct)).OrderBy(s => s.StartTime).ThenBy(s => s.Id).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SessionDto>> GetTaskSessions(int projectId, int taskId, CancellationToken ct)
    {
        if (await repository.FindProjectTask(projectId, taskId, ct) is null) throw new NotFoundException("Задача не найдена.");
        return (await repository.GetTaskSessions(taskId, ct)).OrderBy(s => s.StartTime).ThenBy(s => s.Id).Select(ToDto).ToList();
    }

    public async Task<SessionDto> Close(int id, CancellationToken ct)
    {
        var session = await Find(id, ct);
        if (session.Status == SessionStatus.Closed) throw new ConflictException("Сессия уже закрыта.");
        session.Close(clock.GetUtcNow());
        await repository.SaveChanges(ct);
        return ToDto(session);
    }

    public async Task<SessionDto> Update(int id, UpdateSessionRequest request, CancellationToken ct)
    {
        var session = await Find(id, ct);
        if (!request.StartTime.HasValue) throw new ArgumentException("Укажите время начала сессии.");
        if (!request.EndTime.HasValue && await repository.FindCurrentSession(ct) is { } current && current.Id != id)
            throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");
        session.Update(request.StartTime.Value, request.EndTime, request.Text);
        await repository.SaveChanges(ct);
        return ToDto(session);
    }

    public async Task<SessionDto> UpdateText(int id, UpdateSessionTextRequest request, CancellationToken ct)
    {
        var session = await Find(id, ct);
        session.UpdateText(request.Text);
        await repository.SaveChanges(ct);
        return ToDto(session);
    }

    private async Task<Session> Find(int id, CancellationToken ct) =>
        await repository.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");

    private static SessionDto ToDto(Session session) => new(session.Id, session.SubjectId, session.ProjectId,
        session.TaskId, session.StartTime, session.EndTime, session.Status, session.Text, session.Duration);
}

using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DailyNotes.Infrastructure;

public sealed class NotesRepository(NotesDbContext db) : INotesRepository
{
    public async Task DeleteProjectTask(int projectId, int id, string confirmationCode, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var task = await db.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id, ct)
            ?? throw new NotFoundException("Задача не найдена.");
        if (!string.Equals(confirmationCode, task.Code, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точный код задачи.");
        await db.Sessions.Where(s => s.TaskId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.TaskId == id).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.Id == id && t.ProjectId == projectId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteProject(int id, string confirmationCode, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Проект не найден.");
        if (!string.Equals(confirmationCode, project.Code, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точный код проекта.");

        await db.Sessions.Where(s => s.ProjectId == id || (s.Task != null && s.Task.ProjectId == id)).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.ProjectId == id || (n.Task != null && n.Task.ProjectId == id)).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.ProjectId == id).ExecuteDeleteAsync(ct);
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteSubject(int id, string confirmationShortName, CancellationToken ct)
    {
        // Keep validation and all deletes in one transaction, including on existing databases
        // whose foreign keys deliberately use RESTRICT.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var subject = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Предмет не найден.");
        if (!string.Equals(confirmationShortName, subject.ShortName, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точное короткое имя предмета.");

        await db.Sessions.Where(s => s.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.SubjectId == id || (n.Project != null && n.Project.SubjectId == id)
            || (n.Task != null && n.Task.Project.SubjectId == id)).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.Project.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Projects.Where(p => p.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Subjects.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<TaskNoteDto>> GetTaskNotes(int taskId, CancellationToken ct) =>
        await db.Notes.AsNoTracking().Where(n => n.TaskId == taskId)
            .OrderByDescending(n => n.Date).ThenByDescending(n => n.Time).ThenByDescending(n => n.Id)
            .Select(n => new TaskNoteDto(n.Id, taskId, n.Date, n.Text, n.Time, n.Rating)).ToListAsync(ct);
    public Task<Note?> FindTaskNote(int projectId, int taskId, int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(n => n.Id == id && n.TaskId == taskId && n.Task!.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<ProjectNoteDto>> GetProjectNotes(int projectId, CancellationToken ct) =>
        await db.Notes.AsNoTracking().Where(n => n.ProjectId == projectId)
            .OrderByDescending(n => n.Date).ThenByDescending(n => n.Time).ThenByDescending(n => n.Id)
            .Select(n => new ProjectNoteDto(n.Id, projectId, n.Date, n.Text, n.Time, n.Rating)).ToListAsync(ct);

    public Task<Note?> FindProjectNote(int projectId, int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(n => n.Id == id && n.ProjectId == projectId, ct);

    public Task<Session?> FindSession(int id, CancellationToken ct) => db.Sessions.SingleOrDefaultAsync(s => s.Id == id, ct);
    public Task<Session?> FindCurrentSession(CancellationToken ct) => db.Sessions.SingleOrDefaultAsync(s => s.EndTime == null, ct);
    public async Task<IReadOnlyList<Session>> GetSubjectSessions(int subjectId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(s => s.SubjectId == subjectId).ToListAsync(ct);
    public async Task<IReadOnlyList<Session>> GetProjectSessions(int projectId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(s => s.ProjectId == projectId).ToListAsync(ct);
    public async Task<IReadOnlyList<Session>> GetTaskSessions(int taskId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(s => s.TaskId == taskId).ToListAsync(ct);
    public async Task<IReadOnlyList<Session>> GetSessions(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct)
    {
        // All stored session times and these bounds use UTC. SQLite can compare their
        // canonical text values; EF's DateTimeOffset range comparison is unsupported.
        var start = startTime.ToUniversalTime();
        var end = endTime.ToUniversalTime();
        return await db.Sessions.FromSqlInterpolated($"SELECT * FROM Sessions WHERE StartTime >= {start} AND StartTime < {end}")
            .AsNoTracking().Include(s => s.Subject).Include(s => s.Project).ThenInclude(p => p!.Subject)
            .Include(s => s.Task).ThenInclude(t => t!.Project).ThenInclude(p => p.Subject).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<Session>> GetClosedSessionsOverlapping(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct)
    {
        var start = startTime.ToUniversalTime();
        var end = endTime.ToUniversalTime();
        return await db.Sessions.FromSqlInterpolated($"SELECT * FROM Sessions WHERE EndTime IS NOT NULL AND StartTime < {end} AND EndTime > {start}")
            .AsNoTracking().ToListAsync(ct);
    }

    public Task<ProjectTask?> FindSessionTask(int id, CancellationToken ct) =>
        db.ProjectTasks.Include(t => t.Project).ThenInclude(p => p.Subject).SingleOrDefaultAsync(t => t.Id == id, ct);
    public async Task AddSession(Session session, CancellationToken ct) => await db.Sessions.AddAsync(session, ct);
    public void RemoveSession(Session session) => db.Sessions.Remove(session);
    public Task<IReadOnlyList<SubjectDto>> GetSubjects(CancellationToken ct) => ReadSubjects(null, ct);

    public async Task<SubjectDto?> GetSubject(int id, CancellationToken ct) =>
        (await ReadSubjects(id, ct)).SingleOrDefault();

    private async Task<IReadOnlyList<SubjectDto>> ReadSubjects(int? id, CancellationToken ct)
    {
        var subjects = await db.Subjects.AsNoTracking().Where(s => !id.HasValue || s.Id == id.Value).OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, db.Notes.Count(n => n.SubjectId == s.Id), s.ShortName, 0,
                db.Projects.Count(p => p.SubjectId == s.Id),
                db.ProjectTasks.Count(t => t.Project.SubjectId == s.Id && t.Status == ProjectTaskStatus.InProgress),
                db.ProjectTasks.Count(t => t.Project.SubjectId == s.Id && t.Status == ProjectTaskStatus.Deferred), s.Description))
            .ToListAsync(ct);
        // SQLite does not support subtraction of DateTimeOffset values. Read only
        // the interval fields and sum exact durations in .NET, including multi-day sessions.
        var intervals = await db.Sessions.AsNoTracking()
            .Where(s => s.EndTime != null && (!id.HasValue || s.SubjectId == id.Value))
            .Select(s => new { s.SubjectId, s.StartTime, s.EndTime }).ToListAsync(ct);
        var hours = intervals.GroupBy(s => s.SubjectId)
            .ToDictionary(group => group.Key, group => group.Sum(s => (s.EndTime!.Value - s.StartTime).TotalHours));
        return subjects.Select(s => s with { ClosedSessionHours = hours.GetValueOrDefault(s.Id) }).ToList();
    }

    public Task<bool> SubjectNameExists(string normalizedName, CancellationToken ct, int? excludingId = null) =>
        db.Subjects.AnyAsync(s => s.NormalizedName == normalizedName && (!excludingId.HasValue || s.Id != excludingId.Value), ct);

    public async Task AddSubject(Subject subject, CancellationToken ct) =>
        await db.Subjects.AddAsync(subject, ct);

    public Task<Subject?> FindSubject(int id, CancellationToken ct) => db.Subjects.SingleOrDefaultAsync(s => s.Id == id, ct);
    public Task<Project?> FindProject(int id, CancellationToken ct) =>
        db.Projects.Include(p => p.Subject).Include(p => p.Tasks).SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct) =>
        db.ProjectTasks.Include(t => t.Project).ThenInclude(p => p.Subject)
            .SingleOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId, ct);
    public async Task<IReadOnlyList<Project>> GetProjects(int subjectId, CancellationToken ct, ProjectStatus? status = null) =>
        await db.Projects.AsNoTracking().Include(p => p.Subject).Include(p => p.Tasks)
            .Where(p => p.SubjectId == subjectId && (!status.HasValue || p.Status == status.Value))
            .OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    public async Task<Project> CreateProject(int subjectId, string name, string description, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Subjects SET LastProjectNumber = LastProjectNumber + 1 WHERE Id = {subjectId} AND LastProjectNumber < {GeneratedCode.MaxNumber}", ct);
        if (changed == 0)
        {
            if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct)) throw new NotFoundException("Предмет не найден.");
            throw new ConflictException("Достигнут предел номеров проектов в этом предмете.");
        }
        var subject = await db.Subjects.SingleAsync(s => s.Id == subjectId, ct);
        await db.Entry(subject).ReloadAsync(ct);
        var project = new Project(subject, name, GeneratedCode.Format(subject.LastProjectNumber), description);
        await db.Projects.AddAsync(project, ct);
        await SaveChanges(ct);
        await transaction.CommitAsync(ct);
        return project;
    }

    public async Task<ProjectTask> CreateProjectTask(int projectId, string name, string body, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Projects SET LastTaskNumber = LastTaskNumber + 1 WHERE Id = {projectId} AND LastTaskNumber < {GeneratedCode.MaxNumber}", ct);
        if (changed == 0)
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) throw new NotFoundException("Проект не найден.");
            throw new ConflictException("Достигнут предел номеров задач в этом проекте.");
        }
        var project = await db.Projects.Include(p => p.Subject).SingleAsync(p => p.Id == projectId, ct);
        await db.Entry(project).ReloadAsync(ct);
        var task = new ProjectTask(project, name, GeneratedCode.Format(project.LastTaskNumber), body);
        await db.ProjectTasks.AddAsync(task, ct);
        await SaveChanges(ct);
        await transaction.CommitAsync(ct);
        return task;
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotes(DateOnly? date, int? subjectId, CancellationToken ct)
    {
        var query = db.Notes.AsNoTracking().Where(n => n.SubjectId != null);
        if (date.HasValue) query = query.Where(n => n.Date == date.Value);
        if (subjectId.HasValue) query = query.Where(n => n.SubjectId == subjectId.Value);
        return await query.OrderByDescending(n => n.Date).ThenByDescending(n => n.Time).ThenByDescending(n => n.Id)
            .Select(n => new NoteDto(n.Id, n.SubjectId!.Value, n.Subject!.Name, n.Date, n.Text, n.Time, n.Rating)).ToListAsync(ct);
    }

    public Task<Note?> FindSubjectNote(int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(n => n.Id == id && n.SubjectId != null, ct);
    public async Task AddNote(Note note, CancellationToken ct) => await db.Notes.AddAsync(note, ct);
    public void RemoveNote(Note note) => db.Notes.Remove(note);

    public async Task SaveChanges(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Данные уже изменены другим запросом. Обновите страницу.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            if (ex.Entries.Any(entry => entry.Entity is Session))
                throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");
            throw new ConflictException("Название предмета или код проекта/задачи в этом разделе уже существует.");
        }
    }
}


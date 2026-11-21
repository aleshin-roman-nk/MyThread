using DailyNotes.Domain;

namespace DailyNotes.Application;

public sealed class NotesService(INotesRepository repository)
{
    public async Task<IReadOnlyList<TaskNoteDto>> GetTaskNotes(int projectId, int taskId, CancellationToken ct)
    {
        if (await repository.FindProjectTask(projectId, taskId, ct) is null) throw new NotFoundException("Задача не найдена.");
        return await repository.GetTaskNotes(taskId, ct);
    }

    public async Task<TaskNoteDto> CreateTaskNote(int projectId, int taskId, SaveTaskNoteRequest request, CancellationToken ct)
    {
        if (await repository.FindProjectTask(projectId, taskId, ct) is null) throw new NotFoundException("Задача не найдена.");
        var note = Note.ForTask(taskId, request.Date, request.Time ?? TimeOnly.MinValue, request.Text, request.Rating ?? Note.DefaultRating);
        await repository.AddNote(note, ct);
        await repository.SaveChanges(ct);
        return new(note.Id, taskId, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task<TaskNoteDto> UpdateTaskNote(int projectId, int taskId, int id, SaveTaskNoteRequest request, CancellationToken ct)
    {
        var note = await repository.FindTaskNote(projectId, taskId, id, ct) ?? throw new NotFoundException("Заметка задачи не найдена.");
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        await repository.SaveChanges(ct);
        return new(note.Id, taskId, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task DeleteTaskNote(int projectId, int taskId, int id, CancellationToken ct)
    {
        var note = await repository.FindTaskNote(projectId, taskId, id, ct) ?? throw new NotFoundException("Заметка задачи не найдена.");
        repository.RemoveNote(note);
        await repository.SaveChanges(ct);
    }

    public async Task<IReadOnlyList<ProjectNoteDto>> GetProjectNotes(int projectId, CancellationToken ct)
    {
        if (await repository.FindProject(projectId, ct) is null) throw new NotFoundException("Проект не найден.");
        return await repository.GetProjectNotes(projectId, ct);
    }

    public async Task<ProjectNoteDto> CreateProjectNote(int projectId, SaveProjectNoteRequest request, CancellationToken ct)
    {
        if (await repository.FindProject(projectId, ct) is null) throw new NotFoundException("Проект не найден.");
        var note = Note.ForProject(projectId, request.Date, request.Time ?? TimeOnly.MinValue, request.Text, request.Rating ?? Note.DefaultRating);
        await repository.AddNote(note, ct);
        await repository.SaveChanges(ct);
        return new(note.Id, projectId, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task<ProjectNoteDto> UpdateProjectNote(int projectId, int id, SaveProjectNoteRequest request, CancellationToken ct)
    {
        var note = await repository.FindProjectNote(projectId, id, ct) ?? throw new NotFoundException("Заметка проекта не найдена.");
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        await repository.SaveChanges(ct);
        return new(note.Id, projectId, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task DeleteProjectNote(int projectId, int id, CancellationToken ct)
    {
        var note = await repository.FindProjectNote(projectId, id, ct) ?? throw new NotFoundException("Заметка проекта не найдена.");
        repository.RemoveNote(note);
        await repository.SaveChanges(ct);
    }

    public Task DeleteSubject(int id, DeleteSubjectRequest request, CancellationToken ct) =>
        repository.DeleteSubject(id, request.ConfirmationShortName, ct);

    public Task DeleteProject(int id, DeleteProjectRequest request, CancellationToken ct) =>
        repository.DeleteProject(id, request.ConfirmationCode, ct);

    public Task DeleteProjectTask(int projectId, int id, DeleteTaskRequest request, CancellationToken ct) =>
        repository.DeleteProjectTask(projectId, id, request.ConfirmationCode, ct);

    public Task<IReadOnlyList<SubjectDto>> GetSubjects(CancellationToken ct) => repository.GetSubjects(ct);

    public async Task<SubjectDto> GetSubject(int id, CancellationToken ct) =>
        await repository.GetSubject(id, ct) ?? throw new NotFoundException("Предмет не найден.");

    public async Task<SubjectDto> CreateSubject(CreateSubjectRequest request, CancellationToken ct)
    {
        var subject = new Subject(request.Name, request.ShortName, request.Description);
        if (await repository.SubjectNameExists(subject.NormalizedName, ct))
            throw new ConflictException("Предмет с таким названием уже существует.");
        await repository.AddSubject(subject, ct);
        await repository.SaveChanges(ct);
        return new(subject.Id, subject.Name, 0, subject.ShortName, Description: subject.Description);
    }

    public async Task<SubjectDto> UpdateSubject(int id, UpdateSubjectRequest request, CancellationToken ct)
    {
        var subject = await repository.FindSubject(id, ct) ?? throw new NotFoundException("Предмет не найден.");
        subject.Update(request.Name, request.ShortName, request.Description);
        if (await repository.SubjectNameExists(subject.NormalizedName, ct, id))
            throw new ConflictException("Предмет с таким названием уже существует.");
        await repository.SaveChanges(ct);
        return await GetSubject(id, ct);
    }

    public async Task<SubjectDto> SetSubjectShortName(int id, SetSubjectShortNameRequest request, CancellationToken ct)
    {
        var subject = await repository.FindSubject(id, ct) ?? throw new NotFoundException("Предмет не найден.");
        subject.SetShortName(request.ShortName);
        await repository.SaveChanges(ct);
        return await GetSubject(id, ct);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjects(int subjectId, CancellationToken ct, ProjectStatus? status = null)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ArgumentException("Неизвестное состояние проекта.");
        await GetSubject(subjectId, ct);
        return (await repository.GetProjects(subjectId, ct, status)).Select(ToDto).ToList();
    }

    public async Task<ProjectDto> GetProject(int id, CancellationToken ct) =>
        ToDto(await repository.FindProject(id, ct) ?? throw new NotFoundException("Проект не найден."));

    public async Task<ProjectDto> UpdateProject(int id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await repository.FindProject(id, ct) ?? throw new NotFoundException("Проект не найден.");
        project.Update(request.Name, request.Status, request.Description);
        await repository.SaveChanges(ct);
        return ToDto(project);
    }

    public async Task<ProjectDto> SetProjectStatus(int id, SetProjectStatusRequest request, CancellationToken ct)
    {
        var project = await repository.FindProject(id, ct) ?? throw new NotFoundException("Проект не найден.");
        project.SetStatus(request.Status);
        await repository.SaveChanges(ct);
        return ToDto(project);
    }

    public async Task<ProjectDto> CreateProject(int subjectId, CreateProjectRequest request, CancellationToken ct)
    {
        var project = await repository.CreateProject(subjectId, request.Name, request.Description, ct);
        return ToDto(project);
    }

    public async Task<ProjectTaskDto> CreateProjectTask(int projectId, CreateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await repository.CreateProjectTask(projectId, request.Name, request.Body, ct);
        return ToDto(task);
    }

    public async Task<ProjectTaskDto> UpdateProjectTask(int projectId, int id, UpdateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await repository.FindProjectTask(projectId, id, ct) ?? throw new NotFoundException("Задача не найдена.");
        task.Update(request.Name, request.Body, request.Status);
        await repository.SaveChanges(ct);
        return ToDto(task);
    }

    public async Task<IReadOnlyList<ProjectTaskDto>> GetProjectTasks(int projectId, CancellationToken ct, ProjectTaskStatus? status = null)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ArgumentException("Неизвестный статус задачи.");
        var project = await repository.FindProject(projectId, ct) ?? throw new NotFoundException("Проект не найден.");
        return ToDto(project).Tasks.Where(task => !status.HasValue || task.Status == status.Value).ToList();
    }

    private static ProjectDto ToDto(Project project) => new(project.Id, project.SubjectId, project.Name,
        project.Code, project.FullCode, project.Tasks.OrderBy(t => t.Name).ThenBy(t => t.Id).Select(ToDto).ToList(), project.Status, project.Description, project.CreatedAt);

    private static ProjectTaskDto ToDto(ProjectTask task) =>
        new(task.Id, task.ProjectId, task.Name, task.Code, task.FullCode, task.Body, task.Status, task.CreatedAt);

    public Task<IReadOnlyList<NoteDto>> GetNotes(DateOnly date, CancellationToken ct) =>
        repository.GetNotes(date, null, ct);

    public async Task<IReadOnlyList<NoteDto>> GetSubjectNotes(int subjectId, CancellationToken ct)
    {
        await GetSubject(subjectId, ct);
        return await repository.GetNotes(null, subjectId, ct);
    }

    public async Task<NoteDto> CreateNote(SaveNoteRequest request, CancellationToken ct)
    {
        var subject = await GetSubject(request.SubjectId, ct);
        var note = Note.ForSubject(request.SubjectId, request.Date, request.Time ?? TimeOnly.MinValue, request.Text, request.Rating ?? Note.DefaultRating);
        await repository.AddNote(note, ct);
        await repository.SaveChanges(ct);
        return new(note.Id, subject.Id, subject.Name, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task<NoteDto> UpdateNote(int id, SaveNoteRequest request, CancellationToken ct)
    {
        var note = await FindNote(id, ct);
        var subject = await GetSubject(request.SubjectId, ct);
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        note.SetSubject(request.SubjectId);
        await repository.SaveChanges(ct);
        return new(note.Id, subject.Id, subject.Name, note.Date, note.Text, note.Time, note.Rating);
    }

    public async Task DeleteNote(int id, CancellationToken ct)
    {
        repository.RemoveNote(await FindNote(id, ct));
        await repository.SaveChanges(ct);
    }

    private async Task<Note> FindNote(int id, CancellationToken ct) =>
        await repository.FindSubjectNote(id, ct) ?? throw new NotFoundException("Заметка не найдена.");
}


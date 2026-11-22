namespace JChrono.Domain;

public sealed class Session
{
    public const int MaxTextLength = 1000;
    private Session() { }

    public Session(Subject subject, DateTimeOffset startTime, string text = "",
        Project? project = null, ProjectTask? task = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        project ??= task?.Project;
        if (project is not null && !ReferenceEquals(project.Subject, subject)
            && (subject.Id == 0 || project.SubjectId != subject.Id))
            throw new ArgumentException("Проект сессии должен относиться к выбранному предмету.");
        if (task is not null && !ReferenceEquals(task.Project, project)
            && (project is null || project.Id == 0 || task.ProjectId != project.Id))
            throw new ArgumentException("Задача сессии должна относиться к выбранному проекту.");
        Subject = subject;
        SubjectId = subject.Id;
        Project = project;
        ProjectId = project?.Id;
        Task = task;
        TaskId = task?.Id;
        StartTime = startTime.ToUniversalTime();
        UpdateText(text);
    }

    public int Id { get; private set; }
    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = null!;
    public int? ProjectId { get; private set; }
    public Project? Project { get; private set; }
    public int? TaskId { get; private set; }
    public ProjectTask? Task { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset? EndTime { get; private set; }
    public SessionStatus Status => EndTime.HasValue ? SessionStatus.Closed : SessionStatus.Open;
    public string Text { get; private set; } = "";
    public TimeSpan? Duration => EndTime - StartTime;

    public void Close(DateTimeOffset endTime)
    {
        if (Status == SessionStatus.Closed)
            throw new InvalidOperationException("Сессия уже закрыта.");
        Update(StartTime, endTime, Text);
    }

    public void Update(DateTimeOffset startTime, DateTimeOffset? endTime, string text)
    {
        if (endTime < startTime)
            throw new ArgumentException("Время конца сессии не может быть раньше времени начала.");
        var validText = NormalizeText(text);
        StartTime = startTime.ToUniversalTime();
        EndTime = endTime?.ToUniversalTime();
        Text = validText;
    }

    public void UpdateText(string text) => Text = NormalizeText(text);

    private static string NormalizeText(string text)
    {
        var validText = text?.Trim() ?? "";
        if (validText.Length > MaxTextLength)
            throw new ArgumentException($"Текст сессии должен содержать не больше {MaxTextLength} символов.");
        return validText;
    }
}

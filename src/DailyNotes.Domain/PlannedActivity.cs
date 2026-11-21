namespace DailyNotes.Domain;

public sealed class PlannedActivity
{
    public const int MaxDescriptionLength = 4000;
    private readonly List<PlannedActivityReference> references = [];

    private PlannedActivity() { }

    public PlannedActivity(DateOnly date, TimeOnly startTime, int durationMinutes, string? description,
        int? subjectId, IReadOnlyList<int> projectIds, IReadOnlyList<int> taskIds) =>
        Update(date, startTime, durationMinutes, description, subjectId, projectIds, taskIds);

    public int Id { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public int DurationMinutes { get; private set; }
    public string Description { get; private set; } = "";
    public int? SubjectId { get; private set; }
    public Subject? Subject { get; private set; }
    public IReadOnlyCollection<PlannedActivityReference> References => references.AsReadOnly();
    public DateTime End => Date.ToDateTime(StartTime).AddMinutes(DurationMinutes);

    public void Update(DateOnly date, TimeOnly startTime, int durationMinutes, string? description,
        int? subjectId, IReadOnlyList<int> projectIds, IReadOnlyList<int> taskIds)
    {
        if (startTime.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Укажите время начала с точностью до минуты.");
        if (durationMinutes <= 0 ||
            durationMinutes > (DateTime.MaxValue - date.ToDateTime(startTime)).Ticks / TimeSpan.TicksPerMinute)
            throw new ArgumentException("Укажите допустимую длительность занятия больше нуля.");

        var text = description?.Trim() ?? "";
        if (text.Length > MaxDescriptionLength)
            throw new ArgumentException($"Описание занятия должно содержать не более {MaxDescriptionLength} символов.");
        if (subjectId is null && text.Length == 0)
            throw new ArgumentException("Укажите предмет или опишите занятие.");
        if (subjectId is null && (projectIds.Count != 0 || taskIds.Count != 0))
            throw new ArgumentException("Чтобы добавить проект или задачу, выберите предмет.");

        var targets = projectIds.Select(id => (ProjectId: (int?)id, TaskId: (int?)null))
            .Concat(taskIds.Select(id => (ProjectId: (int?)null, TaskId: (int?)id)))
            .ToHashSet();
        if (targets.Count != projectIds.Count + taskIds.Count)
            throw new ArgumentException("Проект или задачу можно указать только один раз.");

        Date = date;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
        Description = text;
        SubjectId = subjectId;
        references.RemoveAll(reference => !targets.Remove((reference.ProjectId, reference.TaskId)));
        foreach (var (projectId, taskId) in targets)
            references.Add(new PlannedActivityReference(projectId, taskId));
    }
}

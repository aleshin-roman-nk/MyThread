namespace JChrono.Domain;

public sealed class Project
{
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 8;
    public const int MaxDescriptionLength = 4000;
    private readonly List<ProjectTask> tasks = [];
    private Project() { }

    public Project(Subject subject, string name, string code, string description = "", DateTimeOffset? createdAt = null)
    {
        Subject = subject;
        SubjectId = subject.Id;
        CreatedAt = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Code = ProjectCode.Normalize(code, MaxCodeLength, "Код проекта");
        NormalizedCode = Code.ToUpperInvariant();
        Update(name, ProjectStatus.InProgress, description);
    }

    public int Id { get; private set; }
    public DateTimeOffset? CreatedAt { get; private set; }
    public long LastTaskNumber { get; private set; }
    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Code { get; private set; } = "";
    public string NormalizedCode { get; private set; } = "";
    public ProjectStatus Status { get; private set; } = ProjectStatus.InProgress;
    public string FullCode => $"{Subject.ShortName} \\ {Code}";
    public IReadOnlyCollection<ProjectTask> Tasks => tasks.AsReadOnly();

    public void SetStatus(ProjectStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentException("Неизвестное состояние проекта.");
        Status = status;
    }

    public void Update(string name, ProjectStatus status, string description = "")
    {
        var newName = NormalizeName(name);
        var newDescription = description?.Trim() ?? "";
        if (newDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Описание проекта должно содержать не больше {MaxDescriptionLength} символов.");
        if (!Enum.IsDefined(status)) throw new ArgumentException("Неизвестное состояние проекта.");
        Name = newName;
        Description = newDescription;
        Status = status;
    }

    internal static string NormalizeName(string? name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length is 0 or > MaxNameLength)
            throw new ArgumentException($"Название должно содержать от 1 до {MaxNameLength} символов.");
        return value;
    }
}

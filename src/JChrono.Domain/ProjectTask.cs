namespace JChrono.Domain;

public sealed class ProjectTask
{
    public const int MaxBodyLength = 20000;
    private ProjectTask() { }

    public ProjectTask(Project project, string name, string code, string body, DateTimeOffset? createdAt = null)
    {
        Project = project;
        ProjectId = project.Id;
        CreatedAt = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Code = ProjectCode.Normalize(code, ProjectCode.MaxLength, "Код задачи");
        NormalizedCode = Code.ToUpperInvariant();
        Update(name, body, ProjectTaskStatus.InProgress);
    }

    public void Update(string name, string body, ProjectTaskStatus status)
    {
        var validName = global::JChrono.Domain.Project.NormalizeName(name);
        var validBody = body?.Trim() ?? "";
        if (validBody.Length is 0 or > MaxBodyLength)
            throw new ArgumentException($"Описание задачи должно содержать от 1 до {MaxBodyLength} символов.");
        if (!Enum.IsDefined(status)) throw new ArgumentException("Неизвестный статус задачи.");
        Name = validName;
        Body = validBody;
        Status = status;
    }

    public int Id { get; private set; }
    public DateTimeOffset? CreatedAt { get; private set; }
    public int ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public string Code { get; private set; } = "";
    public string NormalizedCode { get; private set; } = "";
    public string Body { get; private set; } = "";
    public ProjectTaskStatus Status { get; private set; } = ProjectTaskStatus.InProgress;
    public string FullCode => $"{Project.FullCode} \\ {Code}";
}

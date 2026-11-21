namespace DailyNotes.Domain;

public sealed class Subject
{
    public const int MaxNameLength = 100;
    public const int MaxShortNameLength = 8;
    public const int MaxDescriptionLength = 4000;
    private readonly List<Project> projects = [];
    private Subject() { }
    public Subject(string name, string shortName, string description = "") => Update(name, shortName, description);

    public void Update(string name, string shortName, string description = "")
    {
        var validName = NormalizeName(name);
        var validShortName = ProjectCode.Normalize(shortName, MaxShortNameLength, "Короткое имя предмета");
        var validDescription = description?.Trim() ?? "";
        if (validDescription.Length > MaxDescriptionLength)
            throw new ArgumentException($"Описание предмета должно содержать не больше {MaxDescriptionLength} символов.");
        Name = validName;
        NormalizedName = Name.ToUpperInvariant();
        ShortName = validShortName;
        Description = validDescription;
    }

    public int Id { get; private set; }
    public long LastProjectNumber { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public string ShortName { get; private set; } = "";
    public string Description { get; private set; } = "";
    public IReadOnlyCollection<Project> Projects => projects.AsReadOnly();

    public void SetShortName(string shortName) =>
        ShortName = ProjectCode.Normalize(shortName, MaxShortNameLength, "Короткое имя предмета");

    public static string NormalizeName(string? name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length is 0 or > MaxNameLength)
            throw new ArgumentException($"Название предмета должно содержать от 1 до {MaxNameLength} символов.");
        return value;
    }
}


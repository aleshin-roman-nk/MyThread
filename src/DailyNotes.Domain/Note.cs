namespace DailyNotes.Domain;

public enum NoteKind { Diary, Subject, Project, Task }

public sealed class Note
{
    public const int MaxTextLength = 4000;
    public const int DefaultRating = 3;

    private Note() { }

    public Note(DateOnly date, TimeOnly time, string? text, int rating) => Update(date, time, text, rating);

    public static Note ForSubject(int subjectId, DateOnly date, TimeOnly time, string? text, int rating)
    {
        var note = new Note(date, time, text, rating);
        note.SetSubject(subjectId);
        return note;
    }

    public static Note ForProject(int projectId, DateOnly date, TimeOnly time, string? text, int rating)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(projectId);
        return new Note(date, time, text, rating) { ProjectId = projectId };
    }

    public static Note ForTask(int taskId, DateOnly date, TimeOnly time, string? text, int rating)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(taskId);
        return new Note(date, time, text, rating) { TaskId = taskId };
    }

    public void SetSubject(int subjectId)
    {
        if (subjectId <= 0) throw new ArgumentException("Выберите предмет.");
        if (ProjectId.HasValue || TaskId.HasValue) throw new InvalidOperationException("У заметки уже есть владелец.");
        SubjectId = subjectId;
    }

    public void Update(DateOnly date, TimeOnly time, string? text, int rating)
    {
        if (date == default) throw new ArgumentException("Укажите дату заметки.");
        var value = text?.Trim() ?? "";
        if (value.Length is 0 or > MaxTextLength)
            throw new ArgumentException($"Текст заметки должен содержать от 1 до {MaxTextLength} символов.");
        if (rating is < 1 or > 5) throw new ArgumentException("Оценка должна быть от 1 до 5.");

        Date = date;
        Time = time;
        Text = value;
        Rating = rating;
    }

    public int Id { get; private set; }
    public int? SubjectId { get; private set; }
    public Subject? Subject { get; private set; }
    public int? ProjectId { get; private set; }
    public Project? Project { get; private set; }
    public int? TaskId { get; private set; }
    public ProjectTask? Task { get; private set; }
    public NoteKind Kind => TaskId.HasValue ? NoteKind.Task : ProjectId.HasValue ? NoteKind.Project
        : SubjectId.HasValue ? NoteKind.Subject : NoteKind.Diary;
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public string Text { get; private set; } = "";
    public int Rating { get; private set; }
}

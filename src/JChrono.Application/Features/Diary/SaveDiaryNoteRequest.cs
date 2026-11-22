namespace JChrono.Application;

public sealed record SaveDiaryNoteRequest(DateOnly Date, TimeOnly? Time, string Text, int Rating)
{
    internal TimeOnly RequiredTime() => Time ?? throw new ArgumentException("Укажите время заметки.");
}

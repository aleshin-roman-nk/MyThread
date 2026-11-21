using DailyNotes.Domain;

namespace DailyNotes.Application;

public sealed record DiaryNoteDto(int Id, DateOnly Date, TimeOnly Time, string Text, int Rating);
public sealed record SaveDiaryNoteRequest(DateOnly Date, TimeOnly? Time, string Text, int Rating);

public interface IDiaryRepository
{
    Task<IReadOnlyList<DiaryNoteDto>> GetNotes(DateOnly from, DateOnly through, CancellationToken ct);
    Task AddNote(Note note, CancellationToken ct);
    Task<Note?> FindNote(int id, CancellationToken ct);
    Task UpdateNote(Note note, CancellationToken ct);
    Task<bool> DeleteNote(int id, CancellationToken ct);
}

public sealed class DiaryService(IDiaryRepository repository)
{
    public Task<IReadOnlyList<DiaryNoteDto>> GetNotes(DateOnly from, DateOnly through, CancellationToken ct)
    {
        if (from == default || through < from || through.DayNumber - from.DayNumber >= 31)
            throw new ArgumentException("Укажите период от 1 до 31 дня.");
        return repository.GetNotes(from, through, ct);
    }

    public async Task<DiaryNoteDto> CreateNote(SaveDiaryNoteRequest request, CancellationToken ct)
    {
        var note = new Note(request.Date, RequiredTime(request), request.Text, request.Rating);
        await repository.AddNote(note, ct);
        return ToDto(note);
    }

    public async Task<DiaryNoteDto> UpdateNote(int id, SaveDiaryNoteRequest request, CancellationToken ct)
    {
        var note = await repository.FindNote(id, ct) ?? throw new NotFoundException("Заметка дневника не найдена.");
        note.Update(request.Date, RequiredTime(request), request.Text, request.Rating);
        await repository.UpdateNote(note, ct);
        return ToDto(note);
    }

    public async Task DeleteNote(int id, CancellationToken ct)
    {
        if (!await repository.DeleteNote(id, ct))
            throw new NotFoundException("Заметка дневника не найдена.");
    }

    private static TimeOnly RequiredTime(SaveDiaryNoteRequest request) =>
        request.Time ?? throw new ArgumentException("Укажите время заметки.");

    private static DiaryNoteDto ToDto(Note note) => new(note.Id, note.Date, note.Time, note.Text, note.Rating);
}

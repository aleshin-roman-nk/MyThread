using JChrono.Domain;

namespace JChrono.Application.Features.Diary;

public interface IUpdateDiaryNoteStore
{
    Task<Note?> FindNote(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateDiaryNote(IUpdateDiaryNoteStore store)
{
    public async Task<DiaryNoteDto> Execute(int id, SaveDiaryNoteRequest request, CancellationToken ct)
    {
        var note = await store.FindNote(id, ct) ?? throw new NotFoundException("Заметка дневника не найдена.");
        note.Update(request.Date, request.RequiredTime(), request.Text, request.Rating);
        await store.SaveChanges(ct);
        return DiaryNoteDto.FromNote(note);
    }
}

namespace JChrono.Application.Features.Diary;

public interface IDeleteDiaryNoteStore
{
    Task<bool> DeleteNote(int id, CancellationToken ct);
}

public sealed class DeleteDiaryNote(IDeleteDiaryNoteStore store)
{
    public async Task Execute(int id, CancellationToken ct)
    {
        if (!await store.DeleteNote(id, ct))
            throw new NotFoundException("Заметка дневника не найдена.");
    }
}

using JChrono.Domain;

namespace JChrono.Application.Features.Diary;

public interface ICreateDiaryNoteStore
{
    Task AddNote(Note note, CancellationToken ct);
}

public sealed class CreateDiaryNote(ICreateDiaryNoteStore store)
{
    public async Task<DiaryNoteDto> Execute(SaveDiaryNoteRequest request, CancellationToken ct)
    {
        var note = new Note(request.Date, request.RequiredTime(), request.Text, request.Rating);
        await store.AddNote(note, ct);
        return DiaryNoteDto.FromNote(note);
    }
}

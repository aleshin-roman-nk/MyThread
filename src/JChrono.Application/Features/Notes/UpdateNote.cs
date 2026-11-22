using JChrono.Application.Features.Subjects;
using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IUpdateNoteStore
{
    Task<Note?> FindSubjectNote(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateNote(IUpdateNoteStore store, ISubjectQuery subjects)
{
    public async Task<NoteDto> Execute(int id, SaveNoteRequest request, CancellationToken ct)
    {
        var note = await store.FindSubjectNote(id, ct) ?? throw new NotFoundException("Заметка не найдена.");
        var subject = await subjects.Find(request.SubjectId, ct) ?? throw new NotFoundException("Предмет не найден.");
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        note.SetSubject(request.SubjectId);
        await store.SaveChanges(ct);
        return NoteDto.FromNote(note, subject);
    }
}

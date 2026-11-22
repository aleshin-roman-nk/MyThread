using JChrono.Application.Features.Subjects;
using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface ICreateNoteStore
{
    Task AddNote(Note note, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateNote(ICreateNoteStore store, ISubjectQuery subjects)
{
    public async Task<NoteDto> Execute(SaveNoteRequest request, CancellationToken ct)
    {
        var subject = await subjects.Find(request.SubjectId, ct) ?? throw new NotFoundException("Предмет не найден.");
        var note = Note.ForSubject(request.SubjectId, request.Date, request.Time ?? TimeOnly.MinValue,
            request.Text, request.Rating ?? Note.DefaultRating);
        await store.AddNote(note, ct);
        await store.SaveChanges(ct);
        return NoteDto.FromNote(note, subject);
    }
}

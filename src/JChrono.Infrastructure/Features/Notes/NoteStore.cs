using JChrono.Application.Features.Notes;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Notes;

public sealed class NoteStore(NotesDbContext db)
    : ICreateNoteStore, IUpdateNoteStore, IDeleteNoteStore,
        ICreateProjectNoteStore, IUpdateProjectNoteStore, IDeleteProjectNoteStore,
        ICreateTaskNoteStore, IUpdateTaskNoteStore, IDeleteTaskNoteStore
{
    public Task<Note?> FindSubjectNote(int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(note => note.Id == id && note.SubjectId != null, ct);

    public Task<Note?> FindProjectNote(int projectId, int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(note => note.Id == id && note.ProjectId == projectId, ct);

    public Task<Note?> FindTaskNote(int projectId, int taskId, int id, CancellationToken ct) =>
        db.Notes.SingleOrDefaultAsync(note => note.Id == id && note.TaskId == taskId && note.Task!.ProjectId == projectId, ct);

    public Task<Project?> FindProject(int id, CancellationToken ct) => db.FindTrackedProject(id, ct);

    public Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct) =>
        db.FindTrackedProjectTask(projectId, id, ct);

    public async Task AddNote(Note note, CancellationToken ct) => await db.Notes.AddAsync(note, ct);

    public void RemoveNote(Note note) => db.Notes.Remove(note);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesWithConflicts(ct);
}

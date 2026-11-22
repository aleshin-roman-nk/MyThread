using JChrono.Application;
using JChrono.Application.Features.Notes;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Notes;

public sealed class NoteQueries(NotesDbContext db)
    : INotesByDateQuery, ISubjectNotesQuery, IProjectNotesQuery, ITaskNotesQuery
{
    public Task<IReadOnlyList<NoteDto>> ListByDate(DateOnly date, CancellationToken ct) =>
        ReadNotes(db.Notes.Where(note => note.SubjectId != null && note.Date == date), ct);

    public Task<bool> SubjectExists(int subjectId, CancellationToken ct) =>
        db.Subjects.AnyAsync(subject => subject.Id == subjectId, ct);

    public Task<IReadOnlyList<NoteDto>> ListBySubject(int subjectId, CancellationToken ct) =>
        ReadNotes(db.Notes.Where(note => note.SubjectId == subjectId), ct);

    public Task<bool> ProjectExists(int projectId, CancellationToken ct) =>
        db.Projects.AnyAsync(project => project.Id == projectId, ct);

    public async Task<IReadOnlyList<ProjectNoteDto>> ListByProject(int projectId, CancellationToken ct) =>
        await InReverseChronologicalOrder(db.Notes.Where(note => note.ProjectId == projectId))
            .Select(note => new ProjectNoteDto(note.Id, projectId, note.Date, note.Text, note.Time, note.Rating))
            .ToListAsync(ct);

    public Task<bool> TaskExistsInProject(int projectId, int taskId, CancellationToken ct) =>
        db.ProjectTasks.AnyAsync(task => task.Id == taskId && task.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<TaskNoteDto>> ListByTask(int taskId, CancellationToken ct) =>
        await InReverseChronologicalOrder(db.Notes.Where(note => note.TaskId == taskId))
            .Select(note => new TaskNoteDto(note.Id, taskId, note.Date, note.Text, note.Time, note.Rating))
            .ToListAsync(ct);

    private static async Task<IReadOnlyList<NoteDto>> ReadNotes(IQueryable<Note> notes, CancellationToken ct) =>
        await InReverseChronologicalOrder(notes)
            .Select(note => new NoteDto(note.Id, note.SubjectId!.Value, note.Subject!.Name, note.Date, note.Text, note.Time, note.Rating))
            .ToListAsync(ct);

    private static IOrderedQueryable<Note> InReverseChronologicalOrder(IQueryable<Note> notes) =>
        notes.AsNoTracking()
            .OrderByDescending(note => note.Date).ThenByDescending(note => note.Time).ThenByDescending(note => note.Id);
}

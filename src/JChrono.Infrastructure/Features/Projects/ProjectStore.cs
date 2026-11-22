using JChrono.Application;
using JChrono.Application.Features.Projects;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Projects;

public sealed class ProjectStore(NotesDbContext db)
    : ICreateProjectStore, IUpdateProjectStore, ISetProjectStatusStore, ICreateProjectTaskStore, IUpdateProjectTaskStore
{
    public Task<Project?> FindProject(int id, CancellationToken ct) => db.FindTrackedProject(id, ct);

    public Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct) =>
        db.FindTrackedProjectTask(projectId, id, ct);

    public async Task<Project> CreateProject(int subjectId, string name, string description, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Subjects SET LastProjectNumber = LastProjectNumber + 1 WHERE Id = {subjectId} AND LastProjectNumber < {GeneratedCode.MaxNumber}", ct);
        if (changed == 0)
        {
            if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct)) throw new NotFoundException("Предмет не найден.");
            throw new ConflictException("Достигнут предел номеров проектов в этом предмете.");
        }
        var subject = await db.Subjects.SingleAsync(s => s.Id == subjectId, ct);
        await db.Entry(subject).ReloadAsync(ct);
        var project = new Project(subject, name, GeneratedCode.Format(subject.LastProjectNumber), description);
        await db.Projects.AddAsync(project, ct);
        await SaveChanges(ct);
        await transaction.CommitAsync(ct);
        return project;
    }

    public async Task<ProjectTask> CreateProjectTask(int projectId, string name, string body, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Projects SET LastTaskNumber = LastTaskNumber + 1 WHERE Id = {projectId} AND LastTaskNumber < {GeneratedCode.MaxNumber}", ct);
        if (changed == 0)
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) throw new NotFoundException("Проект не найден.");
            throw new ConflictException("Достигнут предел номеров задач в этом проекте.");
        }
        var project = await db.Projects.Include(p => p.Subject).SingleAsync(p => p.Id == projectId, ct);
        await db.Entry(project).ReloadAsync(ct);
        var task = new ProjectTask(project, name, GeneratedCode.Format(project.LastTaskNumber), body);
        await db.ProjectTasks.AddAsync(task, ct);
        await SaveChanges(ct);
        await transaction.CommitAsync(ct);
        return task;
    }

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesWithConflicts(ct);
}

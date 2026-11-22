using JChrono.Application.Features.Notes;
using JChrono.Infrastructure.Features.Notes;

namespace JChrono.Web.DependencyInjection;

internal static class NotesServiceCollectionExtensions
{
    public static IServiceCollection AddNotes(this IServiceCollection services)
    {
        services.AddScoped<GetNotes>();
        services.AddScoped<INotesByDateQuery, NoteQueries>();
        services.AddScoped<GetSubjectNotes>();
        services.AddScoped<ISubjectNotesQuery, NoteQueries>();
        services.AddScoped<GetProjectNotes>();
        services.AddScoped<IProjectNotesQuery, NoteQueries>();
        services.AddScoped<CreateProjectNote>();
        services.AddScoped<ICreateProjectNoteStore, NoteStore>();
        services.AddScoped<UpdateProjectNote>();
        services.AddScoped<IUpdateProjectNoteStore, NoteStore>();
        services.AddScoped<DeleteProjectNote>();
        services.AddScoped<IDeleteProjectNoteStore, NoteStore>();
        services.AddScoped<GetTaskNotes>();
        services.AddScoped<ITaskNotesQuery, NoteQueries>();
        services.AddScoped<CreateTaskNote>();
        services.AddScoped<ICreateTaskNoteStore, NoteStore>();
        services.AddScoped<UpdateTaskNote>();
        services.AddScoped<IUpdateTaskNoteStore, NoteStore>();
        services.AddScoped<DeleteTaskNote>();
        services.AddScoped<IDeleteTaskNoteStore, NoteStore>();
        services.AddScoped<CreateNote>();
        services.AddScoped<ICreateNoteStore, NoteStore>();
        services.AddScoped<UpdateNote>();
        services.AddScoped<IUpdateNoteStore, NoteStore>();
        services.AddScoped<DeleteNote>();
        services.AddScoped<IDeleteNoteStore, NoteStore>();
        return services;
    }
}

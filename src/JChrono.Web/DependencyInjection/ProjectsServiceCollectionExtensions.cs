using JChrono.Application.Features.Projects;
using JChrono.Infrastructure.Features.Projects;

namespace JChrono.Web.DependencyInjection;

internal static class ProjectsServiceCollectionExtensions
{
    public static IServiceCollection AddProjects(this IServiceCollection services)
    {
        services.AddScoped<GetProjects>();
        services.AddScoped<IProjectsQuery, ProjectQueries>();
        services.AddScoped<GetProject>();
        services.AddScoped<IProjectQuery, ProjectQueries>();
        services.AddScoped<CreateProject>();
        services.AddScoped<ICreateProjectStore, ProjectStore>();
        services.AddScoped<UpdateProject>();
        services.AddScoped<IUpdateProjectStore, ProjectStore>();
        services.AddScoped<SetProjectStatus>();
        services.AddScoped<ISetProjectStatusStore, ProjectStore>();
        services.AddScoped<DeleteProject>();
        services.AddScoped<IDeleteProjectStore, DeleteProjectStore>();
        services.AddScoped<GetProjectTasks>();
        services.AddScoped<IProjectTasksQuery, ProjectTasksQuery>();
        services.AddScoped<CreateProjectTask>();
        services.AddScoped<ICreateProjectTaskStore, ProjectStore>();
        services.AddScoped<UpdateProjectTask>();
        services.AddScoped<IUpdateProjectTaskStore, ProjectStore>();
        services.AddScoped<DeleteProjectTask>();
        services.AddScoped<IDeleteProjectTaskStore, DeleteProjectTaskStore>();
        return services;
    }
}

using JChrono.Application.Features.Planning;
using JChrono.Infrastructure.Features.Planning;

namespace JChrono.Web.DependencyInjection;

internal static class PlanningServiceCollectionExtensions
{
    public static IServiceCollection AddPlanning(this IServiceCollection services)
    {
        services.AddScoped<ListPlannedActivities>();
        services.AddScoped<IPlannedActivitiesQuery, ListPlannedActivitiesQuery>();
        services.AddScoped<MovePlannedActivityHandler>();
        services.AddScoped<IMovePlannedActivityStore, MovePlannedActivityStore>();
        services.AddScoped<CreatePlannedActivity>();
        services.AddScoped<ICreatePlannedActivityStore, PlanningRepository>();
        services.AddScoped<UpdatePlannedActivity>();
        services.AddScoped<IUpdatePlannedActivityStore, PlanningRepository>();
        services.AddScoped<DeletePlannedActivity>();
        services.AddScoped<IDeletePlannedActivityStore, PlanningRepository>();
        return services;
    }
}

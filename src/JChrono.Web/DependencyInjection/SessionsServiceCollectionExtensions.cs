using JChrono.Application.Features.Sessions;
using JChrono.Infrastructure.Features.Sessions;

namespace JChrono.Web.DependencyInjection;

internal static class SessionsServiceCollectionExtensions
{
    public static IServiceCollection AddSessions(this IServiceCollection services)
    {
        services.AddScoped<GetSubjectSessions>();
        services.AddScoped<ISubjectSessionsQuery, SessionListQueries>();
        services.AddScoped<GetProjectSessions>();
        services.AddScoped<IProjectSessionsQuery, SessionListQueries>();
        services.AddScoped<GetTaskSessions>();
        services.AddScoped<ITaskSessionsQuery, SessionListQueries>();
        services.AddScoped<GetDaySessions>();
        services.AddScoped<IDaySessionsQuery, DaySessionsQuery>();
        services.AddScoped<GetCurrentSession>();
        services.AddScoped<ICurrentSessionQuery, SessionStore>();
        services.AddScoped<GetSession>();
        services.AddScoped<ISessionQuery, SessionStore>();
        services.AddScoped<CreateSession>();
        services.AddScoped<ICreateSessionStore, SessionStore>();
        services.AddScoped<CloseSession>();
        services.AddScoped<ICloseSessionStore, SessionStore>();
        services.AddScoped<UpdateSession>();
        services.AddScoped<IUpdateSessionStore, SessionStore>();
        services.AddScoped<UpdateSessionText>();
        services.AddScoped<IUpdateSessionTextStore, SessionStore>();
        services.AddScoped<DeleteSession>();
        services.AddScoped<IDeleteSessionStore, SessionStore>();
        services.AddScoped<GetSubjectJournal>();
        services.AddScoped<ISubjectJournalQuery, SubjectJournalQuery>();
        services.AddScoped<GetClosedSessionTimes>();
        services.AddScoped<GetUsefulWork>();
        services.AddScoped<ClosedSessionIntervals>();
        services.AddScoped<IClosedSessionTimesQuery, ClosedSessionTimesQuery>();
        return services;
    }
}

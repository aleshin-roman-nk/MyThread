using JChrono.Application.Features.Subjects;
using JChrono.Infrastructure.Features.Subjects;

namespace JChrono.Web.DependencyInjection;

internal static class SubjectsServiceCollectionExtensions
{
    public static IServiceCollection AddSubjects(this IServiceCollection services)
    {
        services.AddScoped<GetSubjects>();
        services.AddScoped<ISubjectsQuery, SubjectQueries>();
        services.AddScoped<GetSubject>();
        services.AddScoped<ISubjectQuery, SubjectQueries>();
        services.AddScoped<CreateSubject>();
        services.AddScoped<ICreateSubjectStore, SubjectStore>();
        services.AddScoped<UpdateSubject>();
        services.AddScoped<IUpdateSubjectStore, SubjectStore>();
        services.AddScoped<SetSubjectShortName>();
        services.AddScoped<ISetSubjectShortNameStore, SubjectStore>();
        services.AddScoped<DeleteSubject>();
        services.AddScoped<IDeleteSubjectStore, DeleteSubjectStore>();
        return services;
    }
}

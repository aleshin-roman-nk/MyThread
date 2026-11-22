using JChrono.Application.Features.Diary;
using JChrono.Infrastructure.Features.Diary;

namespace JChrono.Web.DependencyInjection;

internal static class DiaryServiceCollectionExtensions
{
    public static IServiceCollection AddDiary(this IServiceCollection services)
    {
        services.AddScoped<ListDiaryNotes>();
        services.AddScoped<IDiaryNotesQuery, ListDiaryNotesQuery>();
        services.AddScoped<CreateDiaryNote>();
        services.AddScoped<ICreateDiaryNoteStore, DiaryRepository>();
        services.AddScoped<UpdateDiaryNote>();
        services.AddScoped<IUpdateDiaryNoteStore, DiaryRepository>();
        services.AddScoped<DeleteDiaryNote>();
        services.AddScoped<IDeleteDiaryNoteStore, DiaryRepository>();
        return services;
    }
}

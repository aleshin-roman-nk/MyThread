using JChrono.Application.Features.Desktop;
using JChrono.Infrastructure;
using JChrono.Infrastructure.Features.Desktop;

namespace JChrono.Web.DependencyInjection;

internal static class DesktopServiceCollectionExtensions
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddScoped<GetStickers>();
        services.AddScoped<IStickersQuery, StickersQuery>();
        services.AddScoped<CreateTextSticker>();
        services.AddScoped<ICreateTextStickerStore, DesktopRepository>();
        services.AddScoped<UpdateTextSticker>();
        services.AddScoped<IUpdateTextStickerStore, DesktopRepository>();
        services.AddScoped<DeleteTextSticker>();
        services.AddScoped<IDeleteTextStickerStore, DesktopRepository>();
        services.AddScoped<MoveSticker>();
        services.AddScoped<IMoveStickerStore, DesktopRepository>();
        services.AddScoped<GetTaskPinStatus>();
        services.AddScoped<ITaskPinStatusQuery, TaskPinStore>();
        services.AddScoped<PinTask>();
        services.AddScoped<IPinTaskStore, TaskPinStore>();
        services.AddScoped<UnpinTask>();
        services.AddScoped<IUnpinTaskStore, TaskPinStore>();
        services.AddScoped<GetNotePinStatus>();
        services.AddScoped<INotePinStatusQuery, NotePinStore>();
        services.AddScoped<PinNote>();
        services.AddScoped<IPinNoteStore, NotePinStore>();
        services.AddScoped<UnpinNote>();
        services.AddScoped<IUnpinNoteStore, NotePinStore>();
        return services;
    }
}

using JChrono.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Web.DependencyInjection;

internal static class HostServiceCollectionExtensions
{
    public static IServiceCollection AddJChronoApi(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddJChronoInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        var connectionString = configuration.GetConnectionString("Notes")
            ?? $"Data Source={Path.Combine(environment.ContentRootPath, "App_Data", "notes.db")}";
        var sqlite = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        if (sqlite.DataSource != ":memory:")
        {
            sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, environment.ContentRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
        }
        services.AddDbContext<NotesDbContext>(options => options.UseSqlite(sqlite.ToString()));
        return services;
    }
}

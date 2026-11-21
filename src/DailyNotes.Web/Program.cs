using DailyNotes.Application;
using DailyNotes.Infrastructure;
using DailyNotes.Web;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<NotesService>();
builder.Services.AddScoped<DiaryService>();
builder.Services.AddScoped<PlanningService>();
builder.Services.AddScoped<IPlanningRepository, PlanningRepository>();
builder.Services.AddScoped<IDiaryRepository, DiaryRepository>();
builder.Services.AddScoped<SessionsService>();
builder.Services.AddScoped<DesktopService>();
builder.Services.AddScoped<IDesktopRepository, DesktopRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<INotesRepository, NotesRepository>();
var connectionString = builder.Configuration.GetConnectionString("Notes")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "notes.db")}";
var sqlite = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
if (sqlite.DataSource != ":memory:")
{
    sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
}
builder.Services.AddDbContext<NotesDbContext>(options => options.UseSqlite(sqlite.ToString()));

var app = builder.Build();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
    await DatabaseInitializer.Initialize(db);
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Map("/api/{**path}", () => Results.Problem(statusCode: 404, title: "API endpoint not found."));
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }


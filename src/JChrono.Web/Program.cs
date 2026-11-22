using JChrono.Infrastructure;
using JChrono.Web.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddJChronoApi()
    .AddNotes()
    .AddSubjects()
    .AddProjects()
    .AddDiary()
    .AddPlanning()
    .AddSessions()
    .AddDesktop()
    .AddJChronoInfrastructure(builder.Configuration, builder.Environment);

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


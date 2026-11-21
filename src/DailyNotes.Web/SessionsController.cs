using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;

namespace DailyNotes.Web;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(SessionsService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DaySessionDto>> DaySessions(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        service.GetDaySessions(startTime, endTime, ct);

    [HttpGet("useful-work")]
    public Task<UsefulWorkDto> UsefulWork(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        service.GetUsefulWork(startTime, endTime, ct);

    [HttpGet("current")]
    public async Task<ActionResult<SessionDto>> Current(CancellationToken ct) =>
        await service.GetCurrent(ct) is { } session ? Ok(session) : NoContent();

    [HttpGet("{id:int}")]
    public Task<SessionDto> Get(int id, CancellationToken ct) => service.Get(id, ct);

    [HttpGet("/api/subjects/{subjectId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> SubjectSessions(int subjectId, CancellationToken ct) => service.GetSubjectSessions(subjectId, ct);

    [HttpGet("/api/projects/{projectId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> ProjectSessions(int projectId, CancellationToken ct) => service.GetProjectSessions(projectId, ct);

    [HttpGet("/api/projects/{projectId:int}/tasks/{taskId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> TaskSessions(int projectId, int taskId, CancellationToken ct) => service.GetTaskSessions(projectId, taskId, ct);

    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionRequest request, CancellationToken ct)
    {
        var session = await service.Create(request, ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPost("{id:int}/close")]
    public Task<SessionDto> Close(int id, CancellationToken ct) => service.Close(id, ct);

    [HttpPut("{id:int}")]
    public Task<SessionDto> Update(int id, UpdateSessionRequest request, CancellationToken ct) => service.Update(id, request, ct);

    [HttpPut("{id:int}/text")]
    public Task<SessionDto> UpdateText(int id, UpdateSessionTextRequest request, CancellationToken ct) => service.UpdateText(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.Delete(id, ct);
        return NoContent();
    }
}

using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DailyNotes.Web;

[ApiController]
[Route("api/planning/activities")]
public sealed class PlanningController(PlanningService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PlannedActivityDto>> Get(
        [FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly through, CancellationToken ct) =>
        service.Get(from, through, ct);

    [HttpPost]
    public async Task<ActionResult<PlannedActivityDto>> Create(SavePlannedActivityRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.Save(null, request, ct));

    [HttpPut("{id:int}")]
    public Task<PlannedActivityDto> Update(int id, SavePlannedActivityRequest request, CancellationToken ct) =>
        service.Save(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.Delete(id, ct);
        return NoContent();
    }
}

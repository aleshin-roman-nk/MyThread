using JChrono.Application.Features.Planning;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Planning;

[ApiController]
[Route("api/planning/activities")]
public sealed class DeletePlannedActivityController(DeletePlannedActivity handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await handler.Execute(id, ct);
        return NoContent();
    }
}

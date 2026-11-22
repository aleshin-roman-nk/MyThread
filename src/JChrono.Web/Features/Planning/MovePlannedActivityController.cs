using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Planning;

[ApiController]
[Route("api/planning/activities")]
public sealed class MovePlannedActivityController(MovePlannedActivityHandler handler) : ControllerBase
{
    [HttpPut("{id:int}/position")]
    public Task<PlannedActivityDto> Move(int id, MovePlannedActivity position, CancellationToken ct) =>
        handler.Execute(id, position, ct);
}

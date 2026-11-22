using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Planning;

[ApiController]
[Route("api/planning/activities")]
public sealed class CreatePlannedActivityController(CreatePlannedActivity handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PlannedActivityDto>> Create(SavePlannedActivityRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.Execute(request, ct));
}

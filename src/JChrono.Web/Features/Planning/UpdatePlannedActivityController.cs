using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Planning;

[ApiController]
[Route("api/planning/activities")]
public sealed class UpdatePlannedActivityController(UpdatePlannedActivity handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<PlannedActivityDto> Update(int id, SavePlannedActivityRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}

using JChrono.Application;
using JChrono.Application.Features.Planning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace JChrono.Web.Features.Planning;

[ApiController]
[Route("api/planning/activities")]
public sealed class ListPlannedActivitiesController(ListPlannedActivities handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PlannedActivityDto>> Get(
        [FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly through, CancellationToken ct) =>
        handler.Execute(from, through, ct);
}

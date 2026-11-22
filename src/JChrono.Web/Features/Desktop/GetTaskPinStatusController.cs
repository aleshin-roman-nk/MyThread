using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/tasks/{taskId:int}")]
public sealed class GetTaskPinStatusController(GetTaskPinStatus handler) : ControllerBase
{
    [HttpGet]
    public Task<TaskPinStatusDto> Get(int taskId, CancellationToken ct) => handler.Execute(taskId, ct);
}

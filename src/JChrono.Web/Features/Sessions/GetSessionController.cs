using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class GetSessionController(GetSession handler) : ControllerBase
{
    internal const string RouteName = "GetSession";

    [HttpGet("{id:int}", Name = RouteName)]
    public Task<SessionDto> Get(int id, CancellationToken ct) => handler.Execute(id, ct);
}

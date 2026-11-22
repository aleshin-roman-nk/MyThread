using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class GetSubjectController(GetSubject handler) : ControllerBase
{
    internal const string RouteName = "GetSubject";

    [HttpGet("{id:int}", Name = RouteName)]
    public Task<SubjectDto> Get(int id, CancellationToken ct) => handler.Execute(id, ct);
}

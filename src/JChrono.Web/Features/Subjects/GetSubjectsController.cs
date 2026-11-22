using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class GetSubjectsController(GetSubjects handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SubjectDto>> Get(CancellationToken ct) => handler.Execute(ct);
}

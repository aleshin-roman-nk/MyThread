using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class SetSubjectShortNameController(SetSubjectShortName handler) : ControllerBase
{
    [HttpPut("{id:int}/short-name")]
    public Task<SubjectDto> SetShortName(int id, SetSubjectShortNameRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}

using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class UpdateSubjectController(UpdateSubject handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<SubjectDto> Update(int id, UpdateSubjectRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}

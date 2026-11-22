using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class CreateSubjectController(CreateSubject handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubjectDto>> Create(CreateSubjectRequest request, CancellationToken ct)
    {
        var subject = await handler.Execute(request, ct);
        return CreatedAtRoute(GetSubjectController.RouteName, new { id = subject.Id }, subject);
    }
}

using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Subjects;

[ApiController]
[Route("api/subjects")]
public sealed class DeleteSubjectController(DeleteSubject handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteSubjectRequest request, CancellationToken ct)
    {
        await handler.Execute(id, request, ct);
        return NoContent();
    }
}

using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/subjects/{subjectId:int}/sessions/journal")]
public sealed class GetSubjectJournalController(GetSubjectJournal handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DaySessionDto>> Get(
        int subjectId, DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        handler.Execute(subjectId, startTime, endTime, ct);
}

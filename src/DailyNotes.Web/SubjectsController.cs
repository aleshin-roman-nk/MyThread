using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DailyNotes.Web;

[ApiController]
[Route("api/subjects")]
public sealed class SubjectsController(NotesService service) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteSubjectRequest request, CancellationToken ct)
    {
        await service.DeleteSubject(id, request, ct);
        return NoContent();
    }

    [HttpGet]
    public Task<IReadOnlyList<SubjectDto>> Get(CancellationToken ct) => service.GetSubjects(ct);

    [HttpGet("{id:int}")]
    public Task<SubjectDto> Get(int id, CancellationToken ct) => service.GetSubject(id, ct);

    [HttpGet("{id:int}/notes")]
    public Task<IReadOnlyList<NoteDto>> Notes(int id, CancellationToken ct) => service.GetSubjectNotes(id, ct);

    [HttpPost]
    public async Task<ActionResult<SubjectDto>> Create(CreateSubjectRequest request, CancellationToken ct)
    {
        var subject = await service.CreateSubject(request, ct);
        return CreatedAtAction(nameof(Get), new { id = subject.Id }, subject);
    }

    [HttpPut("{id:int}")]
    public Task<SubjectDto> Update(int id, UpdateSubjectRequest request, CancellationToken ct) =>
        service.UpdateSubject(id, request, ct);

    [HttpPut("{id:int}/short-name")]
    public Task<SubjectDto> SetShortName(int id, SetSubjectShortNameRequest request, CancellationToken ct) =>
        service.SetSubjectShortName(id, request, ct);

    [HttpGet("{id:int}/projects")]
    public Task<IReadOnlyList<ProjectDto>> Projects(int id, ProjectStatus? status, CancellationToken ct) => service.GetProjects(id, ct, status);

    [HttpPost("{id:int}/projects")]
    public async Task<ActionResult<ProjectDto>> CreateProject(int id, CreateProjectRequest request, CancellationToken ct)
    {
        var project = await service.CreateProject(id, request, ct);
        return Created($"/api/subjects/{id}/projects", project);
    }
}


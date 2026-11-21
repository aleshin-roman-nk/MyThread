namespace DailyNotes.Domain;

public sealed class PlannedActivityReference
{
    private PlannedActivityReference() { }

    internal PlannedActivityReference(int? projectId, int? taskId)
    {
        ProjectId = projectId;
        TaskId = taskId;
    }

    public int Id { get; private set; }
    public int ActivityId { get; private set; }
    public int? ProjectId { get; private set; }
    public Project? Project { get; private set; }
    public int? TaskId { get; private set; }
    public ProjectTask? Task { get; private set; }
}

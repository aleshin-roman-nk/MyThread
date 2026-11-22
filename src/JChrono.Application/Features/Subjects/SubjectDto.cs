namespace JChrono.Application;

public sealed record SubjectDto(int Id, string Name, int NoteCount, string ShortName,
    double ClosedSessionHours = 0, int ProjectCount = 0, int InProgressTaskCount = 0, int DeferredTaskCount = 0, string Description = "");

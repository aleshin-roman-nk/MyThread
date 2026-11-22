namespace JChrono.Application;

public record DesktopTaskDto(int Id, int ProjectId, int SubjectId, string Code, string FullCode, string Name);
public record DesktopNoteDto(int Id, string Kind, string Source, DateOnly Date, TimeOnly Time, string Text, int Rating,
    int? SubjectId, int? ProjectId, int? TaskId);
public record StickerDto(int Id, string? Text, DesktopTaskDto? Task, double? X, double? Y, DesktopNoteDto? Note = null);

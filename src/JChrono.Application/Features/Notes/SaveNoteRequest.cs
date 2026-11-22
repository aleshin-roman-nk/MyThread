namespace JChrono.Application;

public sealed record SaveNoteRequest(int SubjectId, DateOnly Date, string Text, TimeOnly? Time = null, int? Rating = null);

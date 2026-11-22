namespace JChrono.Application;

public sealed record SaveProjectNoteRequest(DateOnly Date, string Text, TimeOnly? Time = null, int? Rating = null);

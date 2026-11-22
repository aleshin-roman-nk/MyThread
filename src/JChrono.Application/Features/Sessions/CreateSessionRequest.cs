namespace JChrono.Application;

public sealed record CreateSessionRequest(int SubjectId, int? ProjectId = null, int? TaskId = null, string Text = "");

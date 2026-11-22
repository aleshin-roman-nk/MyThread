namespace JChrono.Application;

public sealed record CreateSubjectRequest(string Name, string ShortName, string Description = "");

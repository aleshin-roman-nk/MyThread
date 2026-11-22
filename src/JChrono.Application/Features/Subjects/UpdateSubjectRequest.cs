namespace JChrono.Application;

public sealed record UpdateSubjectRequest(string Name, string ShortName, string Description = "");

namespace JChrono.Application;

public sealed record UpdateSessionRequest(DateTimeOffset? StartTime, DateTimeOffset? EndTime, string Text);

namespace JChrono.Application;

public sealed record MovePlannedActivity(DateTime OriginalStart, int DurationMinutes, DateTime Start);

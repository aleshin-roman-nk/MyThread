namespace JChrono.Application.Features.Sessions;

internal static class SessionTimeRange
{
    public static (DateTimeOffset Start, DateTimeOffset End) Validate(
        DateTimeOffset? startTime, DateTimeOffset? endTime)
    {
        if (startTime is not { } start || endTime is not { } end || end <= start)
            throw new ArgumentException("Укажите начало и конец выбранного дня; конец должен быть позже начала.");

        return (start, end);
    }
}

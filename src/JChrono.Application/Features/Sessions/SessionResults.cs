using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

internal static class SessionResults
{
    public static IReadOnlyList<DaySessionDto> ToDaySessions(IEnumerable<Session> sessions) =>
        InStartOrder(sessions)
            .Select(s => new DaySessionDto(ToDto(s), s.Task?.FullCode ?? s.Project?.FullCode ?? s.Subject.ShortName,
                s.Task?.Name ?? s.Project?.Name ?? s.Subject.Name)).ToList();

    public static IReadOnlyList<SessionDto> ToSessions(IEnumerable<Session> sessions) =>
        InStartOrder(sessions).Select(ToDto).ToList();

    public static SessionDto ToDto(Session session) => new(session.Id, session.SubjectId, session.ProjectId,
        session.TaskId, session.StartTime, session.EndTime, session.Status, session.Text, session.Duration);

    private static IOrderedEnumerable<Session> InStartOrder(IEnumerable<Session> sessions) =>
        sessions.OrderBy(s => s.StartTime).ThenBy(s => s.Id);
}

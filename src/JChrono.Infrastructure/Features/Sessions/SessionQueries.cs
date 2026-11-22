using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

internal static class SessionQueries
{
    public static IQueryable<Session> WithSessionContext(this IQueryable<Session> sessions) =>
        sessions.AsNoTracking().Include(s => s.Subject).Include(s => s.Project).ThenInclude(p => p!.Subject)
            .Include(s => s.Task).ThenInclude(t => t!.Project).ThenInclude(p => p.Subject);
}

using JChrono.Application;
using JChrono.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure;

internal static class PersistenceConflicts
{
    public static async Task SaveChangesWithConflicts(this NotesDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Данные уже изменены другим запросом. Обновите страницу.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            if (ex.Entries.Any(entry => entry.Entity is Session))
                throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");
            throw new ConflictException("Название предмета или код проекта/задачи в этом разделе уже существует.");
        }
    }
}

using JChrono.Application;
using JChrono.Domain;
using JChrono.Infrastructure;
using JChrono.Infrastructure.Features.Sessions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JChrono.Tests;

public sealed class SessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 23, 30, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Session_status_follows_end_time_and_closed_session_times_can_be_edited()
    {
        var subject = new Subject("Разработка", "DEV");
        var project = new Project(subject, "Конспекты", "Conspect");
        var task = new ProjectTask(project, "SQL", "SQL", "Практика");
        var session = new Session(subject, Start, " Текст ", task: task);
        Assert.Same(project, session.Project);
        Assert.Same(task, session.Task);
        Assert.Equal(SessionStatus.Open, session.Status);
        Assert.Equal(Start, session.StartTime);
        Assert.Null(session.EndTime);
        Assert.Null(session.Duration);
        Assert.Equal("Текст", session.Text);
        Assert.Throws<ArgumentException>(() => session.Close(Start.AddMinutes(-1)));
        Assert.Equal(SessionStatus.Open, session.Status);
        session.Close(Start.AddHours(26));
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(TimeSpan.FromHours(26), session.Duration);
        Assert.Throws<InvalidOperationException>(() => session.Close(Start.AddHours(27)));
        session.UpdateText(new string('я', 1000));
        Assert.Throws<ArgumentException>(() => session.UpdateText(new string('я', 1001)));
        Assert.Equal(new string('я', 1000), session.Text);
        Assert.Equal(Start.AddHours(26), session.EndTime);
        session.Update(Start.AddHours(-1), Start.AddHours(2), "Правка");
        Assert.Equal(TimeSpan.FromHours(3), session.Duration);
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Throws<ArgumentException>(() => session.Update(Start, Start.AddMinutes(-1), "Не сохранять"));
        Assert.Equal("Правка", session.Text);
        Assert.Equal(Start.AddHours(-1), session.StartTime);
        session.Update(Start, null, "Снова открыта");
        Assert.Equal(SessionStatus.Open, session.Status);
        Assert.Null(session.Duration);
    }

    [Fact]
    public void Session_rejects_mismatched_subject_project_task_and_overlong_text()
    {
        var subject = new Subject("Разработка", "DEV");
        var other = new Subject("Английский", "ENG");
        var project = new Project(subject, "Конспекты", "Conspect");
        var otherProject = new Project(subject, "Практика", "Practice");
        var task = new ProjectTask(project, "SQL", "SQL", "Практика");
        Assert.Throws<ArgumentNullException>(() => new Session(null!, Start));
        Assert.Throws<ArgumentException>(() => new Session(other, Start, project: project));
        Assert.Throws<ArgumentException>(() => new Session(other, Start, task: task));
        Assert.Throws<ArgumentException>(() => new Session(subject, Start, project: otherProject, task: task));
        Assert.Throws<ArgumentException>(() => new Session(subject, Start, new string('a', 1001)));
        var subjectSession = new Session(subject, Start);
        Assert.Null(subjectSession.Project);
        Assert.Null(subjectSession.Task);
    }

    [Fact]
    public async Task Database_enforces_single_open_session_even_when_both_contexts_saw_no_current_session()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using var first = new NotesDbContext(options);
        await DatabaseInitializer.Initialize(first);
        var subject = new Subject("Разработка", "DEV");
        first.Subjects.Add(subject);
        await first.SaveChangesAsync();
        await using var second = new NotesDbContext(options);
        var otherSubject = await second.Subjects.SingleAsync();
        Assert.False(await first.Sessions.AnyAsync(s => s.EndTime == null));
        Assert.False(await second.Sessions.AnyAsync(s => s.EndTime == null));
        first.Sessions.Add(new Session(subject, Start));
        second.Sessions.Add(new Session(otherSubject, Start));
        await first.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal(2067, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => new SessionStore(second).SaveChanges(default));
        Assert.Equal("Уже есть открытая сессия. Сначала закройте её.", conflict.Message);
        Assert.Single(await first.Sessions.ToListAsync());
    }

    [Fact]
    public async Task Closing_is_concurrency_protected_and_reopening_respects_single_open_session()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using var first = new NotesDbContext(options);
        await DatabaseInitializer.Initialize(first);
        var session = new Session(new Subject("Разработка", "DEV"), Start);
        first.Sessions.Add(session);
        await first.SaveChangesAsync();
        await using var second = new NotesDbContext(options);
        var stale = await second.Sessions.SingleAsync();
        session.Close(Start.AddHours(1));
        await first.SaveChangesAsync();
        stale.Close(Start.AddHours(2));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => new SessionStore(second).SaveChanges(default));
        Assert.Equal("Данные уже изменены другим запросом. Обновите страницу.", conflict.Message);
        session.Update(Start.AddHours(-1), Start.AddHours(2), "Правка времени");
        await first.SaveChangesAsync();
        Assert.Equal(TimeSpan.FromHours(3), session.Duration);
        session.Update(Start, null, "Исправленный текст");
        await first.SaveChangesAsync();
        Assert.Equal(SessionStatus.Open, session.Status);
        session.Close(Start.AddHours(1));
        await first.SaveChangesAsync();
        first.Sessions.Add(new Session(session.Subject, Start.AddHours(2)));
        await first.SaveChangesAsync();
        session.Update(Start, null, "Конфликт");
        await Assert.ThrowsAsync<DbUpdateException>(() => first.SaveChangesAsync());
        await DatabaseInitializer.Initialize(first);
        await using var check = new NotesDbContext(options);
        var closed = await check.Sessions.SingleAsync(s => s.EndTime != null);
        Assert.Equal("Исправленный текст", closed.Text);
        Assert.Equal(TimeSpan.FromHours(1), closed.Duration);
        Assert.Single(await check.Sessions.Where(s => s.EndTime == null).ToListAsync());
        Assert.Null(check.Model.FindEntityType(typeof(Session))!.FindProperty(nameof(Session.Duration)));
        Assert.Null(check.Model.FindEntityType(typeof(Session))!.FindProperty(nameof(Session.Status)));
    }

    [Fact]
    public async Task Upgrade_removes_stored_status_and_immutable_trigger_preserving_open_and_closed_sessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Subjects.Add(new Subject("Разработка", "DEV"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP TABLE Sessions;
                CREATE TABLE Sessions (Id INTEGER PRIMARY KEY, SubjectId INTEGER NOT NULL, ProjectId INTEGER, TaskId INTEGER,
                    StartTime TEXT NOT NULL, EndTime TEXT, Status INTEGER NOT NULL, Text TEXT NOT NULL,
                    CHECK ((Status = 0 AND EndTime IS NULL) OR (Status = 1 AND EndTime IS NOT NULL)));
                CREATE UNIQUE INDEX IX_Sessions_Status ON Sessions (Status) WHERE Status = 0;
                CREATE TRIGGER TR_Sessions_ClosedImmutable BEFORE UPDATE ON Sessions WHEN OLD.Status = 1
                BEGIN SELECT RAISE(ABORT, 'Immutable'); END;
                INSERT INTO Sessions VALUES (7, 1, NULL, NULL, '2026-11-17 10:00:00+00:00', '2026-11-17 11:00:00+00:00', 1, 'Закрытая');
                INSERT INTO Sessions VALUES (8, 1, NULL, NULL, '2026-11-17 12:00:00+00:00', NULL, 0, 'Открытая');
                """);
            await DatabaseInitializer.Initialize(db);
            await DatabaseInitializer.Initialize(db);
        }
        await using var check = new NotesDbContext(options);
        var closed = await check.Sessions.SingleAsync(s => s.Id == 7);
        var opened = await check.Sessions.SingleAsync(s => s.Id == 8);
        Assert.Equal(SessionStatus.Closed, closed.Status);
        Assert.Equal(TimeSpan.FromHours(1), closed.Duration);
        Assert.Equal("Закрытая", closed.Text);
        Assert.Equal(SessionStatus.Open, opened.Status);
        opened.Close(opened.StartTime.AddHours(1));
        await check.SaveChangesAsync();
        closed.Update(closed.StartTime.AddHours(-1), null, "Открыта повторно");
        await check.SaveChangesAsync();
        Assert.Equal(7, (await check.Sessions.SingleAsync(s => s.EndTime == null)).Id);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pragma_table_info('Sessions') WHERE name = 'Status'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Legacy_sessions_preserve_date_time_links_and_text_as_closed_sessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.ProjectTasks.Add(new ProjectTask(new Project(new Subject("Разработка", "DEV"), "Конспекты", "Conspect"), "SQL", "SQL", "Описание"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP TABLE Sessions;
                CREATE TABLE Sessions (Id INTEGER PRIMARY KEY, SubjectId INTEGER NOT NULL, ProjectId INTEGER, TaskId INTEGER,
                    Date TEXT NOT NULL, StartTime TEXT NOT NULL, EndTime TEXT NOT NULL, Text TEXT NOT NULL);
                INSERT INTO Sessions VALUES (7, 1, 1, 1, '2026-11-17', '10:00:00', '11:30:00', 'Старый текст');
                """);
            await DatabaseInitializer.Initialize(db);
            await DatabaseInitializer.Initialize(db);
        }
        await using var check = new NotesDbContext(options);
        var session = await check.Sessions.SingleAsync();
        Assert.Equal(7, session.Id);
        Assert.Equal(1, session.SubjectId);
        Assert.Equal(1, session.ProjectId);
        Assert.Equal(1, session.TaskId);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 11, 17, 10, 0, 0)).ToUniversalTime(), session.StartTime);
        Assert.Equal(TimeSpan.Zero, session.StartTime.Offset);
        Assert.Equal(TimeSpan.FromMinutes(90), session.Duration);
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal("Старый текст", session.Text);
        session.UpdateText("Правка");
        await check.SaveChangesAsync();
        check.Sessions.Add(new Session(await check.Subjects.SingleAsync(), Start));
        await check.SaveChangesAsync();
    }
}

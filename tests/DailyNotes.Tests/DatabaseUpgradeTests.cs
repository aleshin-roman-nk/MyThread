using DailyNotes.Domain;
using DailyNotes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DailyNotes.Tests;

public sealed class DatabaseUpgradeTests
{
    [Fact]
    public async Task Existing_projects_and_tasks_receive_in_progress_status_and_keep_their_data_on_upgrade()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Конспекты", "Conspect");
            db.Projects.Add(project);
            db.ProjectTasks.Add(new ProjectTask(project, "Практика SQL", "SQL", "Описание"));
            await db.SaveChangesAsync();
            // Reproduce the previous project schema in this isolated in-memory database.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects DROP COLUMN Status;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects DROP COLUMN Description;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE ProjectTasks DROP COLUMN Status;");
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var project = await db.Projects.Include(p => p.Subject).Include(p => p.Tasks).SingleAsync();
            Assert.Equal(ProjectStatus.InProgress, project.Status);
            Assert.Equal("", project.Description);
            project.Update(project.Name, project.Status, "Описание после обновления");
            Assert.Equal("DEV \\ Conspect \\ SQL", Assert.Single(project.Tasks).FullCode);
            var task = Assert.Single(project.Tasks);
            Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
            task.Update(task.Name, task.Body, ProjectTaskStatus.Closed);
            project.SetStatus(ProjectStatus.Deferred);
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            Assert.Equal(ProjectStatus.Deferred, (await db.Projects.SingleAsync()).Status);
            Assert.Equal("Описание после обновления", (await db.Projects.SingleAsync()).Description);
            Assert.Equal(ProjectTaskStatus.Closed, (await db.ProjectTasks.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task Separate_note_tables_are_merged_without_losing_notes_or_sticker_positions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var subject = new Subject("Разработка", "DEV");
            var project = new Project(subject, "Проект", "0001");
            db.Add(new ProjectTask(project, "Задача", "0001", "Описание"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP TABLE Stickers;
                DROP TABLE Note;
                CREATE TABLE Notes (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date TEXT, Time TEXT, Text TEXT, Rating INTEGER);
                CREATE TABLE SubjectNotes (Id INTEGER PRIMARY KEY AUTOINCREMENT, SubjectId INTEGER, Date TEXT, Text TEXT);
                CREATE TABLE ProjectNotes (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProjectId INTEGER, Date TEXT, Text TEXT);
                CREATE TABLE TaskNotes (Id INTEGER PRIMARY KEY AUTOINCREMENT, TaskId INTEGER, Date TEXT, Text TEXT);
                CREATE TABLE Stickers (Id INTEGER PRIMARY KEY AUTOINCREMENT, DesktopId INTEGER, Text TEXT, TaskId INTEGER, X REAL, Y REAL);
                INSERT INTO Notes VALUES (1, '2026-11-21', '13:11:00', 'Diary', 5);
                INSERT INTO SubjectNotes SELECT 1, Id, '2026-11-21', 'Subject' FROM Subjects;
                INSERT INTO ProjectNotes SELECT 1, Id, '2026-11-21', 'Project' FROM Projects;
                INSERT INTO TaskNotes SELECT 1, Id, '2026-11-21', 'Task' FROM ProjectTasks;
                INSERT INTO Desktops VALUES (1);
                INSERT INTO Stickers VALUES (1, 1, 'Text sticker', NULL, 0.2, 0.4);
                INSERT INTO Stickers SELECT 2, 1, NULL, Id, 0.6, 0.8 FROM ProjectTasks;
                """);
        }

        await using var upgraded = new NotesDbContext(options);
        await DatabaseInitializer.Initialize(upgraded);
        await DatabaseInitializer.Initialize(upgraded);
        var notes = await upgraded.Notes.OrderBy(note => note.Id).ToListAsync();
        Assert.Equal(new[] { NoteKind.Diary, NoteKind.Subject, NoteKind.Project, NoteKind.Task }, notes.Select(note => note.Kind));
        Assert.Equal(new[] { "Diary", "Subject", "Project", "Task" }, notes.Select(note => note.Text));
        Assert.Equal((1, new TimeOnly(13, 11), 5), (notes[0].Id, notes[0].Time, notes[0].Rating));
        Assert.All(notes.Skip(1), note => Assert.Equal((TimeOnly.MinValue, Note.DefaultRating), (note.Time, note.Rating)));
        var stickers = await upgraded.Stickers.OrderBy(sticker => sticker.Id).ToListAsync();
        Assert.Collection(stickers,
            sticker => Assert.Equal((1, "Text sticker", 0.2, 0.4), (sticker.Id, sticker.Text, sticker.X, sticker.Y)),
            sticker => Assert.Equal((2, notes[3].TaskId, 0.6, 0.8), (sticker.Id, sticker.TaskId, sticker.X, sticker.Y)));
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_schema WHERE name IN ('Notes', 'SubjectNotes', 'ProjectNotes', 'TaskNotes')";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Original_database_is_upgraded_repeatably_preserving_subjects_and_notes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE Subjects (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, NormalizedName TEXT NOT NULL);
                CREATE UNIQUE INDEX IX_Subjects_NormalizedName ON Subjects (NormalizedName);
                CREATE TABLE Notes (Id INTEGER PRIMARY KEY, SubjectId INTEGER NOT NULL REFERENCES Subjects(Id), Date TEXT NOT NULL, Text TEXT NOT NULL);
                CREATE INDEX IX_Notes_Date_Id ON Notes (Date, Id);
                CREATE INDEX IX_Notes_SubjectId_Date ON Notes (SubjectId, Date);
                INSERT INTO Subjects VALUES (1, 'Development', 'DEVELOPMENT');
                INSERT INTO Notes VALUES (1, 1, '2026-11-17', 'Existing note');
                """;
            await command.ExecuteNonQueryAsync();
        }
        var options = new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options;
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var subject = await db.Subjects.SingleAsync();
            Assert.Equal("S1", subject.ShortName);
            Assert.Equal("Development", subject.Name);
            var subjectNote = await db.Notes.Where(n => n.SubjectId != null).SingleAsync();
            Assert.Equal(1, subjectNote.Id);
            Assert.Equal(subject.Id, subjectNote.SubjectId);
            Assert.Equal("Existing note", subjectNote.Text);
            Assert.Empty(await db.Notes.Where(n => n.SubjectId == null).ToListAsync());
            var project = new Project(subject, "Notes", "Conspect");
            db.Projects.Add(project);
            db.ProjectTasks.Add(new ProjectTask(project, "Learn SQL", "SQL", "Practice joins"));
            await db.SaveChangesAsync();
        }
        await using (var db = new NotesDbContext(options))
        {
            await DatabaseInitializer.Initialize(db);
            var subject = await db.Subjects.Include(s => s.Projects).ThenInclude(p => p.Tasks).SingleAsync();
            var project = Assert.Single(subject.Projects);
            Assert.Same(subject, project.Subject);
            Assert.Equal("S1 \\ Conspect \\ SQL", Assert.Single(project.Tasks).FullCode);
            Assert.Single(await db.Notes.Where(n => n.SubjectId != null).ToListAsync());
        }
    }
}

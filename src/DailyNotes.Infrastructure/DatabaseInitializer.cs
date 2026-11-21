using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DailyNotes.Infrastructure;

public static class DatabaseInitializer
{
    private static async Task UpgradeNotesAndStickers(NotesDbContext db, IDbContextTransaction transaction)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        async Task<object?> Scalar(string sql)
        {
            command.CommandText = sql;
            return await command.ExecuteScalarAsync();
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Note (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                SubjectId INTEGER,
                ProjectId INTEGER,
                TaskId INTEGER,
                Date TEXT NOT NULL,
                Time TEXT NOT NULL,
                Text TEXT NOT NULL,
                Rating INTEGER NOT NULL,
                CONSTRAINT CK_Note_Rating CHECK (Rating BETWEEN 1 AND 5),
                CONSTRAINT CK_Note_Owner CHECK ((SubjectId IS NOT NULL) + (ProjectId IS NOT NULL) + (TaskId IS NOT NULL) <= 1),
                FOREIGN KEY (SubjectId) REFERENCES Subjects (Id) ON DELETE RESTRICT,
                FOREIGN KEY (ProjectId) REFERENCES Projects (Id) ON DELETE RESTRICT,
                FOREIGN KEY (TaskId) REFERENCES ProjectTasks (Id) ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS IX_Note_Date_Time_Id ON Note (Date, Time, Id);
            CREATE INDEX IF NOT EXISTS IX_Note_SubjectId_Date_Time_Id ON Note (SubjectId, Date, Time, Id);
            CREATE INDEX IF NOT EXISTS IX_Note_ProjectId_Date_Time_Id ON Note (ProjectId, Date, Time, Id);
            CREATE INDEX IF NOT EXISTS IX_Note_TaskId_Date_Time_Id ON Note (TaskId, Date, Time, Id);
            """);

        if (await Scalar("SELECT 1 FROM pragma_table_info('Notes') WHERE name = 'SubjectId'") is not null)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Note (Id, SubjectId, Date, Time, Text, Rating)
                SELECT Id, SubjectId, Date, '00:00:00', Text, 3 FROM Notes;
                DROP TABLE Notes;
                """);
        else if (await Scalar("SELECT 1 FROM pragma_table_info('Notes') WHERE name = 'Rating'") is not null)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Note (Id, Date, Time, Text, Rating)
                SELECT Id, Date, Time, Text, Rating FROM Notes;
                INSERT INTO sqlite_sequence (name, seq)
                SELECT 'Note', seq FROM sqlite_sequence WHERE name = 'Notes'
                    AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'Note');
                UPDATE sqlite_sequence SET seq = max(seq, coalesce((SELECT seq FROM sqlite_sequence WHERE name = 'Notes'), 0))
                WHERE name = 'Note';
                DROP TABLE Notes;
                """);

        // Allocate IDs above the legacy ranges before moving scoped notes into the common table.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO sqlite_sequence (name, seq)
            SELECT 'Note', 0 WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'Note');
            UPDATE sqlite_sequence SET seq = max(seq, coalesce((
                SELECT max(seq) FROM sqlite_sequence WHERE name IN ('SubjectNotes', 'ProjectNotes', 'TaskNotes')
            ), 0)) WHERE name = 'Note';
            """);
        if (await Scalar("SELECT 1 FROM pragma_table_info('SubjectNotes') WHERE name = 'Id'") is not null)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Note (SubjectId, Date, Time, Text, Rating)
                SELECT SubjectId, Date, '00:00:00', Text, 3 FROM SubjectNotes ORDER BY Id;
                DROP TABLE SubjectNotes;
                """);
        if (await Scalar("SELECT 1 FROM pragma_table_info('ProjectNotes') WHERE name = 'Id'") is not null)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Note (ProjectId, Date, Time, Text, Rating)
                SELECT ProjectId, Date, '00:00:00', Text, 3 FROM ProjectNotes ORDER BY Id;
                DROP TABLE ProjectNotes;
                """);
        if (await Scalar("SELECT 1 FROM pragma_table_info('TaskNotes') WHERE name = 'Id'") is not null)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Note (TaskId, Date, Time, Text, Rating)
                SELECT TaskId, Date, '00:00:00', Text, 3 FROM TaskNotes ORDER BY Id;
                DROP TABLE TaskNotes;
                """);

        var legacyStickers = await Scalar("""
            SELECT 1 WHERE EXISTS (SELECT 1 FROM pragma_table_info('Stickers'))
            AND NOT EXISTS (SELECT 1 FROM pragma_table_info('Stickers') WHERE name = 'NoteId')
            """) is not null;
        if (legacyStickers)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Stickers RENAME TO Stickers_Legacy;");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Desktops (Id INTEGER NOT NULL PRIMARY KEY);
            INSERT OR IGNORE INTO Desktops (Id) VALUES (1);
            CREATE TABLE IF NOT EXISTS Stickers (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                DesktopId INTEGER NOT NULL,
                Text TEXT,
                TaskId INTEGER,
                NoteId INTEGER,
                X REAL,
                Y REAL,
                CONSTRAINT CK_Stickers_Content CHECK ((Text IS NOT NULL) + (TaskId IS NOT NULL) + (NoteId IS NOT NULL) = 1),
                FOREIGN KEY (DesktopId) REFERENCES Desktops (Id) ON DELETE CASCADE,
                FOREIGN KEY (TaskId) REFERENCES ProjectTasks (Id) ON DELETE CASCADE,
                FOREIGN KEY (NoteId) REFERENCES Note (Id) ON DELETE CASCADE
            );
            """);
        if (legacyStickers)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Stickers (Id, DesktopId, Text, TaskId, X, Y)
                SELECT Id, DesktopId, Text, TaskId, X, Y FROM Stickers_Legacy;
                INSERT INTO sqlite_sequence (name, seq)
                SELECT 'Stickers', seq FROM sqlite_sequence WHERE name = 'Stickers_Legacy'
                    AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'Stickers');
                UPDATE sqlite_sequence SET seq = max(seq, coalesce((SELECT seq FROM sqlite_sequence WHERE name = 'Stickers_Legacy'), 0))
                WHERE name = 'Stickers';
                DROP TABLE Stickers_Legacy;
                """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_Stickers_TaskId ON Stickers (TaskId);
            CREATE INDEX IF NOT EXISTS IX_Stickers_NoteId ON Stickers (NoteId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Stickers_DesktopId_TaskId ON Stickers (DesktopId, TaskId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Stickers_DesktopId_NoteId ON Stickers (DesktopId, NoteId);
            """);
    }

    // Upgrade the original EnsureCreated database without dropping subjects or notes.
    public static async Task Initialize(NotesDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var hasShortName = false;
            var hasSubjectDescription = false;
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('Subjects')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hasShortName |= reader.GetString(1) == "ShortName";
                    hasSubjectDescription |= reader.GetString(1) == "Description";
                }
            }
            if (!hasSubjectDescription)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects ADD COLUMN Description TEXT NOT NULL DEFAULT '';");
            if (!hasShortName)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects ADD COLUMN ShortName TEXT NOT NULL DEFAULT '';");
                await db.Database.ExecuteSqlRawAsync("UPDATE Subjects SET ShortName = substr('S' || Id, 1, 8);");
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS Projects (
                    Id INTEGER NOT NULL CONSTRAINT PK_Projects PRIMARY KEY AUTOINCREMENT,
                    SubjectId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    NormalizedCode TEXT NOT NULL,
                    CONSTRAINT FK_Projects_Subjects_SubjectId FOREIGN KEY (SubjectId) REFERENCES Subjects (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Projects_SubjectId_NormalizedCode ON Projects (SubjectId, NormalizedCode);
                CREATE TABLE IF NOT EXISTS ProjectTasks (
                    Id INTEGER NOT NULL CONSTRAINT PK_ProjectTasks PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    NormalizedCode TEXT NOT NULL,
                    Body TEXT NOT NULL,
                    CONSTRAINT FK_ProjectTasks_Projects_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects (Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_ProjectTasks_ProjectId_NormalizedCode ON ProjectTasks (ProjectId, NormalizedCode);
                """);
            await UpgradeNotesAndStickers(db, transaction);
            var hasProjectStatus = false;
            var hasProjectDescription = false;
            var hasProjectCreatedAt = false;
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('Projects')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hasProjectStatus |= reader.GetString(1) == "Status";
                    hasProjectDescription |= reader.GetString(1) == "Description";
                    hasProjectCreatedAt |= reader.GetString(1) == "CreatedAt";
                }
            }
            if (!hasProjectStatus)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;");
            if (!hasProjectDescription)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects ADD COLUMN Description TEXT NOT NULL DEFAULT '';");
            if (!hasProjectCreatedAt)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects ADD COLUMN CreatedAt TEXT NULL;");
            var hasTaskStatus = false;
            var hasTaskCreatedAt = false;
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('ProjectTasks')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hasTaskStatus |= reader.GetString(1) == "Status";
                    hasTaskCreatedAt |= reader.GetString(1) == "CreatedAt";
                }
            }
            if (!hasTaskStatus)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE ProjectTasks ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;");
            if (!hasTaskCreatedAt)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE ProjectTasks ADD COLUMN CreatedAt TEXT NULL;");
            var legacySessions = false;
            var sessionsWithStatus = false;
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('Sessions')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    legacySessions |= reader.GetString(1) == "Date";
                    sessionsWithStatus |= reader.GetString(1) == "Status";
                }
            }
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TR_Sessions_ClosedImmutable;");
            if (legacySessions || sessionsWithStatus)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sessions RENAME TO Sessions_Legacy;");
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS Sessions (
                    Id INTEGER NOT NULL CONSTRAINT PK_Sessions PRIMARY KEY AUTOINCREMENT,
                    SubjectId INTEGER NOT NULL,
                    ProjectId INTEGER NULL,
                    TaskId INTEGER NULL,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT NULL,
                    Text TEXT NOT NULL,
                    CONSTRAINT FK_Sessions_Subjects_SubjectId FOREIGN KEY (SubjectId) REFERENCES Subjects (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_Sessions_Projects_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_Sessions_ProjectTasks_TaskId FOREIGN KEY (TaskId) REFERENCES ProjectTasks (Id) ON DELETE RESTRICT
                );
                """);
            if (legacySessions)
            {
                // Interpret old calendar date/time in the server's local zone, then persist UTC.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Sessions (Id, SubjectId, ProjectId, TaskId, StartTime, EndTime, Text)
                    SELECT Id, SubjectId, ProjectId, TaskId, Date || ' ' || StartTime, Date || ' ' || EndTime, Text
                    FROM Sessions_Legacy;
                    DROP TABLE Sessions_Legacy;
                    """);
                var times = new List<(int Id, DateTimeOffset Start, DateTimeOffset End)>();
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandText = "SELECT Id, StartTime, EndTime FROM Sessions";
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        times.Add((reader.GetInt32(0),
                            DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
                            DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime()));
                }
                foreach (var time in times)
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Sessions SET StartTime = {time.Start}, EndTime = {time.End} WHERE Id = {time.Id}");
            }
            else if (sessionsWithStatus)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Sessions (Id, SubjectId, ProjectId, TaskId, StartTime, EndTime, Text)
                    SELECT Id, SubjectId, ProjectId, TaskId, StartTime, EndTime, Text FROM Sessions_Legacy;
                    DROP TABLE Sessions_Legacy;
                    """);
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Sessions_OneOpen ON Sessions ((1)) WHERE EndTime IS NULL;
                CREATE INDEX IF NOT EXISTS IX_Sessions_SubjectId_StartTime ON Sessions (SubjectId, StartTime);
                CREATE INDEX IF NOT EXISTS IX_Sessions_ProjectId ON Sessions (ProjectId);
                CREATE INDEX IF NOT EXISTS IX_Sessions_TaskId ON Sessions (TaskId);
                """);
            var subjectHasCounter = false;
            var projectHasCounter = false;
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('Subjects')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) subjectHasCounter |= reader.GetString(1) == "LastProjectNumber";
            }
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "PRAGMA table_info('Projects')";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) projectHasCounter |= reader.GetString(1) == "LastTaskNumber";
            }
            if (!subjectHasCounter)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects ADD COLUMN LastProjectNumber INTEGER NOT NULL DEFAULT 0;");
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE Subjects SET LastProjectNumber = COALESCE((SELECT MAX(CAST(Code AS INTEGER)) FROM Projects
                        WHERE SubjectId = Subjects.Id AND Code <> '' AND Code NOT GLOB '*[^0-9]*'), 0);
                    """);
            }
            if (!projectHasCounter)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects ADD COLUMN LastTaskNumber INTEGER NOT NULL DEFAULT 0;");
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE Projects SET LastTaskNumber = COALESCE((SELECT MAX(CAST(Code AS INTEGER)) FROM ProjectTasks
                        WHERE ProjectId = Projects.Id AND Code <> '' AND Code NOT GLOB '*[^0-9]*'), 0);
                    """);
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS PlannedActivities (
                    Id INTEGER NOT NULL CONSTRAINT PK_PlannedActivities PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    StartTime TEXT NOT NULL,
                    DurationMinutes INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    SubjectId INTEGER,
                    FOREIGN KEY (SubjectId) REFERENCES Subjects (Id) ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS IX_PlannedActivities_SubjectId ON PlannedActivities (SubjectId);
                CREATE INDEX IF NOT EXISTS IX_PlannedActivities_Date_StartTime_Id ON PlannedActivities (Date, StartTime, Id);
                CREATE TABLE IF NOT EXISTS PlannedActivityReferences (
                    Id INTEGER NOT NULL CONSTRAINT PK_PlannedActivityReferences PRIMARY KEY AUTOINCREMENT,
                    ActivityId INTEGER NOT NULL,
                    ProjectId INTEGER,
                    TaskId INTEGER,
                    CONSTRAINT CK_PlannedActivityReferences_Target CHECK ((ProjectId IS NULL) <> (TaskId IS NULL)),
                    FOREIGN KEY (ActivityId) REFERENCES PlannedActivities (Id) ON DELETE CASCADE,
                    FOREIGN KEY (ProjectId) REFERENCES Projects (Id) ON DELETE CASCADE,
                    FOREIGN KEY (TaskId) REFERENCES ProjectTasks (Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_PlannedActivityReferences_ActivityId_ProjectId
                    ON PlannedActivityReferences (ActivityId, ProjectId);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_PlannedActivityReferences_ActivityId_TaskId
                    ON PlannedActivityReferences (ActivityId, TaskId);
                CREATE INDEX IF NOT EXISTS IX_PlannedActivityReferences_ProjectId ON PlannedActivityReferences (ProjectId);
                CREATE INDEX IF NOT EXISTS IX_PlannedActivityReferences_TaskId ON PlannedActivityReferences (TaskId);
                """);
            await transaction.CommitAsync();
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}

using System;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Ben.Data;

public static class LocalMigrationRunner
{
    private const int LatestVersion = 5; // Update this as you add migrations


    private static bool TableExists(LocalSchemaDbContext db, string tableName)
    {
        using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT COUNT(*)
        FROM sqlite_master
        WHERE type = 'table' AND name = $name;
    ";
        var param = command.CreateParameter();
        param.ParameterName = "$name";
        param.Value = tableName;
        command.Parameters.Add(param);

        var result = (long)command.ExecuteScalar()!;
        return result > 0;
    }

    private static bool ColumnExists(LocalSchemaDbContext db, string tableName, string columnName)
    {
        using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader[1]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void ApplyMigrations(LocalSchemaDbContext db)
    {
        db.Database.EnsureCreated();

        bool schemaInfoExists = TableExists(db, "SchemaInfo");

        if (!schemaInfoExists)
        {
            InitializeSchemaInfoForExistingDatabase(db);
            return;
        }

        var info = db.SchemaInfo.SingleOrDefault();

        if (info == null)
        {
            InitializeSchemaInfoForExistingDatabase(db);
            info = db.SchemaInfo.Single();
        }

        RunIncrementalMigrations(db, info.Version);
    }

    private static void InitializeSchemaInfoForExistingDatabase(LocalSchemaDbContext db)
    {
        // Create SchemaInfo table
        db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS SchemaInfo (
            Id INTEGER PRIMARY KEY,
            Version INTEGER NOT NULL
        );
    ");

        // Insert the baseline version
        db.Database.ExecuteSqlRaw(
            "INSERT INTO SchemaInfo (Id, Version) VALUES (1, {0})",
            LatestVersion
        );
    }

    private static void RunIncrementalMigrations(LocalSchemaDbContext db, int currentVersion)
    {
        int version = currentVersion;

        // Example migration: Version 1 → Version 2
        if (version < 2)
        {
            if (!ColumnExists(db, "Tasks", "ParentTaskId"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN ParentTaskId TEXT;");
            }

            if (!ColumnExists(db, "Tasks", "OriginalTaskId"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN OriginalTaskId TEXT;");
            }

            if (!ColumnExists(db, "Tasks", "TaskItemId"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN TaskItemId TEXT;");
            }

            version = 2;
        }

        if (version < 3)
        {
            db.Database.ExecuteSqlRaw(@"
                UPDATE Tasks
                SET [Key] = 'date:' || COALESCE(strftime('%Y-%m-%d', [Key]), substr([Key], 1, 10))
                WHERE [Key] IS NOT NULL
                  AND [Key] NOT LIKE 'date:%'
                  AND [Key] NOT LIKE 'project:%';
            ");

            db.Database.ExecuteSqlRaw(@"
                UPDATE Notes
                SET [Key] = 'date:' || COALESCE(strftime('%Y-%m-%d', [Key]), substr([Key], 1, 10))
                WHERE [Key] IS NOT NULL
                  AND [Key] NOT LIKE 'date:%'
                  AND [Key] NOT LIKE 'project:%';
            ");

            version = 3;
        }

        if (version < 4)
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Projects (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UpdatedAt TEXT NULL,
                    Version TEXT NULL,
                    Deleted INTEGER NOT NULL DEFAULT 0,
                    Name TEXT NOT NULL,
                    NormalizedName TEXT NOT NULL
                );
            ");

            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Projects_NormalizedName ON Projects (NormalizedName);
            ");

            version = 4;
        }

        if (version < 5)
        {
            db.Database.ExecuteSqlRaw(@"
                DROP INDEX IF EXISTS IX_Projects_Key;
            ");

            if (ColumnExists(db, "Projects", "Key"))
            {
                db.Database.ExecuteSqlRaw(@"
                    ALTER TABLE Projects DROP COLUMN [Key];
                ");
            }

            version = 5;
        }

        // Save final version
        db.Database.ExecuteSqlRaw("UPDATE SchemaInfo SET Version = {0} WHERE Id = 1", version);

    }
}

-- ============================================================
-- Ben.Datasync.Server — idempotent migration script
--
-- Safe to run in any state:
--   • Fresh empty database (no tables yet)
--   • Existing database created by EnsureCreated (no __EFMigrationsHistory)
--   • Partially-migrated database (some history rows already present)
--   • Fully-migrated database (safe no-op)
--
-- Run this script against the live database whenever the server
-- cannot apply migrations automatically on startup (e.g., the
-- first time switching from EnsureCreated to MigrateAsync).
-- ============================================================

-- ------------------------------------------------------------
-- Ensure migrations history table exists
-- ------------------------------------------------------------
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory]
    (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

-- ============================================================
-- 20260312004643_Baseline
-- (empty migration — schema was already created by EnsureCreated)
-- ============================================================
IF NOT EXISTS (
    SELECT 1
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260312004643_Baseline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (N'20260312004643_Baseline', N'10.0.3');
END;
GO

-- ============================================================
-- 20260312013726_AddTaskForwardingLineage
-- ============================================================

-- Columns
IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'OriginalTaskId'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [OriginalTaskId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'ParentTaskId'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [ParentTaskId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'TaskItemId'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [TaskItemId] nvarchar(450) NULL;
END;
GO

-- Indexes
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'IX_TaskItems_OriginalTaskId'
)
BEGIN
    CREATE INDEX [IX_TaskItems_OriginalTaskId] ON [TaskItems] ([OriginalTaskId]);
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'IX_TaskItems_ParentTaskId'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ParentTaskId] ON [TaskItems] ([ParentTaskId]);
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'IX_TaskItems_TaskItemId'
)
BEGIN
    CREATE INDEX [IX_TaskItems_TaskItemId] ON [TaskItems] ([TaskItemId]);
END;
GO

-- Foreign keys
IF NOT EXISTS (
    SELECT 1
FROM sys.foreign_keys
WHERE object_id = OBJECT_ID(N'[FK_TaskItems_TaskItems_OriginalTaskId]')
)
BEGIN
    ALTER TABLE [TaskItems]
        ADD CONSTRAINT [FK_TaskItems_TaskItems_OriginalTaskId]
        FOREIGN KEY ([OriginalTaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.foreign_keys
WHERE object_id = OBJECT_ID(N'[FK_TaskItems_TaskItems_ParentTaskId]')
)
BEGIN
    ALTER TABLE [TaskItems]
        ADD CONSTRAINT [FK_TaskItems_TaskItems_ParentTaskId]
        FOREIGN KEY ([ParentTaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.foreign_keys
WHERE object_id = OBJECT_ID(N'[FK_TaskItems_TaskItems_TaskItemId]')
)
BEGIN
    ALTER TABLE [TaskItems]
        ADD CONSTRAINT [FK_TaskItems_TaskItems_TaskItemId]
        FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]);
END;
GO

-- History entry
IF NOT EXISTS (
    SELECT 1
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260312013726_AddTaskForwardingLineage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (N'20260312013726_AddTaskForwardingLineage', N'10.0.3');
END;
GO

-- ============================================================
-- 20260314120000_ConvertKeysToStringConvention
-- ============================================================

-- Drop Key indexes before altering column type (SQL Server requires this).
-- They are recreated below after the ALTER.
IF EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'IX_TaskItems_Key'
)
BEGIN
    DROP INDEX [IX_TaskItems_Key] ON [TaskItems];
END;
GO

IF EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[NoteItems]') AND name = N'IX_NoteItems_Key'
)
BEGIN
    DROP INDEX [IX_NoteItems_Key] ON [NoteItems];
END;
GO

-- Alter column type only if it is still datetime2
IF EXISTS (
    SELECT 1
FROM sys.columns c
    INNER JOIN sys.types t ON c.system_type_id = t.system_type_id
WHERE c.object_id = OBJECT_ID(N'[TaskItems]') AND c.name = N'Key' AND t.name = N'datetime2'
)
BEGIN
    ALTER TABLE [TaskItems] ALTER COLUMN [Key] nvarchar(128) NOT NULL;
END;
GO

IF EXISTS (
    SELECT 1
FROM sys.columns c
    INNER JOIN sys.types t ON c.system_type_id = t.system_type_id
WHERE c.object_id = OBJECT_ID(N'[NoteItems]') AND c.name = N'Key' AND t.name = N'datetime2'
)
BEGIN
    ALTER TABLE [NoteItems] ALTER COLUMN [Key] nvarchar(128) NOT NULL;
END;
GO

-- Normalize any rows not yet using the date:/project: prefix
UPDATE [TaskItems]
SET [Key] =
    CASE
        WHEN TRY_CONVERT(datetime2, [Key]) IS NOT NULL
            THEN 'date:' + CONVERT(varchar(10), TRY_CONVERT(datetime2, [Key]), 23)
        WHEN [Key] LIKE '____-__-__%'
            THEN 'date:' + LEFT([Key], 10)
        ELSE
            'date:' + CONVERT(varchar(10), SYSUTCDATETIME(), 23)
    END
WHERE [Key] NOT LIKE 'date:%' AND [Key] NOT LIKE 'project:%';
GO

UPDATE [NoteItems]
SET [Key] =
    CASE
        WHEN TRY_CONVERT(datetime2, [Key]) IS NOT NULL
            THEN 'date:' + CONVERT(varchar(10), TRY_CONVERT(datetime2, [Key]), 23)
        WHEN [Key] LIKE '____-__-__%'
            THEN 'date:' + LEFT([Key], 10)
        ELSE
            'date:' + CONVERT(varchar(10), SYSUTCDATETIME(), 23)
    END
WHERE [Key] NOT LIKE 'date:%' AND [Key] NOT LIKE 'project:%';
GO

-- Recreate Key indexes
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[TaskItems]') AND name = N'IX_TaskItems_Key'
)
BEGIN
    CREATE INDEX [IX_TaskItems_Key] ON [TaskItems] ([Key]);
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[NoteItems]') AND name = N'IX_NoteItems_Key'
)
BEGIN
    CREATE INDEX [IX_NoteItems_Key] ON [NoteItems] ([Key]);
END;
GO

-- History entry
IF NOT EXISTS (
    SELECT 1
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260314120000_ConvertKeysToStringConvention'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (N'20260314120000_ConvertKeysToStringConvention', N'10.0.3');
END;
GO

-- ============================================================
-- 20260314154500_AddProjects
-- ============================================================

IF OBJECT_ID(N'[ProjectItems]') IS NULL
BEGIN
    CREATE TABLE [ProjectItems]
    (
        [Id] nvarchar(450) NOT NULL,
        [Deleted] bit NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [NormalizedName] nvarchar(128) NOT NULL,
        CONSTRAINT [PK_ProjectItems] PRIMARY KEY ([Id])
    );
END;
GO

IF EXISTS (
    SELECT 1
FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'[ProjectItems]')
    AND c.name = N'UserId'
    AND t.name = N'nvarchar'
    AND c.max_length = -1
)
BEGIN
    UPDATE [ProjectItems]
SET [UserId] = LEFT([UserId], 256)
WHERE LEN([UserId]) > 256;

    ALTER TABLE [ProjectItems] ALTER COLUMN [UserId] nvarchar(256) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[ProjectItems]') AND name = N'IX_ProjectItems_UpdatedAt_Deleted'
)
BEGIN
    CREATE INDEX [IX_ProjectItems_UpdatedAt_Deleted] ON [ProjectItems] ([UpdatedAt], [Deleted]);
END;
GO

-- ============================================================
-- 20260315031000_RemoveProjectKeyColumn
-- ============================================================

IF EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[ProjectItems]') AND name = N'IX_ProjectItems_Key'
)
BEGIN
    DROP INDEX [IX_ProjectItems_Key] ON [ProjectItems];
END;
GO

IF COL_LENGTH(N'ProjectItems', N'Key') IS NOT NULL
BEGIN
    ALTER TABLE [ProjectItems] DROP COLUMN [Key];
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260315031000_RemoveProjectKeyColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (N'20260315031000_RemoveProjectKeyColumn', N'10.0.3');
END;
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[ProjectItems]') AND name = N'IX_ProjectItems_UserId_NormalizedName'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectItems_UserId_NormalizedName] ON [ProjectItems] ([UserId], [NormalizedName]);
END;
GO

UPDATE taskItems
SET [Key] = N'project:' + projectItems.[Id]
FROM [TaskItems] taskItems
    INNER JOIN [ProjectItems] projectItems
    ON projectItems.[UserId] = taskItems.[UserId]
        AND projectItems.[NormalizedName] = UPPER(LTRIM(RTRIM(SUBSTRING(taskItems.[Key], LEN(N'project:') + 1, 4000))))
WHERE taskItems.[Key] LIKE N'project:%'
    AND taskItems.[Key] <> N'project:' + projectItems.[Id];
GO

UPDATE noteItems
SET [Key] = N'project:' + projectItems.[Id]
FROM [NoteItems] noteItems
    INNER JOIN [ProjectItems] projectItems
    ON projectItems.[UserId] = noteItems.[UserId]
        AND projectItems.[NormalizedName] = UPPER(LTRIM(RTRIM(SUBSTRING(noteItems.[Key], LEN(N'project:') + 1, 4000))))
WHERE noteItems.[Key] LIKE N'project:%'
    AND noteItems.[Key] <> N'project:' + projectItems.[Id];
GO

IF NOT EXISTS (
    SELECT 1
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260314154500_AddProjects'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (N'20260314154500_AddProjects', N'10.0.3');
END;
GO


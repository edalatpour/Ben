IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260312004643_Baseline', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [TaskItems] ADD [OriginalTaskId] nvarchar(450) NULL;

ALTER TABLE [TaskItems] ADD [ParentTaskId] nvarchar(450) NULL;

ALTER TABLE [TaskItems] ADD [TaskItemId] nvarchar(450) NULL;

CREATE INDEX [IX_TaskItems_OriginalTaskId] ON [TaskItems] ([OriginalTaskId]);

CREATE INDEX [IX_TaskItems_ParentTaskId] ON [TaskItems] ([ParentTaskId]);

CREATE INDEX [IX_TaskItems_TaskItemId] ON [TaskItems] ([TaskItemId]);

ALTER TABLE [TaskItems] ADD CONSTRAINT [FK_TaskItems_TaskItems_OriginalTaskId] FOREIGN KEY ([OriginalTaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [TaskItems] ADD CONSTRAINT [FK_TaskItems_TaskItems_ParentTaskId] FOREIGN KEY ([ParentTaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [TaskItems] ADD CONSTRAINT [FK_TaskItems_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260312013726_AddTaskForwardingLineage', N'10.0.3');

COMMIT;
GO


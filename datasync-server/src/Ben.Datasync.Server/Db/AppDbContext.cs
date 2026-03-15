// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace Ben.Datasync.Server

{

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<NoteItem> NoteItems => Set<NoteItem>();

        // public DbSet<TodoList> TodoLists => Set<TodoList>();

        public async Task InitializeDatabaseAsync()
        {
            // EnsureCreatedAsync creates the full current schema for a brand-new database.
            // It returns true only when the database (and its tables) were just created.
            bool isNewDatabase = await Database.EnsureCreatedAsync();

            if (isNewDatabase)
            {
                // The schema is already up-to-date via EnsureCreated.
                // Seed all known migration IDs so MigrateAsync treats them as already applied.
                foreach (var migrationId in Database.GetMigrations())
                {
                    await Database.ExecuteSqlRawAsync(
                        "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, {1})",
                        migrationId, "10.0.3");
                }
            }

            // Apply any migrations not yet recorded in history:
            //   - New databases:  all migrations are seeded above; this is a no-op.
            //   - Existing databases that already have history: applies only pending ones.
            //   - Existing databases with no history (transition from EnsureCreated):
            //     run migration.sql against the database first to establish the baseline.
            await Database.MigrateAsync();

            const string datasyncTrigger = @"
            CREATE OR ALTER TRIGGER [dbo].[{0}_datasync] ON [dbo].[{0}] AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                UPDATE
                    [dbo].[{0}]
                SET
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE
                    [Id] IN (SELECT [Id] FROM INSERTED);
            END
        "
            ;

            // Install the above trigger to set the UpdatedAt field automatically on insert or update.
            foreach (IEntityType table in Model.GetEntityTypes())
            {
                string sql = string.Format(datasyncTrigger, table.GetTableName());
                _ = await Database.ExecuteSqlRawAsync(sql);
            }
        }

        [SuppressMessage("Style", "IDE0058:Expression value is never used", Justification = "Model builder ignores return value.")]
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tells EF Core that the TodoItem entity has a trigger.
            modelBuilder.Entity<TaskItem>()
                .ToTable(tb => tb.HasTrigger("TaskItem_datasync"));

            // Tells EF Core that the TodoList entity has a trigger.
            modelBuilder.Entity<NoteItem>()
                .ToTable(tb => tb.HasTrigger("NoteItem_datasync"));

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.ParentTask)
                .WithMany()
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskItem>()
                .HasIndex(t => t.Key);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.OriginalTask)
                .WithMany()
                .HasForeignKey(t => t.OriginalTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NoteItem>()
                .HasIndex(n => n.Key);

        }
    }

}


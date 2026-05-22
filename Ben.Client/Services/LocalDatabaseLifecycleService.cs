using Ben.Data;
using Microsoft.EntityFrameworkCore;

namespace Ben.Services;

public interface ILocalDatabaseLifecycleService
{
    Task<bool> DeleteLocalDatabaseAsync();

    Task<bool> CreateLocalDatabaseAsync();

    Task<bool> DeleteAndRecreateLocalDatabaseAsync();

    Task<bool> RequeueAllLocalDataForUploadAsync();
}

public sealed class LocalDatabaseLifecycleService : ILocalDatabaseLifecycleService
{
    private readonly PlannerDbContext _plannerDbContext;
    private readonly LocalSchemaDbContext _schemaDbContext;

    public LocalDatabaseLifecycleService(
        PlannerDbContext plannerDbContext,
        LocalSchemaDbContext schemaDbContext)
    {
        _plannerDbContext = plannerDbContext;
        _schemaDbContext = schemaDbContext;
    }

    public async Task<bool> DeleteLocalDatabaseAsync()
    {
        _plannerDbContext.ChangeTracker.Clear();
        _schemaDbContext.ChangeTracker.Clear();

        var plannerConnection = _plannerDbContext.Database.GetDbConnection();
        if (plannerConnection.State != System.Data.ConnectionState.Closed)
        {
            plannerConnection.Close();
        }

        var schemaConnection = _schemaDbContext.Database.GetDbConnection();
        if (schemaConnection.State != System.Data.ConnectionState.Closed)
        {
            schemaConnection.Close();
        }

        bool dbDeleted = await _plannerDbContext.DeleteDatabaseFileAsync().ConfigureAwait(false);
        if (!dbDeleted)
        {
            Console.WriteLine("Warning: Database file deletion failed.");
        }

        return dbDeleted;
    }

    public async Task<bool> CreateLocalDatabaseAsync()
    {
        bool dbRecreated = await _plannerDbContext.RecreateAndInitializeDatabaseAsync().ConfigureAwait(false);
        if (!dbRecreated)
        {
            Console.WriteLine("Warning: Failed to create local planner database.");
        }

        await _schemaDbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
        LocalMigrationRunner.ApplyMigrations(_schemaDbContext);

        return dbRecreated;
    }

    public async Task<bool> DeleteAndRecreateLocalDatabaseAsync()
    {
        bool dbDeleted = await DeleteLocalDatabaseAsync().ConfigureAwait(false);
        bool dbCreated = await CreateLocalDatabaseAsync().ConfigureAwait(false);
        return dbDeleted && dbCreated;
    }

    public async Task<bool> RequeueAllLocalDataForUploadAsync()
    {
        try
        {
            var tasks = await _plannerDbContext.Tasks
                .AsNoTracking()
                .Where(item => !item.Deleted)
                .Select(item => new Models.TaskItem
                {
                    Id = item.Id,
                    Key = item.Key,
                    Status = item.Status,
                    Priority = item.Priority,
                    Order = item.Order,
                    Title = item.Title,
                    ParentTaskId = item.ParentTaskId,
                    OriginalTaskId = item.OriginalTaskId,
                    Deleted = false,
                    UpdatedAt = null,
                    Version = null
                })
                .ToListAsync();

            var notes = await _plannerDbContext.Notes
                .AsNoTracking()
                .Where(item => !item.Deleted)
                .Select(item => new Models.NoteItem
                {
                    Id = item.Id,
                    Key = item.Key,
                    Text = item.Text,
                    Order = item.Order,
                    Deleted = false,
                    UpdatedAt = null,
                    Version = null
                })
                .ToListAsync();

            var projects = await _plannerDbContext.Projects
                .AsNoTracking()
                .Where(item => !item.Deleted)
                .Select(item => new Models.ProjectItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    NormalizedName = item.NormalizedName,
                    Deleted = false,
                    UpdatedAt = null,
                    Version = null
                })
                .ToListAsync();

            _plannerDbContext.ChangeTracker.Clear();

            // Reset local rows and queue fresh inserts so next sign-in sync can repopulate cloud.
            await _plannerDbContext.DatasyncOperationsQueue.ExecuteDeleteAsync();
            await _plannerDbContext.Notes.ExecuteDeleteAsync();
            await _plannerDbContext.Tasks.ExecuteDeleteAsync();
            await _plannerDbContext.Projects.ExecuteDeleteAsync();

            _plannerDbContext.Tasks.AddRange(tasks);
            _plannerDbContext.Notes.AddRange(notes);
            _plannerDbContext.Projects.AddRange(projects);
            await _plannerDbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to requeue local data for upload: {ex.Message}");
            return false;
        }
    }
}
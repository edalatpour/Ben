using Ben.Data;
using Microsoft.EntityFrameworkCore;

namespace Ben.Services;

public interface ILocalDatabaseLifecycleService
{
    Task<bool> DeleteLocalDatabaseAsync();

    Task<bool> CreateLocalDatabaseAsync();

    Task<bool> DeleteAndRecreateLocalDatabaseAsync();
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
}
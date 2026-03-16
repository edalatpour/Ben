using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Ben.Data;
using Ben.Services;
using Ben.Views;
using Ben.ViewModels;
using System.Data;

namespace Ben;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>()
               .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("PatrickHand-Regular.ttf", "Patrick Hand");
                });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "planner.datasync.db");

        builder.Services.AddSingleton(new DatasyncOptions
        {
            Endpoint = new Uri(Constants.ServiceUri)
        });

        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);

        builder.Services.AddSingleton<AuthenticationService>();
        builder.Services.AddSingleton<DatasyncSyncService>();

        builder.Services.AddDbContext<LocalSchemaDbContext>(options =>
        {
            options.UseSqlite($"Filename={dbPath}");
        });

        builder.Services.AddDbContext<PlannerDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite($"Filename={dbPath}");
        });

        // Register your data service (repository)
        builder.Services.AddSingleton<PlannerRepository>();

        // Keep one host page and one view model alive so date navigation reuses existing views.
        builder.Services.AddSingleton<DailyViewModel>();
        builder.Services.AddSingleton<DailyHostPage>();

        var app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var pdb = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
            EnsurePlannerSchema(pdb);
            pdb.Database.EnsureCreated();

            var ldb = scope.ServiceProvider.GetRequiredService<LocalSchemaDbContext>();
            LocalMigrationRunner.ApplyMigrations(ldb);

            var repo = scope.ServiceProvider.GetRequiredService<PlannerRepository>();
            repo.EnsureNoteOrderBackfillAsync().GetAwaiter().GetResult();

            var syncService = scope.ServiceProvider.GetRequiredService<DatasyncSyncService>();
            syncService.Start();
        }

        return app;

    }

    static void EnsurePlannerSchema(PlannerDbContext db)
    {
        bool hasTasks = TableExists(db, "Tasks");
        bool hasNotes = TableExists(db, "Notes");
        bool hasProjects = TableExists(db, "Projects");

        if (hasTasks && hasNotes && hasProjects)
        {
            return;
        }

        bool hasAnyPlannerTables = hasTasks || hasNotes || hasProjects;
        bool hasAnyTables = DatabaseHasAnyUserTables(db);

        // Recover from first-run partial schema where only SchemaInfo exists.
        if (!hasAnyPlannerTables && hasAnyTables)
        {
            db.Database.EnsureDeleted();
            return;
        }
    }

    static bool TableExists(DbContext db, string tableName)
    {
        using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $name;
        ";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        object? value = command.ExecuteScalar();
        long count = Convert.ToInt64(value ?? 0);
        return count > 0;
    }

    static bool DatabaseHasAnyUserTables(DbContext db)
    {
        using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%';
        ";

        object? value = command.ExecuteScalar();
        long count = Convert.ToInt64(value ?? 0);
        return count > 0;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Ben.Data;
using Ben.Services;
using Ben.Views;
using Ben.ViewModels;

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
            var ldb = scope.ServiceProvider.GetRequiredService<LocalSchemaDbContext>();
            LocalMigrationRunner.ApplyMigrations(ldb);

            var pdb = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
            pdb.Database.EnsureCreated();

            var repo = scope.ServiceProvider.GetRequiredService<PlannerRepository>();
            repo.EnsureNoteOrderBackfillAsync().GetAwaiter().GetResult();

            var syncService = scope.ServiceProvider.GetRequiredService<DatasyncSyncService>();
            syncService.Start();
        }

        return app;

    }
}

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
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "planner.datasync.db");

        builder.Services.AddSingleton(new DatasyncOptions
        {
            Endpoint = new Uri("https://app-qg762nqxq5bva.azurewebsites.net/")
        });

        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);

        // Authentication
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<DatasyncSyncService>();

        builder.Services.AddSingleton<IAlertService, AlertService>();

        builder.Services.AddDbContext<PlannerDbContext>(options =>
            options.UseSqlite($"Filename={dbPath}"));

        // Register your data service (repository)
        builder.Services.AddSingleton<PlannerRepository>();

        // Register ViewModels + Pages
        builder.Services.AddTransient<DailyViewModel>();
        builder.Services.AddTransient<DailyHostPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();

        // AppShell needs IAuthService
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
            db.Database.EnsureCreated();

            var repo = scope.ServiceProvider.GetRequiredService<PlannerRepository>();
            repo.EnsureNoteOrderBackfillAsync().GetAwaiter().GetResult();

            // Only start sync if the user is authenticated.
            var syncService = scope.ServiceProvider.GetRequiredService<DatasyncSyncService>();
            syncService.Start();
        }

        return app;

    }
}

using System;
using CommunityToolkit.Datasync.Client.Http;
using CommunityToolkit.Datasync.Client.Offline;
using Microsoft.EntityFrameworkCore;
using Ben.Models;
using Ben.Services;

namespace Ben.Data;

public class PlannerDbContext : OfflineDbContext
{
    private readonly DatasyncOptions _options;
    private readonly AuthenticatedHttpHandler _authHandler;

    public PlannerDbContext(
        DbContextOptions<PlannerDbContext> options,
        DatasyncOptions datasyncOptions,
        AuthenticatedHttpHandler authHandler)
        : base(options)
    {
        _options = datasyncOptions;
        _authHandler = authHandler;
    }

    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<NoteItem> Notes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.Key);

        modelBuilder.Entity<NoteItem>()
            .HasIndex(n => n.Key);
    }


    protected override void OnDatasyncInitialization(DatasyncOfflineOptionsBuilder optionsBuilder)
    {
        if (_options.Endpoint == null)
        {
            return;
        }

        // AuthenticatedHttpHandler (injected via DI) adds the Bearer token to outgoing requests
        HttpClientOptions clientOptions = new()
        {
            Endpoint = _options.Endpoint,
            Timeout = TimeSpan.FromSeconds(30)
        };
        optionsBuilder.UseHttpClientOptions(clientOptions);

        optionsBuilder.Entity<TaskItem>(cfg =>
        {
            cfg.Endpoint = new Uri("/tables/taskitem", UriKind.Relative);
            cfg.ConflictResolver = new ClientWinsConflictResolver();
        });

        optionsBuilder.Entity<NoteItem>(cfg =>
        {
            cfg.Endpoint = new Uri("/tables/noteitem", UriKind.Relative);
            cfg.ConflictResolver = new ClientWinsConflictResolver();
        });
    }
}


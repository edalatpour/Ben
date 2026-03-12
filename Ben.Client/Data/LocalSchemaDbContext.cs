using System;
using Microsoft.EntityFrameworkCore;

namespace Ben.Data;

public class LocalSchemaDbContext : DbContext
{
    public LocalSchemaDbContext(DbContextOptions<LocalSchemaDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchemaInfo> SchemaInfo { get; set; }
}

using System;
using Microsoft.EntityFrameworkCore;

namespace Bennie.Data;

public class LocalSchemaDbContext : DbContext
{
    public LocalSchemaDbContext(DbContextOptions<LocalSchemaDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchemaInfo> SchemaInfo { get; set; }
}

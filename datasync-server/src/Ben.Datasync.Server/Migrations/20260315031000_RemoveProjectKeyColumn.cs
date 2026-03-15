using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ben.Datasync.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260315031000_RemoveProjectKeyColumn")]
    public partial class RemoveProjectKeyColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[ProjectItems]') AND name = N'IX_ProjectItems_Key'
)
BEGIN
    DROP INDEX [IX_ProjectItems_Key] ON [ProjectItems];
END;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'ProjectItems', N'Key') IS NOT NULL
BEGIN
    ALTER TABLE [ProjectItems] DROP COLUMN [Key];
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'ProjectItems', N'Key') IS NULL
BEGIN
    ALTER TABLE [ProjectItems] ADD [Key] nvarchar(128) NOT NULL DEFAULT N'';
END;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[ProjectItems]') AND name = N'IX_ProjectItems_Key'
)
BEGIN
    CREATE INDEX [IX_ProjectItems_Key] ON [ProjectItems] ([Key]);
END;
");
        }
    }
}

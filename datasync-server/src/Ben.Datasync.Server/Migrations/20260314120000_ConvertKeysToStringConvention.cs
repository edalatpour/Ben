using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ben.Datasync.Server.Migrations
{
    /// <inheritdoc />
    public partial class ConvertKeysToStringConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "TaskItems",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "NoteItems",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "datetime2");

            migrationBuilder.Sql(@"
                UPDATE [TaskItems]
                SET [Key] =
                    CASE
                        WHEN [Key] LIKE 'date:%' OR [Key] LIKE 'project:%' THEN [Key]
                        WHEN TRY_CONVERT(datetime2, [Key]) IS NOT NULL THEN 'date:' + CONVERT(varchar(10), TRY_CONVERT(datetime2, [Key]), 23)
                        WHEN [Key] LIKE '____-__-__%' THEN 'date:' + LEFT([Key], 10)
                        ELSE 'date:' + CONVERT(varchar(10), SYSUTCDATETIME(), 23)
                    END;
            ");

            migrationBuilder.Sql(@"
                UPDATE [NoteItems]
                SET [Key] =
                    CASE
                        WHEN [Key] LIKE 'date:%' OR [Key] LIKE 'project:%' THEN [Key]
                        WHEN TRY_CONVERT(datetime2, [Key]) IS NOT NULL THEN 'date:' + CONVERT(varchar(10), TRY_CONVERT(datetime2, [Key]), 23)
                        WHEN [Key] LIKE '____-__-__%' THEN 'date:' + LEFT([Key], 10)
                        ELSE 'date:' + CONVERT(varchar(10), SYSUTCDATETIME(), 23)
                    END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [TaskItems]
                SET [Key] =
                    CASE
                        WHEN [Key] LIKE 'date:____-__-__' THEN SUBSTRING([Key], 6, 10)
                        WHEN [Key] LIKE 'project:%' THEN CONVERT(varchar(10), SYSUTCDATETIME(), 23)
                        ELSE LEFT([Key], 10)
                    END;
            ");

            migrationBuilder.Sql(@"
                UPDATE [NoteItems]
                SET [Key] =
                    CASE
                        WHEN [Key] LIKE 'date:____-__-__' THEN SUBSTRING([Key], 6, 10)
                        WHEN [Key] LIKE 'project:%' THEN CONVERT(varchar(10), SYSUTCDATETIME(), 23)
                        ELSE LEFT([Key], 10)
                    END;
            ");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Key",
                table: "TaskItems",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Key",
                table: "NoteItems",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }
    }
}

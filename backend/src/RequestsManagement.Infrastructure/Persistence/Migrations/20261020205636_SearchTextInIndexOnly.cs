using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchTextInIndexOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "Requests",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                stored: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComputedColumnSql: "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "Requests",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                stored: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComputedColumnSql: "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                oldStored: false);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Requests",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "SearchText" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);
        }
    }
}

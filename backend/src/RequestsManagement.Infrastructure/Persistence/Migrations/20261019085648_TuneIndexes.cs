using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TuneIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_AssignedTo_Status",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_OrganizationName",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_Status_CreatedAt",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_AssignedTo_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "AssignedTo", "CreatedAt", "Id" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_OrganizationName_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "OrganizationName", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_CreatedAt_Id",
                table: "Requests",
                columns: new[] { "Status", "CreatedAt", "Id" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_AssignedTo_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_OrganizationName_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_Status_CreatedAt_Id",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_AssignedTo_Status",
                table: "Requests",
                columns: new[] { "AssignedTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_OrganizationName",
                table: "Requests",
                column: "OrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_CreatedAt",
                table: "Requests",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Priority" });
        }
    }
}

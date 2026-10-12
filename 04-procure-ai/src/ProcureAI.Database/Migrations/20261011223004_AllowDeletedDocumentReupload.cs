using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcureAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowDeletedDocumentReupload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true,
                filter: "\"State\" <> 'Deleting'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class BoundDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                schema: "app",
                table: "Documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "app",
                table: "Documents",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                schema: "app",
                table: "Documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "app",
                table: "Documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);
        }
    }
}

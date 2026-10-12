using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class NormalizedPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoObjectKey",
                schema: "app",
                table: "Documents",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PhotoObjectKey", schema: "app", table: "Documents");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessReadAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class VariantStaleness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Stale",
                schema: "app",
                table: "TextVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Stale", schema: "app", table: "TextVariants");
        }
    }
}

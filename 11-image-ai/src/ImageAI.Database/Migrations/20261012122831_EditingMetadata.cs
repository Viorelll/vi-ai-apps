using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class EditingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditMasks",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    RegionHash = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditMasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditMasks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EditMasks_ImageAssets_InputAssetId",
                        column: x => x.InputAssetId,
                        principalSchema: "app",
                        principalTable: "ImageAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EditMasks_DocumentId_InputAssetId",
                schema: "app",
                table: "EditMasks",
                columns: new[] { "DocumentId", "InputAssetId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EditMasks_InputAssetId",
                schema: "app",
                table: "EditMasks",
                column: "InputAssetId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EditMasks", schema: "app");
        }
    }
}

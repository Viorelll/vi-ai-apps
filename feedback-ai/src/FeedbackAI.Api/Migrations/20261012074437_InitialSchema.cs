using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "FeedbackBatches",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxonomyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabelCorrections",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: false),
                    NewValue = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackItems",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    OriginalText = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    RedactedText = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    ActiveTopic = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActiveUrgency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackItems_FeedbackBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "app",
                        principalTable: "FeedbackBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classifications",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Sentiment = table.Column<string>(type: "text", nullable: false),
                    Urgency = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classifications_FeedbackItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "app",
                        principalTable: "FeedbackItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_ItemId",
                schema: "app",
                table: "Classifications",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_BatchId_Ordinal",
                schema: "app",
                table: "FeedbackItems",
                columns: new[] { "BatchId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelCorrections_ItemId",
                schema: "app",
                table: "LabelCorrections",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Classifications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "LabelCorrections",
                schema: "app");

            migrationBuilder.DropTable(
                name: "FeedbackItems",
                schema: "app");

            migrationBuilder.DropTable(
                name: "FeedbackBatches",
                schema: "app");
        }
    }
}

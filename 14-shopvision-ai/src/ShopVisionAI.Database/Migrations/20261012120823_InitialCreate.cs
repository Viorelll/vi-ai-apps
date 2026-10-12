using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShopVisionAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "app");

            migrationBuilder.CreateTable(
                name: "CatalogProducts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    AttributesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Sheet = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false
                    ),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    CatalogVersion = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogProducts", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Mime = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "VisualSearches",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CatalogVersion = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisualSearches_Documents_ImageDocumentId",
                        column: x => x.ImageDocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_VisualSearches_SearchId",
                        column: x => x.SearchId,
                        principalSchema: "app",
                        principalTable: "VisualSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    PipelineVersion = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionJobs_VisualSearches_TargetId",
                        column: x => x.TargetId,
                        principalSchema: "app",
                        principalTable: "VisualSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.InsertData(
                schema: "app",
                table: "CatalogProducts",
                columns: new[]
                {
                    "Id",
                    "AttributesJson",
                    "CapturedAtUtc",
                    "CatalogVersion",
                    "Category",
                    "Currency",
                    "Name",
                    "Price",
                    "Quantity",
                    "Sheet",
                    "Sku",
                },
                values: new object[,]
                {
                    {
                        new Guid("11111111-1111-1111-1111-111111111111"),
                        "{\"color\":\"black\",\"connector\":\"usb\",\"shape\":\"compact\"}",
                        new DateTimeOffset(
                            new DateTime(2026, 10, 11, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "seed-v1",
                        "adapter",
                        "MDL",
                        "Adaptor USB-C Compact",
                        149.90m,
                        8,
                        "Adaptor USB-C negru; USB 3.0 conform fișei seed. Date artificiale.",
                        "USB-BLK-01",
                    },
                    {
                        new Guid("22222222-2222-2222-2222-222222222222"),
                        "{\"color\":\"black\",\"connector\":\"usb\",\"shape\":\"compact\"}",
                        new DateTimeOffset(
                            new DateTime(2026, 10, 11, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "seed-v1",
                        "adapter",
                        "MDL",
                        "Adaptor USB-C Travel",
                        99.50m,
                        0,
                        "Adaptor USB-C negru; USB 2.0 conform fișei seed. Date artificiale.",
                        "USB-BLK-02",
                    },
                    {
                        new Guid("33333333-3333-3333-3333-333333333333"),
                        "{\"color\":\"white\",\"shape\":\"rounded\"}",
                        new DateTimeOffset(
                            new DateTime(2026, 10, 11, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "seed-v1",
                        "mouse",
                        "MDL",
                        "Mouse alb Wireless",
                        249m,
                        4,
                        "Mouse alb wireless. Date artificiale.",
                        "MOUSE-WHT-01",
                    },
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CatalogProducts_Sku",
                schema: "app",
                table: "CatalogProducts",
                column: "Sku",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SearchId_RequestId",
                schema: "app",
                table: "ConversationMessages",
                columns: new[] { "SearchId", "RequestId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_TargetId_RequestId",
                schema: "app",
                table: "IngestionJobs",
                columns: new[] { "TargetId", "RequestId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_VisualSearches_ImageDocumentId",
                schema: "app",
                table: "VisualSearches",
                column: "ImageDocumentId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CatalogProducts", schema: "app");

            migrationBuilder.DropTable(name: "ConversationMessages", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "VisualSearches", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");
        }
    }
}

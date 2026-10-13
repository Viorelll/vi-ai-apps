using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModerateAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "app");

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(
                        type: "character varying(240)",
                        maxLength: 240,
                        nullable: false
                    ),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
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
                name: "ModerationItems",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ImageDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputHash = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    Stale = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationItems_Documents_ImageDocumentId",
                        column: x => x.ImageDocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    PipelineVersion = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id"
                    );
                    table.ForeignKey(
                        name: "FK_IngestionJobs_ModerationItems_TargetId",
                        column: x => x.TargetId,
                        principalSchema: "app",
                        principalTable: "ModerationItems",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PolicyDecisions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    ReasonsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DecisionHash = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyDecisions_ModerationItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "app",
                        principalTable: "ModerationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_ModerationItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "app",
                        principalTable: "ModerationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "SafetyAnalyses",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ApiVersion = table.Column<string>(type: "text", nullable: false),
                    OutputScale = table.Column<string>(type: "text", nullable: false),
                    RawObjectKey = table.Column<string>(type: "text", nullable: false),
                    Complete = table.Column<bool>(type: "boolean", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyAnalyses_ModerationItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "app",
                        principalTable: "ModerationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CategoryFindings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryFindings_SafetyAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalSchema: "app",
                        principalTable: "SafetyAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFindings_AnalysisId_Category",
                schema: "app",
                table: "CategoryFindings",
                columns: new[] { "AnalysisId", "Category" },
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
                name: "IX_IngestionJobs_DocumentId",
                schema: "app",
                table: "IngestionJobs",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_Status_NextAttemptAtUtc",
                schema: "app",
                table: "IngestionJobs",
                columns: new[] { "Status", "NextAttemptAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_TargetId",
                schema: "app",
                table: "IngestionJobs",
                column: "TargetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_ImageDocumentId",
                schema: "app",
                table: "ModerationItems",
                column: "ImageDocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_RequestId",
                schema: "app",
                table: "ModerationItems",
                column: "RequestId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PolicyDecisions_ItemId",
                schema: "app",
                table: "PolicyDecisions",
                column: "ItemId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ItemId_ExpectedRevision",
                schema: "app",
                table: "ReviewDecisions",
                columns: new[] { "ItemId", "ExpectedRevision" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_SafetyAnalyses_ItemId",
                schema: "app",
                table: "SafetyAnalyses",
                column: "ItemId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CategoryFindings", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "PolicyDecisions", schema: "app");

            migrationBuilder.DropTable(name: "ReviewDecisions", schema: "app");

            migrationBuilder.DropTable(name: "SafetyAnalyses", schema: "app");

            migrationBuilder.DropTable(name: "ModerationItems", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");
        }
    }
}

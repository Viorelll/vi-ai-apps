using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcureAI.Database.Migrations
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
                name: "Documents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ActiveGeneration = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRuns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brief = table.Column<string>(type: "text", nullable: false),
                    CriteriaJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    BudgetTokens = table.Column<long>(type: "bigint", nullable: false),
                    UsedTokens = table.Column<long>(type: "bigint", nullable: false),
                    Stale = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSteps",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    InputHash = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: false),
                    Deployment = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSteps_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    PipelineVersion = table.Column<string>(type: "text", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderOperation = table.Column<string>(type: "text", nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngestionJobs_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierLabel = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Net = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Shipping = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LeadDays = table.Column<int>(type: "integer", nullable: true),
                    MeetsRequirements = table.Column<bool>(type: "boolean", nullable: true),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offers_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotHash = table.Column<string>(type: "text", nullable: false),
                    ScoresJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recommendations_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunDocuments",
                schema: "app",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunDocuments", x => new { x.RunId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_RunDocuments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RunDocuments_ProcurementRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "app",
                        principalTable: "ProcurementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "app",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Citations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Citations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Citations_DocumentChunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "app",
                        principalTable: "DocumentChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Citations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Citations_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "app",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_RunId_Role_InputHash_PromptVersion",
                schema: "app",
                table: "AgentSteps",
                columns: new[] { "RunId", "Role", "InputHash", "PromptVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Citations_ChunkId",
                schema: "app",
                table: "Citations",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_Citations_DocumentId",
                schema: "app",
                table: "Citations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Citations_MessageId",
                schema: "app",
                table: "Citations",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RunId",
                schema: "app",
                table: "Conversations",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_Generation_Ordinal",
                schema: "app",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "Generation", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_DocumentId",
                schema: "app",
                table: "IngestionJobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_RunId",
                schema: "app",
                table: "IngestionJobs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_State_NextAttemptAtUtc",
                schema: "app",
                table: "IngestionJobs",
                columns: new[] { "State", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_DocumentId",
                schema: "app",
                table: "Offers",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_RunId",
                schema: "app",
                table: "Offers",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_RunId_SnapshotHash",
                schema: "app",
                table: "Recommendations",
                columns: new[] { "RunId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunDocuments_DocumentId",
                schema: "app",
                table: "RunDocuments",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSteps",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Citations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "IngestionJobs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Offers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Recommendations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RunDocuments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "DocumentChunks",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ProcurementRuns",
                schema: "app");
        }
    }
}

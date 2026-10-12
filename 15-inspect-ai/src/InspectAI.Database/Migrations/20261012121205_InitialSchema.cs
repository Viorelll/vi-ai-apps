using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectAI.Database.Migrations
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
                    ContentType = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Sha256 = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    ObjectKey = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    ErrorCode = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    ActiveGeneration = table.Column<int>(type: "integer", nullable: false),
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
                name: "Inspections",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentLabel = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    EquipmentModel = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    ManualVersion = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    RegionJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: true
                    ),
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
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    PipelineVersion = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    OperationUrl = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
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
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "InspectionReports",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    PromptVersion = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    ResultJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    SourcesJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Deployment = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionReports_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "app",
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "InspectionSources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionSources_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_InspectionSources_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "app",
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "QuestionMessages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    AnswerJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionMessages_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "app",
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "VisualFindings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Description = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Reason = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    RegionJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisualFindings_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_VisualFindings_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "app",
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    EvidenceJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "app",
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ChecklistItems_VisualFindings_FindingId",
                        column: x => x.FindingId,
                        principalSchema: "app",
                        principalTable: "VisualFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_FindingId",
                schema: "app",
                table: "ChecklistItems",
                column: "FindingId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_InspectionId",
                schema: "app",
                table: "ChecklistItems",
                column: "InspectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId_Generation_Ordinal",
                schema: "app",
                table: "DocumentChunks",
                columns: new[] { "DocumentId", "Generation", "Ordinal" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true,
                filter: "\"Status\" <> 'Deleting'"
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
                name: "IX_InspectionReports_InspectionId_InputHash_PromptVersion",
                schema: "app",
                table: "InspectionReports",
                columns: new[] { "InspectionId", "InputHash", "PromptVersion" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSources_DocumentId",
                schema: "app",
                table: "InspectionSources",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSources_InspectionId_DocumentId",
                schema: "app",
                table: "InspectionSources",
                columns: new[] { "InspectionId", "DocumentId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_QuestionMessages_InspectionId_RequestId",
                schema: "app",
                table: "QuestionMessages",
                columns: new[] { "InspectionId", "RequestId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_VisualFindings_DocumentId",
                schema: "app",
                table: "VisualFindings",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_VisualFindings_InspectionId",
                schema: "app",
                table: "VisualFindings",
                column: "InspectionId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChecklistItems", schema: "app");

            migrationBuilder.DropTable(name: "DocumentChunks", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "InspectionReports", schema: "app");

            migrationBuilder.DropTable(name: "InspectionSources", schema: "app");

            migrationBuilder.DropTable(name: "QuestionMessages", schema: "app");

            migrationBuilder.DropTable(name: "VisualFindings", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");

            migrationBuilder.DropTable(name: "Inspections", schema: "app");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractAI.Database.Migrations
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
                name: "ContractVersions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractKey = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    VersionLabel = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    ParentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Summary = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    UnknownsJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    InputHash = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    PromptVersion = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Deployment = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractVersions_ContractVersions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ContractVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
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
                name: "Comparisons",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeftVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RightVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineVersion = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    InputHash = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    ResultJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Deployment = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comparisons_ContractVersions_LeftVersionId",
                        column: x => x.LeftVersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Comparisons_ContractVersions_RightVersionId",
                        column: x => x.RightVersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_ContractVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Clauses",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Heading = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    Text = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SpanStart = table.Column<int>(type: "integer", nullable: false),
                    SpanLength = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clauses_ContractVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Clauses_DocumentChunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "app",
                        principalTable: "DocumentChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Answer = table.Column<string>(
                        type: "character varying(100000)",
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
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "app",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClauseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Severity = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    Explanation = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    EvidenceJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Clauses_ClauseId",
                        column: x => x.ClauseId,
                        principalSchema: "app",
                        principalTable: "Clauses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Findings_ContractVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Obligations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClauseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyText = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    ActionText = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RelativeTerm = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: true
                    ),
                    EvidenceJson = table.Column<string>(
                        type: "jsonb",
                        maxLength: 100000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Obligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Obligations_Clauses_ClauseId",
                        column: x => x.ClauseId,
                        principalSchema: "app",
                        principalTable: "Clauses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Obligations_ContractVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "app",
                        principalTable: "ContractVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Citations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Excerpt = table.Column<string>(
                        type: "character varying(100000)",
                        maxLength: 100000,
                        nullable: false
                    ),
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
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Citations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Citations_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "app",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Citations_ChunkId",
                schema: "app",
                table: "Citations",
                column: "ChunkId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Citations_DocumentId",
                schema: "app",
                table: "Citations",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Citations_MessageId",
                schema: "app",
                table: "Citations",
                column: "MessageId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Clauses_ChunkId",
                schema: "app",
                table: "Clauses",
                column: "ChunkId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Clauses_VersionId_Ordinal",
                schema: "app",
                table: "Clauses",
                columns: new[] { "VersionId", "Ordinal" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Comparisons_LeftVersionId_RightVersionId_PipelineVersion_In~",
                schema: "app",
                table: "Comparisons",
                columns: new[]
                {
                    "LeftVersionId",
                    "RightVersionId",
                    "PipelineVersion",
                    "InputHash",
                },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Comparisons_RightVersionId",
                schema: "app",
                table: "Comparisons",
                column: "RightVersionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ContractVersions_ContractKey_VersionLabel",
                schema: "app",
                table: "ContractVersions",
                columns: new[] { "ContractKey", "VersionLabel" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ContractVersions_DocumentId",
                schema: "app",
                table: "ContractVersions",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ContractVersions_ParentVersionId",
                schema: "app",
                table: "ContractVersions",
                column: "ParentVersionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_VersionId",
                schema: "app",
                table: "Conversations",
                column: "VersionId"
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
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ClauseId",
                schema: "app",
                table: "Findings",
                column: "ClauseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Findings_VersionId",
                schema: "app",
                table: "Findings",
                column: "VersionId"
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
                name: "IX_Messages_ConversationId_RequestId",
                schema: "app",
                table: "Messages",
                columns: new[] { "ConversationId", "RequestId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_ClauseId",
                schema: "app",
                table: "Obligations",
                column: "ClauseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_VersionId",
                schema: "app",
                table: "Obligations",
                column: "VersionId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Citations", schema: "app");

            migrationBuilder.DropTable(name: "Comparisons", schema: "app");

            migrationBuilder.DropTable(name: "Findings", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "Obligations", schema: "app");

            migrationBuilder.DropTable(name: "Messages", schema: "app");

            migrationBuilder.DropTable(name: "Clauses", schema: "app");

            migrationBuilder.DropTable(name: "Conversations", schema: "app");

            migrationBuilder.DropTable(name: "DocumentChunks", schema: "app");

            migrationBuilder.DropTable(name: "ContractVersions", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");
        }
    }
}

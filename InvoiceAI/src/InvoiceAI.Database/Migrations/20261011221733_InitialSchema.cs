using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceAI.Database.Migrations
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
                    FileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActiveGeneration = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
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
                    Text = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true)
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
                name: "IngestionJobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PipelineVersion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Phase = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderOperation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RawObjectKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    PollCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VendorName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Tax = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    SuggestedCategory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "app",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportSnapshots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportSnapshots_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "app",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedFields",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RawText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    CorrectedValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedFields_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "app",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "app",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationIssues",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationIssues_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "app",
                        principalTable: "Invoices",
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
                    Role = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Text = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
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
                    SourceId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Excerpt = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Citations", x => x.Id);
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
                name: "IX_Conversations_InvoiceId",
                schema: "app",
                table: "Conversations",
                column: "InvoiceId");

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
                name: "IX_ExportSnapshots_InvoiceId_Revision_SchemaVersion",
                schema: "app",
                table: "ExportSnapshots",
                columns: new[] { "InvoiceId", "Revision", "SchemaVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedFields_InvoiceId_Path",
                schema: "app",
                table: "ExtractedFields",
                columns: new[] { "InvoiceId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_DocumentId",
                schema: "app",
                table: "IngestionJobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_Status_NextAttemptAtUtc",
                schema: "app",
                table: "IngestionJobs",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionJobs_TargetId_PipelineVersion_Kind",
                schema: "app",
                table: "IngestionJobs",
                columns: new[] { "TargetId", "PipelineVersion", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_Ordinal",
                schema: "app",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DocumentId",
                schema: "app",
                table: "Invoices",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationIssues_InvoiceId",
                schema: "app",
                table: "ValidationIssues",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Citations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "DocumentChunks",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ExportSnapshots",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ExtractedFields",
                schema: "app");

            migrationBuilder.DropTable(
                name: "IngestionJobs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "InvoiceLines",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ValidationIssues",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "app");
        }
    }
}

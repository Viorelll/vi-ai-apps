using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildBudgetAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "app");

            migrationBuilder.CreateTable(
                name: "BuildPlans",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Currency = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPlans", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    ContentType = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Sha256 = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    ObjectKey = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    ActiveGeneration = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: true
                    ),
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
                name: "BudgetSnapshots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    InputsHash = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    LinesJson = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Total = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                    Currency = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Incomplete = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_BuildPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "BuildPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_BuildPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "BuildPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MaterialNeeds",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Unit = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Quantity = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                    WastePercent = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialNeeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialNeeds_BuildPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "BuildPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
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
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
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
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    PipelineVersion = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
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
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    OperationUrl = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: true
                    ),
                    ErrorCode = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
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
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MaterialOffers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierLabel = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Currency = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    TaxBasis = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialOffers_BuildPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "BuildPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_MaterialOffers_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PlanSources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanSources_BuildPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "app",
                        principalTable: "BuildPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PlanSources_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
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
                    Role = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Text = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Deployment = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    PromptVersion = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
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
                name: "OfferLines",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialLabel = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Unit = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    PackQuantity = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: true
                    ),
                    Price = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                    Transport = table.Column<decimal>(
                        type: "numeric(18,6)",
                        precision: 18,
                        scale: 6,
                        nullable: true
                    ),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceJson = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferLines_MaterialOffers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "app",
                        principalTable: "MaterialOffers",
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
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    Excerpt = table.Column<string>(
                        type: "character varying(200000)",
                        maxLength: 200000,
                        nullable: false
                    ),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
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
                name: "IX_BudgetSnapshots_PlanId_InputsHash",
                schema: "app",
                table: "BudgetSnapshots",
                columns: new[] { "PlanId", "InputsHash" },
                unique: true
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
                name: "IX_Conversations_PlanId",
                schema: "app",
                table: "Conversations",
                column: "PlanId"
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
                name: "IX_IngestionJobs_DocumentId",
                schema: "app",
                table: "IngestionJobs",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MaterialNeeds_PlanId",
                schema: "app",
                table: "MaterialNeeds",
                column: "PlanId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MaterialOffers_DocumentId",
                schema: "app",
                table: "MaterialOffers",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MaterialOffers_PlanId",
                schema: "app",
                table: "MaterialOffers",
                column: "PlanId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages",
                column: "ConversationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfferLines_OfferId",
                schema: "app",
                table: "OfferLines",
                column: "OfferId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlanSources_DocumentId",
                schema: "app",
                table: "PlanSources",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlanSources_PlanId_DocumentId",
                schema: "app",
                table: "PlanSources",
                columns: new[] { "PlanId", "DocumentId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BudgetSnapshots", schema: "app");

            migrationBuilder.DropTable(name: "Citations", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "MaterialNeeds", schema: "app");

            migrationBuilder.DropTable(name: "OfferLines", schema: "app");

            migrationBuilder.DropTable(name: "PlanSources", schema: "app");

            migrationBuilder.DropTable(name: "DocumentChunks", schema: "app");

            migrationBuilder.DropTable(name: "Messages", schema: "app");

            migrationBuilder.DropTable(name: "MaterialOffers", schema: "app");

            migrationBuilder.DropTable(name: "Conversations", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");

            migrationBuilder.DropTable(name: "BuildPlans", schema: "app");
        }
    }
}

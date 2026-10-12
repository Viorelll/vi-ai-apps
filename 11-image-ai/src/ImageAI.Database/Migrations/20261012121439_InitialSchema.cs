using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageAI.Database.Migrations
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
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    HasAlpha = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "ImageProjects",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brief = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageProjects", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "GenerationJobs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
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
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationJobs", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "GenerationSnapshots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Deployment = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    UsageJson = table.Column<string>(type: "jsonb", nullable: true),
                    SafetyStatus = table.Column<string>(type: "text", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "text", nullable: true),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationSnapshots", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageAssets",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageAssets_ImageAssets_ParentAssetId",
                        column: x => x.ParentAssetId,
                        principalSchema: "app",
                        principalTable: "ImageAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ImageAssets_ImageProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "app",
                        principalTable: "ImageProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageRuns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    InputAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaskDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageRuns_Documents_InputDocumentId",
                        column: x => x.InputDocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ImageRuns_Documents_MaskDocumentId",
                        column: x => x.MaskDocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ImageRuns_ImageAssets_InputAssetId",
                        column: x => x.InputAssetId,
                        principalSchema: "app",
                        principalTable: "ImageAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ImageRuns_ImageProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "app",
                        principalTable: "ImageProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_RunId",
                schema: "app",
                table: "GenerationJobs",
                column: "RunId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_GenerationSnapshots_RunId",
                schema: "app",
                table: "GenerationSnapshots",
                column: "RunId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_ParentAssetId",
                schema: "app",
                table: "ImageAssets",
                column: "ParentAssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_ProjectId",
                schema: "app",
                table: "ImageAssets",
                column: "ProjectId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_RunId",
                schema: "app",
                table: "ImageAssets",
                column: "RunId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageRuns_InputAssetId",
                schema: "app",
                table: "ImageRuns",
                column: "InputAssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageRuns_InputDocumentId",
                schema: "app",
                table: "ImageRuns",
                column: "InputDocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageRuns_MaskDocumentId",
                schema: "app",
                table: "ImageRuns",
                column: "MaskDocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageRuns_ProjectId",
                schema: "app",
                table: "ImageRuns",
                column: "ProjectId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageRuns_RequestId",
                schema: "app",
                table: "ImageRuns",
                column: "RequestId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GenerationJobs_ImageRuns_RunId",
                schema: "app",
                table: "GenerationJobs",
                column: "RunId",
                principalSchema: "app",
                principalTable: "ImageRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GenerationSnapshots_ImageRuns_RunId",
                schema: "app",
                table: "GenerationSnapshots",
                column: "RunId",
                principalSchema: "app",
                principalTable: "ImageRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ImageAssets_ImageRuns_RunId",
                schema: "app",
                table: "ImageAssets",
                column: "RunId",
                principalSchema: "app",
                principalTable: "ImageRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageAssets_ImageRuns_RunId",
                schema: "app",
                table: "ImageAssets"
            );

            migrationBuilder.DropTable(name: "GenerationJobs", schema: "app");

            migrationBuilder.DropTable(name: "GenerationSnapshots", schema: "app");

            migrationBuilder.DropTable(name: "ImageRuns", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");

            migrationBuilder.DropTable(name: "ImageAssets", schema: "app");

            migrationBuilder.DropTable(name: "ImageProjects", schema: "app");
        }
    }
}

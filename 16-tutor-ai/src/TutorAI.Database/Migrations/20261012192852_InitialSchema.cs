using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorAI.Database.Migrations
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
                    PhotoObjectKey = table.Column<string>(type: "text", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
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
                name: "TutorSessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ExplanationJson = table.Column<string>(type: "jsonb", nullable: true),
                    CurrentExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorSessions", x => x.Id);
                    table.CheckConstraint("CK_Difficulty", "\"Difficulty\" BETWEEN 1 AND 3");
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
                    Text = table.Column<string>(type: "text", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    RegionJson = table.Column<string>(type: "text", nullable: true),
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
                    OperationUrl = table.Column<string>(type: "text", nullable: true),
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
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LearningSources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OfficialUrl = table.Column<string>(type: "text", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CheckedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSources_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "app",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_TutorSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "TutorSessions",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Exercises",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RubricJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercises_TutorSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "TutorSessions",
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
                    Role = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Deployment = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    InputHash = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
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
                name: "Attempts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(
                        type: "numeric(5,4)",
                        precision: 5,
                        scale: 4,
                        nullable: true
                    ),
                    FeedbackJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.CheckConstraint(
                        "CK_Score",
                        "\"Score\" IS NULL OR (\"Score\" BETWEEN 0 AND 1)"
                    );
                    table.ForeignKey(
                        name: "FK_Attempts_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalSchema: "app",
                        principalTable: "Exercises",
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
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficialUrl = table.Column<string>(type: "text", nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Generation = table.Column<int>(type: "integer", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "AdaptationSteps",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousLevel = table.Column<int>(type: "integer", nullable: false),
                    NextLevel = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdaptationSteps_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "app",
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AdaptationSteps_TutorSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "TutorSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AdaptationSteps_AttemptId",
                schema: "app",
                table: "AdaptationSteps",
                column: "AttemptId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_AdaptationSteps_SessionId",
                schema: "app",
                table: "AdaptationSteps",
                column: "SessionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_ExerciseId",
                schema: "app",
                table: "Attempts",
                column: "ExerciseId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_ExerciseId_RequestId",
                schema: "app",
                table: "Attempts",
                columns: new[] { "ExerciseId", "RequestId" },
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
                name: "IX_Conversations_SessionId",
                schema: "app",
                table: "Conversations",
                column: "SessionId",
                unique: true
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
                name: "IX_Exercises_SessionId",
                schema: "app",
                table: "Exercises",
                column: "SessionId"
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
                name: "IX_LearningSources_DocumentId",
                schema: "app",
                table: "LearningSources",
                column: "DocumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages",
                column: "ConversationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Messages_OperationId",
                schema: "app",
                table: "Messages",
                column: "OperationId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AdaptationSteps", schema: "app");

            migrationBuilder.DropTable(name: "Citations", schema: "app");

            migrationBuilder.DropTable(name: "IngestionJobs", schema: "app");

            migrationBuilder.DropTable(name: "LearningSources", schema: "app");

            migrationBuilder.DropTable(name: "Attempts", schema: "app");

            migrationBuilder.DropTable(name: "DocumentChunks", schema: "app");

            migrationBuilder.DropTable(name: "Messages", schema: "app");

            migrationBuilder.DropTable(name: "Exercises", schema: "app");

            migrationBuilder.DropTable(name: "Documents", schema: "app");

            migrationBuilder.DropTable(name: "Conversations", schema: "app");

            migrationBuilder.DropTable(name: "TutorSessions", schema: "app");
        }
    }
}

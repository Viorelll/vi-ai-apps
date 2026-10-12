using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "incidents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Service = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "diagnosis_runs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    Hypotheses = table.Column<string>(type: "jsonb", nullable: false),
                    NextChecks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnosis_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnosis_runs_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "app",
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "log_events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Severity = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: true),
                    MessageRedacted = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_events_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "app",
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "log_sources",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_log_sources_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "app",
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_snapshots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeployedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_release_snapshots_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "app",
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_diagnosis_runs_IncidentId",
                schema: "app",
                table: "diagnosis_runs",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_Service_FromUtc_ToUtc",
                schema: "app",
                table: "incidents",
                columns: new[] { "Service", "FromUtc", "ToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_log_events_IncidentId_TimestampUtc_TraceId",
                schema: "app",
                table: "log_events",
                columns: new[] { "IncidentId", "TimestampUtc", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_log_sources_IncidentId",
                schema: "app",
                table: "log_sources",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_log_sources_Sha256",
                schema: "app",
                table: "log_sources",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_release_snapshots_IncidentId",
                schema: "app",
                table: "release_snapshots",
                column: "IncidentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnosis_runs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "log_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "log_sources",
                schema: "app");

            migrationBuilder.DropTable(
                name: "release_snapshots",
                schema: "app");

            migrationBuilder.DropTable(
                name: "incidents",
                schema: "app");
        }
    }
}

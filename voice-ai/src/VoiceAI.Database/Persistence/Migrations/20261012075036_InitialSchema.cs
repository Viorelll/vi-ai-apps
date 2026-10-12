using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceAI.Database.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "app");

            migrationBuilder.CreateTable(
                name: "availability_slots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ResourceLabel = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_slots", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "voice_sessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_sessions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "booking_proposals",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotRevision = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_proposals_availability_slots_SlotId",
                        column: x => x.SlotId,
                        principalSchema: "app",
                        principalTable: "availability_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_booking_proposals_voice_sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "voice_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "voice_turns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Transcript = table.Column<string>(type: "text", nullable: false),
                    Reply = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_turns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voice_turns_voice_sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "voice_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "local_bookings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_local_bookings_availability_slots_SlotId",
                        column: x => x.SlotId,
                        principalSchema: "app",
                        principalTable: "availability_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_local_bookings_booking_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "app",
                        principalTable: "booking_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_availability_slots_StartUtc",
                schema: "app",
                table: "availability_slots",
                column: "StartUtc"
            );

            migrationBuilder.CreateIndex(
                name: "IX_booking_proposals_SessionId",
                schema: "app",
                table: "booking_proposals",
                column: "SessionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_booking_proposals_SlotId",
                schema: "app",
                table: "booking_proposals",
                column: "SlotId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_local_bookings_IdempotencyKey",
                schema: "app",
                table: "local_bookings",
                column: "IdempotencyKey",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_local_bookings_ProposalId",
                schema: "app",
                table: "local_bookings",
                column: "ProposalId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_local_bookings_SlotId",
                schema: "app",
                table: "local_bookings",
                column: "SlotId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_voice_turns_SessionId_RequestId",
                schema: "app",
                table: "voice_turns",
                columns: new[] { "SessionId", "RequestId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "local_bookings", schema: "app");

            migrationBuilder.DropTable(name: "voice_turns", schema: "app");

            migrationBuilder.DropTable(name: "booking_proposals", schema: "app");

            migrationBuilder.DropTable(name: "availability_slots", schema: "app");

            migrationBuilder.DropTable(name: "voice_sessions", schema: "app");
        }
    }
}

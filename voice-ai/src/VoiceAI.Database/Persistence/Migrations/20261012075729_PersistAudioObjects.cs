using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceAI.Database.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistAudioObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputAudioKey",
                schema: "app",
                table: "voice_turns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "OutputAudioKey",
                schema: "app",
                table: "voice_turns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "VoiceTurnId",
                schema: "app",
                table: "booking_proposals",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_booking_proposals_VoiceTurnId",
                schema: "app",
                table: "booking_proposals",
                column: "VoiceTurnId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_booking_proposals_voice_turns_VoiceTurnId",
                schema: "app",
                table: "booking_proposals",
                column: "VoiceTurnId",
                principalSchema: "app",
                principalTable: "voice_turns",
                principalColumn: "Id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_booking_proposals_voice_turns_VoiceTurnId",
                schema: "app",
                table: "booking_proposals"
            );

            migrationBuilder.DropIndex(
                name: "IX_booking_proposals_VoiceTurnId",
                schema: "app",
                table: "booking_proposals"
            );

            migrationBuilder.DropColumn(name: "InputAudioKey", schema: "app", table: "voice_turns");

            migrationBuilder.DropColumn(
                name: "OutputAudioKey",
                schema: "app",
                table: "voice_turns"
            );

            migrationBuilder.DropColumn(
                name: "VoiceTurnId",
                schema: "app",
                table: "booking_proposals"
            );
        }
    }
}

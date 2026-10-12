using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class ActiveHashAndQuestionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_RequestId",
                schema: "app",
                table: "Messages"
            );

            migrationBuilder.DropIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "VersionId",
                schema: "app",
                table: "Messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.Sql(
                """
                UPDATE app."Messages" m SET "VersionId" = c."VersionId"
                FROM app."Conversations" c WHERE m."ConversationId" = c."Id";
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages",
                column: "ConversationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Messages_VersionId_RequestId",
                schema: "app",
                table: "Messages",
                columns: new[] { "VersionId", "RequestId" },
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

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ContractVersions_VersionId",
                schema: "app",
                table: "Messages",
                column: "VersionId",
                principalSchema: "app",
                principalTable: "ContractVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ContractVersions_VersionId",
                schema: "app",
                table: "Messages"
            );

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId",
                schema: "app",
                table: "Messages"
            );

            migrationBuilder.DropIndex(
                name: "IX_Messages_VersionId_RequestId",
                schema: "app",
                table: "Messages"
            );

            migrationBuilder.DropIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents"
            );

            migrationBuilder.DropColumn(name: "VersionId", schema: "app", table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_RequestId",
                schema: "app",
                table: "Messages",
                columns: new[] { "ConversationId", "RequestId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                schema: "app",
                table: "Documents",
                column: "Sha256",
                unique: true
            );
        }
    }
}

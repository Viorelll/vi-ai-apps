using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAI.Database.Persistence.Migrations
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
                name: "AnalysisRuns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SpecJson = table.Column<string>(type: "text", nullable: false),
                    RowsJson = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    ResultHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DatasetRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Datasets",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesRows",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoldOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesRows_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalSchema: "app",
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_DatasetId_CreatedAtUtc",
                schema: "app",
                table: "AnalysisRuns",
                columns: new[] { "DatasetId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRows_DatasetId_SoldOn",
                schema: "app",
                table: "SalesRows",
                columns: new[] { "DatasetId", "SoldOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisRuns",
                schema: "app");

            migrationBuilder.DropTable(
                name: "SalesRows",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Datasets",
                schema: "app");
        }
    }
}

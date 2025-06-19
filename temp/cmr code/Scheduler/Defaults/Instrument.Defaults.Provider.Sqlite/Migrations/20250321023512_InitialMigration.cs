using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Instrument.Defaults.Provider.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sequence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sequence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestMethod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TechnologyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestMethod", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestMethodSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TestMethodId = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestMethodSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Unit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unit", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Sequence",
                columns: new[] { "Id", "Created", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 3, 21, 2, 35, 12, 42, DateTimeKind.Utc).AddTicks(7106), "Stop Station Home", true, "SS_Home" },
                    { 2, new DateTime(2025, 3, 21, 2, 35, 12, 42, DateTimeKind.Utc).AddTicks(7108), "Reaction Wheel Home", true, "ERW_Home" }
                });

            migrationBuilder.InsertData(
                table: "Technology",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "ImmunoCap" },
                    { 2, "Elia" },
                    { 3, "ImmunoCapViewAllergy" },
                    { 4, "EliaDualWash" }
                });

            migrationBuilder.InsertData(
                table: "TestMethod",
                columns: new[] { "Id", "Name", "TechnologyId" },
                values: new object[] { 1, "sIgE", 1 });

            migrationBuilder.InsertData(
                table: "TestMethodSequences",
                columns: new[] { "Id", "IsActive", "SequenceId", "TestMethodId" },
                values: new object[,]
                {
                    { 1, true, 1, 1 },
                    { 2, true, 2, 1 }
                });

            migrationBuilder.InsertData(
                table: "Unit",
                columns: new[] { "Id", "Abbreviation", "Description", "Name" },
                values: new object[] { 1, "mm", "Millimeter", "Millimeter" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sequence");

            migrationBuilder.DropTable(
                name: "Technology");

            migrationBuilder.DropTable(
                name: "TestMethod");

            migrationBuilder.DropTable(
                name: "TestMethodSequences");

            migrationBuilder.DropTable(
                name: "Unit");
        }
    }
}

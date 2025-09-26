using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWA_tinder_for_pets.Migrations
{
    /// <inheritdoc />
    public partial class GoogleGeminiGenImageWIP2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageDataMineCaches");

            migrationBuilder.CreateTable(
                name: "ImageDataMimeCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDataMimeCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageDataMimeCaches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageDataMimeCaches_UserId",
                table: "ImageDataMimeCaches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageDataMimeCaches");

            migrationBuilder.CreateTable(
                name: "ImageDataMineCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDataMineCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageDataMineCaches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageDataMineCaches_UserId",
                table: "ImageDataMineCaches",
                column: "UserId");
        }
    }
}

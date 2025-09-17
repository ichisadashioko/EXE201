using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWA_tinder_for_pets.Migrations
{
    /// <inheritdoc />
    public partial class ChatSchema2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserAId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserBId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatThreads_Users_UserAId",
                        column: x => x.UserAId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatThreads_Users_UserBId",
                        column: x => x.UserBId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatThreadId",
                table: "ChatMessages",
                column: "ChatThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserAId",
                table: "ChatThreads",
                column: "UserAId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserBId",
                table: "ChatThreads",
                column: "UserBId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatThreads_ChatThreadId",
                table: "ChatMessages",
                column: "ChatThreadId",
                principalTable: "ChatThreads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatThreads_ChatThreadId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ChatThreadId",
                table: "ChatMessages");
        }
    }
}

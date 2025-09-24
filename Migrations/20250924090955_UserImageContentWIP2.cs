using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWA_tinder_for_pets.Migrations
{
    /// <inheritdoc />
    public partial class UserImageContentWIP2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraInfoJson",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileUserImageId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfileUserImageId",
                table: "Users",
                column: "ProfileUserImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users",
                column: "ProfileUserImageId",
                principalTable: "UserImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ProfileUserImageId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExtraInfoJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileUserImageId",
                table: "Users");
        }
    }
}

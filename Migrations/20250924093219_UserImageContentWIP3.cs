using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWA_tinder_for_pets.Migrations
{
    /// <inheritdoc />
    public partial class UserImageContentWIP3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users",
                column: "ProfileUserImageId",
                principalTable: "UserImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_UserImages_ProfileUserImageId",
                table: "Users",
                column: "ProfileUserImageId",
                principalTable: "UserImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

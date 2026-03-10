using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetLearning.Migrations
{
    /// <inheritdoc />
    public partial class addedOnBoardingComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboardingComplete",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnboardingComplete",
                table: "AspNetUsers");
        }
    }
}

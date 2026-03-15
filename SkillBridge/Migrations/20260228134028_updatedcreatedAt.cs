using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkillBridge.Migrations
{
    /// <inheritdoc />
    public partial class updatedcreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the FK that is holding the index first
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Skills_SkillId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Skills_SkillId",
                table: "Skills");

            // Re-add the FK — SQL Server will now use PK_Skills to enforce it
            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Skills_SkillId",
                table: "Bookings",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "SkillId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Skills",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher1" });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher1" });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher2" });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher3" });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher3" });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, "teacher4" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Skills",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Skills",
                keyColumn: "SkillId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "TeacherId" },
                values: new object[] { null, null });

            // Drop the FK before recreating the index it depends on
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Skills_SkillId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_SkillId",
                table: "Skills",
                column: "SkillId",
                unique: true);

            // Re-add FK so it uses IX_Skills_SkillId again
            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Skills_SkillId",
                table: "Bookings",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "SkillId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

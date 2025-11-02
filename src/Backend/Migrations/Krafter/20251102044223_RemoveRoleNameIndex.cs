using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations.Krafter
{
    /// <inheritdoc />
    public partial class RemoveRoleNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "BrightLightRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "BrightLightRole",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");
        }
    }
}

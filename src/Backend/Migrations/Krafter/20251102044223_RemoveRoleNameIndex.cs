using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations.Krafter;

/// <inheritdoc />
public partial class RemoveRoleNameIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            "RoleNameIndex",
            "BrightLightRole");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            "RoleNameIndex",
            "BrightLightRole",
            "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");
    }
}

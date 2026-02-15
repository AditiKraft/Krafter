using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations.BackgroundJobs;

/// <inheritdoc />
public partial class NewChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_TimeTickers_TimeTickers_BatchParent",
            schema: "ticker",
            table: "TimeTickers");

        migrationBuilder.AddForeignKey(
            "FK_TimeTickers_TimeTickers_BatchParent",
            schema: "ticker",
            table: "TimeTickers",
            column: "BatchParent",
            principalSchema: "ticker",
            principalTable: "TimeTickers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_TimeTickers_TimeTickers_BatchParent",
            schema: "ticker",
            table: "TimeTickers");

        migrationBuilder.AddForeignKey(
            "FK_TimeTickers_TimeTickers_BatchParent",
            schema: "ticker",
            table: "TimeTickers",
            column: "BatchParent",
            principalSchema: "ticker",
            principalTable: "TimeTickers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}

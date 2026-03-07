using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations
{
    /// <inheritdoc />
    public partial class MakeTenantIdentifierRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill any legacy rows before the NOT NULL constraint is enforced.
            migrationBuilder.Sql("""
                                 UPDATE "Tenant"
                                 SET "Identifier" = "Id"
                                 WHERE "Identifier" IS NULL OR btrim("Identifier") = '';
                                 """);

            migrationBuilder.AlterColumn<string>(
                name: "Identifier",
                table: "Tenant",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Identifier",
                table: "Tenant",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

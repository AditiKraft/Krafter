using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations.TenantDb;

public partial class UniqueTenantIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL expression indexes are not represented by EF property indexes.
        // Existing duplicate nondeleted identifiers must be resolved before applying this migration.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_Tenant_Identifier_Lower"
            ON "Tenant" (lower("Identifier"))
            WHERE NOT "IsDeleted";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX "IX_Tenant_Identifier_Lower";""");
    }
}

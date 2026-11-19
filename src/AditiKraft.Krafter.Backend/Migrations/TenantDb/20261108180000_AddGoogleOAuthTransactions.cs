using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations.TenantDb;

public partial class AddGoogleOAuthTransactions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GoogleLoginTransactions",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                BrowserProofHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProtectedPayload = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GoogleLoginTransactions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_GoogleLoginTransactions_ExpiresAt",
            table: "GoogleLoginTransactions", column: "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "GoogleLoginTransactions");
}

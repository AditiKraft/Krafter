using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthenticationVersion",
                table: "ApplicationUser",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TwoFactorChallenges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PrimaryMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AuthenticationVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ProtectedKey = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    ProtectedKey = table.Column<string>(type: "text", nullable: true),
                    LastAcceptedStep = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_TwoFactorCredentials_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorRecoveryCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_ExpiresAt",
                table: "TwoFactorChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_TenantId_UserId",
                table: "TwoFactorChallenges",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRecoveryCodes_TenantId_UserId",
                table: "TwoFactorRecoveryCodes",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwoFactorChallenges");

            migrationBuilder.DropTable(
                name: "TwoFactorCredentials");

            migrationBuilder.DropTable(
                name: "TwoFactorRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "AuthenticationVersion",
                table: "ApplicationUser");
        }
    }
}

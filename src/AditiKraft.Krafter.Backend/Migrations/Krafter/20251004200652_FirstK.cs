using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations.Krafter;

/// <inheritdoc />
public partial class FirstK : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "KrafterUser",
            table => new
            {
                Id = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                IsDeleted = table.Column<bool>("boolean", nullable: false),
                CreatedOn =
                    table.Column<DateTime>("timestamp with time zone", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedById = table.Column<string>("character varying(36)", maxLength: 36, nullable: true),
                UpdatedOn = table.Column<DateTime>("timestamp with time zone", nullable: true),
                UpdatedById = table.Column<string>("character varying(36)", nullable: true),
                Name = table.Column<string>("text", nullable: true),
                FirstName = table.Column<string>("text", nullable: true),
                LastName = table.Column<string>("text", nullable: true),
                IsActive = table.Column<bool>("boolean", nullable: false),
                DeleteReason = table.Column<string>("text", nullable: true),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                IsOwner = table.Column<bool>("boolean", nullable: false),
                UserName = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>("boolean", nullable: false),
                PasswordHash = table.Column<string>("text", nullable: true),
                SecurityStamp = table.Column<string>("text", nullable: true),
                ConcurrencyStamp = table.Column<string>("text", nullable: true),
                PhoneNumber = table.Column<string>("text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>("boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>("boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>("boolean", nullable: false),
                AccessFailedCount = table.Column<int>("integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterUser", x => x.Id);
                table.ForeignKey(
                    "FK_KrafterUser_KrafterUser_CreatedById",
                    x => x.CreatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUser_KrafterUser_UpdatedById",
                    x => x.UpdatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "UserRefreshTokens",
            table => new
            {
                UserId = table.Column<string>("text", nullable: false),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                RefreshToken = table.Column<string>("text", nullable: true),
                RefreshTokenExpiryTime = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_UserRefreshTokens", x => x.UserId); });

        migrationBuilder.CreateTable(
            "KrafterRole",
            table => new
            {
                Id = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                CreatedOn =
                    table.Column<DateTime>("timestamp with time zone", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedById = table.Column<string>("character varying(36)", maxLength: 36, nullable: true),
                UpdatedOn = table.Column<DateTime>("timestamp with time zone", nullable: true),
                UpdatedById = table.Column<string>("character varying(36)", nullable: true),
                Description = table.Column<string>("text", nullable: true),
                IsDeleted = table.Column<bool>("boolean", nullable: false),
                DeleteReason = table.Column<string>("text", nullable: true),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                Name = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterRole", x => x.Id);
                table.ForeignKey(
                    "FK_KrafterRole_KrafterUser_CreatedById",
                    x => x.CreatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterRole_KrafterUser_UpdatedById",
                    x => x.UpdatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "KrafterUserClaim",
            table => new
            {
                Id = table.Column<int>("integer", maxLength: 36, nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedOn =
                    table.Column<DateTime>("timestamp with time zone", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedById = table.Column<string>("character varying(36)", maxLength: 36, nullable: true),
                UpdatedOn = table.Column<DateTime>("timestamp with time zone", nullable: true),
                UpdatedById = table.Column<string>("character varying(36)", nullable: true),
                IsDeleted = table.Column<bool>("boolean", nullable: false),
                DeleteReason = table.Column<string>("text", nullable: true),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                UserId = table.Column<string>("character varying(36)", nullable: false),
                ClaimType = table.Column<string>("text", nullable: true),
                ClaimValue = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterUserClaim", x => x.Id);
                table.ForeignKey(
                    "FK_KrafterUserClaim_KrafterUser_CreatedById",
                    x => x.CreatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUserClaim_KrafterUser_UpdatedById",
                    x => x.UpdatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUserClaim_KrafterUser_UserId",
                    x => x.UserId,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "KrafterUserLogins",
            table => new
            {
                LoginProvider = table.Column<string>("text", nullable: false),
                ProviderKey = table.Column<string>("text", nullable: false),
                ProviderDisplayName = table.Column<string>("text", nullable: true),
                UserId = table.Column<string>("character varying(36)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    "FK_KrafterUserLogins_KrafterUser_UserId",
                    x => x.UserId,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "KrafterUserTokens",
            table => new
            {
                UserId = table.Column<string>("character varying(36)", nullable: false),
                LoginProvider = table.Column<string>("text", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Value = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    "FK_KrafterUserTokens_KrafterUser_UserId",
                    x => x.UserId,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "KrafterRoleClaim",
            table => new
            {
                Id = table.Column<int>("integer", maxLength: 36, nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedOn =
                    table.Column<DateTime>("timestamp with time zone", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedById = table.Column<string>("character varying(36)", maxLength: 36, nullable: true),
                UpdatedOn = table.Column<DateTime>("timestamp with time zone", nullable: true),
                UpdatedById = table.Column<string>("character varying(36)", nullable: true),
                IsDeleted = table.Column<bool>("boolean", nullable: false),
                DeleteReason = table.Column<string>("text", nullable: true),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false),
                RoleId = table.Column<string>("character varying(36)", nullable: false),
                ClaimType = table.Column<string>("text", nullable: true),
                ClaimValue = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterRoleClaim", x => x.Id);
                table.ForeignKey(
                    "FK_KrafterRoleClaim_KrafterRole_RoleId",
                    x => x.RoleId,
                    "KrafterRole",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_KrafterRoleClaim_KrafterUser_CreatedById",
                    x => x.CreatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterRoleClaim_KrafterUser_UpdatedById",
                    x => x.UpdatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "KrafterUserRole",
            table => new
            {
                UserId = table.Column<string>("character varying(36)", nullable: false),
                RoleId = table.Column<string>("character varying(36)", nullable: false),
                CreatedOn =
                    table.Column<DateTime>("timestamp with time zone", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                CreatedById = table.Column<string>("character varying(36)", maxLength: 36, nullable: true),
                UpdatedOn = table.Column<DateTime>("timestamp with time zone", nullable: true),
                UpdatedById = table.Column<string>("character varying(36)", nullable: true),
                IsDeleted = table.Column<bool>("boolean", nullable: false),
                DeleteReason = table.Column<string>("text", nullable: true),
                TenantId = table.Column<string>("character varying(36)", maxLength: 36, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KrafterUserRole", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    "FK_KrafterUserRole_KrafterRole_RoleId",
                    x => x.RoleId,
                    "KrafterRole",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUserRole_KrafterUser_CreatedById",
                    x => x.CreatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUserRole_KrafterUser_UpdatedById",
                    x => x.UpdatedById,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_KrafterUserRole_KrafterUser_UserId",
                    x => x.UserId,
                    "KrafterUser",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_KrafterRole_CreatedById",
            "KrafterRole",
            "CreatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterRole_NormalizedName_TenantId",
            "KrafterRole",
            new[] { "NormalizedName", "TenantId" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_KrafterRole_UpdatedById",
            "KrafterRole",
            "UpdatedById");

        migrationBuilder.CreateIndex(
            "RoleNameIndex",
            "KrafterRole",
            "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_KrafterRoleClaim_CreatedById",
            "KrafterRoleClaim",
            "CreatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterRoleClaim_RoleId",
            "KrafterRoleClaim",
            "RoleId");

        migrationBuilder.CreateIndex(
            "IX_KrafterRoleClaim_UpdatedById",
            "KrafterRoleClaim",
            "UpdatedById");

        migrationBuilder.CreateIndex(
            "EmailIndex",
            "KrafterUser",
            "NormalizedEmail");

        migrationBuilder.CreateIndex(
            "IX_KrafterUser_CreatedById",
            "KrafterUser",
            "CreatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterUser_NormalizedEmail_TenantId",
            "KrafterUser",
            new[] { "NormalizedEmail", "TenantId" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_KrafterUser_NormalizedUserName_TenantId",
            "KrafterUser",
            new[] { "NormalizedUserName", "TenantId" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_KrafterUser_UpdatedById",
            "KrafterUser",
            "UpdatedById");

        migrationBuilder.CreateIndex(
            "UserNameIndex",
            "KrafterUser",
            "NormalizedUserName");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserClaim_CreatedById",
            "KrafterUserClaim",
            "CreatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserClaim_UpdatedById",
            "KrafterUserClaim",
            "UpdatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserClaim_UserId",
            "KrafterUserClaim",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserLogins_UserId",
            "KrafterUserLogins",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserRole_CreatedById",
            "KrafterUserRole",
            "CreatedById");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserRole_RoleId",
            "KrafterUserRole",
            "RoleId");

        migrationBuilder.CreateIndex(
            "IX_KrafterUserRole_UpdatedById",
            "KrafterUserRole",
            "UpdatedById");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "KrafterRoleClaim");

        migrationBuilder.DropTable(
            "KrafterUserClaim");

        migrationBuilder.DropTable(
            "KrafterUserLogins");

        migrationBuilder.DropTable(
            "KrafterUserRole");

        migrationBuilder.DropTable(
            "KrafterUserTokens");

        migrationBuilder.DropTable(
            "UserRefreshTokens");

        migrationBuilder.DropTable(
            "KrafterRole");

        migrationBuilder.DropTable(
            "KrafterUser");
    }
}

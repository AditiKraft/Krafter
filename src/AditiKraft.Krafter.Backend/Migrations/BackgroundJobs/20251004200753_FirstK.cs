using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AditiKraft.Krafter.Backend.Migrations.BackgroundJobs;

/// <inheritdoc />
public partial class FirstK : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            "ticker");

        migrationBuilder.CreateTable(
            "CronTickers",
            schema: "ticker",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Expression = table.Column<string>("text", nullable: true),
                Request = table.Column<byte[]>("bytea", nullable: true),
                Retries = table.Column<int>("integer", nullable: false),
                RetryIntervals = table.Column<int[]>("integer[]", nullable: true),
                Function = table.Column<string>("text", nullable: true),
                Description = table.Column<string>("text", nullable: true),
                InitIdentifier = table.Column<string>("text", nullable: true),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_CronTickers", x => x.Id); });

        migrationBuilder.CreateTable(
            "TimeTickers",
            schema: "ticker",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Status = table.Column<int>("integer", nullable: false),
                LockHolder = table.Column<string>("text", nullable: true),
                Request = table.Column<byte[]>("bytea", nullable: true),
                ExecutionTime = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LockedAt = table.Column<DateTime>("timestamp with time zone", nullable: true),
                ExecutedAt = table.Column<DateTime>("timestamp with time zone", nullable: true),
                Exception = table.Column<string>("text", nullable: true),
                ElapsedTime = table.Column<long>("bigint", nullable: false),
                Retries = table.Column<int>("integer", nullable: false),
                RetryCount = table.Column<int>("integer", nullable: false),
                RetryIntervals = table.Column<int[]>("integer[]", nullable: true),
                BatchParent = table.Column<Guid>("uuid", nullable: true),
                BatchRunCondition = table.Column<int>("integer", nullable: true),
                Function = table.Column<string>("text", nullable: true),
                Description = table.Column<string>("text", nullable: true),
                InitIdentifier = table.Column<string>("text", nullable: true),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeTickers", x => x.Id);
                table.ForeignKey(
                    "FK_TimeTickers_TimeTickers_BatchParent",
                    x => x.BatchParent,
                    principalSchema: "ticker",
                    principalTable: "TimeTickers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            "CronTickerOccurrences",
            schema: "ticker",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Status = table.Column<int>("integer", nullable: false),
                LockHolder = table.Column<string>("text", nullable: true),
                ExecutionTime = table.Column<DateTime>("timestamp with time zone", nullable: false),
                CronTickerId = table.Column<Guid>("uuid", nullable: false),
                LockedAt = table.Column<DateTime>("timestamp with time zone", nullable: true),
                ExecutedAt = table.Column<DateTime>("timestamp with time zone", nullable: true),
                Exception = table.Column<string>("text", nullable: true),
                ElapsedTime = table.Column<long>("bigint", nullable: false),
                RetryCount = table.Column<int>("integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CronTickerOccurrences", x => x.Id);
                table.ForeignKey(
                    "FK_CronTickerOccurrences_CronTickers_CronTickerId",
                    x => x.CronTickerId,
                    principalSchema: "ticker",
                    principalTable: "CronTickers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_CronTickerOccurrence_CronTickerId",
            schema: "ticker",
            table: "CronTickerOccurrences",
            column: "CronTickerId");

        migrationBuilder.CreateIndex(
            "IX_CronTickerOccurrence_ExecutionTime",
            schema: "ticker",
            table: "CronTickerOccurrences",
            column: "ExecutionTime");

        migrationBuilder.CreateIndex(
            "IX_CronTickerOccurrence_Status_ExecutionTime",
            schema: "ticker",
            table: "CronTickerOccurrences",
            columns: new[] { "Status", "ExecutionTime" });

        migrationBuilder.CreateIndex(
            "UQ_CronTickerId_ExecutionTime",
            schema: "ticker",
            table: "CronTickerOccurrences",
            columns: new[] { "CronTickerId", "ExecutionTime" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_CronTickers_Expression",
            schema: "ticker",
            table: "CronTickers",
            column: "Expression");

        migrationBuilder.CreateIndex(
            "IX_TimeTicker_ExecutionTime",
            schema: "ticker",
            table: "TimeTickers",
            column: "ExecutionTime");

        migrationBuilder.CreateIndex(
            "IX_TimeTicker_Status_ExecutionTime",
            schema: "ticker",
            table: "TimeTickers",
            columns: new[] { "Status", "ExecutionTime" });

        migrationBuilder.CreateIndex(
            "IX_TimeTickers_BatchParent",
            schema: "ticker",
            table: "TimeTickers",
            column: "BatchParent");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "CronTickerOccurrences",
            "ticker");

        migrationBuilder.DropTable(
            "TimeTickers",
            "ticker");

        migrationBuilder.DropTable(
            "CronTickers",
            "ticker");
    }
}

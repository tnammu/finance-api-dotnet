using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinanceApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    DividendYield = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Stocks",
                columns: new[] { "Id", "CompanyName", "DividendYield", "LastUpdated", "Price", "Symbol" },
                values: new object[,]
                {
                    { 1, "Apple Inc.", 0.6m, new DateTime(2025, 10, 3, 16, 23, 3, 24, DateTimeKind.Utc).AddTicks(4062), 170.50m, "AAPL" },
                    { 2, "Microsoft Corp.", 0.8m, new DateTime(2025, 10, 3, 16, 23, 3, 24, DateTimeKind.Utc).AddTicks(4065), 320.75m, "MSFT" },
                    { 3, "Tesla Inc.", 0.0m, new DateTime(2025, 10, 3, 16, 23, 3, 24, DateTimeKind.Utc).AddTicks(4067), 250.10m, "TSLA" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stocks");
        }
    }
}

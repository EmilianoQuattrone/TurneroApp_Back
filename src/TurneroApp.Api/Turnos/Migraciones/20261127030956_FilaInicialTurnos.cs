using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Api.Turnos.Migraciones
{
    /// <inheritdoc />
    public partial class FilaInicialTurnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastCalledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CallCount = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BusinessDate_ServiceType_Number",
                table: "Tickets",
                columns: new[] { "BusinessDate", "ServiceType", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_ActivePerService",
                table: "Tickets",
                columns: new[] { "BusinessDate", "ServiceType" },
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}

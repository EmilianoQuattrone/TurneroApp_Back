using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Api.Turnos.Migraciones
{
    public partial class RenombrarTicketsATurnos : Migration
    {
        protected override void Up(MigrationBuilder constructorMigracion)
        {
            constructorMigracion.RenameTable(
                name: "Tickets",
                schema: "dbo",
                newName: "Turnos",
                newSchema: "dbo");
        }

        protected override void Down(MigrationBuilder constructorMigracion)
        {
            constructorMigracion.RenameTable(
                name: "Turnos",
                schema: "dbo",
                newName: "Tickets",
                newSchema: "dbo");
        }
    }
}

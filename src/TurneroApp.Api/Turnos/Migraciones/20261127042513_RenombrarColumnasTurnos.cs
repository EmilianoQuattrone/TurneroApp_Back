using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Api.Turnos.Migraciones
{
    /// <inheritdoc />
    public partial class RenombrarColumnasTurnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder constructorMigracion)
        {
            // El filtro del índice debe usar el nombre nuevo de la columna Estado.
            constructorMigracion.DropIndex(
                name: "UX_Tickets_ActivePerService",
                schema: "dbo",
                table: "Turnos");

            constructorMigracion.RenameColumn(
                name: "Status",
                schema: "dbo",
                table: "Turnos",
                newName: "Estado");

            constructorMigracion.RenameColumn(
                name: "ServiceType",
                schema: "dbo",
                table: "Turnos",
                newName: "TipoAtencion");

            constructorMigracion.RenameColumn(
                name: "Number",
                schema: "dbo",
                table: "Turnos",
                newName: "Numero");

            constructorMigracion.RenameColumn(
                name: "LastCalledAt",
                schema: "dbo",
                table: "Turnos",
                newName: "FechaUltimoLlamado");

            constructorMigracion.RenameColumn(
                name: "IssuedAt",
                schema: "dbo",
                table: "Turnos",
                newName: "FechaEmision");

            constructorMigracion.RenameColumn(
                name: "ClosedAt",
                schema: "dbo",
                table: "Turnos",
                newName: "FechaCierre");

            constructorMigracion.RenameColumn(
                name: "CallCount",
                schema: "dbo",
                table: "Turnos",
                newName: "CantidadLlamados");

            constructorMigracion.RenameColumn(
                name: "BusinessDate",
                schema: "dbo",
                table: "Turnos",
                newName: "FechaJornada");

            constructorMigracion.RenameColumn(
                name: "Id",
                schema: "dbo",
                table: "Turnos",
                newName: "IdTurno");

            constructorMigracion.CreateIndex(
                name: "UX_Tickets_ActivePerService",
                schema: "dbo",
                table: "Turnos",
                columns: new[] { "FechaJornada", "TipoAtencion" },
                unique: true,
                filter: "[Estado] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder constructorMigracion)
        {
            constructorMigracion.DropIndex(
                name: "UX_Tickets_ActivePerService",
                schema: "dbo",
                table: "Turnos");

            constructorMigracion.RenameColumn(
                name: "TipoAtencion",
                schema: "dbo",
                table: "Turnos",
                newName: "ServiceType");

            constructorMigracion.RenameColumn(
                name: "Numero",
                schema: "dbo",
                table: "Turnos",
                newName: "Number");

            constructorMigracion.RenameColumn(
                name: "FechaUltimoLlamado",
                schema: "dbo",
                table: "Turnos",
                newName: "LastCalledAt");

            constructorMigracion.RenameColumn(
                name: "FechaJornada",
                schema: "dbo",
                table: "Turnos",
                newName: "BusinessDate");

            constructorMigracion.RenameColumn(
                name: "FechaEmision",
                schema: "dbo",
                table: "Turnos",
                newName: "IssuedAt");

            constructorMigracion.RenameColumn(
                name: "FechaCierre",
                schema: "dbo",
                table: "Turnos",
                newName: "ClosedAt");

            constructorMigracion.RenameColumn(
                name: "Estado",
                schema: "dbo",
                table: "Turnos",
                newName: "Status");

            constructorMigracion.RenameColumn(
                name: "CantidadLlamados",
                schema: "dbo",
                table: "Turnos",
                newName: "CallCount");

            constructorMigracion.RenameColumn(
                name: "IdTurno",
                schema: "dbo",
                table: "Turnos",
                newName: "Id");

            constructorMigracion.CreateIndex(
                name: "UX_Tickets_ActivePerService",
                schema: "dbo",
                table: "Turnos",
                columns: new[] { "BusinessDate", "ServiceType" },
                unique: true,
                filter: "[Status] = 1");
        }
    }
}

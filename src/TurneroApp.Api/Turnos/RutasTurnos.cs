using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace TurneroApp.Api.Turnos;

internal sealed record SolicitudAccionTurno(
    [property: JsonPropertyName("currentTicketId")] long? IdTurnoActual);

internal static class RutasTurnos
{
    public static void MapearRutasTurnos(this WebApplication aplicacion)
    {
        var grupo = aplicacion.MapGroup("/api/turnos");
        grupo.MapGet("", (ServicioTurnos servicio, CancellationToken cancelacion) =>
            EjecutarAsync(() => servicio.ConsultarEstadoAsync(cancelacion)));
        grupo.MapPost("/{tipo}/issue", (string tipo, ServicioTurnos servicio, CancellationToken cancelacion) =>
            SegunTipoAtencion(tipo, valor => servicio.EmitirAsync(valor, cancelacion)));

        MapearAccion(grupo, "siguiente", AccionTurno.Siguiente);
        MapearAccion(grupo, "recall", AccionTurno.Rellamar);
        MapearAccion(grupo, "complete", AccionTurno.Finalizar);
        MapearAccion(grupo, "absent", AccionTurno.MarcarAusente);
    }

    private static void MapearAccion(RouteGroupBuilder grupo, string ruta, AccionTurno accion)
    {
        grupo.MapPost("/{tipo}/" + ruta,
            (string tipo, SolicitudAccionTurno? solicitud, ServicioTurnos servicio, CancellationToken cancelacion) =>
                SegunTipoAtencion(tipo, valor => servicio.ProcesarAsync(valor, accion, solicitud?.IdTurnoActual, cancelacion)));
    }

    private static Task<IResult> SegunTipoAtencion(string tipo, Func<TipoAtencion, Task<EstadoFilas>> operacion) =>
        tipo switch
        {
            "obra-social" => EjecutarAsync(() => operacion(TipoAtencion.ObraSocial)),
            "particular" => EjecutarAsync(() => operacion(TipoAtencion.Particular)),
            _ => Task.FromResult(Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Tipo de atención inexistente"))
        };

    private static async Task<IResult> EjecutarAsync(Func<Task<EstadoFilas>> operacion)
    {
        try
        {
            return Results.Ok(await operacion());
        }
        catch (ConflictoTurnoException excepcion)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: excepcion.Message);
        }
        catch (SqlException excepcion) when (excepcion.Number == 51000)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "La fila está ocupada. Volvé a consultar los turnos.");
        }
    }
}

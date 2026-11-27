using System.Data;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace TurneroApp.Api.Turnos;

internal sealed record TurnoActual(
    [property: JsonPropertyName("numero")] int Numero,
    [property: JsonPropertyName("codigo")] string Codigo,
    long Id,
    [property: JsonPropertyName("callCount")] int CantidadLlamados);

internal sealed record TurnoEnLista(
    long Id,
    [property: JsonPropertyName("code")] string Codigo,
    [property: JsonPropertyName("status")] string Estado,
    [property: JsonPropertyName("fechaCierre")] DateTimeOffset? FechaCierre);

internal sealed record EstadoFilas(
    [property: JsonPropertyName("obraSocial")] TurnoActual? ObraSocial,
    [property: JsonPropertyName("particular")] TurnoActual? Particular,
    [property: JsonPropertyName("businessDate")] DateOnly FechaJornada,
    [property: JsonPropertyName("insuredWaiting")] int ObraSocialEnEspera,
    [property: JsonPropertyName("selfPayWaiting")] int ParticularEnEspera,
    [property: JsonPropertyName("insuredTickets")] IReadOnlyList<TurnoEnLista> TurnosObraSocial,
    [property: JsonPropertyName("selfPayTickets")] IReadOnlyList<TurnoEnLista> TurnosParticulares,
    [property: JsonPropertyName("maxCallCount")] int MaximoLlamados,
    [property: JsonPropertyName("issued")] TurnoActual? Emitido = null);

internal enum AccionTurno { Siguiente, Rellamar, Finalizar, MarcarAusente }
internal sealed class ConflictoTurnoException(string mensaje) : Exception(mensaje);

internal sealed class ServicioTurnos(ContextoTurnos contexto, TimeProvider reloj)
{
    private const int MaximoLlamados = 3;

    public Task<EstadoFilas> ConsultarEstadoAsync(CancellationToken cancelacion) =>
        LeerEstadoAsync(CalendarioJornada.ObtenerFecha(reloj.GetUtcNow()), null, cancelacion);

    public Task<EstadoFilas> EmitirAsync(TipoAtencion tipo, CancellationToken cancelacion) =>
        EjecutarCambioAsync(async (fecha, ahora) =>
        {
            var fila = FiltrarPorJornada(fecha, tipo);
            var ultimoNumero = await fila.MaxAsync(turno => (int?)turno.Numero, cancelacion) ?? 0;
            var turno = new Turno
            {
                FechaJornada = fecha,
                TipoAtencion = tipo,
                Numero = ultimoNumero + 1,
                Estado = EstadoTurno.EnEspera,
                FechaEmision = ahora
            };
            // Si la caja está libre, llama al primero en espera antes de confirmar la emisión.
            // Si había turnos pendientes, respeta su orden y deja el nuevo al final.
            if (!await fila.AnyAsync(pendiente => pendiente.Estado == EstadoTurno.Llamado, cancelacion))
            {
                var siguiente = await BuscarSiguienteAsync(fila, cancelacion) ?? turno;
                Llamar(siguiente, ahora);
            }

            contexto.Turnos.Add(turno);
            await contexto.SaveChangesAsync(cancelacion);
            return ConvertirTurnoActual(turno);
        }, cancelacion);

    public Task<EstadoFilas> ProcesarAsync(
        TipoAtencion tipo, AccionTurno accion, long? idEsperado, CancellationToken cancelacion) =>
        EjecutarCambioAsync(async (fecha, ahora) =>
        {
            var fila = FiltrarPorJornada(fecha, tipo);
            var actual = await fila.SingleOrDefaultAsync(turno => turno.Estado == EstadoTurno.Llamado, cancelacion);
            if (actual?.Id != idEsperado || (actual is null && accion != AccionTurno.Siguiente))
            {
                throw new ConflictoTurnoException("El turno actual cambió. Consultá la fila y volvé a intentar.");
            }

            if (actual is not null)
            {
                ProcesarTurnoActual(actual, accion, ahora);

                // Libera la caja antes de asignar otro turno dentro de la misma transacción.
                await contexto.SaveChangesAsync(cancelacion);
            }

            if (actual is null || actual.Estado != EstadoTurno.Llamado)
            {
                var siguiente = await BuscarSiguienteAsync(fila, cancelacion);
                if (siguiente is not null)
                {
                    Llamar(siguiente, ahora);
                    await contexto.SaveChangesAsync(cancelacion);
                }
            }

            return null;
        }, cancelacion);

    // Coordina las escrituras entre instancias de la API. La base libera el bloqueo
    // al confirmar o revertir la transacción que cambia la fila.
    private async Task<EstadoFilas> EjecutarCambioAsync(
        Func<DateOnly, DateTimeOffset, Task<TurnoActual?>> cambiar, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancelacion);
        await contexto.Database.ExecuteSqlRawAsync("""
            DECLARE @resultadoBloqueo int;
            EXEC @resultadoBloqueo = sys.sp_getapplock
                @Resource = N'TurneroApp.TicketWrites', @LockMode = N'Exclusive',
                @LockOwner = N'Transaction', @LockTimeout = 4000;
            IF @resultadoBloqueo < 0 THROW 51000, 'No se pudo bloquear la fila de turnos.', 1;
            """, cancelacion);

        var ahora = reloj.GetUtcNow();
        var fecha = CalendarioJornada.ObtenerFecha(ahora);
        var emitido = await cambiar(fecha, ahora);
        var estado = await LeerEstadoAsync(fecha, emitido, cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return estado;
    }

    private async Task<EstadoFilas> LeerEstadoAsync(
        DateOnly fecha, TurnoActual? emitido, CancellationToken cancelacion)
    {
        var turnosDeHoy = await contexto.Turnos.AsNoTracking()
            .Where(turno => turno.FechaJornada == fecha)
            .OrderBy(turno => turno.TipoAtencion).ThenBy(turno => turno.Numero)
            .ToListAsync(cancelacion);
        var obraSocial = turnosDeHoy.Where(turno => turno.TipoAtencion == TipoAtencion.ObraSocial).ToArray();
        var particular = turnosDeHoy.Where(turno => turno.TipoAtencion == TipoAtencion.Particular).ToArray();

        return new EstadoFilas(
            ConvertirTurnoActual(obraSocial.SingleOrDefault(turno => turno.Estado == EstadoTurno.Llamado)),
            ConvertirTurnoActual(particular.SingleOrDefault(turno => turno.Estado == EstadoTurno.Llamado)),
            fecha,
            obraSocial.Count(turno => turno.Estado == EstadoTurno.EnEspera),
            particular.Count(turno => turno.Estado == EstadoTurno.EnEspera),
            obraSocial.Select(ConvertirTurnoEnLista).ToArray(),
            particular.Select(ConvertirTurnoEnLista).ToArray(),
            MaximoLlamados,
            emitido);
    }

    private IQueryable<Turno> FiltrarPorJornada(DateOnly fecha, TipoAtencion tipo) =>
        contexto.Turnos.Where(turno => turno.FechaJornada == fecha && turno.TipoAtencion == tipo);

    private static Task<Turno?> BuscarSiguienteAsync(IQueryable<Turno> fila, CancellationToken cancelacion) =>
        fila.Where(turno => turno.Estado == EstadoTurno.EnEspera)
            .OrderBy(turno => turno.Numero).FirstOrDefaultAsync(cancelacion);

    private static void ProcesarTurnoActual(Turno turno, AccionTurno accion, DateTimeOffset ahora)
    {
        if (accion == AccionTurno.Rellamar)
        {
            Llamar(turno, ahora);
            if (turno.CantidadLlamados < MaximoLlamados)
            {
                return;
            }
        }

        turno.Estado = accion == AccionTurno.Finalizar ? EstadoTurno.Finalizado : EstadoTurno.Ausente;
        turno.FechaCierre = ahora;
    }

    private static void Llamar(Turno turno, DateTimeOffset ahora)
    {
        if (turno.CantidadLlamados >= MaximoLlamados)
        {
            throw new ConflictoTurnoException($"El turno ya alcanzó el máximo de {MaximoLlamados} llamados. Volvé a consultar antes de continuar.");
        }

        turno.Estado = EstadoTurno.Llamado;
        turno.FechaUltimoLlamado = ahora;
        turno.CantidadLlamados++;
    }

    // Los valores de estado de la API se conservan por compatibilidad.
    private static TurnoEnLista ConvertirTurnoEnLista(Turno turno) =>
        new(turno.Id, FormatearCodigo(turno), turno.Estado switch
        {
            EstadoTurno.EnEspera => "Waiting",
            EstadoTurno.Llamado => "Called",
            EstadoTurno.Finalizado => "Completed",
            EstadoTurno.Ausente => "Absent",
            _ => ((int)turno.Estado).ToString(CultureInfo.InvariantCulture)
        }, turno.FechaCierre);

    private static TurnoActual? ConvertirTurnoActual(Turno? turno) =>
        turno is null ? null : new TurnoActual(turno.Numero, FormatearCodigo(turno), turno.Id, turno.CantidadLlamados);

    private static string FormatearCodigo(Turno turno) =>
        (turno.TipoAtencion == TipoAtencion.ObraSocial ? "OS-" : "P-") +
        turno.Numero.ToString("D3", CultureInfo.InvariantCulture);
}

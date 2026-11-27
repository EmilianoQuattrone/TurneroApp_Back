using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TurneroApp.Api.Turnos;

internal sealed class OpcionesRetencionTurnos
{
    public const string NombreSeccion = "TicketRetention";

    [ConfigurationKeyName("Enabled")]
    public bool Habilitada { get; set; }

    [ConfigurationKeyName("RetentionDays")]
    public int DiasRetencion { get; set; } = 90;
}

internal sealed class TareaRetencionTurnos(
    IServiceScopeFactory fabricaAmbitos,
    IOptions<OpcionesRetencionTurnos> opciones,
    TimeProvider reloj,
    ILogger<TareaRetencionTurnos> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        var configuracion = opciones.Value;
        if (!configuracion.Habilitada)
        {
            return;
        }

        DateOnly? ultimoMesProcesado = null;
        using var temporizador = new PeriodicTimer(TimeSpan.FromHours(1), reloj);
        do
        {
            var hoy = CalendarioJornada.ObtenerFecha(reloj.GetUtcNow());
            var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
            if (inicioMes == ultimoMesProcesado)
            {
                continue;
            }

            // Usa el mismo corte durante todo el mes. Si la API estuvo apagada, recupera
            // la ejecución al iniciar. Repetir ese corte no elimina turnos adicionales.
            var fechaCorte = inicioMes.AddDays(-Math.Min(configuracion.DiasRetencion, inicioMes.DayNumber));
            try
            {
                await using var ambito = fabricaAmbitos.CreateAsyncScope();
                var contexto = ambito.ServiceProvider.GetRequiredService<ContextoTurnos>();
                var eliminados = await contexto.Turnos
                    .Where(turno => turno.FechaJornada < fechaCorte)
                    .ExecuteDeleteAsync(cancelacion);

                ultimoMesProcesado = inicioMes;
                registro.LogInformation(
                    "Limpieza de turnos: {CantidadEliminada} eliminados con fecha anterior a {FechaCorte}.",
                    eliminados, fechaCorte);
            }
            catch (Exception excepcion) when (!cancelacion.IsCancellationRequested)
            {
                registro.LogError(excepcion,
                    "No se pudo completar la limpieza de turnos con corte {FechaCorte}. Se reintentará en la próxima revisión.",
                    fechaCorte);
            }
        } while (await temporizador.WaitForNextTickAsync(cancelacion));
    }
}

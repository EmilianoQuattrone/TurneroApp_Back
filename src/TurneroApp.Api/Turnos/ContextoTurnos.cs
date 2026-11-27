using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TurneroApp.Api.Turnos;

internal enum TipoAtencion { ObraSocial = 0, Particular = 1 }
internal enum EstadoTurno { EnEspera = 0, Llamado = 1, Finalizado = 2, Ausente = 3 }

internal sealed class Turno
{
    public long Id { get; set; }
    public DateOnly FechaJornada { get; set; }
    public TipoAtencion TipoAtencion { get; set; }
    public int Numero { get; set; }
    public EstadoTurno Estado { get; set; }
    public DateTimeOffset FechaEmision { get; set; }
    public DateTimeOffset? FechaUltimoLlamado { get; set; }
    public int CantidadLlamados { get; set; }
    public DateTimeOffset? FechaCierre { get; set; }
}

internal sealed class ContextoTurnos(DbContextOptions<ContextoTurnos> opciones) : DbContext(opciones)
{
    public DbSet<Turno> Turnos => Set<Turno>();

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        // Conserva los nombres de las restricciones y de los indices instalados.
        var turno = constructorModelo.Entity<Turno>();
        turno.ToTable("Turnos", "dbo");
        turno.HasKey(x => x.Id).HasName("PK_Tickets");
        turno.Property(x => x.Id).HasColumnName("IdTurno");
        turno.HasIndex(x => new { x.FechaJornada, x.TipoAtencion, x.Numero })
            .IsUnique()
            .HasDatabaseName("IX_Tickets_BusinessDate_ServiceType_Number");
        turno.HasIndex(x => new { x.FechaJornada, x.TipoAtencion })
            .IsUnique()
            .HasFilter("[Estado] = 1")
            .HasDatabaseName("UX_Tickets_ActivePerService");
    }
}

internal sealed class VerificacionBaseTurnos(IServiceScopeFactory fabricaAmbitos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contextoVerificacion, CancellationToken cancelacion = default)
    {
        await using var ambito = fabricaAmbitos.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoTurnos>();
        try
        {
            await contexto.Turnos.AnyAsync(cancelacion);
            return HealthCheckResult.Healthy();
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("No se pudo consultar la tabla de turnos.");
        }
    }
}

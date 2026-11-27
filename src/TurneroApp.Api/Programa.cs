using Microsoft.EntityFrameworkCore;
using TurneroApp.Api.Turnos;

var constructor = WebApplication.CreateBuilder(args);

var cadenaConexion = constructor.Configuration.GetConnectionString("Turnero")
    ?? throw new InvalidOperationException("Configurá la conexión ConnectionStrings:Turnero para iniciar la API.");

constructor.Services.AddDbContext<ContextoTurnos>(opciones => opciones.UseSqlServer(cadenaConexion));
constructor.Services.AddSingleton<TimeProvider>(TimeProvider.System);
constructor.Services.AddScoped<ServicioTurnos>();
constructor.Services.AddOptions<OpcionesRetencionTurnos>()
    .BindConfiguration(OpcionesRetencionTurnos.NombreSeccion)
    .Validate(opciones => opciones.DiasRetencion > 0, "TicketRetention:RetentionDays debe ser mayor que cero.")
    .ValidateOnStart();
constructor.Services.AddHostedService<TareaRetencionTurnos>();
constructor.Services.AddHealthChecks().AddCheck<VerificacionBaseTurnos>("turnero-db");

var aplicacion = constructor.Build();

aplicacion.MapHealthChecks("/health");
aplicacion.MapearRutasTurnos();

aplicacion.Run();

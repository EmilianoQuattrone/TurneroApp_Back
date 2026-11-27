# TurneroApp · Backend

API ASP.NET Core para la demo web de turnos de farmacia. Guarda los turnos en SQL Server y usa la fecha de Buenos Aires para separar las jornadas.

- **Obra social** corresponde a Caja 1 y usa códigos `OS-001`, `OS-002`, etc.
- **Particular** corresponde a Caja 4 y usa códigos `P-001`, `P-002`, etc.
- Al emitir un turno con la caja libre, la API llama al primero en espera. Si la caja está ocupada, el nuevo turno queda en la fila.
- **Finalizar**, **Marcar ausente** y **Siguiente turno** cierran el turno actual y llaman al próximo cuando hay alguien esperando. **Siguiente turno** marca como ausente al actual si todavía sigue llamado.
- El máximo es **tres llamados en total**. El tercero marca el turno como ausente y hace avanzar la caja. La numeración se reinicia por tipo de atención al cambiar el día. Los registros de jornadas anteriores quedan en la base.

## Ejecutar en local

Necesitás el SDK de .NET 10, SQL Server Express y una base local `TurneroDb`. Desde la raíz del backend, si todavía no tenés `dotnet-ef` 10.0.10:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.10
```

Configurá la conexión local y aplicá las migraciones:

```powershell
$proyecto = 'src/TurneroApp.Api'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet user-secrets set 'ConnectionStrings:Turnero' 'Server=.\SQLEXPRESS;Database=TurneroDb;Integrated Security=True;TrustServerCertificate=True' --project $proyecto
dotnet ef database update --project $proyecto
dotnet run --project $proyecto --launch-profile TurneroApp.Api
```

También podés abrir `TurneroApp.slnx` y arrancar el perfil `TurneroApp.Api` con **F5**. Confirmá que [el chequeo de salud](http://127.0.0.1:5080/health) devuelva `Healthy`.

La API no aplica migraciones al arrancar. Si ya tenés una versión anterior, detené la API y aplicá las migraciones pendientes antes de reiniciarla. El esquema actual usa `dbo.Turnos` y nombres en español como `IdTurno`, `FechaJornada`, `TipoAtencion` y `Estado`. Las migraciones de renombre conservan los registros existentes. La conexión de ejemplo usa autenticación de Windows y confía en el certificado solo para desarrollo local.

## Probar la API

Usá `obra-social` o `particular` en las rutas de cada caja.

| Acción | Ruta | Cuerpo |
| --- | --- | --- |
| Consultar la jornada | `GET /api/turnos` | Sin cuerpo |
| Emitir | `POST /api/turnos/{tipo}/issue` | Sin cuerpo |
| Llamar al siguiente | `POST /api/turnos/{tipo}/siguiente` | `{"currentTicketId":null}` si está libre, o el ID del turno llamado |
| Volver a llamar | `POST /api/turnos/{tipo}/recall` | `{"currentTicketId":<id>}` |
| Finalizar | `POST /api/turnos/{tipo}/complete` | `{"currentTicketId":<id>}` |
| Marcar ausente | `POST /api/turnos/{tipo}/absent` | `{"currentTicketId":<id>}` |

Con una caja de Obra social libre y sin turnos en espera, emití dos turnos. El primero debe quedar activo y el segundo, en espera. Al finalizar el primero, el segundo debe quedar activo:

```powershell
$api = 'http://127.0.0.1:5080/api/turnos'
$primero = Invoke-RestMethod -Method Post "$api/obra-social/issue"
$segundo = Invoke-RestMethod -Method Post "$api/obra-social/issue"
$cuerpo = @{ currentTicketId = $primero.obraSocial.id } | ConvertTo-Json -Compress
$resultado = Invoke-RestMethod -Method Post "$api/obra-social/complete" -ContentType 'application/json' -Body $cuerpo
$resultado.obraSocial
```

Si el turno activo cambia entre la consulta y una acción, la API devuelve `409` para que el panel vuelva a consultar. Reiniciá la API y consultá la jornada para comprobar que los turnos sigan disponibles.

## Limpieza mensual

Está **desactivada por defecto**. Si se habilita, la API elimina de `dbo.Turnos` los registros con `FechaJornada` anterior al primer día del mes de Buenos Aires menos el plazo de retención, que por defecto es de 90 días. La API revisa el calendario al arrancar y cada hora. Si estuvo apagada al cambiar de mes, aplica el corte cuando vuelve a iniciar. El registro de ejecución indica el corte y el total eliminado.

Para revisar cuántos registros entran en el corte predeterminado, ejecutá en SSMS:

```sql
USE [TurneroDb];

DECLARE @hoy date = CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'Argentina Standard Time');
DECLARE @inicioMes date = DATEFROMPARTS(YEAR(@hoy), MONTH(@hoy), 1);
DECLARE @fechaCorte date = DATEADD(day, -90, @inicioMes);

SELECT @fechaCorte AS FechaCorte, COUNT_BIG(*) AS TurnosElegibles
FROM dbo.Turnos
WHERE FechaJornada < @fechaCorte;
```

Después de revisar el resultado, habilitala en desarrollo y reiniciá la API:

```powershell
dotnet user-secrets set 'TicketRetention:Enabled' 'true' --project src/TurneroApp.Api
```

Para cambiar el plazo, usá la clave `TicketRetention:RetentionDays`. En un despliegue se pueden usar las variables `TicketRetention__Enabled` y `TicketRetention__RetentionDays`. La demo local usa cajas fijas y no incluye autenticación.

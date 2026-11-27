namespace TurneroApp.Api.Turnos;

internal static class CalendarioJornada
{
    private static readonly TimeZoneInfo ZonaHoraria =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static DateOnly ObtenerFecha(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, ZonaHoraria).DateTime);
}

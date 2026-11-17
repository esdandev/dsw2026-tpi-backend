namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class Clock
{
    private static readonly TimeZoneInfo ArgentinaTimeZone = FindArgentinaTimeZone();

    private static TimeZoneInfo FindArgentinaTimeZone()
    {
        // IANA en Linux/macOS; id de Windows como fallback.
        foreach (var id in new[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        throw new TimeZoneNotFoundException("No se encontró la zona horaria de Argentina.");
    }

    public static DateTime Now => DateTime.UtcNow; // UTC real.

    public static DateTime Today => ToArgentina(DateTime.UtcNow).Date; // fecha de negocio en Argentina.

    public static DateTime ToArgentina(DateTime utc) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ArgentinaTimeZone),
            DateTimeKind.Unspecified);

    public static DateTime FromArgentina(DateTime local) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ArgentinaTimeZone),
            DateTimeKind.Utc);
}

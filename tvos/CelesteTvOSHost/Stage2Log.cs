using System.Globalization;

namespace CelesteTvOSHost;

internal static class Stage2Log
{
    private static readonly object Gate = new();

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warning(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"STAGE2 {DateTimeOffset.UtcNow:O} [{level}] {message}"
                )
            );
        }
    }
}

using System.Globalization;

namespace CelesteTvOSHost;

internal static class RuntimeLog
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"STAGE3B {DateTimeOffset.UtcNow:O} [{level}] {message}"
            ));
        }
    }
}

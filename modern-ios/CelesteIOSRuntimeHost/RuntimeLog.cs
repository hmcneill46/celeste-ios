namespace CelesteIOSRuntimeHost;

internal static class RuntimeLog
{
    internal static void Info(string message) => Console.WriteLine($"IOS_FOUNDATION {message}");
    internal static void Warning(string message) => Console.WriteLine($"IOS_FOUNDATION_WARNING {message}");
    internal static void Error(string message) => Console.Error.WriteLine($"IOS_FOUNDATION_ERROR {message}");
}

using System;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.Helpers
{
    // Everest uses this interface to model compile-time log levels. Keep the
    // exact public constraint so ordinary precompiled mod DLLs resolve without
    // relinking or runtime code generation.
    public interface IConst<out T>
    {
    }
}

namespace Celeste.Mod
{
    public enum LogLevel
    {
        Verbose,
        Debug,
        Info,
        Warn,
        Error
    }

    public static class LogLevelConstTypes
    {
        public struct Verbose : Helpers.IConst<LogLevel> { public static LogLevel Value => LogLevel.Verbose; }
        public struct Debug : Helpers.IConst<LogLevel> { public static LogLevel Value => LogLevel.Debug; }
        public struct Info : Helpers.IConst<LogLevel> { public static LogLevel Value => LogLevel.Info; }
        public struct Warn : Helpers.IConst<LogLevel> { public static LogLevel Value => LogLevel.Warn; }
        public struct Error : Helpers.IConst<LogLevel> { public static LogLevel Value => LogLevel.Error; }
    }

    public static partial class Logger
    {
        [InterpolatedStringHandler]
        public ref struct LogInterpolatedStringHandler<TLevel>
            where TLevel : struct, Helpers.IConst<LogLevel>
        {
            private DefaultInterpolatedStringHandler handler;

            public bool ShouldLog { get; }

            public LogInterpolatedStringHandler(
                int literalLength,
                int formattedCount,
                string tag,
                out bool shouldLog)
            {
                ShouldLog = shouldLog = Logger.ShouldLog(tag, Level());
                handler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
            }

            private static LogLevel Level()
            {
                if (typeof(TLevel) == typeof(LogLevelConstTypes.Verbose)) return LogLevel.Verbose;
                if (typeof(TLevel) == typeof(LogLevelConstTypes.Debug)) return LogLevel.Debug;
                if (typeof(TLevel) == typeof(LogLevelConstTypes.Warn)) return LogLevel.Warn;
                if (typeof(TLevel) == typeof(LogLevelConstTypes.Error)) return LogLevel.Error;
                return LogLevel.Info;
            }

            internal string ToStringAndClear() => handler.ToStringAndClear();
            public void AppendLiteral(string value) => handler.AppendLiteral(value);
            public void AppendFormatted<T>(T value) => handler.AppendFormatted(value);
            public void AppendFormatted<T>(T value, string format) => handler.AppendFormatted(value, format);
            public void AppendFormatted<T>(T value, int alignment) => handler.AppendFormatted(value, alignment);
            public void AppendFormatted<T>(T value, int alignment, string format) => handler.AppendFormatted(value, alignment, format);
            public void AppendFormatted(ReadOnlySpan<char> value) => handler.AppendFormatted(value);
            public void AppendFormatted(ReadOnlySpan<char> value, int alignment, string format = null) =>
                handler.AppendFormatted(value, alignment, format);
            public void AppendFormatted(string value) => handler.AppendFormatted(value);
            public void AppendFormatted(string value, int alignment, string format = null) =>
                handler.AppendFormatted(value, alignment, format);
        }

        public static void Verbose(string tag, LogInterpolatedStringHandler<LogLevelConstTypes.Verbose> value) =>
            WriteInterpolated("mod-verbose", tag, ref value);
        public static void Debug(string tag, LogInterpolatedStringHandler<LogLevelConstTypes.Debug> value) =>
            WriteInterpolated("mod-debug", tag, ref value);
        public static void Info(string tag, LogInterpolatedStringHandler<LogLevelConstTypes.Info> value) =>
            WriteInterpolated("mod-info", tag, ref value);
        public static void Warn(string tag, LogInterpolatedStringHandler<LogLevelConstTypes.Warn> value) =>
            WriteInterpolated("mod-warning", tag, ref value);
        public static void Error(string tag, LogInterpolatedStringHandler<LogLevelConstTypes.Error> value) =>
            WriteInterpolated("mod-error", tag, ref value);

        private static void WriteInterpolated<TLevel>(
            string level,
            string tag,
            ref LogInterpolatedStringHandler<TLevel> value)
            where TLevel : struct, Helpers.IConst<LogLevel>
        {
            if (value.ShouldLog)
                AppleEverestStaticRuntime.Log($"{level} tag={tag} message={value.ToStringAndClear()}");
        }
    }
}

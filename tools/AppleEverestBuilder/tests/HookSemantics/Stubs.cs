using System.Collections.Generic;

namespace Celeste.Mod
{
    internal static class GeneratedAppleEverestConfiguredOrdinals
    {
        internal static long? Resolve(string owner, string targetId) =>
            owner == "ConfiguredAfterAll" && targetId == "celeste-player-die"
                ? 1L << 60
                : null;
    }

    public enum LogLevel { Verbose, Debug, Info, Warn, Error }

    public static class AppleEverestStaticRuntime
    {
        private static readonly Dictionary<string, bool> Enabled = new();
        public static string CurrentOwner { get; set; } = "test";
        public static readonly List<string> Trace = new();
        public static bool IsModuleEnabled(string owner) => !Enabled.TryGetValue(owner, out bool enabled) || enabled;
        public static void SetEnabled(string owner, bool value) { Enabled[owner] = value; AppleEverestHookList.Invalidate(); }
        public static void RecordHook(string value) => Trace.Add(value);
    }
}

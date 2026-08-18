using System.Collections.Generic;

namespace Celeste
{
    public sealed class Language { }
}

namespace Celeste.Mod
{
    public static class AppleEverestStaticRuntime
    {
        private static readonly Dictionary<string, bool> Enabled = new();
        public static string CurrentOwner { get; set; } = "test";
        public static readonly List<string> Trace = new();
        public static bool IsModuleEnabled(string owner) => !Enabled.TryGetValue(owner, out bool enabled) || enabled;
        public static void SetEnabled(string owner, bool value) { Enabled[owner] = value; On.Celeste.Dialog.RebuildActiveChain(); }
        public static void RecordHook(string value) => Trace.Add(value);
    }
}

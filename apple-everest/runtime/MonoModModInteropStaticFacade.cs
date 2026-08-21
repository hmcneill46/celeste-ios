using System;

namespace MonoMod.ModInterop
{
    /// <summary>
    /// Static-AOT ABI facade for ordinary Everest binaries compiled against
    /// the pinned MonoMod.Utils ModInterop extension. All discovery and
    /// delegate construction is generated on the Mac before device compile.
    /// </summary>
    public static class ModInteropManager
    {
        public static void ModInterop(this Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!global::Celeste.Mod.GeneratedAppleEverestModInterop.Register(type))
                throw new InvalidOperationException("ModInterop type is absent from the closed Apple static-AOT plan");
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModExportNameAttribute : Attribute
    {
        public string Name { get; }
        public ModExportNameAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
    public sealed class ModImportNameAttribute : Attribute
    {
        public string Name { get; }
        public ModImportNameAttribute(string name) => Name = name;
    }
}

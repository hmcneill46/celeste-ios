using System;

namespace Celeste.Mod.Backdrops;

// Binary-compatibility surface for ordinary precompiled Everest helpers.
// Device registration is generated from the attribute metadata at build time;
// no runtime assembly scan or reflective construction is performed.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CustomBackdropAttribute : Attribute
{
    public string[] IDs { get; }

    public CustomBackdropAttribute(params string[] ids)
    {
        IDs = ids;
    }
}

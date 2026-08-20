using System;

namespace Celeste.Mod.Entities;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CustomEntityAttribute : Attribute
{
    public string[] IDs { get; }

    public CustomEntityAttribute(params string[] ids)
    {
        IDs = ids;
    }
}

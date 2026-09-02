using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace Celeste.Mod
{
    public static class DecalRegistry
    {
        private static readonly Dictionary<string, Action<global::Celeste.Decal, XmlAttributeCollection>>
            PropertyHandlers = new(StringComparer.Ordinal);

        public static void AddPropertyHandler(string propertyName,
            Action<global::Celeste.Decal, XmlAttributeCollection> action)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || action == null)
                throw new ArgumentException("a named decal property handler is required");
            PropertyHandlers[propertyName] = action;
        }

        internal static bool TryApply(string propertyName, global::Celeste.Decal decal,
            XmlAttributeCollection attributes)
        {
            if (!PropertyHandlers.TryGetValue(propertyName, out var handler)) return false;
            handler(decal, attributes);
            return true;
        }
    }

    public static class StrawberryRegistry
    {
        private static readonly HashSet<(Type Type, string Id, bool Tracked, bool BlocksNormalCollection)>
            Registered = new();

        public static void Register(Type type, bool tracked = true, bool blocksNormalCollection = false)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            object[] attributes = type.GetCustomAttributes(typeof(Entities.CustomEntityAttribute), false);
            if (attributes.Length != 1)
                throw new InvalidOperationException("registered strawberry must declare one CustomEntityAttribute");
            foreach (string id in ((Entities.CustomEntityAttribute)attributes[0]).IDs)
                Registered.Add((type, id, tracked, blocksNormalCollection));
        }
    }
}

namespace Celeste.Mod.Helpers
{
    public static class FakeAssembly
    {
        public static Assembly GetFakeEntryAssembly() => typeof(global::Celeste.Celeste).Assembly;
    }
}

using System;
using System.Xml;

namespace Celeste.Mod;

// Shared by host preflight and the opted-in static runtime. The selected
// pinned definitions contain square 3x3 and 5x5 masks, using only 0/1/x.
internal static class AppleEverestTileMaskRules
{
    internal const int MaximumDimension = 5;
    internal const int MaximumCells = MaximumDimension * MaximumDimension;

    internal static void ValidateDefinition(XmlElement definition, out int width, out int height)
    {
        width = Dimension(definition, "scanWidth");
        height = Dimension(definition, "scanHeight");
        if (width != height)
            throw new InvalidOperationException("Unsupported autotiler scan dimensions: only 3x3 and 5x5 are accepted.");
        foreach (string attribute in new[] { "ignoreExceptions", "soundPath", "soundParam", "debrisImpactSfx" })
            if (definition.HasAttribute(attribute))
                throw new InvalidOperationException("Unsupported autotiler profile attribute: " + attribute);
        foreach (XmlNode node in definition.ChildNodes)
        {
            if (node is not XmlElement rule) continue;
            if (rule.Name != "set")
                throw new InvalidOperationException("Unsupported autotiler rule: " + rule.Name);
            string mask = rule.GetAttribute("mask");
            if (mask != "center" && mask != "padding")
                _ = ParseMask(mask, width, height);
        }
    }

    private static int Dimension(XmlElement definition, string name)
    {
        if (!definition.HasAttribute(name)) return 3;
        if (!int.TryParse(definition.GetAttribute(name), out int value) || value is not (3 or 5))
            throw new InvalidOperationException("Unsupported autotiler " + name + ": expected 3 or 5.");
        return value;
    }

    internal static byte[] ParseMask(string text, int width, int height)
    {
        if (width != height || width is not (3 or 5))
            throw new InvalidOperationException("Unsupported autotiler mask dimensions.");
        byte[] cells = new byte[width * height];
        int count = 0;
        foreach (char value in text)
        {
            byte cell;
            switch (value)
            {
                case '0': cell = 0; break;
                case '1': cell = 1; break;
                case 'x': case 'X': cell = 2; break;
                case '-': case ' ': case '\t': case '\r': case '\n': continue;
                default: throw new InvalidOperationException("Unsupported autotiler mask symbol: " + value);
            }
            if (count == cells.Length)
                throw new InvalidOperationException("Autotiler mask exceeds its scan dimensions.");
            cells[count++] = cell;
        }
        // Deliberately reject short masks instead of accepting the desktop
        // parser's implicit zero fill. No selected authored mask needs it.
        if (count != cells.Length)
            throw new InvalidOperationException("Autotiler mask length differs from its scan dimensions.");
        return cells;
    }
}

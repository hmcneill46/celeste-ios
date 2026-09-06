#nullable disable
using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestSelectedVisualHelpers
{
    // All selected Femto colors are six-digit RGB. Preserve Everest's byte
    // constructor rather than loading its general helper/module initializer.
    internal static Color Rgb(string value)
    {
        string hex = value.StartsWith("#", StringComparison.Ordinal) ? value.Substring(1) : value;
        if (hex.Length != 6) throw new InvalidOperationException("unreviewed selected RGB color");
        return new Color(int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    // Frozen Everest CullHelper rectangle calculation, including its additional
    // 180-pixel bottom lenience. This is observable in waterfall draw/update work.
    internal static bool IsRectangleVisible(float x, float y, float width, float height)
    {
        Camera camera = (Engine.Scene as Level)?.Camera;
        return camera == null || (x + width >= camera.Left - 4f && x <= camera.Right + 4f &&
            y + height >= camera.Top - 4f && y <= camera.Bottom + 180f + 4f);
    }
}

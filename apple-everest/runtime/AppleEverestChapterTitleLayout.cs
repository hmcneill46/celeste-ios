#nullable disable
using System;

namespace Celeste.Mod;

internal static class AppleEverestChapterTitleLayout
{
    // Pinned Everest OuiChapterPanel._FixTitleLength. Both bookmark layers
    // move together; short titles retain the canonical offset. This rule
    // requires Everest's wider areaselect/title graphic in the content plan.
    internal static float BannerOffset(AreaKey area, float vanillaValue)
    {
        float mapNameSize = ActiveFont.Measure(Dialog.Clean(AreaData.Get(area).Name)).X;
        return vanillaValue - Math.Max(0f, mapNameSize + vanillaValue - 490f);
    }
}

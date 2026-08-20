using Monocle;

namespace Celeste;

// The full desktop Everest renderer gives SubHUD its own high-resolution pass.
// The current static Apple subset has no SubHudRenderer events or buffer, so
// map-helper overlays share Celeste's existing high-resolution HUD pass. This
// preserves their 1920x1080 coordinate space and depth ordering without adding
// an unused desktop event surface or a second render target.
public static class TagsExt
{
    public static readonly BitTag SubHUD = Tags.HUD;
}

namespace Celeste;

/// <summary>
/// Bounded static Everest compatibility for the current canonical Apple map
/// host. Static mod maps are mounted on Celeste's canonical AreaKey, so the
/// exact Everest level-set query used by the accepted DJMapHelper binary
/// resolves to the vanilla level set without carrying Everest's runtime area
/// registry onto the device.
/// </summary>
public static class AreaKeyExt
{
    public static string GetLevelSet(this AreaKey area) => area.LevelSet;
}

#nullable disable
using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod;

// SJ 1.0.12 CS_Credits.Level_OnLoadEntity consumes playbackTutorial markers
// in its lobbies even when no credits cutscene is pending. Those markers name
// TAS inputs for the heartside credits, not PlaybackData .bin tutorials.
// Only the normal-play branch is reachable in the selected two-map product;
// its heartside and credits cutscene remain outside the generated closure.
internal static class AppleEverestStrawberryJamLobbyLoading
{
    internal static void Load()
    {
        Unload();
        Everest.Events.Level.OnLoadEntity += LoadEntity;
    }

    internal static void Unload() => Everest.Events.Level.OnLoadEntity -= LoadEntity;

    private static bool LoadEntity(Level level, LevelData room, Vector2 offset, EntityData data)
    {
        if (AppleEverestMapBinding.ForSession(level.Session)?.Sid != "StrawberryJam2021/0-Lobbies/1-Beginner" ||
            data.Name != "playbackTutorial") return false;
        if (level.InCredits)
            throw new InvalidOperationException("SJ heartside credits are outside the selected normal-lobby profile");
        return true;
    }
}

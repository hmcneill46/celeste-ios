using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace Celeste.Mod;

// Pinned Everest Level.DefaultSpawnPoint and LevelData.CheckForDefaultSpawn.
// Resolve the mounted map as well as registered areas so the isolated debug
// launcher follows the same authored spawn order.
internal static class AppleEverestDefaultSpawn
{
    internal static Vector2 Get(Level level) =>
        AppleEverestMapBinding.ForSession(level.Session) == null
            ? level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Bottom))
            : level.Session.LevelData.DefaultSpawn ?? level.Session.LevelData.Spawns[0];

    internal static void Record(LevelData level, Dictionary<string, object> attributes, Vector2 coordinates)
    {
        if (level.DefaultSpawn == null && attributes.TryGetValue("isDefaultSpawn", out object isDefaultSpawn) &&
            Convert.ToBoolean(isDefaultSpawn, CultureInfo.InvariantCulture))
            level.DefaultSpawn = coordinates;
    }
}

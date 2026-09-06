#nullable disable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// FancyTileEntities 1.6.2, exact selected profiles. The constructor retains the
// same tile map passed to SolidTiles instead of reflecting its private field.
internal sealed class AppleEverestFancySolidTiles : SolidTiles
{
    private readonly VirtualMap<char> tileMap;
    private readonly bool blendEdges;
    private readonly int seed;

    internal AppleEverestFancySolidTiles(EntityData data, Vector2 offset)
        : this(data, offset, GenerateTileMap(data.Attr("tileData"))) { }

    private AppleEverestFancySolidTiles(EntityData data, Vector2 offset, VirtualMap<char> map)
        : base(data.Position + offset, map)
    {
        tileMap = map;
        blendEdges = data.Bool("blendEdges");
        seed = data.Int("randomSeed");
        if (!data.Bool("loadGlobally")) RemoveTag(Tags.Global);
        Remove(Tiles);
        Remove(AnimatedTiles);
        for (int x = 0; x < map.Columns; x++)
            for (int y = 0; y < map.Rows; y++)
                if (map.AnyInSegmentAtTile(x, y) && map[x, y] != '0')
                    Add(new LightOcclude(new Rectangle(x * 8, y * 8, 8, 8)));
    }

    private static VirtualMap<char> GenerateTileMap(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Empty FancySolidTiles tile map");
        string[] rows = Array.ConvertAll(text.Split(text.Contains(',') ? ',' : '\n'), row => row.Trim());
        int width = rows.Max(row => row.Length);
        char[,] map = new char[width, rows.Length];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < rows.Length; y++)
                map[x, y] = x < rows[y].Length ? rows[y][x] : '0';
        return new VirtualMap<char>(map, '\0');
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Calc.PushRandom(seed != 0 ? seed : Calc.Random.Next());
        Autotiler.Generated generated;
        try
        {
            if (blendEdges)
            {
                Level level = (Level)scene;
                Rectangle bounds = level.Session.MapData.TileBounds;
                generated = GFX.FGAutotiler.AppleEverestFancyOverlay(tileMap,
                    (int)X / 8 - bounds.Left, (int)Y / 8 - bounds.Top, level.SolidsData);
            }
            else generated = GFX.FGAutotiler.GenerateMap(tileMap, default(Autotiler.Behaviour));
        }
        finally { Calc.PopRandom(); }
        Tiles = generated.TileGrid;
        Tiles.VisualExtend = 1;
        Add(Tiles);
        Add(AnimatedTiles = generated.SpriteOverlay);
    }
}

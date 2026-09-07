#nullable disable
using System;
using System.Xml;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

// Bounded static implementation of the selected stable-1.6458.0 terrain
// contract. Ordinary 3x3 definitions keep the accepted parser and matcher.
public partial class Autotiler
{
    private static MTexture ResolveTerrainTexture(string key) =>
        GFX.Game.Has(key) ? GFX.Game[key] : GFX.Game["__fallback"];

    public Generated GenerateOverlay(char id, int x, int y, int tilesX, int tilesY, VirtualMap<char> mapData)
    {
        // Pinned Everest materializes crossed virtual segments before the
        // canonical Generate traversal, including previously empty segments.
        for (int column = x; column < x + tilesX; column = (column / 50 + 1) * 50)
            for (int row = y; row < y + tilesY; row = (row / 50 + 1) * 50)
                if (!mapData.AnyInSegmentAtTile(column, row)) mapData[column, row] = mapData.EmptyValue;
        return AppleEverestOriginalGenerateOverlay(id, x, y, tilesX, tilesY, mapData);
    }

    private partial class TerrainType
    {
        internal int ScanWidth = 3;
        internal int ScanHeight = 3;
        internal bool TemplateRead;
    }

    private void ReadInto(TerrainType data, Tileset tileset, XmlElement xml)
    {
        AppleEverestTileMaskRules.ValidateDefinition(xml, out int width, out int height);
        if (data.TemplateRead && (width != data.ScanWidth || height != data.ScanHeight))
            throw new InvalidOperationException("Autotiler copies must use the same scan dimensions.");
        data.ScanWidth = width; data.ScanHeight = height; data.TemplateRead = true;
        if (data.ScanWidth == 3 && data.ScanHeight == 3)
            AppleEverestOriginalReadInto(data, tileset, xml);
        else
            ReadIntoBoundedTemplate(data, tileset, xml);

        // Everest applies this on every read, including a copied template.
        char id = xml.AttrChar("id");
        if (xml.HasAttr("sound")) SurfaceIndex.TileToIndex[id] = xml.AttrInt("sound");
        else if (!SurfaceIndex.TileToIndex.ContainsKey(id)) SurfaceIndex.TileToIndex[id] = 0;
        if (xml.HasAttr("debris")) data.Debris = xml.Attr("debris");
    }

    private void ReadIntoBoundedTemplate(TerrainType data, Tileset tileset, XmlElement xml)
    {
        foreach (XmlNode node in xml.ChildNodes)
        {
            if (node is not XmlElement rule) continue;
            string mask = rule.Attr("mask");
            Tiles tiles;
            if (mask == "center") tiles = data.Center;
            else if (mask == "padding") tiles = data.Padded;
            else
            {
                Masked entry = new Masked
                {
                    Mask = AppleEverestTileMaskRules.ParseMask(mask, data.ScanWidth, data.ScanHeight)
                };
                data.Masked.Add(entry);
                tiles = entry.Tiles;
            }
            foreach (string coordinates in rule.Attr("tiles").Split(';'))
            {
                string[] pair = coordinates.Split(',');
                tiles.Textures.Add(tileset[int.Parse(pair[0]), int.Parse(pair[1])]);
            }
            if (rule.HasAttr("sprites"))
            {
                foreach (string sprite in rule.Attr("sprites").Split(',')) tiles.OverlapSprites.Add(sprite);
                tiles.HasOverlays = true;
            }
        }
        // Keep the pinned List.Sort comparator and equal-priority behavior.
        // The selected masks contain no custom filters or "not this" cells.
        data.Masked.Sort((a, b) =>
        {
            int aAny = 0, bAny = 0;
            for (int index = 0; index < data.ScanWidth * data.ScanHeight; index++)
            {
                if (a.Mask[index] == 2) aAny++;
                if (b.Mask[index] == 2) bAny++;
            }
            return aAny - bAny;
        });
    }

    private Tiles TileHandler(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour)
    {
        char tile = GetTile(mapData, x, y, forceFill, forceID, behaviour);
        if (IsEmpty(tile)) return null;
        // Missing definitions fail closed; product preflight checks every used
        // tile ID before this path can be reached on a device.
        TerrainType terrain = lookup[tile];
        if (terrain.ScanWidth == 3 && terrain.ScanHeight == 3)
            return AppleEverestOriginalTileHandler(mapData, x, y, forceFill, forceID, behaviour);

        int width = terrain.ScanWidth, height = terrain.ScanHeight;
        Span<byte> neighborhood = stackalloc byte[AppleEverestTileMaskRules.MaximumCells];
        bool filled = true;
        int index = 0;
        for (int offsetY = 0; offsetY < height; offsetY++)
        {
            for (int offsetX = 0; offsetX < width; offsetX++)
            {
                bool present = BoundedTilePresent(terrain, mapData, x + offsetX - width / 2,
                    y + offsetY - height / 2, forceFill, behaviour);
                // Deliberately retain pinned Everest's unshifted same-level
                // coordinates in this custom-template edge case.
                if (!present && behaviour.EdgesIgnoreOutOfLevel &&
                    !CheckForSameLevel(x, y, x + offsetX, y + offsetY)) present = true;
                neighborhood[index++] = (byte)(present ? 1 : 0);
                if (!present) filled = false;
            }
        }
        if (filled)
        {
            int distanceX = 1 + width / 2, distanceY = 1 + height / 2;
            return BoundedPaddingPresent(terrain, mapData, x, y, x - distanceX, y, forceFill, behaviour) &&
                BoundedPaddingPresent(terrain, mapData, x, y, x + distanceX, y, forceFill, behaviour) &&
                BoundedPaddingPresent(terrain, mapData, x, y, x, y - distanceY, forceFill, behaviour) &&
                BoundedPaddingPresent(terrain, mapData, x, y, x, y + distanceY, forceFill, behaviour)
                ? terrain.Center : terrain.Padded;
        }
        foreach (Masked candidate in terrain.Masked)
        {
            bool matched = true;
            for (int cell = 0; cell < width * height; cell++)
                if (candidate.Mask[cell] != 2 && candidate.Mask[cell] != neighborhood[cell])
                {
                    matched = false;
                    break;
                }
            if (matched) return candidate.Tiles;
        }
        return null;
    }

    private bool BoundedPaddingPresent(TerrainType terrain, VirtualMap<char> mapData,
        int x, int y, int adjacentX, int adjacentY, Rectangle forceFill, Behaviour behaviour) =>
        CheckTile(terrain, mapData, adjacentX, adjacentY, forceFill, behaviour) ||
        (behaviour.PaddingIgnoreOutOfLevel && !CheckForSameLevel(x, y, adjacentX, adjacentY));

    // Pinned custom templates use TryGetTile for neighborhood presence, which
    // bypasses FancyTileEntities' CheckTile hook. The distance-three padding
    // cross intentionally still uses CheckTile above.
    private bool BoundedTilePresent(TerrainType terrain, VirtualMap<char> mapData,
        int x, int y, Rectangle forceFill, Behaviour behaviour)
    {
        if (forceFill.Contains(x, y)) return true;
        if (mapData == null) return behaviour.EdgesExtend;
        if (x < 0 || y < 0 || x >= mapData.Columns || y >= mapData.Rows)
        {
            if (!behaviour.EdgesExtend) return false;
            x = Calc.Clamp(x, 0, mapData.Columns - 1);
            y = Calc.Clamp(y, 0, mapData.Rows - 1);
        }
        char tile = mapData[x, y];
        return !IsEmpty(tile) && !terrain.Ignore(tile);
    }
}

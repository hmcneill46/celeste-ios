#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestCollabSticker
{
    internal readonly string Path;
    internal readonly float X, Y, Rotation, Scale;
    internal readonly string[] FinishedMaps;
    internal AppleEverestCollabSticker(string path, float x, float y, float rotation, float scale, string[] finishedMaps)
    { Path = path; X = x; Y = y; Rotation = rotation; Scale = scale; FinishedMaps = finishedMaps; }
}

internal sealed class AppleEverestCollabJournalCover : OuiJournalCover
{
    private static readonly Dictionary<string, MTexture> textures = new();
    private readonly List<AppleEverestCollabSticker> stickers = new();
    private static IEnumerable<AppleEverestCollabSticker> Metadata(string sid) =>
        sid == "StrawberryJam2021/0-Lobbies/1-Beginner" ? AppleEverestCollabMapMetadata.BeginnerStickers :
        sid == AppleEverestFactoryCanaryStickers.LobbySid ? AppleEverestFactoryCanaryStickers.Entries : Array.Empty<AppleEverestCollabSticker>();
    private static bool Eligible(AppleEverestCollabSticker sticker) => sticker.FinishedMaps.All(sid =>
        AppleEverestCollabPresentation.Area(sid) != null && AppleEverestCollabPresentation.Stats(AppleEverestCollabPresentation.Area(sid)).Modes[0].Completed);
    internal static void PrepareLevel(Session session)
    {
        foreach (MTexture texture in textures.Values) texture.Unload();
        textures.Clear();
        foreach (AppleEverestCollabSticker sticker in Metadata(session.Area.SID))
        {
            if (textures.ContainsKey(sticker.Path) || !Eligible(sticker)) continue;
            ModAsset asset = Everest.Content.Map["Graphics/Atlases/Stickers/" + sticker.Path];
            VirtualTexture texture = VirtualContent.CreateDeferredTexture(asset.LogicalPath);
            texture.Name = asset.PathVirtual;
            textures.Add(sticker.Path, new MTexture(texture));
        }
    }
    internal AppleEverestCollabJournalCover(OuiJournal journal) : base(journal)
    {
        foreach (AppleEverestCollabSticker sticker in Metadata(SaveData.Instance.CurrentSession.Area.SID))
            if (textures.ContainsKey(sticker.Path) && Eligible(sticker)) stickers.Add(sticker);
    }
    public override void Redraw(VirtualRenderTarget buffer)
    {
        base.Redraw(buffer);
        Draw.SpriteBatch.Begin();
        foreach (AppleEverestCollabSticker sticker in stickers)
            textures[sticker.Path].DrawCentered(new Vector2(sticker.X, sticker.Y), Color.White, sticker.Scale,
                (float)((double)sticker.Rotation * Math.PI / 180.0));
        Draw.SpriteBatch.End();
    }
}

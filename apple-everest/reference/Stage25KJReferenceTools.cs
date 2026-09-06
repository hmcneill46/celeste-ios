// Desktop-only navigation and observation of original helper behavior.
using System;
using System.IO;
using System.Linq;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace AppleEverest.Reference;

public sealed class ReferenceTools : EverestModule
{
    public override void Load()
    {
        // Keep this owned comparison separate from other desktop references.
        Celeste.Mod.Core.CoreModule.Settings.DebugRCPort = 32271;
    }
    public override void Unload() { }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, FMOD.Studio.EventInstance snapshot)
    {
        menu.Add(new TextMenu.SubHeader("STAGE 25K-J REFERENCE ROOMS"));
        foreach (AreaData area in AreaData.Areas.Where(a => a.SID.StartsWith("AppleEverestStage25KJ/FactoryProfiles/")))
        {
            string group = area.SID.Split('/').Last();
            foreach (LevelData room in area.Mode[0].MapData.Levels)
            {
                string level = room.Name;
                menu.Add(new TextMenu.Button(group + " / " + level).Pressed(() => Open(group, level)));
            }
        }
        foreach (string group in new[] { "Lobby", "Sideways", "Dialogue", "Effects", "Audio" })
            menu.Add(new TextMenu.Button(group).Pressed(() => Open(group)));
    }

    [Command("kj", "Open K-J reference: group and optional room, e.g. kj Frost factory_01")]
    public static void Open(string group = "CrystalCave", string room = null)
    {
        string sid = group.ToLowerInvariant() switch {
            "lobby" => "AppleEverestStage25KJ/0-Lobbies/1-Fixture",
            "sideways" => "AppleEverest/Stage25KJMax",
            "dialogue" => "AppleEverest/Stage25KH",
            "effects" => "AppleEverest/Stage25KE",
            "audio" => "AppleEverest/Stage25KF",
            _ => "AppleEverestStage25KJ/FactoryProfiles/" + group
        };
        AreaData area = AreaData.Areas.SingleOrDefault(a => a.SID.Equals(sid, StringComparison.OrdinalIgnoreCase));
        if (area == null) { Engine.Commands.Log("Unknown reference group: " + group); return; }
        room ??= area.Mode[0].MapData.StartLevel().Name;
        Engine.Commands.ExecuteCommand("load", new[] { area.SID, room });
    }

    [Command("kj_capture", "Save the current in-game reference frame to the isolated reference tmp directory")]
    public static void Capture()
    {
        if (Engine.Scene is not Level || GameplayBuffers.Level == null) return;
        string path = Path.Combine(Environment.GetEnvironmentVariable("EVEREST_TMPDIR"), "reference-frame.png");
        using FileStream output = File.Create(path);
        ((RenderTarget2D)GameplayBuffers.Level).SaveAsPng(output, 320, 180);
        Engine.Commands.Log("Saved reference-frame.png");
    }

    [Command("kj_probe", "Report the current reference room and interactive sprite state")]
    public static void Probe()
    {
        if (Engine.Scene is not Level level) { Engine.Commands.Log("No level"); return; }
        Engine.Commands.Log("KJ_REFERENCE " + level.Session.Area.SID + " / " + level.Session.Level);
        foreach (Entity entity in level.Entities.Where(e => e.Get<TalkComponent>() != null ||
                     e.GetType().Name.Contains("ItemCrystal")))
        {
            string textures = string.Join(",", entity.Components.OfType<Image>().Select(i => i.Texture?.AtlasPath));
            TalkComponent talk = entity.Get<TalkComponent>();
            Engine.Commands.Log(entity.GetType().Name + " at " + entity.Position + " textures=" + textures +
                (talk == null ? "" : " talk=" + talk.Bounds));
        }
    }
}

// These are the same project-owned K-F probes used on Apple. All helper
// behavior they exercise still comes from the original desktop packages.
[CustomEntity("appleEverest/stage25kfAudio")]
public sealed class ReferenceAudioCanary : Entity
{
    public ReferenceAudioCanary(EntityData data, Vector2 offset) : base(data.Position + offset) { }
    public override void Added(Scene scene)
    {
        base.Added(scene);
        foreach (string path in new[] { "event:/sj21_jamjar-blue", "event:/sj21_BegLobby",
                     "event:/sj21_bingovergoogle", "event:/sj21_levelselect" }) Audio.Play(path, Position);
        RemoveSelf();
    }
}

[CustomEntity("appleEverest/stage25kfDepthTarget")]
public sealed class ReferenceDepthTarget : Entity
{
    public ReferenceDepthTarget(EntityData data, Vector2 offset) : base(data.Position + offset)
    { Collider = new Hitbox(Math.Max(8, data.Width), Math.Max(8, data.Height)); Depth = -100; }
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (Depth != -20000) throw new InvalidOperationException("Reference Crystalline depth probe did not change depth");
    }
    public override void Render()
    {
        Draw.Rect(X, Y, Width, Height, Color.LimeGreen * 0.8f);
        Draw.HollowRect(X, Y, Width, Height, Color.White);
    }
}

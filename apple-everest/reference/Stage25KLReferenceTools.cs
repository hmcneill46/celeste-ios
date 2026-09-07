// Desktop-only navigation. Every gameplay behavior comes from original mods.
using System;
using System.Linq;
using Celeste;
using Celeste.Mod;
using Monocle;

namespace AppleEverest.Reference;

public sealed class RealSliceReferenceTools : EverestModule
{
    private const string Lobby = "StrawberryJam2021/0-Lobbies/1-Beginner";
    public override void Load() => Celeste.Mod.Core.CoreModule.Settings.DebugRCPort = 32272;
    public override void Unload() { }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, FMOD.Studio.EventInstance snapshot)
    {
        menu.Add(new TextMenu.SubHeader("STAGE 25K-L ORIGINAL REFERENCE"));
        if (SaveData.Instance == null) menu.Add(new TextMenu.SubHeader("Select a save file to begin"));
        menu.Add(new TextMenu.Button("Play unchanged Beginner lobby") { Disabled = SaveData.Instance == null }.Pressed(OpenLobby));
    }

    [Command("kl", "Open the unchanged Beginner lobby using its authored intro")]
    public static void OpenLobby()
    {
        if (SaveData.Instance == null)
        {
            Engine.Commands.Log("Select a save file first, then use Mod Options or kl.");
            return;
        }
        AreaData area = AreaData.Areas.Single(a => a.SID == Lobby);
        LevelEnter.Go(new Session(new AreaKey(area.ID)), fromSaveData: false);
    }
}

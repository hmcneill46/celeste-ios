using System;
using Celeste;
using Celeste.Mod;

namespace AppleEverest.Canaries;

public sealed class CanarySettings : EverestModuleSettings
{
    public bool BannerEnabled { get; set; } = true;
}

public sealed class CanarySaveData : EverestModuleSaveData
{
    public int RoomEntries { get; set; }
}

public sealed class CanarySession : EverestModuleSession
{
    public bool SawCanaryRoom { get; set; }
}

public sealed class CanaryCoreModule : EverestModule
{
    public override Type SettingsType => typeof(CanarySettings);
    public override Type SaveDataType => typeof(CanarySaveData);
    public override Type SessionType => typeof(CanarySession);

    public override void Load()
    {
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
    }

    public override void Unload()
    {
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
    }

    private static void OnLoadLevel(Level level, Player.IntroTypes intro, bool fromLoader)
    {
        CanarySettings settings = AppleEverestStaticRuntime.GetSettings<CanarySettings>("AppleEverestCanaryCore");
        if (settings.BannerEnabled && level.Session.MapData.ModeData.Path == "AppleEverest/Canary")
            AppleEverestStaticRuntime.AttachCanaryBanner(level, "A");
    }

}

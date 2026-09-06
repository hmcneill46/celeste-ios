#nullable disable
namespace Celeste;

public partial class HeartGemDoor
{
    internal float AppleEverestCollabOpenPercent => openPercent;
    internal void AppleEverestCollabSkipOpening()
    {
        openPercent = 1f;
        Opened = true;
        Counter = Requires;
        TopSolid.Bottom = Y - openDistance;
        BotSolid.Top = Y + openDistance;
    }
}

public partial class Level
{
    internal void AppleEverestCollabUnpause() { Paused = false; unpauseTimer = .15f; }
}

#nullable disable
namespace Celeste.Mod;

// Original CollabSession shape. Its owner is the generated typed module;
// serialization uses the existing paired module-session snapshots.
internal sealed class AppleEverestCollabSession : EverestModuleSession
{
    public string LobbySID { get; set; }
    public string LobbyRoom { get; set; }
    public float LobbySpawnPointX { get; set; }
    public float LobbySpawnPointY { get; set; }
    public string GymExitMapSID { get; set; }
    public bool GymExitSaveAllowed { get; set; }
    public bool SaveAndReturnToLobbyAllowed { get; set; }
    internal AppleEverestCollabSession Copy()
    {
        var result = new AppleEverestCollabSession();
        CopyTo(result);
        return result;
    }
    internal void CopyTo(AppleEverestCollabSession target)
    {
        if (target == null) return;
        target.LobbySID = LobbySID;
        target.LobbyRoom = LobbyRoom;
        target.LobbySpawnPointX = LobbySpawnPointX;
        target.LobbySpawnPointY = LobbySpawnPointY;
        target.GymExitMapSID = GymExitMapSID;
        target.GymExitSaveAllowed = GymExitSaveAllowed;
        target.SaveAndReturnToLobbyAllowed = SaveAndReturnToLobbyAllowed;
    }
}

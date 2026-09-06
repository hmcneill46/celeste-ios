using System.Text;
using Celeste.Mod;

internal static class CollabSaveStateTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value) { if (!value) throw new Exception("Collab save-state regression"); count++; }
        var codec = AppleEverestCollabDurability.Adapter;
        var empty = (AppleEverestCollabSaveData)codec.DeserializeSave!(Encoding.UTF8.GetBytes("{}"), 2);
        Check(empty.Index == 2 && empty.VisitedLobbyPositions.Count == 0 && empty.OpenedMiniHeartDoors.Count == 0 &&
            empty.CombinedRainbowBerries.Count == 0 && !empty.RevealMap && !empty.PauseVisitingPoints && !empty.ShowVisitedPoints);
        var save = new AppleEverestCollabSaveData();
        save.SessionsPerLevel.Add("map", "session-text");
        save.ModSessionsPerLevel.Add("map", new() { ["mod"] = "session-yaml" });
        save.ModSessionsPerLevelBinary.Add("map", new() { ["mod"] = "AQI=" });
        save.VisitedLobbyPositions.Add("map.room", "AQABAAAABQAJAAEAAAABMQ==");
        save.LearnedTech.Add("chapter", new() { "wallbounce", "wavedash" });
        save.OpenedMiniHeartDoors.Add("map:gate"); save.CombinedRainbowBerries.Add("map");
        save.SpeedBerryPBs.Add("map", long.MaxValue - 1); save.CompletedWarpPedestalSIDs.Add("map");
        save.SpeedberryOptionMessageShown = save.RevealMap = save.PauseVisitingPoints = save.ShowVisitedPoints = true;
        byte[] bytes = codec.SerializeSave!(save);
        var restored = (AppleEverestCollabSaveData)codec.DeserializeSave(bytes, 1);
        Check(restored.Index == 1 && restored.SessionsPerLevel["map"] == "session-text");
        Check(restored.ModSessionsPerLevel["map"]["mod"] == "session-yaml" && restored.ModSessionsPerLevelBinary["map"]["mod"] == "AQI=");
        Check(restored.VisitedLobbyPositions["map.room"] == save.VisitedLobbyPositions["map.room"]);
        Check(restored.LearnedTech["chapter"].SetEquals(save.LearnedTech["chapter"]));
        Check(restored.OpenedMiniHeartDoors.Contains("map:gate") && restored.CombinedRainbowBerries.Contains("map"));
        Check(restored.SpeedBerryPBs["map"] == long.MaxValue - 1 && restored.CompletedWarpPedestalSIDs.Contains("map"));
        Check(restored.SpeedberryOptionMessageShown && restored.RevealMap && restored.PauseVisitingPoints && restored.ShowVisitedPoints);
        restored.LearnedTech["chapter"].Clear(); restored.LearnedTech["chapter"].UnionWith(new[] { "wavedash", "wallbounce" });
        Check(bytes.SequenceEqual(codec.SerializeSave(restored)));
        save.ModSessionsPerLevel["map"]["mod"] = "changed"; save.VisitedLobbyPositions.Clear();
        Check(restored.ModSessionsPerLevel["map"]["mod"] == "session-yaml" && restored.VisitedLobbyPositions.Count == 1 && empty.VisitedLobbyPositions.Count == 0);
        var module = new AppleEverestCollabModule { _SaveData = restored };
        Check(ReferenceEquals(AppleEverestCollabModule.Instance.SaveData, restored));
        module._SaveData = empty;
        Check(AppleEverestCollabModule.Instance.SaveData.Index == 2 && restored.OpenedMiniHeartDoors.Contains("map:gate"));
        foreach (string invalid in new[] { "{\"VisitedLobbyPositions\":true}", "{\"SpeedBerryPBs\":{\"a\":2.5}}", "{\"OpenedMiniHeartDoors\":[7]}", "{\"RevealMap\":1}", "{\"VisitedLobbyPositions\":{\"a\":\"1\",\"a\":\"2\"}}" })
        {
            bool rejected = false;
            try { codec.DeserializeSave(Encoding.UTF8.GetBytes(invalid), 0); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or FormatException) { rejected = true; }
            Check(rejected);
        }
        var route = new AppleEverestCollabSession { LobbySID = "fixture/lobby", LobbyRoom = "room",
            LobbySpawnPointX = -424.5f, LobbySpawnPointY = 552.25f, GymExitMapSID = "fixture/gym", GymExitSaveAllowed = true, SaveAndReturnToLobbyAllowed = true };
        var routeBytes = codec.SerializeSession!(route);
        var loadedRoute = (AppleEverestCollabSession)codec.DeserializeSession!(routeBytes, 2);
        Check(loadedRoute.LobbySID == route.LobbySID && loadedRoute.LobbyRoom == route.LobbyRoom && loadedRoute.LobbySpawnPointX == -424.5f && loadedRoute.LobbySpawnPointY == 552.25f);
        Check(loadedRoute.GymExitMapSID == "fixture/gym" && loadedRoute.GymExitSaveAllowed && loadedRoute.SaveAndReturnToLobbyAllowed);
        Check(routeBytes.SequenceEqual(codec.SerializeSession(loadedRoute)));
        var blankRoute = (AppleEverestCollabSession)codec.DeserializeSession(Encoding.UTF8.GetBytes("{}"), 0);
        Check(blankRoute.LobbySID == null && blankRoute.GymExitMapSID == null && blankRoute.LobbySpawnPointX == 0f && !blankRoute.SaveAndReturnToLobbyAllowed);
        module._Session = loadedRoute;
        Check(ReferenceEquals(AppleEverestCollabModule.Instance.Session, loadedRoute));
        var copiedRoute = loadedRoute.Copy();
        copiedRoute.LobbySpawnPointY = 100f;
        Check(loadedRoute.LobbySpawnPointY == 552.25f);
        copiedRoute.CopyTo(blankRoute);
        Check(blankRoute.LobbySpawnPointY == 100f && blankRoute.LobbySID == "fixture/lobby");
        foreach (string invalid in new[] { "{\"LobbySID\":true}", "{\"LobbySpawnPointX\":\"1\"}", "{\"SaveAndReturnToLobbyAllowed\":7}" })
        {
            bool rejected = false;
            try { codec.DeserializeSession(Encoding.UTF8.GetBytes(invalid), 0); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or FormatException) { rejected = true; }
            Check(rejected);
        }
        return count;
    }
}

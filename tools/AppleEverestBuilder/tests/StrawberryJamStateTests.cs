using System.Text;
using Celeste.Mod;

internal static class StrawberryJamStateTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value) { if (!value) throw new Exception("SJ typed state regression"); count++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidOperationException or System.IO.InvalidDataException or System.Text.Json.JsonException)
            { count++; return; }
            throw new Exception("SJ invalid state was accepted");
        }
        var adapter = AppleEverestStrawberryJamModuleDurability.Adapter;
        var defaults = (AppleEverestStrawberryJamSession)adapter.DeserializeSession!(Encoding.UTF8.GetBytes("{}"), 4);
        Check(defaults.Index == 4 && defaults.CassetteBlocksDisabled && defaults.CassetteBlocksLastParameter == "" &&
            defaults.RainDensityData.Density == 1 && defaults.RainDensityData.StartDensity == 1 &&
            defaults.RainDensityData.EndDensity == 1 && defaults.RainDensityData.Duration == 0 &&
            defaults.MusicWonkyBeatIndex == 0 && defaults.ExpiringDashRemainingTime == 0 && !defaults.ZeroG);
        var settings = new AppleEverestStrawberryJamSettings();
        Check(!settings.DisplayDashSequence && settings.TogglePlaybacks.DefaultButton == Microsoft.Xna.Framework.Input.Buttons.Back &&
            settings.TogglePlaybacks.DefaultKeys.SequenceEqual(new[] { Microsoft.Xna.Framework.Input.Keys.Tab }));
        var first = new AppleEverestStrawberryJamSaveData();
        first.ModifiedThemeMaps.UnionWith(new[] { "z", "a" });
        first.FilledJamJarSIDs.UnionWith(new[] { "map/z", "map/a" });
        var second = new AppleEverestStrawberryJamSaveData();
        second.ModifiedThemeMaps.UnionWith(new[] { "a", "z" });
        second.FilledJamJarSIDs.UnionWith(new[] { "map/a", "map/z" });
        byte[] bytes = adapter.SerializeSave!(first);
        Check(bytes.SequenceEqual(adapter.SerializeSave(second)));
        var restored = (AppleEverestStrawberryJamSaveData)adapter.DeserializeSave!(bytes, 7);
        Check(restored.Index == 7 && restored.ModifiedThemeMaps.SetEquals(first.ModifiedThemeMaps) &&
            restored.FilledJamJarSIDs.SetEquals(first.FilledJamJarSIDs));
        var unknown = (AppleEverestStrawberryJamSaveData)adapter.DeserializeSave!(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"futureField\":123,\"FilledJamJarSIDs\":[\"a\",\"a\"]}"), 0);
        Check(unknown.FilledJamJarSIDs.SetEquals(new[] { "a" }) && unknown.ModifiedThemeMaps.Count == 0);
        Reject(() => adapter.DeserializeSave!(Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"), 0));
        Reject(() => adapter.DeserializeSession!(Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"), 0));
        Reject(() => adapter.DeserializeSave!(Encoding.UTF8.GetBytes("{\"FilledJamJarSIDs\":true}"), 0));
        Reject(() => adapter.DeserializeSession!(Encoding.UTF8.GetBytes("{\"RainDensityData\":[]}"), 0));
        Reject(() => adapter.DeserializeSave!(Encoding.UTF8.GetBytes("{\"FilledJamJarSIDs\":[\"" + new string('a', 513) + "\"]}"), 0));
        defaults.MusicWonkyBeatIndex = 25;
        defaults.CassetteWonkyBeatIndex = 11;
        defaults.RainDensityData.Density = .5f;
        defaults.RainDensityData.Duration = .25f;
        var session = (AppleEverestStrawberryJamSession)adapter.DeserializeSession!(adapter.SerializeSession!(defaults), 4);
        Check(session.MusicWonkyBeatIndex == 25 && session.CassetteWonkyBeatIndex == 11 &&
            session.RainDensityData.Density == .5f && session.RainDensityData.Duration == .25f);
        Check(!Encoding.UTF8.GetString(adapter.SerializeSession!(session)).Contains("DashSequenceDisplay"));
        var module = new AppleEverestStrawberryJamModule();
        Check(module.LifecycleState == 0 && ReferenceEquals(AppleEverestStrawberryJamModule.Instance, module));
        module.Load(); Check(module.LifecycleState == 1);
        module.Initialize(); Check(module.LifecycleState == 2);
        module.LoadContent(true); Check(module.LifecycleState == 3 && module.SpriteBank != null);
        module.Unload(); Check(module.LifecycleState == 0);
        return count;
    }
}

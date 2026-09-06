using System.Text;
using Celeste.Mod;

internal static class CoreSessionStateTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value) { if (!value) throw new Exception("Core session state regression"); count++; }
        var adapter = AppleEverestCoreDurability.Adapter;
        var empty = (AppleEverestCoreSession)adapter.DeserializeSession!(Encoding.UTF8.GetBytes("{}"), 2);
        Check(empty.WhateverElseCount == 1337 && empty.AttachedDecals.Count == 0 && empty.Index == 2);
        var first = new AppleEverestCoreSession { WhateverElseCount = 42 };
        first.AttachedDecals.UnionWith(new[] { "decals/soap ||-2.5||12", "decals/grass||10||20" });
        var second = new AppleEverestCoreSession { WhateverElseCount = 42 };
        second.AttachedDecals.UnionWith(first.AttachedDecals.Reverse());
        byte[] saved = adapter.SerializeSession!(first);
        Check(saved.SequenceEqual(adapter.SerializeSession(second)));
        var restored = (AppleEverestCoreSession)adapter.DeserializeSession(saved, 7);
        Check(restored.Index == 7 && restored.WhateverElseCount == 42 && restored.AttachedDecals.SetEquals(first.AttachedDecals));
        first.AttachedDecals.Clear();
        Check(restored.AttachedDecals.Count == 2 && empty.AttachedDecals.Count == 0);
        var module = new AppleEverestCoreModule { _Session = restored };
        Check(ReferenceEquals(AppleEverestCoreModule.Session, restored));
        module._Session = new AppleEverestCoreSession();
        Check(AppleEverestCoreModule.Session.AttachedDecals.Count == 0 && restored.AttachedDecals.Count == 2);
        var deduplicated = (AppleEverestCoreSession)adapter.DeserializeSession(
            Encoding.UTF8.GetBytes("{\"AttachedDecals\":[\"a\",\"a\"],\"future\":true}"), 0);
        Check(deduplicated.AttachedDecals.SetEquals(new[] { "a" }));
        foreach (string invalid in new[] { "{\"AttachedDecals\":true}", "{\"AttachedDecals\":[7]}", "{\"WhateverElseCount\":2.5}" })
        {
            bool rejected = false;
            try { adapter.DeserializeSession(Encoding.UTF8.GetBytes(invalid), 0); }
            catch (System.IO.InvalidDataException) { rejected = true; }
            Check(rejected);
        }
        Check(adapter.SerializeSave == null && adapter.DeserializeSave == null);
        return count;
    }
}

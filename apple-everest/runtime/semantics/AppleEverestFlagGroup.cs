#nullable disable
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Immutable map-processor output, emitted only after the host has validated the
// original map, all group members, their profiles, and the creation closure.
internal sealed class AppleEverestFlagGroup
{
    internal readonly string Sid, Room, Flag, SourceSha256;
    internal readonly int Mode;
    internal readonly bool LegacyMode, GroupPersistence;
    internal readonly AppleEverestFlagMember[] Members;

    internal AppleEverestFlagGroup(string sid, int mode, string room, string flag,
        string sourceSha256, bool legacyMode, bool groupPersistence, AppleEverestFlagMember[] members)
    {
        Sid = sid; Mode = mode; Room = room; Flag = flag; SourceSha256 = sourceSha256;
        LegacyMode = legacyMode; GroupPersistence = groupPersistence; Members = members;
    }

    internal static AppleEverestFlagGroup Bind(EntityData data, Vector2 offset, EntityID id, bool gate)
    {
        // Keep the production guard at the factory entry and at this direct
        // constructor boundary. A new caller cannot bypass the authored guard.
        AppleEverestSelectedProfileGuard.Entity(data.Name, data);
        AppleEverestFlagGroup match = null;
        foreach (AppleEverestFlagGroup group in GeneratedAppleEverestFlagGroups.Groups)
        {
            if (group.Room != id.Level || group.Flag != data.Attr("flag")) continue;
            foreach (AppleEverestFlagMember member in group.Members)
            {
                if (member.Id != id.ID || member.Gate != gate) continue;
                if (match != null || data.Level == null || data.Level.Name != group.Room ||
                    data.ID != id.ID || data.Position != member.Position || offset != data.Level.Position ||
                    data.Bool("persistent") != member.Persistent || data.Bool("inverted") ||
                    !group.LegacyMode || !group.GroupPersistence)
                    throw Outside();
                match = group;
            }
        }
        return match ?? throw Outside();
    }

    internal Level Require(Scene scene, EntityData source)
    {
        if (scene is not Level level || level.Session == null || level.Session.Area.SID != Sid ||
            (int)level.Session.Area.Mode != Mode || source.Level == null ||
            !ReferenceEquals(level.Session.MapData.Get(Room), source.Level))
            throw Outside();
        return level;
    }

    internal Level RequireCreation(Scene scene, EntityData source)
    {
        Level level = Require(scene, source);
        if (level.Session.Level != Room) throw Outside();
        return level;
    }

    internal void ValidateMembers(Scene scene)
    {
        int total = 0;
        foreach (AppleEverestFlagMember member in Members)
        {
            int count = 0;
            if (member.Gate)
            {
                foreach (Entity entity in scene.Tracker.GetEntities<AppleEverestFlagSwitchGate>())
                    if (entity is AppleEverestFlagSwitchGate gate && ReferenceEquals(gate.Group, this) &&
                        gate.SourceId == member.Id) count++;
            }
            else
            {
                foreach (Entity entity in scene.Tracker.GetEntities<AppleEverestFlagTouchSwitch>())
                    if (entity is AppleEverestFlagTouchSwitch touch && ReferenceEquals(touch.Group, this) &&
                        touch.SourceId == member.Id) count++;
            }
            if (count != 1) throw new InvalidOperationException("generated flag group member is missing or duplicated");
        }
        foreach (Entity entity in scene.Tracker.GetEntities<AppleEverestFlagTouchSwitch>())
            if (entity is AppleEverestFlagTouchSwitch touch && ReferenceEquals(touch.Group, this)) total++;
        foreach (Entity entity in scene.Tracker.GetEntities<AppleEverestFlagSwitchGate>())
            if (entity is AppleEverestFlagSwitchGate gate && ReferenceEquals(gate.Group, this)) total++;
        if (total != Members.Length) throw new InvalidOperationException("generated flag group census differs");
    }

    internal static void ValidateCreation(Scene scene, Entity entity, bool beforeAdded = false)
    {
        // The owning EntityList's scene is authoritative during background
        // loading. Engine.Scene can still point to a different level/loader.
        if (entity is AppleEverestFlagTouchSwitch touch) touch.ValidateScene(scene);
        if (entity is AppleEverestFlagSwitchGate gate) gate.ValidateScene(scene);
        // Enqueueing can precede Session binding. Recheck before EntityList
        // makes an actor visible to trackers or calls Added; never classify an
        // unbound pending actor as belonging to the selected map.
        if (scene is not Level level || entity is not (Seeker or Puffer) ||
            GeneratedAppleEverestFlagGroups.Groups.Length == 0) return;
        if (level.Session == null)
        {
            if (beforeAdded)
                throw new InvalidOperationException("actor materialization requires a bound level session");
            return;
        }
        foreach (AppleEverestFlagGroup group in GeneratedAppleEverestFlagGroups.Groups)
            if (level.Session.Area.SID == group.Sid && (int)level.Session.Area.Mode == group.Mode)
                throw new InvalidOperationException("actor creation is outside the selected flag-switch closure");
    }

    private static InvalidOperationException Outside() =>
        new("flag switch/gate creation is outside its source-bound SID/mode/room/group");
}

internal sealed class AppleEverestFlagMember
{
    internal readonly int Id;
    internal readonly bool Gate, Persistent;
    internal readonly Vector2 Position;
    internal AppleEverestFlagMember(int id, bool gate, bool persistent, Vector2 position)
    { Id = id; Gate = gate; Persistent = persistent; Position = position; }
}

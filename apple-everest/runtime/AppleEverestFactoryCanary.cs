#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Project-owned fixture evidence. All observations come after real virtual
// dispatches. This never invokes lifecycle methods or constructs a helper.
internal static class AppleEverestFactoryCanary
{
    internal sealed class Definition
    {
        internal readonly string Kind, Id, Sid, Room, Profile;
        internal readonly int EntityId;
        internal Definition(string kind, string id, string sid, string room, int entityId, string profile)
        { Kind = kind; Id = id; Sid = sid; Room = room; EntityId = entityId; Profile = profile; }
    }
    private sealed class Observation
    {
        internal Definition Definition;
        internal object Instance;
        internal readonly HashSet<string> Returned = new(StringComparer.Ordinal);
        internal readonly List<Observation> Successors = new();
        internal string InFlight, Completion;
        internal bool Reported;
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<object, Observation> Observed = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<string> PassedFactories = new(StringComparer.Ordinal);
    private static Session session;
    private static Definition[] expected = Array.Empty<Definition>();
    private static string sid, room;
    private static int frames;
    private static bool smoke, roomReported, stalled;
    private static string activatorState;

    internal static bool IsRegisteredFixture(string map, string level) =>
        AppleEverestFactoryCanaryPlan.Factories.Any(value => value.Sid == map && value.Room == level);

    internal static void Begin(Session value, string map, string level, bool continueSmoke)
    {
        lock (Gate)
        {
            session = value; sid = map; room = level;
            expected = AppleEverestFactoryCanaryPlan.Factories.Where(item => item.Sid == map && item.Room == level).ToArray();
            Observed.Clear(); frames = 0; roomReported = stalled = false; activatorState = null;
            if (!continueSmoke) { smoke = false; PassedFactories.Clear(); }
        }
        // Silver's construction room uses a
        // completed-map context; real completion is tested in the separate
        // authored Collab interaction graph.
        if (expected.Any(item => item.Id == "CollabUtils2/SilverBerry"))
        {
            SaveData.Instance.Areas[value.Area.ID].Modes[0].Completed = true;
            AppleEverestStaticRuntime.Log("factory-canary-context=silver-construction-completed-map real-completion-proof=false");
        }
    }

    internal static void End()
    {
        lock (Gate) { session = null; Observed.Clear(); expected = Array.Empty<Definition>(); smoke = false; }
    }

    internal static void Constructing(string id, string kind, int entityId)
    {
        lock (Gate)
            if (Match(id, kind, entityId) != null)
                AppleEverestStaticRuntime.Log("factory-canary-constructor=entered kind=" + kind + " id=" + id + " room=" + room);
    }

    internal static void Created(object instance, string id, string kind, int entityId)
    {
        lock (Gate)
        {
            Definition definition = Match(id, kind, entityId);
            if (definition == null) return;
            if (instance == null) throw new InvalidOperationException("factory returned null: " + id);
            Observation item = new() { Definition = definition, Instance = instance };
            item.Returned.Add("Constructor"); Observed.Add(instance, item);
        }
    }

    private static Definition Match(string id, string kind, int entityId) => session == null ? null :
        expected.SingleOrDefault(value => value.Id == id && value.Kind == kind && (kind == "backdrop" || value.EntityId == entityId));

    internal static void Dispatch(object instance, string stage, bool returned)
    {
        lock (Gate)
        {
            if (session == null || instance == null || !Observed.TryGetValue(instance, out Observation item)) return;
            if (returned) { item.Returned.Add(stage); item.InFlight = null; }
            else item.InFlight = stage;
        }
    }

    internal static void RemovalRequested(Entity entity) => Dispatch(entity, "RemoveSelf", true);

    internal static void Completion(Entity entity, string effect, bool observed)
    {
        lock (Gate)
            if (session != null && Observed.TryGetValue(entity, out Observation item) && observed) item.Completion = effect;
    }

    internal static void Successor(Entity source, Entity successor)
    {
        lock (Gate)
        {
            if (session == null || !Observed.TryGetValue(source, out Observation parent)) return;
            if (!Observed.TryGetValue(successor, out Observation child))
            {
                child = new() { Instance = successor };
                child.Returned.Add("Constructor"); Observed.Add(successor, child);
            }
            if (!parent.Successors.Contains(child)) parent.Successors.Add(child);
        }
    }

    private static bool Has(Observation item, params string[] stages) => item.InFlight == null && stages.All(item.Returned.Contains);

    private static bool Complete(Observation item, Level level)
    {
        string id = item.Definition.Id;
        if (item.Definition.Kind == "backdrop") return Has(item, "Constructor", "Configured", "Attached", "Update", "BeforeRender", "Render");
        if (!Has(item, "Constructor", "Added")) return false;
        if (id == "MaxHelpingHand/SetFlagOnSpawnController")
            return Has(item, "RemoveSelf", "Removed") && level.Session.GetFlag("camera_07B");
        if (id == "CollabUtils2/RainbowBerry" && item.Returned.Contains("RemoveSelf"))
            return Has(item, "Removed") && item.Successors.Count > 0 && item.Successors.All(SuccessorComplete);
        if (!Has(item, "Awake")) return false;
        if (id == "FrostHelper/DecalContainer")
            return Has(item, "RemoveSelf", "Removed") && item.Completion == "decals-converted" &&
                item.Successors.Count > 0 && item.Successors.All(SuccessorComplete);
        if (id == "FrostHelper/OnSpawnActivator")
            return Has(item, "RemoveSelf", "Removed") && item.Completion == "reset-variants-dispatched";
        if (item.Returned.Contains("RemoveSelf") || item.Returned.Contains("Removed")) return false;
        bool noUpdate = id is "CollabUtils2/LobbyMapMarker" or "SJ2021/AllInOneMask" or "SJ2021/BloomMask" or "SJ2021/StylegroundMask" or "SJ2021/GlowController";
        bool noRender = item.Instance is Trigger trigger && !trigger.Visible || id is "CollabUtils2/LobbyMapMarker" or "SJ2021/GlowController";
        if (!noUpdate && !Has(item, "Update") || !noRender && !Has(item, "Render")) return false;
        if (id == "FrostHelper/IceSpinner") return item.Successors.Count > 0 && item.Successors.All(SuccessorComplete);
        return true;
    }

    private static bool SuccessorComplete(Observation value) => Has(value, "Constructor", "Added", "Awake", "Update", "Render");

    internal static void Frame(Level level)
    {
        string nextSid = null, nextRoom = null;
        lock (Gate)
        {
            if (session == null || !ReferenceEquals(level.Session, session)) return;
            frames++;
            if (!smoke && sid == "AppleEverestStage25KJ/FactoryProfiles/CameraCorridor" && frames % 15 == 0)
            {
                Spring west = level.Entities.OfType<Spring>().SingleOrDefault(value => Math.Abs(value.X - level.Bounds.Left - 64f) < 1f);
                Spring east = level.Entities.OfType<Spring>().SingleOrDefault(value => Math.Abs(value.X - level.Bounds.Left - 1120f) < 1f);
                if (west != null && east != null)
                {
                    string next = "west=" + SpringState(west) + " east=" + SpringState(east);
                    if (next != activatorState)
                    {
                        activatorState = next;
                        AppleEverestStaticRuntime.Log("factory-canary-activator=OBSERVED " + next);
                        AppleEverestStaticRuntime.ShowStatus("K-J SPRING STATE (ACTIVE / VISIBLE / COLLIDABLE)\n" +
                            "WEST " + Bits(west) + "    EAST " + Bits(east));
                    }
                }
            }
            if (frames < 30 || roomReported) return;
            foreach (Definition definition in expected)
            {
                Observation item = Observed.Values.SingleOrDefault(value => value.Definition == definition);
                if (item == null || item.Reported || !Complete(item, level)) continue;
                item.Reported = true; PassedFactories.Add(definition.Kind + ":" + definition.Id);
                AppleEverestStaticRuntime.Log("factory-canary-lifecycle=PASS id=" + definition.Id + " profile=" + definition.Profile +
                    " room=" + room + " observed=" + string.Join(",", item.Returned.OrderBy(value => value, StringComparer.Ordinal)) +
                    " semantic-physical-proof=separate");
            }
            roomReported = expected.All(definition => Observed.Values.Any(value => value.Definition == definition && value.Reported));
            if (!roomReported && frames >= 600 && !stalled)
            {
                stalled = true; smoke = false;
                Definition[] pending = expected.Where(definition => !Observed.Values.Any(value => value.Definition == definition && value.Reported)).ToArray();
                foreach (Definition definition in pending)
                {
                    Observation item = Observed.Values.SingleOrDefault(value => value.Definition == definition);
                    AppleEverestStaticRuntime.Log("factory-canary-lifecycle=PENDING id=" + definition.Id + " observed=" +
                        (item == null ? "constructor-not-returned" : string.Join(",", item.Returned)) + " in-flight=" + item?.InFlight);
                }
                AppleEverestStaticRuntime.ShowStatus("K-J INCOMPLETE " + PassedFactories.Count + "/73: " + room +
                    "\n" + string.Join(", ", pending.Select(value => value.Id)));
            }
            if (roomReported && smoke)
            {
                var rooms = Rooms();
                int current = Array.FindIndex(rooms, value => value.Sid == sid && value.Room == room);
                if (current + 1 < rooms.Length) { nextSid = rooms[current + 1].Sid; nextRoom = rooms[current + 1].Room; }
                else
                {
                    smoke = false;
                    AppleEverestStaticRuntime.Log("factory-canary-suite=" + (PassedFactories.Count == 73 ? "PASS" : "FAIL") +
                        " instantiated=" + PassedFactories.Count + " selected=73 physical-semantics=separate");
                    AppleEverestStaticRuntime.ShowStatus("K-J FACTORY LIFECYCLE " + PassedFactories.Count + "/73\nRepresentative play checks remain separate");
                }
            }
        }
        if (nextSid != null)
            level.OnEndOfFrame += () => AppleEverestStaticRuntime.LaunchRegisteredFactoryCanary(nextSid, nextRoom, continueSmoke: true);
    }

    private static string Bits(Entity value) => (value.Active ? "1" : "0") + "/" +
        (value.Visible ? "1" : "0") + "/" + (value.Collidable ? "1" : "0");

    private static string SpringState(Spring value) => Bits(value) + " components=" +
        string.Join(",", value.Components.Select(component => (component.Active ? "1" : "0") + "/" + (component.Visible ? "1" : "0")));

    private static (string Sid, string Room)[] Rooms() => AppleEverestFactoryCanaryPlan.Factories
        .Where(value => AppleEverestProgressionRuntime.TryDescriptor(value.Sid, out _))
        .Select(value => (value.Sid, value.Room)).Distinct().OrderBy(value => value.Sid, StringComparer.Ordinal)
        .ThenBy(value => value.Room, StringComparer.Ordinal).ToArray();

    internal static void AddOptions(TextMenu menu)
    {
        var rooms = Rooms();
        if (rooms.Length == 0) return;
        menu.Add(new TextMenu.SubHeader("STAGE 25K-J FACTORY CANARIES"));
        if (AppleEverestProgressionRuntime.TryDescriptor("AppleEverestStage25KJ/0-Lobbies/1-Fixture", out _))
            menu.Add(new TextMenu.Button("Play Collab Interaction Fixture").Pressed(AppleEverestStaticRuntime.LaunchFactoryInteractionLobby));
        menu.Add(new TextMenu.Button("Run All Factory Lifecycle Checks").Pressed(() =>
        {
            AppleEverestStaticRuntime.LaunchRegisteredFactoryCanary(rooms[0].Sid, rooms[0].Room);
            smoke = true;
        }));
        foreach (var fixture in rooms)
        {
            var selected = fixture;
            string label = fixture.Sid.Split('/').Last().Replace("Stage25KJ", "") + " / " + fixture.Room.Replace("lvl_", "");
            menu.Add(new TextMenu.Button(label).Pressed(() => AppleEverestStaticRuntime.LaunchRegisteredFactoryCanary(selected.Sid, selected.Room)));
        }
    }
}

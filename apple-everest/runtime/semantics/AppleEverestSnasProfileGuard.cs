// Generated from the exact unchanged snas BIN by generate-apple-everest-stage25kn-profiles.py.
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
namespace Celeste.Mod;
internal static class AppleEverestSnasProfileGuard
{
    private sealed class Profile
    {
        internal readonly int Width, Height;
        internal readonly Dictionary<string, object> Values;
        internal readonly Vector2[] Nodes;
        internal Profile(int width, int height, Dictionary<string, object> values, Vector2[] nodes)
        { Width = width; Height = height; Values = values; Nodes = nodes; }
    }
    private static readonly Dictionary<string, Profile[]> Profiles = new(StringComparer.Ordinal)
    {
        ["CollabUtils2/MiniHeart"] = new Profile[]
        {
            new(0, 0, new(StringComparer.Ordinal) { ["noGhostSprite"] = true, ["particleColor"] = "", ["refillDash"] = true, ["requireDashToBreak"] = true, ["sprite"] = "beginner" }, new Vector2[] {  }),
        },
        ["CollabUtils2/SilverBerry"] = new Profile[]
        {
            new(0, 0, new(StringComparer.Ordinal) {  }, new Vector2[] {  }),
        },
        ["CommunalHelper/PlayerBubbleRegion"] = new Profile[]
        {
            new(14, 14, new(StringComparer.Ordinal) {  }, new Vector2[] { new Vector2(203.0f, 87.0f), new Vector2(411.0f, 139.0f) }),
        },
        ["ContortHelper/RandomSoundTrigger"] = new Profile[]
        {
            new(24, 8, new(StringComparer.Ordinal) { ["audioEvents"] = "event:/sj21_snas_flourish", ["delay"] = 0, ["flagsAfterInvoke"] = "", ["neededFlags"] = "", ["occurOnEnter"] = true, ["oneUse"] = false, ["persistent"] = false }, new Vector2[] {  }),
            new(24, 8, new(StringComparer.Ordinal) { ["audioEvents"] = "event:/sj21_snas_flourish", ["delay"] = 0, ["flagsAfterInvoke"] = "", ["neededFlags"] = "", ["occurOnEnter"] = true, ["oneUse"] = true, ["persistent"] = false }, new Vector2[] {  }),
        },
        ["MaxHelpingHand/CameraOffsetBorder"] = new Profile[]
        {
            new(176, 16, new(StringComparer.Ordinal) { ["bottomCenter"] = true, ["bottomLeft"] = false, ["bottomRight"] = false, ["centerLeft"] = false, ["centerRight"] = false, ["flag"] = "", ["inside"] = true, ["topCenter"] = false, ["topLeft"] = false, ["topRight"] = false }, new Vector2[] {  }),
            new(128, 16, new(StringComparer.Ordinal) { ["bottomCenter"] = true, ["bottomLeft"] = false, ["bottomRight"] = false, ["centerLeft"] = false, ["centerRight"] = false, ["flag"] = "", ["inside"] = true, ["topCenter"] = false, ["topLeft"] = false, ["topRight"] = false }, new Vector2[] {  }),
            new(176, 32, new(StringComparer.Ordinal) { ["bottomCenter"] = true, ["bottomLeft"] = false, ["bottomRight"] = false, ["centerLeft"] = false, ["centerRight"] = false, ["flag"] = "", ["inside"] = true, ["topCenter"] = false, ["topLeft"] = false, ["topRight"] = false }, new Vector2[] {  }),
            new(176, 8, new(StringComparer.Ordinal) { ["bottomCenter"] = true, ["bottomLeft"] = false, ["bottomRight"] = false, ["centerLeft"] = false, ["centerRight"] = false, ["flag"] = "", ["inside"] = false, ["topCenter"] = false, ["topLeft"] = false, ["topRight"] = false }, new Vector2[] {  }),
        },
        ["MaxHelpingHand/FlagSwitchGate"] = new Profile[]
        {
            new(32, 32, new(StringComparer.Ordinal) { ["activeColor"] = "FFFFFF", ["allowReturn"] = false, ["finishColor"] = "F141DF", ["finishedSound"] = "event:/game/general/touchswitch_gate_finish", ["flag"] = "flag_snasberry_switch", ["icon"] = "vanilla", ["inactiveColor"] = "5FCDE4", ["moveEased"] = true, ["moveSound"] = "event:/game/general/touchswitch_gate_open", ["moveTime"] = 1.7999999523162842f, ["persistent"] = false, ["shakeTime"] = 0.5f, ["sprite"] = "block" }, new Vector2[] { new Vector2(40.0f, -32.0f) }),
        },
        ["MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger"] = new Profile[]
        {
            new(56, 32, new(StringComparer.Ordinal) { ["flag"] = "camera_3_a", ["inverted"] = false, ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = -1, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight" }, new Vector2[] {  }),
            new(8, 40, new(StringComparer.Ordinal) { ["flag"] = "camera_3_a", ["inverted"] = false, ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = -1, ["offsetYTo"] = 0, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight" }, new Vector2[] {  }),
        },
        ["MaxHelpingHand/FlagTouchSwitch"] = new Profile[]
        {
            new(0, 0, new(StringComparer.Ordinal) { ["activeColor"] = "FFFFFF", ["allowDisable"] = false, ["completeSoundFromScene"] = "event:/game/general/touchswitch_last_oneshot", ["completeSoundFromSwitch"] = "event:/game/general/touchswitch_last_cutoff", ["finishColor"] = "F141DF", ["flag"] = "flag_snasberry_switch", ["hitSound"] = "event:/game/general/touchswitch_any", ["icon"] = "vanilla", ["inactiveColor"] = "5FCDE4", ["inverted"] = false, ["persistent"] = true, ["smoke"] = true }, new Vector2[] {  }),
        },
        ["MaxHelpingHand/SetFlagOnSpawnController"] = new Profile[]
        {
            new(0, 0, new(StringComparer.Ordinal) { ["enable"] = true, ["flag"] = "camera_3_a", ["ifFlag"] = "", ["onlyOnRespawn"] = false }, new Vector2[] {  }),
        },
        ["MaxHelpingHand/SidewaysJumpThru"] = new Profile[]
        {
            new(0, 8, new(StringComparer.Ordinal) { ["animationDelay"] = 0, ["left"] = false, ["letSeekersThrough"] = false, ["pushPlayer"] = false, ["surfaceIndex"] = -1, ["texture"] = "wood" }, new Vector2[] {  }),
        },
        ["everest/flagTrigger"] = new Profile[]
        {
            new(40, 40, new(StringComparer.Ordinal) { ["death_count"] = -1, ["flag"] = "camera_3_a", ["mode"] = "OnPlayerEnter", ["only_once"] = false, ["state"] = false }, new Vector2[] {  }),
            new(8, 8, new(StringComparer.Ordinal) { ["death_count"] = -1, ["flag"] = "camera_3_a", ["mode"] = "OnPlayerEnter", ["only_once"] = false, ["state"] = false }, new Vector2[] {  }),
            new(16, 8, new(StringComparer.Ordinal) { ["death_count"] = -1, ["flag"] = "camera_3_a", ["mode"] = "OnPlayerEnter", ["only_once"] = false, ["state"] = true }, new Vector2[] {  }),
        },
        ["everest/smoothCameraOffsetTrigger"] = new Profile[]
        {
            new(8, 56, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(8, 72, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0.699999988079071f, ["onlyOnce"] = false, ["positionMode"] = "TopToBottom", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(64, 32, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 1, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(120, 16, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0, ["onlyOnce"] = false, ["positionMode"] = "TopToBottom", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(56, 152, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0.8999999761581421f, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(128, 32, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = -0.6299999952316284f, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(32, 40, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = -1, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = false }, new Vector2[] {  }),
            new(80, 72, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 1.1200000047683716f, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight", ["xOnly"] = false, ["yOnly"] = false }, new Vector2[] {  }),
            new(8, 40, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0.7200000286102295f, ["offsetYTo"] = -1.100000023841858f, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(40, 24, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = false }, new Vector2[] {  }),
            new(128, 32, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 1.2999999523162842f, ["onlyOnce"] = false, ["positionMode"] = "NoEffect", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(16, 72, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0, ["offsetYTo"] = 0.8500000238418579f, ["onlyOnce"] = false, ["positionMode"] = "TopToBottom", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(64, 136, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = -0.6299999952316284f, ["offsetYTo"] = 0.7200000286102295f, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
            new(32, 88, new(StringComparer.Ordinal) { ["offsetXFrom"] = 0, ["offsetXTo"] = 0, ["offsetYFrom"] = 0.8999999761581421f, ["offsetYTo"] = 0, ["onlyOnce"] = false, ["positionMode"] = "LeftToRight", ["xOnly"] = false, ["yOnly"] = true }, new Vector2[] {  }),
        },
    };

    internal static bool Accepts(string id, EntityData data)
    {
        if (data.Name != id || data.Origin != Vector2.Zero || !Profiles.TryGetValue(id, out Profile[] profiles)) return false;
        foreach (Profile profile in profiles)
        {
            if (data.Width != profile.Width || data.Height != profile.Height ||
                (data.Values?.Count ?? 0) != profile.Values.Count || (data.Nodes?.Length ?? 0) != profile.Nodes.Length) continue;
            bool match = true;
            foreach (var pair in profile.Values)
            {
                if (!data.Values.TryGetValue(pair.Key, out object value)) { match = false; break; }
                if (pair.Value is string || pair.Value is bool)
                { if (!pair.Value.Equals(value)) { match = false; break; } }
                else if (value is not (byte or short or int or float or double) ||
                    Convert.ToSingle(pair.Value, CultureInfo.InvariantCulture) != Convert.ToSingle(value, CultureInfo.InvariantCulture))
                { match = false; break; }
            }
            for (int i = 0; match && i < profile.Nodes.Length; i++)
                if (data.Nodes[i] - data.Position != profile.Nodes[i]) match = false;
            if (match) return true;
        }
        return false;
    }
}

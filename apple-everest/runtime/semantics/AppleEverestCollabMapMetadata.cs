#nullable disable
using System;
using System.Linq;
namespace Celeste.Mod;

// Read-only original presentation/collectible metadata. These entries do not
// register areas, mount map bytes, or make their gameplay available.
internal static class AppleEverestCollabMapMetadata
{
    internal static string Icon(string sid) => sid switch
    {
        "StrawberryJam2021/1-Beginner/Bing_Over_Google" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/Ceph" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/Circumplex" => "areas/SJ2021/meters/3-hard",
        "StrawberryJam2021/1-Beginner/CoupCritik" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/Eclipse" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/Flagpole1up" => "areas/SJ2021/meters/3-hard",
        "StrawberryJam2021/1-Beginner/HankyMueller" => "areas/SJ2021/meters/3-hard",
        "StrawberryJam2021/1-Beginner/Jadeturtle" => "areas/SJ2021/meters/3-hard",
        "StrawberryJam2021/1-Beginner/NotYourBadeline" => "areas/SJ2021/meters/1-easy",
        "StrawberryJam2021/1-Beginner/Owen-Shirrell" => "areas/SJ2021/meters/3-hard",
        "StrawberryJam2021/1-Beginner/Quinnigan" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/asteriskblue" => "areas/SJ2021/meters/1-easy",
        "StrawberryJam2021/1-Beginner/cellularAutomaton" => "areas/SJ2021/meters/1-easy",
        "StrawberryJam2021/1-Beginner/coffe" => "areas/SJ2021/meters/1-easy",
        "StrawberryJam2021/1-Beginner/frozenflygone" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/hyperlife" => "areas/SJ2021/meters/1-easy",
        "StrawberryJam2021/1-Beginner/joltik" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/mosscairn" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/skeleton" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/snas" => "areas/SJ2021/meters/2-med",
        "StrawberryJam2021/1-Beginner/voliver9" => "areas/SJ2021/meters/3-hard",
        _ => null
    };
    internal static readonly (string Sid, EntityID Id)[] BeginnerSilverBerries =
    {
        ("StrawberryJam2021/1-Beginner/Bing_Over_Google", new EntityID("00- intro", 581)),
        ("StrawberryJam2021/1-Beginner/Ceph", new EntityID("1", 1115)),
        ("StrawberryJam2021/1-Beginner/Circumplex", new EntityID("01", 63)),
        ("StrawberryJam2021/1-Beginner/CoupCritik", new EntityID("01", 555)),
        ("StrawberryJam2021/1-Beginner/Eclipse", new EntityID("a_02", 162)),
        ("StrawberryJam2021/1-Beginner/Flagpole1up", new EntityID("a01", 252)),
        ("StrawberryJam2021/1-Beginner/HankyMueller", new EntityID("Intro B", 278)),
        ("StrawberryJam2021/1-Beginner/Jadeturtle", new EntityID("a-00", 1045)),
        ("StrawberryJam2021/1-Beginner/NotYourBadeline", new EntityID("a_01", 53)),
        ("StrawberryJam2021/1-Beginner/Owen-Shirrell", new EntityID("00 - Overpass", 187)),
        ("StrawberryJam2021/1-Beginner/Quinnigan", new EntityID("q00", 1786)),
        ("StrawberryJam2021/1-Beginner/asteriskblue", new EntityID("a-01", 1329)),
        ("StrawberryJam2021/1-Beginner/cellularAutomaton", new EntityID("01", 797)),
        ("StrawberryJam2021/1-Beginner/coffe", new EntityID("c-01", 60)),
        ("StrawberryJam2021/1-Beginner/frozenflygone", new EntityID("SS2-0", 786)),
        ("StrawberryJam2021/1-Beginner/hyperlife", new EntityID("a-01", 235)),
        ("StrawberryJam2021/1-Beginner/joltik", new EntityID("a_01", 1462)),
        ("StrawberryJam2021/1-Beginner/mosscairn", new EntityID("intro", 2544)),
        ("StrawberryJam2021/1-Beginner/skeleton", new EntityID("skeleton_01", 1109)),
        ("StrawberryJam2021/1-Beginner/snas", new EntityID("1", 467)),
        ("StrawberryJam2021/1-Beginner/voliver9", new EntityID("a-01", 160)),
    };
    internal static readonly AppleEverestCollabSticker[] BeginnerStickers =
    {
        new("SJ2021/1-Beginner/Asterisk", 120.0f, 222.0f, 14.0f, 0.89f, new[] { "StrawberryJam2021/1-Beginner/asteriskblue" }),
        new("SJ2021/1-Beginner/BingOverGoogle", 650.0f, 245.0f, -5.0f, 0.826f, new[] { "StrawberryJam2021/1-Beginner/Bing_Over_Google" }),
        new("SJ2021/1-Beginner/CellularAutomaton", 128.0f, 480.0f, 10.0f, 0.95f, new[] { "StrawberryJam2021/1-Beginner/cellularAutomaton" }),
        new("SJ2021/1-Beginner/Ceph", 365.0f, 820.0f, -5.0f, 1.0f, new[] { "StrawberryJam2021/1-Beginner/Ceph" }),
        new("SJ2021/1-Beginner/Circumplex", 600.0f, 560.0f, 20.0f, 0.93f, new[] { "StrawberryJam2021/1-Beginner/Circumplex" }),
        new("SJ2021/1-Beginner/Coffe", 380.0f, 170.0f, 5.0f, 1.26f, new[] { "StrawberryJam2021/1-Beginner/coffe" }),
        new("SJ2021/1-Beginner/CoupCritik", 1500.0f, 160.0f, -5.0f, 0.73f, new[] { "StrawberryJam2021/1-Beginner/CoupCritik" }),
        new("SJ2021/1-Beginner/Eclipse", 1030.0f, 660.0f, 0.0f, 0.83f, new[] { "StrawberryJam2021/1-Beginner/Eclipse" }),
        new("SJ2021/1-Beginner/Flagpole", 1495.0f, 450.0f, -6.0f, 0.72f, new[] { "StrawberryJam2021/1-Beginner/Flagpole1up" }),
        new("SJ2021/1-Beginner/frozenflygone", 835.0f, 545.0f, -70.0f, 1.25f, new[] { "StrawberryJam2021/1-Beginner/frozenflygone" }),
        new("SJ2021/1-Beginner/HankyMueller", 1275.0f, 550.0f, -5.0f, 0.87f, new[] { "StrawberryJam2021/1-Beginner/HankyMueller" }),
        new("SJ2021/1-Beginner/Hyperlife", 1272.0f, 200.0f, -5.0f, 1.0f, new[] { "StrawberryJam2021/1-Beginner/hyperlife" }),
        new("SJ2021/1-Beginner/Jadeturtle", 1510.0f, 730.0f, 10.0f, 0.95f, new[] { "StrawberryJam2021/1-Beginner/Jadeturtle" }),
        new("SJ2021/1-Beginner/Joltik", 150.0f, 765.0f, 0.0f, 1.17f, new[] { "StrawberryJam2021/1-Beginner/joltik" }),
        new("SJ2021/1-Beginner/mosscairn", 1300.0f, 820.0f, -10.0f, 1.1f, new[] { "StrawberryJam2021/1-Beginner/mosscairn" }),
        new("SJ2021/1-Beginner/NotYourBadeline", 564.0f, 827.0f, 8.0f, 0.92f, new[] { "StrawberryJam2021/1-Beginner/NotYourBadeline" }),
        new("SJ2021/1-Beginner/OwenShirrell", 808.0f, 820.0f, -5.0f, 0.77f, new[] { "StrawberryJam2021/1-Beginner/Owen-Shirrell" }),
        new("SJ2021/1-Beginner/Quinnigan", 1034.0f, 230.0f, 90.0f, 0.87f, new[] { "StrawberryJam2021/1-Beginner/Quinnigan" }),
        new("SJ2021/1-Beginner/Skeleton", 822.0f, 115.0f, 15.0f, 0.84f, new[] { "StrawberryJam2021/1-Beginner/skeleton" }),
        new("SJ2021/1-Beginner/Snas", 340.0f, 580.0f, -16.0f, 0.98f, new[] { "StrawberryJam2021/1-Beginner/snas" }),
        new("SJ2021/1-Beginner/voliver9", 1040.0f, 880.0f, -2.0f, 0.93f, new[] { "StrawberryJam2021/1-Beginner/voliver9" }),
    };
}

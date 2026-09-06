using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

/// <summary>
/// Closed registry for real, source-audited HookGen IL manipulators which are
/// executed on the Mac and removed from the device assembly before Apple AOT.
/// This is intentionally not a generic arbitrary-mod execution surface.
/// </summary>
internal static class StaticIlFreeze
{
    internal const string FixtureName = "DashToggleHelper";
    internal const string FixtureVersion = "1.1.0";
    internal const string FixtureSourceSha256 = "a26ac163b4184cc0daccfd99f4ef11aeeeef7a2858b7d84938beb0dc6afd5d09";
    internal const string FixtureDllSha256 = "531eaa8a719cb81cc84adf2b9e930dcb3abae73c60406b8f44b823c9c4b4a083";
    internal const string FixtureZipSha256 = "677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523";
    internal const string FixtureUrl = "https://gamebanana.com/mmdl/1460721";
    internal const string FixtureSourceCommit = "9b140684c2ee80ddae3c9ef032de0c767a67530c";
    internal const string DisposableTheoName = "DisposableTheo";
    internal const string DisposableTheoVersion = "1.0.6";
    internal const string DisposableTheoSourceSha256 = "fc6aa15ee69311eac205af76e382d8a09dfb16ebe73eb05163ba90da7c21d597";
    internal const string DisposableTheoDllSha256 = "1d47c08238fd0dd29eaa5c6e53a36e7d72942fdb7b7abc2a3870f09fbc952dcc";
    internal const string DisposableTheoZipSha256 = "df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5";
    internal const string DisposableTheoUrl = "https://gamebanana.com/mmdl/929736";
    internal const string VortexHelperName = "VortexHelper";
    internal const string VortexHelperVersion = "1.2.19";
    internal const string VortexHelperSourceSha256 = "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b";
    internal const string VortexHelperDllSha256 = "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73";
    internal const string VortexHelperZipSha256 = "b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2";
    internal const string VortexHelperUrl = "https://gamebanana.com/mmdl/1368600";
    internal const string VortexHelperSourceCommit = "b37b67b9365d769ba0fd19a67a1327260988fd6e";
    internal const string VortexHelperLicenseSha256 = "051f92453f04ec0a8a9dff60882264ca949ea8e86de5f6aa96e04acdee90d359";
    internal const string CaeruleaName = "CaeruleaHelper";
    internal const string CaeruleaVersion = "1.11.1";
    internal const string CaeruleaSourceSha256 = "036bc9adbc5471931ca6cfb1aa574d0bbcfe3a6dce9025a09b56a0c522054d07";
    internal const string CaeruleaDllSha256 = "3c5b79a57ce03b6c98e8ae12d781ec6baddce944995b2b4928f067a5ad7973ae";
    internal const string CaeruleaZipSha256 = "6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807";
    internal const string CaeruleaUrl = "https://gamebanana.com/mmdl/1784884";
    internal const string CaeruleaSourceCommit = "ce2ad0694feb28cd3dff0a5d7501f6e60d620fd5";
    internal const string WorkerVersion = "apple-everest-static-il-worker-v2";
    internal const string DirectWorkerVersion = "apple-everest-static-il-worker-v3";

    internal static int SchemaVersionFor(IEnumerable<FrozenIlTransformPlan> plans) =>
        plans.Any(plan => plan.Mechanism == SelectedSidewaysIlPlans.Mechanism) ? 4 :
        plans.Any(plan => plan.Mechanism == "DIRECT_ILHOOK") ? 3 : 2;

    internal static string WorkerVersionFor(IEnumerable<FrozenIlTransformPlan> plans) =>
        plans.Any(plan => plan.Mechanism == SelectedSidewaysIlPlans.Mechanism) ? "apple-everest-static-il-worker-v4" :
        plans.Any(plan => plan.Mechanism == "DIRECT_ILHOOK") ? DirectWorkerVersion : WorkerVersion;

    private static readonly FrozenIlTransformPlan[] DashTogglePlans =
    [
        new(
            "DashToggleHelper:CrystalStaticSpinner.CreateSprites:CreateSpritesOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "CreateSprites",
            "System.Void Celeste.CrystalStaticSpinner::AppleEverestOriginal_CreateSprites()",
            "System.Void Celeste.CrystalStaticSpinner::CreateSprites()",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "CreateSpritesOverride",
            true, 0,
            "60e4d178d19f1e70f21e9e88243354830db71155aa33de6b7b1f9b1e6abf6a94",
            "f03103f1f63b71351054d68b8fc6ed52a06dc1e690b616cf993885c93b3bd0d8",
            "da499a5a57b7ecb09f1d15ec20b61caa445252b8f631789d7723cbc02dbc81a9",
            ["DTSpinnerImage", "DTSpinnerColor", "isDTSpinner"], []),
        new(
            "DashToggleHelper:CrystalStaticSpinner.AddSprite:AddSpriteOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "AddSprite",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "AddSpriteOverride",
            true, 0,
            "0a10b7404b2394238548b3e32d89c8f15298c32f293a2bc23153e0c1a8ebd051",
            "91f955361cc5df3c643c8bd09b9cd211cf7ee09d6817ae2175d6569e2f9ec0aa",
            "17bf0d8a9050ef5f8372e08dc41367a800344a877a009096e20787dd64bb79e7",
            ["DTSpinnerImage", "tintIfDTSpinner"], [])
    ];

    private static readonly FrozenIlTransformPlan[] DisposableTheoPlans =
    [
        new(
            "DisposableTheo:TheoCrystal.Die:TheoCrystal_Die",
            DisposableTheoName, "DisposableTheo.dll", DisposableTheoDllSha256,
            "IL.Celeste.TheoCrystal", "Die",
            "System.Void Celeste.TheoCrystal::Die()",
            "System.Void Celeste.TheoCrystal::Die()",
            "Celeste.Mod.DisposableTheo.DisposableTheoModule", "TheoCrystal_Die",
            false, 0,
            "ec6294022668396295da4d81b61192b01bcbc9e898399d94a8a74251d3c87911",
            "c538112f327281bfd4fa0af488a3ee175ff8662a63bfd3fced969e1d8e52ba55",
            "107ea6b57c066477eda086f303ca7331c86adbe40964fe30a266d27691c02987",
            [], ["Celeste.Mod.DisposableTheo.DisposableTheoModule+<>c::<TheoCrystal_Die>b__9_0"]),
        new(
            "DisposableTheo:Level.EnforceBounds:Level_EnforceBounds",
            DisposableTheoName, "DisposableTheo.dll", DisposableTheoDllSha256,
            "IL.Celeste.Level", "EnforceBounds",
            "System.Void Celeste.Level::EnforceBounds(Celeste.Player)",
            "System.Void Celeste.Level::EnforceBounds(Celeste.Player)",
            "Celeste.Mod.DisposableTheo.DisposableTheoModule", "Level_EnforceBounds",
            false, 0,
            "90f8c7928bd3cf8fff7db66aaebc122dc4f8082a947e41b78d5bba7d0021c1ac",
            "025cf84ebf83868acd00f5bab4bc7c028f6190159e89c4199ec08d03db5ecd78",
            "480bd4cdc59a703a66658d048a57067eebc55bde531c52acf344b6a3d3694fc1",
            [], ["Celeste.Mod.DisposableTheo.DisposableTheoModule+<>c::<Level_EnforceBounds>b__10_0"])
    ];

    // Exact ordinary HookGen IL-event breadth selected from the Stage 25G
    // Space Trip graph. VortexHelper's current compiler caches both event
    // delegates in nested <>O types and registers them from entity-local
    // Hook/Unhook methods rather than the module Load/Unload body. The real
    // distributed methods remain the only host-side transformation authority.
    private static readonly FrozenIlTransformPlan[] VortexHelperPlans =
    [
        new(
            "VortexHelper:Player.NormalUpdate:Player_FrictionNormalUpdate",
            VortexHelperName, "Code/bin/VortexHelper.dll", VortexHelperDllSha256,
            "IL.Celeste.Player", "NormalUpdate",
            "System.Int32 Celeste.Player::NormalUpdate()",
            "System.Int32 Celeste.Player::NormalUpdate()",
            "Celeste.Mod.VortexHelper.Entities.FloorBooster+Hooks", "Player_FrictionNormalUpdate",
            true, 0,
            "c38137044853b6ec5f8d42c364743bd82982a79314fe493a5070ef028e0f6e4b",
            "9f34b6f8ce802587df35fea0cd211a72f1de4446e0bf692e938bfdcc93401991",
            "c75c39af18902072eefb40b5fe349a108bfdbb77eb011e2f2d2002fffb4f862e",
            ["GetPlayerFriction"], []),
        new(
            "VortexHelper:Player.WallJumpCheck:Player_WallJumpCheck",
            VortexHelperName, "Code/bin/VortexHelper.dll", VortexHelperDllSha256,
            "IL.Celeste.Player", "WallJumpCheck",
            "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)",
            "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)",
            "Celeste.Mod.VortexHelper.Entities.PurpleBooster+Hooks", "Player_WallJumpCheck",
            true, 0,
            "7c87ba1ac56a612e7487dfc598e2d4b2f7d81817bd0d97fe1ec1e0d329128cf0",
            "6749ff2746b4d742c2a10ba54d3ff344ffc09c0ca4a3b30dbdf33a6dc6c8162b",
            "1cc8f904f6f72282372628b0ad40350e637bf9048f704a743023ad17adf2e05e",
            [], ["Celeste.Mod.VortexHelper.Entities.PurpleBooster+Hooks+<>c::<Player_WallJumpCheck>b__3_1"])
    ];

    // Exact DJMapHelper 1.13.4 ordinary HookGen IL surface selected by the
    // LittleEpic graph. The real distributed manipulators execute on the Mac;
    // only their fingerprinted target bodies and reviewed static delegate
    // calls enter the Apple products. Horizontal and vertical collision each
    // preserve the distributed Feather-then-Theo registration sequence. Freeze
    // the stored original body beneath the accepted HookGen dispatch wrapper;
    // canonical identities and before/after/diff locks remain unchanged.
    private static readonly FrozenIlTransformPlan[] DJMapHelperPlans =
    [
        new("DJMapHelper:player-h-feather", StaticAotCompatibility.DJName,
            StaticAotCompatibility.DJDllPath, StaticAotCompatibility.DJDllSha256,
            "IL.Celeste.Player", "OnCollideH",
            "System.Void Celeste.Player::AppleEverestOriginal_OnCollideH(Celeste.CollisionData)",
            "System.Void Celeste.Player::OnCollideH(Celeste.CollisionData)",
            "Celeste.Mod.DJMapHelper.Entities.FeatherBarrier", "AddCollideCheck", true, 0,
            "71468bceff7398edb3f4a093087b6bcedf0bc9476a10e09ff9be65574f22f2b9",
            "f0659cb0f07350663fbf5ac9ab68cf4d53b2674db66c1422568d5b50f2246ee4",
            "4d9b78648d0a72da8127cd9f2410c66673eaf7db4c5d3400209de1d7dd8992e6", [],
            Repeat("Celeste.Mod.DJMapHelper.Entities.FeatherBarrier::CheckCollide", 10)),
        new("DJMapHelper:player-h-theo", StaticAotCompatibility.DJName,
            StaticAotCompatibility.DJDllPath, StaticAotCompatibility.DJDllSha256,
            "IL.Celeste.Player", "OnCollideH",
            "System.Void Celeste.Player::AppleEverestOriginal_OnCollideH(Celeste.CollisionData)",
            "System.Void Celeste.Player::OnCollideH(Celeste.CollisionData)",
            "Celeste.Mod.DJMapHelper.Entities.TheoCrystalBarrier", "AddCollideCheck", true, 1,
            "f0659cb0f07350663fbf5ac9ab68cf4d53b2674db66c1422568d5b50f2246ee4",
            "a86a9eff25145c269bd65955eebbb4dbd63f2d0d9e33160365cacc15239748a3",
            "17dc28cf86b319fd781fc8c6f8a1f4b1f4e0dcd97ce75f27be2bbc5ba6b14327", [],
            Repeat("Celeste.Mod.DJMapHelper.Entities.TheoCrystalBarrier::CheckCollide", 10)),
        new("DJMapHelper:player-v-feather", StaticAotCompatibility.DJName,
            StaticAotCompatibility.DJDllPath, StaticAotCompatibility.DJDllSha256,
            "IL.Celeste.Player", "OnCollideV",
            "System.Void Celeste.Player::AppleEverestOriginal_OnCollideV(Celeste.CollisionData)",
            "System.Void Celeste.Player::OnCollideV(Celeste.CollisionData)",
            "Celeste.Mod.DJMapHelper.Entities.FeatherBarrier", "AddCollideCheck", true, 0,
            "40e6847041906b2c83a493f5477a19c24d2bb8d2cbce4c40780c67df40866271",
            "d07b75bd9c0e47ef8d27b3bdf3a9feb04a03414ca4b9d70760b6318d3b84fb50",
            "cc5f9e0b1744e1f32ec2508776aa6b0a441a466a8399873fc33f9d51b3ae4d6f", [],
            Repeat("Celeste.Mod.DJMapHelper.Entities.FeatherBarrier::CheckCollide", 15)),
        new("DJMapHelper:player-v-theo", StaticAotCompatibility.DJName,
            StaticAotCompatibility.DJDllPath, StaticAotCompatibility.DJDllSha256,
            "IL.Celeste.Player", "OnCollideV",
            "System.Void Celeste.Player::AppleEverestOriginal_OnCollideV(Celeste.CollisionData)",
            "System.Void Celeste.Player::OnCollideV(Celeste.CollisionData)",
            "Celeste.Mod.DJMapHelper.Entities.TheoCrystalBarrier", "AddCollideCheck", true, 1,
            "d07b75bd9c0e47ef8d27b3bdf3a9feb04a03414ca4b9d70760b6318d3b84fb50",
            "0dd3e5e5322fc56809670a4273eb164165f682926dad364634a872ad754147e1",
            "b44772e4195ca2e9f5b0b08e8705a4a766504d6f70574051691970405ae9bb1f", [],
            Repeat("Celeste.Mod.DJMapHelper.Entities.TheoCrystalBarrier::CheckCollide", 15)),
        new("DJMapHelper:fling-awake", StaticAotCompatibility.DJName,
            StaticAotCompatibility.DJDllPath, StaticAotCompatibility.DJDllSha256,
            "IL.Celeste.FlingBird", "Awake",
            "System.Void Celeste.FlingBird::Awake(Monocle.Scene)",
            "System.Void Celeste.FlingBird::Awake(Monocle.Scene)",
            "Celeste.Mod.DJMapHelper.Entities.FlingBirdReversed", "ModFlingBirdAwake", true, 0,
            "1e0337d08e443cba4876899b602bbe31d9af5f1c5c9f0d13b04e03794cd2c55c",
            "1423bb5b7cba3820c9ad3f67fdef7377e59fc0c28487a8c0abb8ad09298c1d92",
            "a8c1cc2d2a19c9020938368478998d5dadae76f64270913c0dfe0d2038f3bb10", [],
            ["Celeste.Mod.DJMapHelper.Entities.FlingBirdReversed::RemoveFlingBirdReversedFrom"])
    ];

    private static readonly FrozenIlTransformPlan[] CaeruleaPlans =
    [
        new("CaeruleaHelper:BackdropRenderer.Render:ModifyBackdropRenderer", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.BackdropRenderer", "Render", "System.Void Celeste.BackdropRenderer::Render(Monocle.Scene)", "System.Void Celeste.BackdropRenderer::Render(Monocle.Scene)",
            "Celeste.Mod.CaeruleaHelper.Hooks.BackdropRenderHook", "ModifyBackdropRenderer", true, 0,
            "05d01dd1e76c07bcea466886f6fd1e9b958b6b5bec7d1979ab8236271bebdbcd", "184e32955ca81217fbaba56b9e08a5bfef303f02876c23bb078c91f410c6c526", "8d3917f245aaaa315152a81027a59ddb815f2cc640c1ddcc5ef6944643a7e3de", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.BackdropRenderHook::ShouldBeTakenByCaerulea", "Celeste.Mod.CaeruleaHelper.Hooks.BackdropRenderHook::RenderBackdrop"]),
        new("CaeruleaHelper:Strawberry.Added:ModSprite", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.Strawberry", "Added", "System.Void Celeste.Strawberry::Added(Monocle.Scene)", "System.Void Celeste.Strawberry::Added(Monocle.Scene)",
            "Celeste.Mod.CaeruleaHelper.Hooks.BerryHook", "ModSprite", true, 0,
            "939b9a1e1faaf43b20fa16d1fb56e13062e06d844bf7e2e7e997575a5f81220c", "b8948879c28f56887ff2bafd9ec16e70e26869b4607b9ce765edf71691f117ec", "7d7caf17dba771689cbbee54eea05ebaf795f36474b4b56e1360949489066109", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.BerryHook::HookSprite"]),
        new("CaeruleaHelper:Spikes.OnCollide:ModifySpikesCollideIL", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.Spikes", "OnCollide", "System.Void Celeste.Spikes::OnCollide(Celeste.Player)", "System.Void Celeste.Spikes::OnCollide(Celeste.Player)",
            "Celeste.Mod.CaeruleaHelper.Hooks.DashCorrectionProtection", "ModifySpikesCollideIL", true, 0,
            "dd08f8a2c271fd3ad42a4c2fb4673ac1b1630064f83f3ea302263f3df46b4c2c", "554cdbfe0a0c833e9c75876e5134b7aff16528c0a2d72ad5bb9fc78b39ae520e", "e28e6965dbfae21d1e60213189ea9b41a35b37b029a89e68e8e9e0ae8b69aa1b", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.DashCorrectionProtection::ShouldProtect", "Celeste.Mod.CaeruleaHelper.Hooks.DashCorrectionProtection::ShouldProtect", "Celeste.Mod.CaeruleaHelper.Hooks.DashCorrectionProtection::ShouldProtect", "Celeste.Mod.CaeruleaHelper.Hooks.DashCorrectionProtection::ShouldProtect"]),
        new("CaeruleaHelper:Player.DashUpdate:ModifyPlayerDashIL", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.Player", "DashUpdate", "System.Int32 Celeste.Player::DashUpdate()", "System.Int32 Celeste.Player::DashUpdate()",
            "Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook", "ModifyPlayerDashIL", true, 0,
            "df42662b02b0a77c87500037bdc0390f959cc1267a42d962cbea39a40fefb6ae", "f5018ad2a07cb777ddf9e1790cc2397d221437ba1232547f07102de5b6ad3894", "8199216c3624e9ddcb28c4a8c98d0864a70c16d1b0c418549f576b2e87f841a2", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook::JumpGraceTimerFactor"]),
        new("CaeruleaHelper:Player.RedDashUpdate:ModifyPlayerDashIL", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.Player", "RedDashUpdate", "System.Int32 Celeste.Player::RedDashUpdate()", "System.Int32 Celeste.Player::RedDashUpdate()",
            "Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook", "ModifyPlayerDashIL", true, 0,
            "4d812db45ef141b543a278cc513ca808b203ec32a0fbe2e739471b36edcf1cfb", "b41644bac8a95fe829afabff1b765aa6606a4feedf11e8d85493b790bfd7c77e", "8ab8cd706add63aad38aca5ba529a4173a611542752cf3d7106926bd3742b8ff", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook::JumpGraceTimerFactor"]),
        new("CaeruleaHelper:Player.WallJumpCheck:ModifyWallJumpCheckIL", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.Player", "WallJumpCheck", "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)", "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)",
            "Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook", "ModifyWallJumpCheckIL", true, 0,
            "7c87ba1ac56a612e7487dfc598e2d4b2f7d81817bd0d97fe1ec1e0d329128cf0", "9ff7c55d36cd3d7d5f290a1a41db985fc947a11b488af11b4065c9075726cb25", "00e1b541ac05c4bdcb76381af59fd4a10518b6c9f019f50a07fb0e89accb2794", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.SuperJumpHook::CheckWallBouncable"]),
        new("CaeruleaHelper:StarJumpBlock.Awake:ModifyAwake", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.StarJumpBlock", "Awake", "System.Void Celeste.StarJumpBlock::Awake(Monocle.Scene)", "System.Void Celeste.StarJumpBlock::Awake(Monocle.Scene)",
            "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock", "ModifyAwake", true, 0,
            "d6dcbeb3c3f59add0b5355861ab37af8f309729e2498ae61dce92004e37563cb", "6616737eac7570da180eec43d65c6c742bc5c052bf2b47164d465244d19237b5", "7d1c0061c5c2d5d1c0155f6b0ce48e0a47934fdd63eeb8df7d384025efe32d90", [],
            ["Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::WrapImage", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender", "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::ShouldRender"]),
        new("CaeruleaHelper:NorthernLights.Strand.Reset:ModifyStrandReset", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "IL.Celeste.NorthernLights/Strand", "Reset", "System.Void Celeste.NorthernLights/Strand::Reset(System.Single)", "System.Void Celeste.NorthernLights/Strand::Reset(System.Single)",
            "Celeste.Mod.CaeruleaHelper.Hooks.NorthernLightsHook", "ModifyStrandReset", true, 0,
            "2916caccb38f808aee2584bfb89016a02671bc56678627bc929b82f43d8e62cf", "24eb224abd9a5b42137daa81c87caa646cb1721e079f3fbaf302a963901d4ab0", "f14a019c7a6b02bd3433a639889b60905acfb91aed73e09b3f642035007a2ace", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.NorthernLightsHook::GetSeededRandomNumber", "Celeste.Mod.CaeruleaHelper.Hooks.NorthernLightsHook::GetSeededRandomNumber", "Celeste.Mod.CaeruleaHelper.Hooks.NorthernLightsHook::OverridePositionRNG"]),
        new("CaeruleaHelper:Player.DashCoroutine.MoveNext:ModifyDashCoroutineIL", CaeruleaName, "bin/CaeruleaHelper.dll", CaeruleaDllSha256,
            "DIRECT_ILHOOK", "DashCoroutine.MoveNext", "System.Boolean Celeste.Player/<DashCoroutine>d__*::MoveNext()", "System.Boolean Celeste.Player/<DashCoroutine>d__::MoveNext()",
            "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook", "ModifyDashCoroutineIL", true, 0,
            "65ad66657afe6ca8d9d440be58b258cb48159b966b515aafe7b3b3ae85325416", "ebafbfe93b806ace3b0e49324702ff7a164adbd0379f142af2c864a6ae697abc", "a941d338396c91447d9c0f331137cbd0d04016da5b2eaf32ab0f684a71a93f30", [],
            ["Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook::PositiveINF", "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook::PositiveINF"],
            "DIRECT_ILHOOK", "System.Void MonoMod.RuntimeDetour.ILHook::.ctor(System.Reflection.MethodBase,MonoMod.Cil.ILContext/Manipulator)",
            "typeof(Celeste.Player).GetMethod(\"DashCoroutine\", NonPublic|Instance).GetStateMachineTarget()",
            "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook.ModifyDashCoroutineIL", "absent", "implicit-true", "DashCoroutineHook", "MODULE_IMMUTABLE_ACTIVE")
    ];

    internal static IReadOnlyList<FrozenIlTransformPlan> Resolve(ModInput input, EverestYamlEntry metadata)
    {
        if (metadata.Name == FixtureName && metadata.Version == FixtureVersion &&
            input.SourceSha256 == FixtureSourceSha256 && metadata.DLL == "bin/DashToggleHelper.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "bin", "DashToggleHelper.dll");
            if (Hashing.FileSha256(dll) != FixtureDllSha256)
                throw new InvalidDataException("registered frozen-IL fixture DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "EverestCore" ||
                metadata.Dependencies[0].Version != "1.5421.0" || metadata.OptionalDependencies.Count != 1 ||
                metadata.OptionalDependencies[0].Name != "MoreDasheline" ||
                metadata.OptionalDependencies[0].Version != "1.7.1")
                throw new InvalidDataException("registered frozen-IL fixture metadata drifted");
            ValidateRegistrations(dll, DashTogglePlans, "Celeste.Mod.DashToggleHelper.DashToggleHelperModule");
            return DashTogglePlans;
        }
        if (metadata.Name == DisposableTheoName && metadata.Version == DisposableTheoVersion &&
            input.SourceSha256 == DisposableTheoSourceSha256 && metadata.DLL == "DisposableTheo.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "DisposableTheo.dll");
            if (Hashing.FileSha256(dll) != DisposableTheoDllSha256)
                throw new InvalidDataException("registered DisposableTheo DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "Everest" ||
                metadata.Dependencies[0].Version != "1.0.0" || metadata.OptionalDependencies.Count != 0)
                throw new InvalidDataException("registered DisposableTheo metadata drifted");
            ValidateRegistrations(dll, DisposableTheoPlans, "Celeste.Mod.DisposableTheo.DisposableTheoModule");
            return DisposableTheoPlans;
        }
        if (metadata.Name == VortexHelperName && metadata.Version == VortexHelperVersion &&
            input.SourceSha256 == VortexHelperSourceSha256 && metadata.DLL == "Code/bin/VortexHelper.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "Code", "bin", "VortexHelper.dll");
            if (Hashing.FileSha256(dll) != VortexHelperDllSha256)
                throw new InvalidDataException("registered VortexHelper DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "EverestCore" ||
                metadata.Dependencies[0].Version != "1.4465.0" || metadata.OptionalDependencies.Count != 1 ||
                metadata.OptionalDependencies[0].Name != "GravityHelper" ||
                metadata.OptionalDependencies[0].Version != "1.2.10")
                throw new InvalidDataException("registered VortexHelper metadata drifted");
            ValidateRegistrations(dll, VortexHelperPlans, VortexHelperName);
            return VortexHelperPlans;
        }
        if (metadata.Name == CaeruleaName && metadata.Version == CaeruleaVersion &&
            input.SourceSha256 == CaeruleaSourceSha256 && metadata.DLL == "bin/CaeruleaHelper.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "bin", "CaeruleaHelper.dll");
            if (Hashing.FileSha256(dll) != CaeruleaDllSha256)
                throw new InvalidDataException("registered CaeruleaHelper DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "EverestCore" ||
                metadata.Dependencies[0].Version != "1.5577.0" || metadata.OptionalDependencies.Count != 1 ||
                metadata.OptionalDependencies[0].Name != "DashlessHelper" ||
                metadata.OptionalDependencies[0].Version != "1.0.0")
                throw new InvalidDataException("registered CaeruleaHelper metadata drifted");
            ValidateCaeruleaRegistrations(dll);
            return CaeruleaPlans;
        }
        if (metadata.Name == StaticAotCompatibility.DJName &&
            metadata.Version == StaticAotCompatibility.DJVersion &&
            input.SourceSha256 == StaticAotCompatibility.DJSourceSha256 &&
            metadata.DLL == StaticAotCompatibility.DJDllPath)
        {
            string dll = Path.Combine(input.StagingRoot, StaticAotCompatibility.DJDllPath);
            if (Hashing.FileSha256(dll) != StaticAotCompatibility.DJDllSha256)
                throw new InvalidDataException("registered DJMapHelper frozen-IL DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "Everest" ||
                metadata.Dependencies[0].Version != "1.1963.0" || metadata.OptionalDependencies.Count != 0)
                throw new InvalidDataException("registered DJMapHelper frozen-IL metadata drifted");
            ValidateDJMapHelperRegistrations(dll);
            return DJMapHelperPlans;
        }
        return [];
    }

    private static string[] Repeat(string value, int count) => Enumerable.Repeat(value, count).ToArray();

    private static void ValidateRegistrations(string dll, IReadOnlyList<FrozenIlTransformPlan> plans, string module)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dll,
            new ReaderParameters { ReadSymbols = false });
        List<(string Containing, string Operation, string EventType, string EventName, string Manipulator)> found = [];
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index].Operand is not MethodReference called ||
                    !called.DeclaringType.Namespace.StartsWith("IL.", StringComparison.Ordinal) ||
                    !(called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                      called.Name.StartsWith("remove_", StringComparison.Ordinal))) continue;
                MethodReference? manipulator = body.Take(index).Reverse().Take(16)
                    .Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                    .Select(instruction => instruction.Operand).OfType<MethodReference>().FirstOrDefault();
                if (manipulator == null)
                    throw new InvalidDataException("frozen-IL registration does not use a bounded static ldftn delegate: " +
                        string.Join(" | ", body.Skip(Math.Max(0, index - 16)).Take(17).Select(instruction =>
                            instruction.OpCode + ":" + (instruction.Operand is MemberReference member ? member.FullName : instruction.Operand))));
                found.Add((method.FullName,
                    called.Name.StartsWith("add_", StringComparison.Ordinal) ? "add" : "remove",
                    called.DeclaringType.FullName, called.Name[(called.Name[0] == 'a' ? 4 : 7)..],
                    manipulator.DeclaringType.FullName + "::" + manipulator.Name));
            }
        }
        foreach (FrozenIlTransformPlan plan in plans.GroupBy(plan =>
                     plan.ManipulatorType + "\0" + plan.ManipulatorMethod, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            string cecilManipulatorType = plan.ManipulatorType.Replace('+', '/');
            string manipulator = cecilManipulatorType + "::" + plan.ManipulatorMethod;
            string addContainer = module == VortexHelperName
                ? "System.Void " + cecilManipulatorType + "::Hook()"
                : "System.Void " + module + "::Load()";
            string removeContainer = module == VortexHelperName
                ? "System.Void " + cecilManipulatorType + "::Unhook()"
                : "System.Void " + module + "::Unload()";
            if (found.Count(item => item.Operation == "add" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator &&
                    item.Containing == addContainer) != 1 ||
                found.Count(item => item.Operation == "remove" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator &&
                    item.Containing == removeContainer) != 1)
                throw new InvalidDataException("frozen-IL lifecycle registration contract drifted: " + plan.PlanId);
        }
        if (found.Count != plans.Count * 2)
            throw new InvalidDataException("registered fixture contains an unreviewed IL event subscription");
        if (assembly.MainModule.GetTypeReferences().Any(type =>
                type.FullName == "MonoMod.RuntimeDetour.ILHook"))
            throw new InvalidDataException("registered fixture unexpectedly uses direct ILHook");
    }

    private static void ValidateCaeruleaRegistrations(string dll)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dll,
            new ReaderParameters { ReadSymbols = false });
        FrozenIlTransformPlan[] events = CaeruleaPlans.Where(plan => plan.Mechanism == "HOOKGEN_IL_EVENT").ToArray();
        List<(string Operation, string EventType, string EventName, string Manipulator)> found = [];
        int directConstructors = 0;
        int directDisposals = 0;
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        foreach (Instruction instruction in method.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference called) continue;
            if (called.DeclaringType.FullName.StartsWith("IL.", StringComparison.Ordinal) &&
                (called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                 called.Name.StartsWith("remove_", StringComparison.Ordinal)))
            {
                MethodReference? manipulator = method.Body.Instructions.TakeWhile(value => value != instruction)
                    .Reverse().Take(16).Where(value => value.OpCode == OpCodes.Ldftn)
                    .Select(value => value.Operand).OfType<MethodReference>().FirstOrDefault();
                if (manipulator == null)
                    throw new InvalidDataException("CaeruleaHelper IL event is not a bounded static delegate");
                found.Add((called.Name.StartsWith("add_", StringComparison.Ordinal) ? "add" : "remove",
                    called.DeclaringType.FullName, called.Name[(called.Name[0] == 'a' ? 4 : 7)..],
                    manipulator.DeclaringType.FullName.Replace('/', '+') + "::" + manipulator.Name));
            }
            if (called.DeclaringType.FullName == "MonoMod.RuntimeDetour.ILHook" && called.Name == ".ctor")
            {
                directConstructors++;
                if (method.FullName != "System.Void Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook::Load()" ||
                    instruction.Offset != 55 || called.Parameters.Count != 2)
                    throw new InvalidDataException("CaeruleaHelper direct ILHook constructor contract drifted");
            }
            if (called.DeclaringType.FullName == "MonoMod.RuntimeDetour.ILHook" && called.Name == "Dispose")
            {
                directDisposals++;
                if (method.FullName != "System.Void Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook::Unload()" ||
                    instruction.Offset != 6)
                    throw new InvalidDataException("CaeruleaHelper direct ILHook lifetime contract drifted");
            }
        }
        foreach (FrozenIlTransformPlan plan in events)
        {
            string manipulator = plan.ManipulatorType + "::" + plan.ManipulatorMethod;
            if (found.Count(item => item.Operation == "add" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator) != 1 ||
                found.Count(item => item.Operation == "remove" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator) != 1)
                throw new InvalidDataException("CaeruleaHelper frozen-IL event contract drifted: " + plan.PlanId +
                    " found=" + string.Join('|', found.Select(item => item.EventType + "." + item.EventName +
                        ":" + item.Operation + ":" + item.Manipulator)));
        }
        if (found.Count != events.Length * 2 || directConstructors != 1 || directDisposals != 1)
            throw new InvalidDataException("CaeruleaHelper contains an unreviewed IL lifecycle operation");
        TypeDefinition dashSpeed = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook");
        FieldDefinition storage = dashSpeed.Fields.Single(field => field.Name == "DashCoroutineHook");
        if (storage.FieldType.FullName != "MonoMod.RuntimeDetour.ILHook" ||
            dashSpeed.Methods.Any(method => method.Name is "Apply" or "Undo"))
            throw new InvalidDataException("CaeruleaHelper direct ILHook storage/apply contract drifted");
    }

    private static void ValidateDJMapHelperRegistrations(string dll)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dll,
            new ReaderParameters { ReadSymbols = false });
        List<(string Operation, string EventType, string EventName, string Manipulator)> found = [];
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index].Operand is not MethodReference called ||
                    !called.DeclaringType.FullName.StartsWith("IL.", StringComparison.Ordinal) ||
                    !(called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                      called.Name.StartsWith("remove_", StringComparison.Ordinal))) continue;
                MethodReference? manipulator = body.Take(index).Reverse().Take(20)
                    .Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                    .Select(instruction => instruction.Operand).OfType<MethodReference>().FirstOrDefault();
                if (manipulator == null || !manipulator.Resolve().IsStatic)
                    throw new InvalidDataException("DJMapHelper frozen-IL registration is not a static ldftn delegate");
                found.Add((called.Name.StartsWith("add_", StringComparison.Ordinal) ? "add" : "remove",
                    called.DeclaringType.FullName, called.Name[(called.Name[0] == 'a' ? 4 : 7)..],
                    manipulator.DeclaringType.FullName.Replace('/', '+') + "::" + manipulator.Name));
            }
        }
        foreach (FrozenIlTransformPlan plan in DJMapHelperPlans)
        {
            string manipulator = plan.ManipulatorType + "::" + plan.ManipulatorMethod;
            if (found.Count(item => item.Operation == "add" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator) != 1 ||
                found.Count(item => item.Operation == "remove" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator) != 1)
                throw new InvalidDataException("DJMapHelper frozen-IL lifecycle contract drifted: " + plan.PlanId);
        }
        if (found.Count != DJMapHelperPlans.Length * 2 || assembly.MainModule.GetTypeReferences().Any(type =>
                type.FullName == "MonoMod.RuntimeDetour.ILHook"))
            throw new InvalidDataException("DJMapHelper contains an unreviewed IL lifecycle operation");
    }

    internal static void RewriteDeviceAssembly(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        if (plans.Count == 0) return;
        if (plans.Any(plan => plan.Owner != assembly.Name.Name) ||
            assembly.Name.Name is not (FixtureName or DisposableTheoName or VortexHelperName or CaeruleaName or
                StaticAotCompatibility.DJName))
            throw new InvalidDataException("frozen-IL device rewrite received an unregistered assembly");

        if (assembly.Name.Name == StaticAotCompatibility.DJName)
        {
            RewriteDJMapHelper(assembly, plans);
            return;
        }

        if (assembly.Name.Name == CaeruleaName)
        {
            RewriteCaerulea(assembly, plans);
            return;
        }

        if (assembly.Name.Name == DisposableTheoName)
        {
            RewriteDisposableTheo(assembly, plans);
            return;
        }
        if (assembly.Name.Name == VortexHelperName)
        {
            RewriteVortexHelper(assembly, plans);
            return;
        }

        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes)
            .Single(type => type.FullName == "Celeste.Mod.DashToggleHelper.DashToggleHelperModule");
        HashSet<string> manipulators = plans.Select(plan => plan.ManipulatorMethod).ToHashSet(StringComparer.Ordinal);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Load"), "add_", plans.Count, 6);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Unload"), "remove_", plans.Count, 6);

        foreach (string name in manipulators)
        {
            MethodDefinition method = module.Methods.Single(candidate => candidate.Name == name);
            module.Methods.Remove(method);
        }
        foreach (string name in plans.SelectMany(plan => plan.InjectedMethods).Distinct(StringComparer.Ordinal))
        {
            MethodDefinition method = module.Methods.Single(candidate => candidate.Name == name);
            method.IsPublic = true;
            method.IsPrivate = false;
        }

        // The fixture's public release declares MoreDasheline as optional. The
        // selected closure does not contain it, so freeze exactly the ordinary
        // fallback branch and remove its unreachable adapter/reference.
        RewriteDashColorFallback(module, assembly.MainModule);
        RewriteOptionalDependencyConstructor(module, assembly.MainModule);
        FieldDefinition optionalLoaded = module.Fields.Single(field => field.Name == "moreDashelineLoaded");
        module.Fields.Remove(optionalLoaded);
        TypeDefinition optional = assembly.MainModule.Types.Single(type =>
            type.FullName == "Celeste.Mod.DashToggleHelper.MoreDashelineIntegration");
        assembly.MainModule.Types.Remove(optional);

        TypeDefinition[] hostOnlyTypes = assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
            type.Name.StartsWith("<>", StringComparison.Ordinal) && TypeUsesHostIl(type)).ToArray();
        foreach (TypeDefinition type in hostOnlyTypes)
        {
            if (type.DeclaringType == null) assembly.MainModule.Types.Remove(type);
            else type.DeclaringType.NestedTypes.Remove(type);
        }

        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        foreach (string referenceName in new[] { "MoreDasheline", "MonoMod.Utils", "Mono.Cecil" })
        {
            AssemblyNameReference? reference = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
                value.Name == referenceName);
            if (reference == null) continue;
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, referenceName).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("frozen-IL host reference survived device rewrite: " +
                                               referenceName + ":" + string.Join(',', residual));
            // Cecil retains imported-but-now-unreferenced TypeRef rows in memory after their
            // owning methods/types are removed. Those rows are not part of the executable
            // assembly graph and are discarded when the module is written. Validate the live
            // graph above, then remove the now-unneeded AssemblyRef explicitly.
            assembly.MainModule.AssemblyReferences.Remove(reference);
        }
    }

    private static void RewriteDisposableTheoEverestAbi(AssemblyDefinition assembly, TypeDefinition module)
    {
        // DisposableTheo 1.0.6 was compiled against three small APIs from its
        // pinned desktop Everest Celeste contract. Keep the shared canonical
        // Apple game tree unchanged and normalize this one exact, hash-locked
        // binary instead:
        //
        //  * Everest's integer VirtualIntegerAxis conversion is the public
        //    Value field in canonical Celeste.
        //  * Everest's two-argument Input.Rumble overload maps to canonical
        //    Celeste's source-aware three-argument overload with a null source.
        //  * CreateEnabledEntry is an Everest reflection-menu hook. The closed
        //    Apple runtime owns settings through generated descriptors, so the
        //    unreachable desktop menu body is deliberately reduced to a no-op.
        //
        // Every count below is part of the exact pinned-DLL contract. Drift
        // fails closed rather than expanding this into a generic ABI shim.
        MethodDefinition throwMethod = module.Methods.SingleOrDefault(method => method.Name == "Player_Throw")
            ?? throw new InvalidDataException("DisposableTheo pinned Player_Throw ABI method missing: " +
                string.Join(',', module.Methods.Select(method => method.Name)));
        Instruction[] original = throwMethod.Body.Instructions.ToArray();
        Instruction[] axisConversions = original.Where(instruction =>
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "Monocle.VirtualIntegerAxis" &&
            called.Name == "op_Implicit" && called.Parameters.Count == 1 &&
            called.ReturnType.MetadataType == MetadataType.Int32).ToArray();
        if (axisConversions.Length != 4)
            throw new InvalidDataException("DisposableTheo pinned integer-axis ABI contract drifted");
        foreach (Instruction instruction in axisConversions)
        {
            MethodReference conversion = (MethodReference)instruction.Operand;
            instruction.OpCode = OpCodes.Ldfld;
            instruction.Operand = assembly.MainModule.ImportReference(new FieldReference(
                "Value", assembly.MainModule.TypeSystem.Int32, conversion.DeclaringType));
        }

        Instruction[] rumbleCalls = original.Where(instruction =>
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "Celeste.Input" && called.Name == "Rumble" &&
            called.Parameters.Count == 2).ToArray();
        if (rumbleCalls.Length != 2)
            throw new InvalidDataException("DisposableTheo pinned rumble ABI contract drifted");
        ILProcessor processor = throwMethod.Body.GetILProcessor();
        foreach (Instruction instruction in rumbleCalls)
        {
            MethodReference old = (MethodReference)instruction.Operand;
            MethodReference replacement = new("Rumble", assembly.MainModule.TypeSystem.Void,
                assembly.MainModule.ImportReference(old.DeclaringType))
            {
                HasThis = false,
                CallingConvention = old.CallingConvention
            };
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(
                old.Parameters[0].ParameterType)));
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(
                old.Parameters[1].ParameterType)));
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
            processor.InsertBefore(instruction, processor.Create(OpCodes.Ldnull));
            instruction.Operand = assembly.MainModule.ImportReference(replacement);
        }

        TypeDefinition settings = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DisposableTheo.DisposableTheoSettings");
        CustomAttribute[] desktopSettingAttributes = settings.Properties
            .SelectMany(property => property.CustomAttributes)
            .Where(attribute => attribute.AttributeType.FullName == "Celeste.Mod.SettingIgnoreAttribute")
            .ToArray();
        if (desktopSettingAttributes.Length != 3)
            throw new InvalidDataException("DisposableTheo pinned settings metadata contract drifted");
        foreach (PropertyDefinition property in settings.Properties)
            for (int index = property.CustomAttributes.Count - 1; index >= 0; index--)
                if (property.CustomAttributes[index].AttributeType.FullName == "Celeste.Mod.SettingIgnoreAttribute")
                    property.CustomAttributes.RemoveAt(index);
        MethodDefinition menu = settings.Methods.Single(method => method.Name == "CreateEnabledEntry" &&
            method.Parameters.Count == 2);
        int desktopMenuReferences = menu.Body.Instructions.Count(instruction =>
            instruction.Operand is MemberReference member &&
            (member.DeclaringType.FullName == "Celeste.TextMenuExt/OptionSubMenu" ||
             member.DeclaringType.FullName.StartsWith("Celeste.TextMenuExt", StringComparison.Ordinal)));
        if (desktopMenuReferences != 5)
            throw new InvalidDataException("DisposableTheo pinned desktop settings-menu ABI contract drifted: " +
                desktopMenuReferences);
        menu.Body.Instructions.Clear();
        menu.Body.ExceptionHandlers.Clear();
        menu.Body.Variables.Clear();
        menu.Body.InitLocals = false;
        menu.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
    }

    private static void RewriteDisposableTheo(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DisposableTheo.DisposableTheoModule");
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Load"), "add_", plans.Count, 2);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Unload"), "remove_", plans.Count, 1);
        foreach (string name in plans.Select(plan => plan.ManipulatorMethod).Distinct(StringComparer.Ordinal))
            module.Methods.Remove(module.Methods.Single(method => method.Name == name));

        TypeDefinition singleton = module.NestedTypes.Single(type => type.Name == "<>c");
        HashSet<string> runtimeTargets = plans.SelectMany(plan => plan.ExpectedDelegateTargets)
            .Select(value => value[(value.LastIndexOf("::", StringComparison.Ordinal) + 2)..])
            .ToHashSet(StringComparer.Ordinal);
        foreach (MethodDefinition method in singleton.Methods.Where(method =>
                     MethodUsesHostIl(method) || (method.Name.StartsWith("<", StringComparison.Ordinal) &&
                     !runtimeTargets.Contains(method.Name))).ToArray())
            singleton.Methods.Remove(method);
        foreach (FieldDefinition field in singleton.Fields.Where(field =>
                     field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
                     field.FieldType.FullName.Contains("Mono.Cecil", StringComparison.Ordinal) ||
                     field.FieldType.FullName.Contains("MonoMod.Cil", StringComparison.Ordinal)).ToArray())
            singleton.Fields.Remove(field);
        MakePublic(singleton);
        foreach (FieldDefinition field in singleton.Fields.Where(field => field.Name == "<>9"))
        {
            field.IsPublic = true;
            field.IsPrivate = false;
        }
        foreach (MethodDefinition method in singleton.Methods.Where(method => runtimeTargets.Contains(method.Name)))
        {
            method.IsPublic = true;
            method.IsPrivate = false;
        }

        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        RewriteDisposableTheoEverestAbi(assembly, module);

        foreach (string referenceName in new[] { "MonoMod.Utils", "Mono.Cecil" })
        {
            AssemblyNameReference? reference = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
                value.Name == referenceName);
            if (reference == null) continue;
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, referenceName).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("frozen-IL host reference survived DisposableTheo rewrite: " +
                                               referenceName + ":" + string.Join(',', residual));
            assembly.MainModule.AssemblyReferences.Remove(reference);
        }
    }

    private static void RewriteVortexHelper(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        TypeDefinition floor = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.VortexHelper.Entities.FloorBooster/Hooks");
        TypeDefinition purple = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.VortexHelper.Entities.PurpleBooster/Hooks");
        RewriteLifecycleMethod(floor.Methods.Single(method => method.Name == "Hook"), "add_", 1, 3);
        RewriteLifecycleMethod(floor.Methods.Single(method => method.Name == "Unhook"), "remove_", 1, 3);
        RewriteLifecycleMethod(purple.Methods.Single(method => method.Name == "Hook"), "add_", 1, 1);
        RewriteLifecycleMethod(purple.Methods.Single(method => method.Name == "Unhook"), "remove_", 1, 1);

        foreach (FrozenIlTransformPlan plan in plans)
        {
            TypeDefinition owner = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
                type.FullName == plan.ManipulatorType.Replace('+', '/'));
            owner.Methods.Remove(owner.Methods.Single(method => method.Name == plan.ManipulatorMethod));
        }

        HashSet<string> runtimeTargets = plans.SelectMany(plan => plan.ExpectedDelegateTargets)
            .Select(value => value[(value.LastIndexOf("::", StringComparison.Ordinal) + 2)..])
            .ToHashSet(StringComparer.Ordinal);
        foreach (TypeDefinition compilerType in new[] { floor, purple }.SelectMany(type => type.NestedTypes)
                     .Where(type => type.Name is "<>c" or "<>O").ToArray())
        {
            foreach (MethodDefinition method in compilerType.Methods.Where(method =>
                         MethodUsesHostIl(method) ||
                         (method.Name.StartsWith("<Player_", StringComparison.Ordinal) &&
                          !runtimeTargets.Contains(method.Name))).ToArray())
                compilerType.Methods.Remove(method);
            foreach (FieldDefinition field in compilerType.Fields.Where(field =>
                         field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
                         field.FieldType.FullName.Contains("Mono.Cecil", StringComparison.Ordinal) ||
                         field.FieldType.FullName.Contains("MonoMod.Cil", StringComparison.Ordinal)).ToArray())
                compilerType.Fields.Remove(field);
            if (compilerType.Methods.Any(method => runtimeTargets.Contains(method.Name)))
            {
                MakePublic(compilerType);
                foreach (FieldDefinition field in compilerType.Fields.Where(field => field.Name == "<>9"))
                {
                    field.IsPublic = true;
                    field.IsPrivate = false;
                }
                foreach (MethodDefinition method in compilerType.Methods.Where(method =>
                             runtimeTargets.Contains(method.Name)))
                {
                    method.IsPublic = true;
                    method.IsPrivate = false;
                }
            }
        }
        foreach (string name in plans.SelectMany(plan => plan.InjectedMethods).Distinct(StringComparer.Ordinal))
        {
            MethodDefinition method = assembly.MainModule.Types.SelectMany(AllTypes).SelectMany(type => type.Methods)
                .Single(candidate => candidate.Name == name);
            method.IsPublic = true;
            method.IsPrivate = false;
            MakePublic(method.DeclaringType);
        }

        AssemblyNameReference? cecil = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
            value.Name == "Mono.Cecil");
        if (cecil != null)
        {
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, "Mono.Cecil").ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("frozen-IL host reference survived VortexHelper rewrite: " +
                                               string.Join(',', residual));
            assembly.MainModule.AssemblyReferences.Remove(cecil);
        }
        // MonoMod.Utils intentionally remains blocked by VortexHelper's separate
        // live DynamicData usage. Stage 25H-C does not broaden into that class.
    }

    private static void RewriteCaerulea(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        if (plans.Count != 9 || plans.Count(plan => plan.Mechanism == "DIRECT_ILHOOK") != 1)
            throw new InvalidDataException("CaeruleaHelper complete frozen-IL census drifted");

        int removedEvents = 0;
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            foreach (int index in Enumerable.Range(0, body.Length).Where(index =>
                         body[index].Operand is MethodReference called &&
                         called.DeclaringType.FullName.StartsWith("IL.", StringComparison.Ordinal) &&
                         (called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                          called.Name.StartsWith("remove_", StringComparison.Ordinal))))
            {
                int lower = Math.Max(0, index - 16);
                int start = Enumerable.Range(lower, index - lower)
                    .Where(candidate => body[candidate].OpCode == OpCodes.Ldsfld).LastOrDefault(-1);
                if (start < 0)
                    start = Enumerable.Range(lower, index - lower)
                        .Where(candidate => body[candidate].OpCode == OpCodes.Ldnull).LastOrDefault(-1);
                if (start < 0 || !body.Skip(start).Take(index - start).Any(value => value.OpCode == OpCodes.Ldftn))
                    throw new InvalidDataException("CaeruleaHelper IL event removal shape drifted: " + method.FullName);
                for (int cursor = start; cursor <= index; cursor++)
                {
                    body[cursor].OpCode = OpCodes.Nop;
                    body[cursor].Operand = null;
                }
                removedEvents++;
            }
        }
        if (removedEvents != 16)
            throw new InvalidDataException("CaeruleaHelper frozen event removal count drifted: " + removedEvents);

        foreach (FrozenIlTransformPlan plan in plans.GroupBy(plan =>
                     plan.ManipulatorType + "\0" + plan.ManipulatorMethod, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            TypeDefinition owner = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
                type.FullName == plan.ManipulatorType.Replace('+', '/'));
            MethodDefinition manipulator = owner.Methods.Single(method => method.Name == plan.ManipulatorMethod);
            owner.Methods.Remove(manipulator);
        }

        TypeDefinition dashSpeed = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook");
        RewriteAsReturn(dashSpeed.Methods.Single(method => method.Name == "Load"));
        RewriteAsReturn(dashSpeed.Methods.Single(method => method.Name == "Unload"));
        dashSpeed.Fields.Remove(dashSpeed.Fields.Single(field => field.Name == "DashCoroutineHook"));

        // DashlessHelper is an optional dependency outside this selected static
        // closure. Freeze the exact ordinary StarJumpBlock.Open fallback and
        // remove only the unreachable reflective direct-Hook branch.
        TypeDefinition star = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock");
        RewriteOnOnly(star.Methods.Single(method => method.Name == "Load"), "add_", "Open", "IsOpen");
        RewriteOnOnly(star.Methods.Single(method => method.Name == "Unload"), "remove_", "Open", "IsOpen");
        MethodDefinition starInitializer = star.Methods.Single(method => method.IsConstructor && method.IsStatic);
        Instruction[] initializerBody = starInitializer.Body.Instructions.ToArray();
        foreach (string fieldName in new[] { "hook_dashless_open", "normal_hook" })
        {
            int store = Array.FindIndex(initializerBody, instruction => instruction.OpCode == OpCodes.Stsfld &&
                instruction.Operand is FieldReference field && field.Name == fieldName);
            if (store <= 0)
                throw new InvalidDataException("CaeruleaHelper optional field initializer drifted: " + fieldName);
            initializerBody[store - 1].OpCode = OpCodes.Nop;
            initializerBody[store - 1].Operand = null;
            initializerBody[store].OpCode = OpCodes.Nop;
            initializerBody[store].Operand = null;
        }
        foreach (string field in new[] { "hook_dashless_open", "normal_hook" })
            star.Fields.Remove(star.Fields.Single(value => value.Name == field));
        star.Methods.Remove(star.Methods.Single(method => method.Name == "IsOpenDashless"));
        TypeDefinition dashlessDelegate = star.NestedTypes.Single(type => type.Name == "DashlessHelperHookOpen");
        star.NestedTypes.Remove(dashlessDelegate);

        HashSet<string> runtimeMethods = plans.SelectMany(plan => plan.ExpectedDelegateTargets)
            .Select(value => value[(value.LastIndexOf("::", StringComparison.Ordinal) + 2)..])
            .ToHashSet(StringComparer.Ordinal);
        foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes).ToArray())
        {
            foreach (MethodDefinition method in type.Methods.Where(method => MethodUsesHostIl(method)).ToArray())
                type.Methods.Remove(method);
            foreach (FieldDefinition field in type.Fields.Where(field =>
                         field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
                         field.FieldType.FullName.Contains("MonoMod.Cil", StringComparison.Ordinal) ||
                         field.FieldType.FullName.Contains("Mono.Cecil", StringComparison.Ordinal) ||
                         field.FieldType.FullName == "MonoMod.RuntimeDetour.ILHook").ToArray())
                type.Fields.Remove(field);
            foreach (MethodDefinition method in type.Methods.Where(method => runtimeMethods.Contains(method.Name)))
            {
                method.IsPublic = true;
                method.IsPrivate = false;
                MakePublic(type);
            }
        }
        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        foreach (string referenceName in new[] { "Mono.Cecil", "MonoMod.Utils" })
        {
            AssemblyNameReference? reference = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
                value.Name == referenceName);
            if (reference == null) continue;
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, referenceName).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("CaeruleaHelper host IL reference survived: " +
                                               referenceName + ":" + string.Join(',', residual));
            assembly.MainModule.AssemblyReferences.Remove(reference);
        }
    }

    private static void RewriteDJMapHelper(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        if (plans.Count != 5 || plans.Any(plan => plan.Mechanism != "HOOKGEN_IL_EVENT"))
            throw new InvalidDataException("DJMapHelper complete frozen-IL census drifted");

        int removedEvents = 0;
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            foreach (int index in Enumerable.Range(0, body.Length).Where(index =>
                         body[index].Operand is MethodReference called &&
                         called.DeclaringType.FullName.StartsWith("IL.", StringComparison.Ordinal) &&
                         (called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                          called.Name.StartsWith("remove_", StringComparison.Ordinal))))
            {
                int lower = Math.Max(0, index - 20);
                int start = Enumerable.Range(lower, index - lower)
                    .Where(candidate => body[candidate].OpCode == OpCodes.Ldnull ||
                                        body[candidate].OpCode == OpCodes.Ldsfld)
                    .LastOrDefault(-1);
                if (start < 0 || !body.Skip(start).Take(index - start).Any(value => value.OpCode == OpCodes.Ldftn))
                    throw new InvalidDataException("DJMapHelper IL event removal shape drifted: " + method.FullName);
                for (int cursor = start; cursor <= index; cursor++)
                {
                    body[cursor].OpCode = OpCodes.Nop;
                    body[cursor].Operand = null;
                }
                removedEvents++;
            }
        }
        if (removedEvents != plans.Count * 2)
            throw new InvalidDataException("DJMapHelper frozen event removal count drifted: " + removedEvents);

        foreach (FrozenIlTransformPlan plan in plans.GroupBy(plan =>
                     plan.ManipulatorType + "\0" + plan.ManipulatorMethod, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            TypeDefinition owner = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
                type.FullName == plan.ManipulatorType.Replace('+', '/'));
            owner.Methods.Remove(owner.Methods.Single(method => method.Name == plan.ManipulatorMethod));
        }

        HashSet<string> runtimeTargets = plans.SelectMany(plan => plan.ExpectedDelegateTargets)
            .ToHashSet(StringComparer.Ordinal);
        foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes).ToArray())
        {
            foreach (MethodDefinition method in type.Methods.Where(MethodUsesDJHostIl).ToArray())
                type.Methods.Remove(method);
            foreach (FieldDefinition field in type.Fields.Where(field =>
                         field.FieldType.Scope?.Name == "Mono.Cecil" ||
                         field.FieldType.FullName.StartsWith("MonoMod.Cil.", StringComparison.Ordinal) ||
                         field.FieldType.FullName.StartsWith("Mono.Cecil.", StringComparison.Ordinal)).ToArray())
                type.Fields.Remove(field);
            foreach (MethodDefinition method in type.Methods.Where(method =>
                         runtimeTargets.Contains(type.FullName.Replace('/', '+') + "::" + method.Name)))
            {
                method.IsPublic = true;
                method.IsPrivate = false;
                MakePublic(type);
            }
        }
        foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes).ToArray()
                     .Where(type => type.Name.StartsWith("<>", StringComparison.Ordinal) && TypeUsesDJHostIl(type)))
        {
            if (type.DeclaringType == null) assembly.MainModule.Types.Remove(type);
            else type.DeclaringType.NestedTypes.Remove(type);
        }

        AssemblyNameReference? cecil = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference =>
            reference.Name == "Mono.Cecil");
        if (cecil != null)
        {
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, "Mono.Cecil").ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("DJMapHelper host IL reference survived: " + string.Join(',', residual));
            // Cecil retains an otherwise-unreachable TypeRef row for Instruction after the
            // five host-only manipulators are removed. Removing its AssemblyRef without
            // repairing that orphan serializes a null scope which Apple's full trimmer
            // rejects while sweeping metadata. ActiveReferenceIdentities proved that no
            // executable signature or body can observe these rows, so normalize the orphan
            // metadata itself to System.Object before removing the host-only dependency.
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences()
                         .Where(type => type.GetElementType().Scope?.Name == cecil.Name))
            {
                TypeReference orphan = type.GetElementType();
                orphan.Namespace = assembly.MainModule.TypeSystem.Object.Namespace;
                orphan.Name = assembly.MainModule.TypeSystem.Object.Name;
                orphan.Scope = assembly.MainModule.TypeSystem.Object.Scope;
                orphan.IsValueType = false;
            }
            assembly.MainModule.AssemblyReferences.Remove(cecil);
        }
    }

    private static void RewriteAsReturn(MethodDefinition method)
    {
        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        method.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
    }

    private static void RewriteOnOnly(MethodDefinition method, string operation, string eventName, string handlerName)
    {
        Instruction[] original = method.Body.Instructions.ToArray();
        MethodReference eventMethod = original.Select(value => value.Operand).OfType<MethodReference>().Single(value =>
            value.DeclaringType.FullName == "On.Celeste.StarJumpBlock" &&
            value.Name == operation + eventName);
        MethodReference handler = original.Select(value => value.Operand).OfType<MethodReference>().Single(value =>
            value.DeclaringType.FullName == method.DeclaringType.FullName && value.Name == handlerName);
        MethodReference constructor = original.Select(value => value.Operand).OfType<MethodReference>()
            .First(value => value.Name == ".ctor" && value.DeclaringType.FullName ==
                "On.Celeste.StarJumpBlock/hook_Open");
        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldnull));
        il.Append(il.Create(OpCodes.Ldftn, handler));
        il.Append(il.Create(OpCodes.Newobj, constructor));
        il.Append(il.Create(OpCodes.Call, eventMethod));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static IEnumerable<string> ActiveReferenceIdentities(ModuleDefinition module, string assemblyName)
    {
        foreach (TypeDefinition type in module.Types.SelectMany(AllTypes))
        {
            if (UsesAssembly(type.BaseType, assemblyName)) yield return "base:" + type.FullName;
            foreach (InterfaceImplementation implementation in type.Interfaces)
                if (UsesAssembly(implementation.InterfaceType, assemblyName)) yield return "interface:" + type.FullName;
            foreach (FieldDefinition field in type.Fields)
                if (UsesAssembly(field.FieldType, assemblyName)) yield return "field:" + field.FullName;
            foreach (PropertyDefinition property in type.Properties)
                if (UsesAssembly(property.PropertyType, assemblyName) ||
                    property.Parameters.Any(parameter => UsesAssembly(parameter.ParameterType, assemblyName)))
                    yield return "property:" + property.FullName;
            foreach (EventDefinition @event in type.Events)
                if (UsesAssembly(@event.EventType, assemblyName)) yield return "event:" + @event.FullName;
            foreach (MethodDefinition method in type.Methods)
            {
                if (UsesAssembly(method.ReturnType, assemblyName) ||
                    method.Parameters.Any(parameter => UsesAssembly(parameter.ParameterType, assemblyName)) ||
                    method.Overrides.Any(@override => UsesAssembly(@override.DeclaringType, assemblyName)))
                    yield return "signature:" + method.FullName;
                if (!method.HasBody) continue;
                if (method.Body.Variables.Any(variable => UsesAssembly(variable.VariableType, assemblyName)) ||
                    method.Body.ExceptionHandlers.Any(handler => UsesAssembly(handler.CatchType, assemblyName)))
                    yield return "body-type:" + method.FullName;
                foreach (MemberReference member in method.Body.Instructions.Select(instruction => instruction.Operand)
                             .OfType<MemberReference>())
                    if (UsesAssembly(member.DeclaringType, assemblyName) ||
                        member is TypeReference referencedType && UsesAssembly(referencedType, assemblyName))
                        yield return "body-member:" + method.FullName + "->" + member.FullName;
            }
        }
    }

    private static bool UsesAssembly(TypeReference? type, string assemblyName) =>
        type != null && type.GetElementType().Scope is AssemblyNameReference reference &&
        string.Equals(reference.Name, assemblyName, StringComparison.Ordinal);

    private static void RewriteLifecycleMethod(MethodDefinition method, string operation, int expectedIlEvents,
        int expectedOnEvents)
    {
        Instruction[] body = method.Body.Instructions.ToArray();
        int ilEvents = body.Count(instruction => instruction.Operand is MethodReference called &&
            called.DeclaringType.Namespace.StartsWith("IL.", StringComparison.Ordinal) &&
            called.Name.StartsWith(operation, StringComparison.Ordinal));
        if (ilEvents != expectedIlEvents)
            throw new InvalidDataException("frozen-IL lifecycle event count drifted: " + method.FullName);

        List<(MethodReference Event, MethodReference Constructor, MethodReference Handler)> onEvents = [];
        for (int index = 0; index < body.Length; index++)
        {
            if (body[index].Operand is not MethodReference called ||
                !called.DeclaringType.Namespace.StartsWith("On.", StringComparison.Ordinal) ||
                !called.Name.StartsWith(operation, StringComparison.Ordinal)) continue;
            int functionIndex = Enumerable.Range(Math.Max(0, index - 20), Math.Min(20, index))
                .Reverse().FirstOrDefault(candidate => body[candidate].OpCode == OpCodes.Ldftn &&
                    body[candidate].Operand is MethodReference);
            if (body[functionIndex].Operand is not MethodReference handler)
                throw new InvalidDataException("static On lifecycle handler could not be resolved");
            MethodReference? constructor = body.Skip(functionIndex + 1).Take(index - functionIndex - 1)
                .Where(instruction => instruction.OpCode == OpCodes.Newobj)
                .Select(instruction => instruction.Operand).OfType<MethodReference>().LastOrDefault();
            if (constructor == null)
                throw new InvalidDataException("static On lifecycle delegate constructor could not be resolved");
            onEvents.Add((called, constructor, handler));
        }
        if (onEvents.Count != expectedOnEvents)
            throw new InvalidDataException("frozen-IL On lifecycle contract drifted: " + method.FullName);

        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        foreach ((MethodReference eventMethod, MethodReference constructor, MethodReference handler) in onEvents)
        {
            il.Append(handler.HasThis ? il.Create(OpCodes.Ldarg_0) : il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldftn, handler));
            il.Append(il.Create(OpCodes.Newobj, constructor));
            il.Append(il.Create(OpCodes.Call, eventMethod));
        }
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void RewriteDashColorFallback(TypeDefinition module, ModuleDefinition assembly)
    {
        MethodDefinition method = module.Methods.Single(candidate => candidate.Name == "getColor" &&
            candidate.IsStatic && candidate.Parameters.Count == 1);
        FieldReference colors = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<FieldReference>().First(field => field.Name == "dashColors");
        MethodReference contains = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "ContainsKey");
        MethodReference item = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "get_Item");
        MethodReference white = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "get_White");
        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        Instruction fallback = il.Create(OpCodes.Call, assembly.ImportReference(white));
        il.Append(il.Create(OpCodes.Ldsfld, assembly.ImportReference(colors)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Callvirt, assembly.ImportReference(contains)));
        il.Append(il.Create(OpCodes.Brfalse, fallback));
        il.Append(il.Create(OpCodes.Ldsfld, assembly.ImportReference(colors)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Callvirt, assembly.ImportReference(item)));
        il.Append(il.Create(OpCodes.Ret));
        il.Append(fallback);
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void RewriteOptionalDependencyConstructor(TypeDefinition module, ModuleDefinition assembly)
    {
        MethodDefinition constructor = module.Methods.Single(method => method.IsConstructor && !method.IsStatic);
        MethodReference baseConstructor = constructor.Body.Instructions
            .Where(instruction => instruction.OpCode == OpCodes.Call)
            .Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Single(method => method.Name == ".ctor" && method.DeclaringType.FullName == "Celeste.Mod.EverestModule");
        MethodReference instanceSetter = constructor.Body.Instructions
            .Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Single(method => method.Name == "set_Instance" && method.DeclaringType.FullName == module.FullName);
        int dependencyCalls = constructor.Body.Instructions.Count(instruction =>
            instruction.Operand is MethodReference method && method.Name == "DependencyLoaded" &&
            method.DeclaringType.FullName == "Celeste.Mod.Everest/Loader");
        if (dependencyCalls != 1)
            throw new InvalidDataException("DashToggleHelper optional-dependency constructor contract drifted");

        constructor.Body.Instructions.Clear();
        constructor.Body.ExceptionHandlers.Clear();
        constructor.Body.Variables.Clear();
        constructor.Body.InitLocals = false;
        ILProcessor il = constructor.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, assembly.ImportReference(baseConstructor)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, assembly.ImportReference(instanceSetter)));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static bool MethodUsesHostIl(MethodDefinition method) => method.HasBody &&
        (method.ReturnType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
         method.Parameters.Any(parameter => parameter.ParameterType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil") ||
         method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MemberReference>()
             .Any(reference => reference.DeclaringType?.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil"));

    private static bool MethodUsesDJHostIl(MethodDefinition method) => method.HasBody &&
        (DJHostIlType(method.ReturnType) || method.Parameters.Any(parameter => DJHostIlType(parameter.ParameterType)) ||
         method.Body.Variables.Any(variable => DJHostIlType(variable.VariableType)) ||
         method.Body.ExceptionHandlers.Any(handler => DJHostIlType(handler.CatchType)) ||
         method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MemberReference>()
             .Any(reference => DJHostIlType(reference.DeclaringType) ||
                 reference is MethodReference called &&
                 (DJHostIlType(called.ReturnType) || called.Parameters.Any(parameter => DJHostIlType(parameter.ParameterType))) ||
                 reference is FieldReference field && DJHostIlType(field.FieldType)));

    private static bool DJHostIlType(TypeReference? type) => type != null &&
        (type.GetElementType().Scope?.Name == "Mono.Cecil" ||
         type.FullName.StartsWith("Mono.Cecil.", StringComparison.Ordinal) ||
         type.FullName.StartsWith("MonoMod.Cil.", StringComparison.Ordinal));

    private static bool TypeUsesDJHostIl(TypeDefinition type) =>
        type.Fields.Any(field => DJHostIlType(field.FieldType)) || type.Methods.Any(MethodUsesDJHostIl);

    private static bool TypeUsesHostIl(TypeDefinition type) =>
        type.Fields.Any(field => field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
            field.FieldType.FullName.StartsWith("MonoMod.Cil.", StringComparison.Ordinal) ||
            field.FieldType.FullName.StartsWith("Mono.Cecil.", StringComparison.Ordinal)) ||
        type.Methods.Any(MethodUsesHostIl);

    private static void MakePublic(TypeDefinition type)
    {
        if (type.DeclaringType == null)
        {
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.Public;
        }
        else
        {
            MakePublic(type.DeclaringType);
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
        }
    }

    internal static string PlanSha256(IEnumerable<FrozenIlTransformPlan> plans) => Hashing.BytesSha256(
        Encoding.UTF8.GetBytes(string.Join("\n", plans
            .Select(plan => string.Join("\0", plan.PlanId, plan.AssemblySha256, plan.EventType, plan.EventName,
                plan.TargetMethod, plan.CanonicalTargetMethod, plan.ManipulatorType, plan.ManipulatorMethod,
                plan.ManipulatorIsStatic, plan.RegistrationOrdinal, plan.BeforeSha256,
                plan.AfterSha256, plan.DiffSha256, string.Join(',', plan.ExpectedDelegateTargets)) +
                (plan.Mechanism == "DIRECT_ILHOOK" ? "\0" + string.Join("\0", plan.Mechanism,
                    plan.ConstructorSignature, plan.TargetExpression, plan.ManipulatorExpression,
                    plan.Config, plan.ApplyByDefault, plan.Storage, plan.Lifetime) :
                 plan.Mechanism == SelectedSidewaysIlPlans.Mechanism ? "\0" + string.Join("\0", plan.Mechanism, plan.Lifetime) : ""))) + "\n"));

    internal static string Targets(IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        string host = "$(MSBuildProjectDirectory)/.AppleEverestStaticIlHost";
        StringBuilder xml = new StringBuilder("<Project>\n  <PropertyGroup>\n    <AppleEverestStaticIlHost>")
            .Append(host).AppendLine("</AppleEverestStaticIlHost>")
            .AppendLine("    <AppleEverestDotNetHost>$(DOTNET_HOST_PATH)</AppleEverestDotNetHost>")
            .AppendLine("  </PropertyGroup>")
            .AppendLine("  <Target Name=\"AppleEverestFreezeStaticIl\" AfterTargets=\"CoreCompile\" Condition=\"'$(DesignTimeBuild)' != 'true'\">")
            .AppendLine("    <PropertyGroup><AppleEverestIntermediateAssembly>$(IntermediateOutputPath)$(TargetFileName)</AppleEverestIntermediateAssembly></PropertyGroup>")
            .AppendLine("    <Error Condition=\"!Exists('$(AppleEverestDotNetHost)')\" Text=\"The active pinned .NET SDK host is unavailable for the Apple Everest static-IL freeze.\" />")
            .AppendLine("    <Error Condition=\"!Exists('$(AppleEverestIntermediateAssembly)')\" Text=\"The compiled Celeste intermediate assembly is missing at the static-IL freeze boundary.\" />")
            .AppendLine("    <MakeDir Directories=\"$(IntermediateOutputPath)apple-everest-static-il\" />");
        IGrouping<string, FrozenIlTransformPlan>[] groups = plans.GroupBy(plan => plan.TargetMethod,
            StringComparer.Ordinal).ToArray();
        for (int index = 0; index < groups.Length; index++)
        {
            IGrouping<string, FrozenIlTransformPlan> group = groups[index];
            FrozenIlTransformPlan plan = group.First();
            string temp = "$(IntermediateOutputPath)apple-everest-static-il/target-" + index + ".dll";
            string manifest = "$(IntermediateOutputPath)apple-everest-static-il/transform-" + index + ".json";
            xml.Append("    <Exec Command=\"&quot;$(AppleEverestDotNetHost)&quot; &quot;$(AppleEverestStaticIlHost)/AppleEverestIlWorker.dll&quot; --target &quot;$(AppleEverestIntermediateAssembly)&quot; --output &quot;")
                .Append(temp).Append("&quot; --plan &quot;$(AppleEverestStaticIlHost)/frozen-il-plan.json&quot; --target-method ")
                .Append(Escape("'" + plan.TargetMethod.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'"))
                .Append(" --manifest &quot;").Append(manifest)
                .Append("&quot; --runtime-dir &quot;$(IntermediateOutputPath)&quot; --runtime-dir &quot;$(TargetDir)&quot; --runtime-dir &quot;$(MSBuildProjectDirectory)/AppleEverestAssemblies&quot; --runtime-dir &quot;$(AppleEverestStaticIlHost)&quot; @(ReferenceCopyLocalPaths-&gt;'--runtime-dir &quot;%(RootDir)%(Directory)&quot;', ' ')\" />\n")
                .Append("    <Copy SourceFiles=\"").Append(temp).AppendLine("\" DestinationFiles=\"$(AppleEverestIntermediateAssembly)\" />");
        }
        xml.AppendLine("  </Target>\n</Project>");
        return xml.ToString();
    }

    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? "";

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}

namespace AppleEverestBuilder;

// Exact compiled selected-profile targets, measured with the same normalizer
// used by the production freeze worker. Existing DJ transform pins are retained.
internal static class SelectedSidewaysIlPlans
{
    internal const string Mechanism = "HASH_LOCKED_STATIC_SEMANTIC_LOWERING";
    internal const string Lifetime = "AFTER_LEVEL_LOADER_ALL_ROOMS_UNTIL_NEXT_LEVEL_OR_OVERWORLD_EXCEPT_MINUS_ONE";
    private static readonly FrozenIlTransformPlan[] Plans =
    [
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:WallJumpCheck",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "WallJumpCheck",
            "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)",
            "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "7c87ba1ac56a612e7487dfc598e2d4b2f7d81817bd0d97fe1ec1e0d329128cf0",
            "3b871a992f0618ba61d8f5124ccef393b6ce992661a80b1a0bb6b975b5a0800c",
            "5616ab08b28d2ecc0625f32ed076187d123d6d5c12a19bb0b14519c4a59a1c30", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityWallJump"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:NormalUpdate",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "NormalUpdate",
            "System.Int32 Celeste.Player::AppleEverestSidewaysOriginalNormalUpdate()",
            "System.Int32 Celeste.Player::NormalUpdate()",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "c38137044853b6ec5f8d42c364743bd82982a79314fe493a5070ef028e0f6e4b",
            "9030e8ae7dfc476abf4281f5d693db923e26cffc081ab3533596c4f09f7aa2d7",
            "58b62c7f88b99b478e57a84e001f80c44caab85660f6bac2709133e8ae52742b", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityWallJump", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityWallJump"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:ClimbCheck",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "ClimbCheck",
            "System.Boolean Celeste.Player::ClimbCheck(System.Int32,System.Int32)",
            "System.Boolean Celeste.Player::ClimbCheck(System.Int32,System.Int32)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "35abd1cb1a998b925fc54281fa7f4c708ef61331a9f4fde9d93dc4a7ef3a77b6",
            "a5e8b5c64af7f17f62d53981686a9b7e6341681f1ebf4d4a11edbb1a8de88751",
            "775a29f0c38a5b0da2f1ed7c4e4271c40bca7f6c8f2695fdb803f7d6ce2b8428", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityClimb"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:ClimbBegin",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "ClimbBegin",
            "System.Void Celeste.Player::ClimbBegin()",
            "System.Void Celeste.Player::ClimbBegin()",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "32f8f267964729e625671f1755267ade6e118f5489afb4c8f72f0b45d0bbaea7",
            "da9949173f0654abb70a5e27c3c26c2d95a2eae4658fe21269f42a089a39cc55",
            "98db5df3902cf5b346ab97a914403759178d1f94d148b99b2430afa4588daacf", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityClimb"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:ClimbUpdate",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "ClimbUpdate",
            "System.Int32 Celeste.Player::AppleEverestOriginal_ClimbUpdate()",
            "System.Int32 Celeste.Player::ClimbUpdate()",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "bc64b92b2faa1c6af949e07602c13b2ee4d756b1874ba58fbb7e199a6be1cd2b",
            "e953fff4ca40bc54c85495e571dd0aa663caf954ef18aad5ecb80dde3189f5a4",
            "99468c27ac712b1567b0a12f6d667b7976c12617decda72133dae51b096f92a2", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityClimb", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityClimb", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityClimb"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:SlipCheck",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "SlipCheck",
            "System.Boolean Celeste.Player::SlipCheck(System.Single)",
            "System.Boolean Celeste.Player::SlipCheck(System.Single)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "c71c8c1e90197002dcecb48afd488c28ce7c58e8c7cfda0fbdc0bf23ddb2f134",
            "c15cc86d1f2e1ccf26331d37d56281a5f1759416dc15da4924a74e300e6084a5",
            "059112592813b7b04262495f0dce3f467d3753eb582bc0d28ef05d6ac41425fe", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:OnCollideH",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "OnCollideH",
            "System.Void Celeste.Player::AppleEverestOriginal_OnCollideH(Celeste.CollisionData)",
            "System.Void Celeste.Player::OnCollideH(Celeste.CollisionData)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "a86a9eff25145c269bd65955eebbb4dbd63f2d0d9e33160365cacc15239748a3",
            "9c49d541aeb24d367b7dfd2110f73d5dd258317805dfed2771637115ba8df580",
            "b13797e2dbddaa61ea904a10c00ae3268c2907e9ea8e568acc87ebd9180132cf", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:Update",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "Update",
            "System.Void Celeste.Player::AppleEverestOriginal_Update()",
            "System.Void Celeste.Player::Update()",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "a55c8287d128b3b8271b55f2d1a062197372e95c09c8728ef2ce2058ce00cc98",
            "0a3e9c5e58f02bd4c9531b3211b618119e9e1fdaedd82b0d63ee870d49602bea",
            "c39f978add8d542d9e89cdba2cef9923fc15e02ae865ab5bddfa3d3e7993446e", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Player:UpdateSprite",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Player", "UpdateSprite",
            "System.Void Celeste.Player::AppleEverestOriginal_UpdateSprite()",
            "System.Void Celeste.Player::UpdateSprite()",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "9302375df7320d4f0a035bd6d617d375b41d970c39cff444c347967a6d853278",
            "e2cfee12c4d89c59905a54ec2933a1c98b37dd35bc2ef0c5c282748a962157e4",
            "6ba2925e9a8b5578593055b20aa1999a2610d9203db56c4278e11acd23613cd6", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::SceneNeutral"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Seeker:OnCollideH",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Seeker", "OnCollideH",
            "System.Void Celeste.Seeker::OnCollideH(Celeste.CollisionData)",
            "System.Void Celeste.Seeker::OnCollideH(Celeste.CollisionData)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "modCollideChecks", true, 0,
            "c04da9a1ebdb3e452692a8d5a54b4a2d620d5ce0fa598e99081940084d3d5780",
            "b61aee4c05e755b01adc28c9dace3ff7f3350cf208e6d3e496ec69d21ceec0fd",
            "ca31b1890da6ecbc6cb45102de208a154267d8fbd25d8655ad9e792202e99ec3", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral", "Celeste.Mod.AppleEverestSidewaysJumpThru::EntityNeutral"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Actor:MoveHExact",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Actor", "MoveHExact",
            "System.Boolean Celeste.Actor::AppleEverestOriginal_MoveHExact(System.Int32,Celeste.Collision,Celeste.Solid)",
            "System.Boolean Celeste.Actor::MoveHExact(System.Int32,Celeste.Collision,Celeste.Solid)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "addSidewaysJumpthrusInHorizontalMoveMethods", true, 0,
            "111c78c69a51425f7b20868b9670321db2dbeeebf35f68b562d0abbf9ca5474e",
            "83325dd23c3906cdc0eb92ca8b25cd2a427de849d5e50ab283299a8d7f515a1e",
            "666ad435ed8161b9e4412e56c414daa41af24247356be49faa0daed9d64a6d94", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::CollideWithSolid"], Mechanism: Mechanism, Lifetime: Lifetime),
        new("MaxHelpingHand:SidewaysJumpThru:Celeste.Platform:MoveHExactCollideSolids",
            "MaxHelpingHand", "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            "IL.Celeste.Platform", "MoveHExactCollideSolids",
            "System.Boolean Celeste.Platform::MoveHExactCollideSolids(System.Int32,System.Boolean,System.Action`3<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,Celeste.Platform>)",
            "System.Boolean Celeste.Platform::MoveHExactCollideSolids(System.Int32,System.Boolean,System.Action`3<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,Celeste.Platform>)",
            "Celeste.Mod.MaxHelpingHand.Entities.SidewaysJumpThru", "addSidewaysJumpthrusInHorizontalMoveMethods", true, 0,
            "489c3ff7d4e59260607306e66d3bce8df54916b0ce00152c8fdd520445083b71",
            "af6ab306fee9ef4cdd90f9e1ea1944be35c55d9014901a04f42010a36caafde7",
            "bc476d50ddb0c9d21fd20f6ad5e4f316e9c464c8381a4a3519327130929e7378", [],
            ["Celeste.Mod.AppleEverestSidewaysJumpThru::CollideWithSolid"], Mechanism: Mechanism, Lifetime: Lifetime),
    ];

    internal static IReadOnlyList<FrozenIlTransformPlan> Append(IReadOnlyList<ResolvedMod> mods,
        IReadOnlyList<FrozenIlTransformPlan> existing)
    {
        var owner = mods.SingleOrDefault(mod => mod.Metadata.Name == "MaxHelpingHand");
        if (owner?.StaticSemanticLowering?.Id != "maxhelpinghand-1.40.9-henny-v1" ||
            owner.Metadata.Version != "1.40.9")
            throw new InvalidDataException("selected sideways collision closure requires exact MaxHelpingHand 1.40.9");
        List<FrozenIlTransformPlan> result = [.. existing];
        foreach (var plan in Plans)
        {
            var predecessors = result.Where(value => value.CanonicalTargetMethod == plan.CanonicalTargetMethod).ToArray();
            if (predecessors.Any(value => value.TargetMethod != plan.TargetMethod) ||
                (predecessors.Length > 0 && predecessors[^1].AfterSha256 != plan.BeforeSha256) ||
                (plan.EventName == "OnCollideH" && plan.EventType == "IL.Celeste.Player" &&
                    !predecessors.Select(value => value.PlanId).SequenceEqual(new[]
                        { "DJMapHelper:player-h-feather", "DJMapHelper:player-h-theo" })))
                throw new InvalidDataException("unreviewed selected sideways predecessor chain: " + plan.TargetMethod);
            result.Add(plan with { RegistrationOrdinal = predecessors.Length });
        }
        return result;
    }
}

using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

// Host-only lowering of the two audited MaxHelpingHand 1.40.9 manipulators.
// The ordinary freeze worker owns ordering, package/baseline/result/diff locks,
// idempotence, evidence and forbidden-reference checks. No transform is installed
// at runtime: the concrete helpers carry the source's level-lifetime predicate.
internal static class SelectedSidewaysIlLowering
{
    internal const string Mechanism = "HASH_LOCKED_STATIC_SEMANTIC_LOWERING";
    internal const string RuntimeType = "Celeste.Mod.AppleEverestSidewaysJumpThru";
    internal const string PackageDllSha256 = "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6";
    internal const string Lifetime = "AFTER_LEVEL_LOADER_ALL_ROOMS_UNTIL_NEXT_LEVEL_OR_OVERWORLD_EXCEPT_MINUS_ONE";
    internal sealed record SiteProfile(string Target, string Canonical, int EntitySites, int SceneSites, string Helper, bool Movement = false);
    internal static readonly SiteProfile[] Profiles =
    [
        new("System.Boolean Celeste.Player::WallJumpCheck(System.Int32)", "System.Boolean Celeste.Player::WallJumpCheck(System.Int32)", 1, 0, "EntityWallJump"),
        new("System.Int32 Celeste.Player::AppleEverestSidewaysOriginalNormalUpdate()", "System.Int32 Celeste.Player::NormalUpdate()", 2, 0, "EntityWallJump"),
        new("System.Boolean Celeste.Player::ClimbCheck(System.Int32,System.Int32)", "System.Boolean Celeste.Player::ClimbCheck(System.Int32,System.Int32)", 1, 0, "EntityClimb"),
        new("System.Void Celeste.Player::ClimbBegin()", "System.Void Celeste.Player::ClimbBegin()", 1, 0, "EntityClimb"),
        new("System.Int32 Celeste.Player::AppleEverestOriginal_ClimbUpdate()", "System.Int32 Celeste.Player::ClimbUpdate()", 3, 0, "EntityClimb"),
        new("System.Boolean Celeste.Player::SlipCheck(System.Single)", "System.Boolean Celeste.Player::SlipCheck(System.Single)", 0, 2, "EntityNeutral"),
        new("System.Void Celeste.Player::AppleEverestOriginal_OnCollideH(Celeste.CollisionData)", "System.Void Celeste.Player::OnCollideH(Celeste.CollisionData)", 2, 0, "EntityNeutral"),
        new("System.Void Celeste.Player::AppleEverestOriginal_Update()", "System.Void Celeste.Player::Update()", 3, 0, "EntityNeutral"),
        new("System.Void Celeste.Player::AppleEverestOriginal_UpdateSprite()", "System.Void Celeste.Player::UpdateSprite()", 2, 4, "EntityNeutral"),
        new("System.Void Celeste.Seeker::OnCollideH(Celeste.CollisionData)", "System.Void Celeste.Seeker::OnCollideH(Celeste.CollisionData)", 2, 0, "EntityNeutral"),
        new("System.Boolean Celeste.Actor::AppleEverestOriginal_MoveHExact(System.Int32,Celeste.Collision,Celeste.Solid)", "System.Boolean Celeste.Actor::MoveHExact(System.Int32,Celeste.Collision,Celeste.Solid)", 1, 0, "CollideWithSolid", true),
        new("System.Boolean Celeste.Platform::MoveHExactCollideSolids(System.Int32,System.Boolean,System.Action`3<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,Celeste.Platform>)", "System.Boolean Celeste.Platform::MoveHExactCollideSolids(System.Int32,System.Boolean,System.Action`3<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,Celeste.Platform>)", 1, 0, "CollideWithSolid", true)
    ];
    internal static string[] ExpectedHelpers(SiteProfile profile) =>
        Enumerable.Repeat(RuntimeType + "::" + profile.Helper, profile.EntitySites)
            .Concat(Enumerable.Repeat(RuntimeType + "::SceneNeutral", profile.SceneSites)).Order(StringComparer.Ordinal).ToArray();

    internal static MethodReference[] Apply(MethodDefinition target, string canonical)
    {
        SiteProfile profile = Profiles.SingleOrDefault(value => value.Target == target.FullName && value.Canonical == canonical)
            ?? throw new InvalidDataException("unreviewed sideways target: " + target.FullName);
        if (target.IsStatic || !target.HasBody) throw new InvalidDataException("sideways target has no concrete instance body");
        ModuleDefinition module = target.Module;
        TypeDefinition runtime = module.Types.Single(value => value.FullName == RuntimeType);
        MethodDefinition Helper(string name, string receiver, string result, string third)
        {
            MethodDefinition method = runtime.Methods.Single(value => value.Name == name);
            if (!method.IsStatic || !method.HasBody || method.ReturnType.FullName != result ||
                !method.Parameters.Select(value => value.ParameterType.FullName).SequenceEqual(new[] { result, receiver, third }))
                throw new InvalidDataException("sideways typed helper signature/body drift: " + name);
            return method;
        }
        const string entityCheck = "System.Boolean Monocle.Entity::CollideCheck<Celeste.Solid>(Microsoft.Xna.Framework.Vector2)";
        const string sceneCheck = "System.Boolean Monocle.Scene::CollideCheck<Celeste.Solid>(Microsoft.Xna.Framework.Vector2)";
        const string movementCheck = "T Monocle.Entity::CollideFirst<Celeste.Solid>(Microsoft.Xna.Framework.Vector2)";
        // Cecil represents generic returns as T; no overload/name-only matching.
        Instruction[] entities = target.Body.Instructions.Where(value => value.OpCode == OpCodes.Call &&
            value.Operand is MethodReference method && method.FullName == (profile.Movement ? movementCheck : entityCheck)).ToArray();
        Instruction[] scenes = target.Body.Instructions.Where(value => value.OpCode == OpCodes.Callvirt &&
            value.Operand is MethodReference method && method.FullName == sceneCheck).ToArray();
        if (entities.Length != profile.EntitySites || scenes.Length != profile.SceneSites)
            throw new InvalidDataException($"sideways call-site census drift: {target.FullName}: entity={entities.Length}; scene={scenes.Length}");
        Instruction[] sites = entities.Concat(scenes).ToArray();
        foreach (Instruction site in sites)
        {
            if (site.Previous?.OpCode.OpCodeType == OpCodeType.Prefix || target.Body.Instructions.Any(value =>
                    ReferenceEquals(value.Operand, site) || value.Operand is Instruction[] branches && branches.Contains(site)) ||
                target.Body.ExceptionHandlers.Any(value => value.TryStart == site || value.TryEnd == site ||
                    value.HandlerStart == site || value.HandlerEnd == site || value.FilterStart == site))
                throw new InvalidDataException("unreviewed control-flow boundary at sideways collision call");
        }
        ILProcessor il = target.Body.GetILProcessor();
        List<MethodReference> calls = [];
        VariableDefinition? vector = null;
        if (!profile.Movement)
        {
            vector = new VariableDefinition(((MethodReference)sites[0].Operand).Parameters[0].ParameterType);
            target.Body.Variables.Add(vector);
        }
        foreach (Instruction site in sites)
        {
            bool scene = scenes.Contains(site);
            MethodDefinition helper = profile.Movement
                ? Helper(profile.Helper, "Monocle.Entity", "Celeste.Solid", "System.Int32")
                : Helper(scene ? "SceneNeutral" : profile.Helper, scene ? "Monocle.Scene" : "Monocle.Entity", "System.Boolean", "Microsoft.Xna.Framework.Vector2");
            if (vector != null)
            {
                il.InsertBefore(site, il.Create(OpCodes.Stloc, vector));
                il.InsertBefore(site, il.Create(OpCodes.Ldloc, vector));
            }
            Instruction cursor = site;
            void Emit(Instruction instruction) { il.InsertAfter(cursor, instruction); cursor = instruction; }
            Emit(il.Create(OpCodes.Ldarg_0));
            if (scene)
            {
                MethodDefinition getter = module.Types.Single(value => value.FullName == "Monocle.Entity")
                    .Methods.Single(value => value.Name == "get_Scene" && value.Parameters.Count == 0);
                Emit(il.Create(OpCodes.Call, getter));
            }
            Emit(profile.Movement ? il.Create(OpCodes.Ldarg_1) : il.Create(OpCodes.Ldloc, vector!));
            Emit(il.Create(OpCodes.Call, helper));
            calls.Add(helper);
        }
        target.FixShortLongOps();
        return calls.ToArray();
    }
}

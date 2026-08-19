namespace Celeste
{
    public sealed class Language { }

    public sealed class PlayerDeadBody
    {
        public PlayerDeadBody(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Player
    {
        public PlayerDeadBody Die(int direction, bool evenIfInvincible = false, bool registerDeathInStats = true) =>
            global::On.Celeste.Player.Invoke_Die(this, direction, evenIfInvincible, registerDeathInStats,
                (self, nextDirection, nextInvincible, nextStats) => self.orig_Die(nextDirection, nextInvincible, nextStats));

        public PlayerDeadBody orig_Die(int direction, bool evenIfInvincible, bool registerDeathInStats)
        {
            global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("original");
            return new PlayerDeadBody($"{direction}:{evenIfInvincible}:{registerDeathInStats}:O");
        }
    }

    public sealed class HotUpdateTarget
    {
        private static readonly global::On.Celeste.HotUpdateTarget.orig_Update OriginalBody = Original;

        public int Ticks { get; private set; }

        public void Update() => global::On.Celeste.HotUpdateTarget.Invoke_Update(this, OriginalBody);

        private static void Original(HotUpdateTarget self) => self.Ticks++;
    }
}

namespace On.Celeste
{
    public static class Dialog
    {
        public delegate string orig_Clean(string value, global::Celeste.Language language);
        public delegate string hook_Clean(orig_Clean orig, string value, global::Celeste.Language language);
        private static readonly List<global::Celeste.Mod.AppleEverestManagedHook<hook_Clean>> Hooks_Clean = new();
        private static int version = -1;
        private static orig_Clean original;
        private static orig_Clean active;
        public static event hook_Clean Clean
        {
            add => global::Celeste.Mod.AppleEverestHookList.AddEvent(Hooks_Clean, value);
            remove => global::Celeste.Mod.AppleEverestHookList.RemoveEvent(Hooks_Clean, value);
        }
        internal static int ActiveHandlerCount => Hooks_Clean.Count(value => value.IsValid);
        internal static void RemoveOwner_Clean(string owner) => global::Celeste.Mod.AppleEverestHookList.RemoveOwner(Hooks_Clean, owner);
        internal static string Invoke_Clean(string value, global::Celeste.Language language, orig_Clean originalBody)
        {
            int current = global::Celeste.Mod.AppleEverestHookList.Version;
            if (version != current || original != originalBody)
            {
                original = originalBody;
                active = originalBody;
                foreach (hook_Clean handler in global::Celeste.Mod.AppleEverestHookList.Active(Hooks_Clean))
                {
                    orig_Clean next = active;
                    active = (nextValue, nextLanguage) => handler(next, nextValue, nextLanguage);
                }
                version = current;
            }
            return active(value, language);
        }
    }

    public static class Player
    {
        public delegate global::Celeste.PlayerDeadBody orig_Die(global::Celeste.Player self, int direction, bool evenIfInvincible, bool registerDeathInStats);
        public delegate global::Celeste.PlayerDeadBody hook_Die(orig_Die orig, global::Celeste.Player self, int direction, bool evenIfInvincible, bool registerDeathInStats);
        private static readonly List<global::Celeste.Mod.AppleEverestManagedHook<hook_Die>> Hooks_Die = new();
        private static int version = -1;
        private static orig_Die original;
        private static orig_Die active;
        public static event hook_Die Die
        {
            add => global::Celeste.Mod.AppleEverestHookList.AddEvent(Hooks_Die, value);
            remove => global::Celeste.Mod.AppleEverestHookList.RemoveEvent(Hooks_Die, value);
        }
        internal static global::Celeste.Mod.IAppleEverestManagedHookRegistration RegisterDirect_Die(
            hook_Die handler, global::MonoMod.RuntimeDetour.DetourConfig config, bool applyByDefault) =>
            global::Celeste.Mod.AppleEverestHookList.AddDirect(Hooks_Die, handler, config, applyByDefault);
        internal static void RemoveOwner_Die(string owner) => global::Celeste.Mod.AppleEverestHookList.RemoveOwner(Hooks_Die, owner);
        internal static global::Celeste.PlayerDeadBody Invoke_Die(global::Celeste.Player self, int direction,
            bool evenIfInvincible, bool registerDeathInStats, orig_Die originalBody)
        {
            int current = global::Celeste.Mod.AppleEverestHookList.Version;
            if (version != current || original != originalBody)
            {
                original = originalBody;
                active = originalBody;
                foreach (hook_Die handler in global::Celeste.Mod.AppleEverestHookList.Active(Hooks_Die))
                {
                    orig_Die next = active;
                    active = (nextSelf, nextDirection, nextInvincible, nextStats) =>
                        handler(next, nextSelf, nextDirection, nextInvincible, nextStats);
                }
                version = current;
            }
            return active(self, direction, evenIfInvincible, registerDeathInStats);
        }
    }

    public static class HotUpdateTarget
    {
        public delegate void orig_Update(global::Celeste.HotUpdateTarget self);
        public delegate void hook_Update(orig_Update orig, global::Celeste.HotUpdateTarget self);
        private static readonly List<global::Celeste.Mod.AppleEverestManagedHook<hook_Update>> Hooks_Update = new();
        private static int version = -1;
        private static orig_Update original;
        private static orig_Update active;
        public static event hook_Update Update
        {
            add => global::Celeste.Mod.AppleEverestHookList.AddEvent(Hooks_Update, value);
            remove => global::Celeste.Mod.AppleEverestHookList.RemoveEvent(Hooks_Update, value);
        }
        internal static void RemoveOwner_Update(string owner) =>
            global::Celeste.Mod.AppleEverestHookList.RemoveOwner(Hooks_Update, owner);
        internal static void Invoke_Update(global::Celeste.HotUpdateTarget self, orig_Update originalBody)
        {
            int current = global::Celeste.Mod.AppleEverestHookList.Version;
            if (version != current || original != originalBody)
            {
                original = originalBody;
                active = originalBody;
                foreach (hook_Update handler in global::Celeste.Mod.AppleEverestHookList.Active(Hooks_Update))
                {
                    orig_Update next = active;
                    active = nextSelf => handler(next, nextSelf);
                }
                version = current;
            }
            active(self);
        }
    }
}

namespace Celeste.Mod
{
    internal static class GeneratedAppleEverestManagedDetourRegistry
    {
        internal static void RemoveOwner(string owner)
        {
            global::On.Celeste.Dialog.RemoveOwner_Clean(owner);
            global::On.Celeste.Player.RemoveOwner_Die(owner);
            global::On.Celeste.HotUpdateTarget.RemoveOwner_Update(owner);
        }
    }

    internal static class GeneratedAppleEverestDirectHookRegistry
    {
        internal static IAppleEverestManagedHookRegistration CreateByPlan(string planId, bool applyByDefault)
        {
            return global::On.Celeste.Player.RegisterDirect_Die(Adapter(planId), null, applyByDefault);
        }

        internal static IAppleEverestManagedHookRegistration CreateConfiguredForTest(
            string planId, global::MonoMod.RuntimeDetour.DetourConfig config, bool applyByDefault = true)
            => global::On.Celeste.Player.RegisterDirect_Die(Adapter(planId), config, applyByDefault);

        private static global::On.Celeste.Player.hook_Die Adapter(string planId) => planId switch
        {
            "A" => (orig, self, direction, invincible, stats) => global::DirectFixtures.A(
                    (nextSelf, nextDirection, nextInvincible, nextStats) => orig(nextSelf, nextDirection, nextInvincible, nextStats),
                    self, direction, invincible, stats),
            "B" => (orig, self, direction, invincible, stats) => global::DirectFixtures.B(
                    (nextSelf, nextDirection, nextInvincible, nextStats) => orig(nextSelf, nextDirection, nextInvincible, nextStats),
                    self, direction, invincible, stats),
            "Stop" => (orig, self, direction, invincible, stats) => global::DirectFixtures.Stop(
                    (nextSelf, nextDirection, nextInvincible, nextStats) => orig(nextSelf, nextDirection, nextInvincible, nextStats),
                    self, direction, invincible, stats),
            _ => throw new NotSupportedException("direct managed Hook plan was not statically authorized")
        };
    }
}

public static class DirectFixtures
{
    public static global::Celeste.PlayerDeadBody A(
        Func<global::Celeste.Player, int, bool, bool, global::Celeste.PlayerDeadBody> orig,
        global::Celeste.Player self, int direction, bool invincible, bool stats)
    {
        global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("A-before");
        global::Celeste.PlayerDeadBody body = orig(self, direction + 1, invincible, stats);
        global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("A-after");
        return new global::Celeste.PlayerDeadBody(body.Value + ":A");
    }

    public static global::Celeste.PlayerDeadBody B(
        Func<global::Celeste.Player, int, bool, bool, global::Celeste.PlayerDeadBody> orig,
        global::Celeste.Player self, int direction, bool invincible, bool stats)
    {
        global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("B-before");
        global::Celeste.PlayerDeadBody body = orig(self, direction, invincible, stats);
        global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("B-after");
        return new global::Celeste.PlayerDeadBody(body.Value + ":B");
    }

    public static global::Celeste.PlayerDeadBody Stop(
        Func<global::Celeste.Player, int, bool, bool, global::Celeste.PlayerDeadBody> orig,
        global::Celeste.Player self, int direction, bool invincible, bool stats)
    {
        global::Celeste.Mod.AppleEverestStaticRuntime.Trace.Add("stop");
        return new global::Celeste.PlayerDeadBody("STOP");
    }
}

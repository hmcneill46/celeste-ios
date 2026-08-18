using System.Collections.Generic;
using System.Linq;

namespace On.Celeste;

public static class Dialog
{
    public delegate string orig_Clean(string name, global::Celeste.Language language = null);
    public delegate string hook_Clean(orig_Clean orig, string name, global::Celeste.Language language = null);

    private sealed class Subscription
    {
        public string Owner;
        public hook_Clean Handler;
    }

    private static readonly List<Subscription> Handlers = new();
    private static orig_Clean Original;
    private static orig_Clean ActiveChain;
    private static bool dirty = true;

    public static event hook_Clean Clean
    {
        add
        {
            if (value == null) return;
            Handlers.Add(new Subscription { Owner = global::Celeste.Mod.AppleEverestStaticRuntime.CurrentOwner, Handler = value });
            dirty = true;
        }
        remove
        {
            if (value == null) return;
            string owner = global::Celeste.Mod.AppleEverestStaticRuntime.CurrentOwner;
            for (int index = Handlers.Count - 1; index >= 0; index--)
            {
                Subscription candidate = Handlers[index];
                if (candidate.Owner == owner && candidate.Handler == value)
                {
                    Handlers.RemoveAt(index);
                    dirty = true;
                    break;
                }
            }
        }
    }

    internal static string Invoke(string name, global::Celeste.Language language, orig_Clean original)
    {
        if (Original == null)
        {
            Original = original;
            dirty = true;
        }
        if (dirty) RebuildActiveChain();
        return ActiveChain(name, language);
    }

    internal static void RebuildActiveChain()
    {
        if (Original == null) return;
        orig_Clean chain = (value, selectedLanguage) =>
        {
            if (value == "APPLE_EVEREST_HOOK_CANARY")
                global::Celeste.Mod.AppleEverestStaticRuntime.RecordHook("original");
            return Original(value, selectedLanguage);
        };
        for (int index = 0; index < Handlers.Count; index++)
        {
            Subscription subscription = Handlers[index];
            if (!global::Celeste.Mod.AppleEverestStaticRuntime.IsModuleEnabled(subscription.Owner)) continue;
            hook_Clean handler = subscription.Handler;
            orig_Clean next = chain;
            chain = (value, selectedLanguage) => handler(next, value, selectedLanguage);
        }
        ActiveChain = chain;
        dirty = false;
    }

    internal static int ActiveHandlerCount => Handlers.Count(handler =>
        global::Celeste.Mod.AppleEverestStaticRuntime.IsModuleEnabled(handler.Owner));
}

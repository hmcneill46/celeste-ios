using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace On.Celeste;

public static class TrailManager
{
    public delegate void orig_Add_Entity_Color_float_bool_bool(global::Monocle.Entity entity, Color color, float duration, bool frozenUpdate, bool useRawDeltaTime);
    public delegate void hook_Add_Entity_Color_float_bool_bool(orig_Add_Entity_Color_float_bool_bool orig, global::Monocle.Entity entity, Color color, float duration, bool frozenUpdate, bool useRawDeltaTime);
    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Add_Entity_Color_float_bool_bool>> Hooks = new();
    private static int activeVersion = -1;
    private static orig_Add_Entity_Color_float_bool_bool originalHandler;
    private static orig_Add_Entity_Color_float_bool_bool activeHandler;
    public static event hook_Add_Entity_Color_float_bool_bool Add_Entity_Color_float_bool_bool
    {
        add => global::Celeste.Mod.AppleEverestHookList.Add(Hooks, value);
        remove => global::Celeste.Mod.AppleEverestHookList.Remove(Hooks, value);
    }

    internal static void Invoke(global::Monocle.Entity entity, Color color, float duration, bool frozenUpdate, bool useRawDeltaTime, orig_Add_Entity_Color_float_bool_bool original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (activeVersion != version || originalHandler != original)
        {
            originalHandler = original;
            activeHandler = original;
            foreach (hook_Add_Entity_Color_float_bool_bool handler in global::Celeste.Mod.AppleEverestHookList.Active(Hooks))
            {
                orig_Add_Entity_Color_float_bool_bool next = activeHandler;
                activeHandler = (e, c, d, f, r) => handler(next, e, c, d, f, r);
            }
            activeVersion = version;
        }
        activeHandler(entity, color, duration, frozenUpdate, useRawDeltaTime);
    }
}

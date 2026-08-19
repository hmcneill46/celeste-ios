using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace On.Monocle;

public static class ParticleSystem
{
    public delegate void orig_Emit_ParticleType_Vector2(global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position);
    public delegate void hook_Emit_ParticleType_Vector2(orig_Emit_ParticleType_Vector2 orig, global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position);
    public delegate void orig_Emit_ParticleType_Vector2_float(global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, float direction);
    public delegate void hook_Emit_ParticleType_Vector2_float(orig_Emit_ParticleType_Vector2_float orig, global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, float direction);
    public delegate void orig_Emit_ParticleType_Vector2_Color(global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, Color color);
    public delegate void hook_Emit_ParticleType_Vector2_Color(orig_Emit_ParticleType_Vector2_Color orig, global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, Color color);
    public delegate void orig_Emit_ParticleType_Vector2_Color_float(global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, Color color, float direction);
    public delegate void hook_Emit_ParticleType_Vector2_Color_float(orig_Emit_ParticleType_Vector2_Color_float orig, global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, Vector2 position, Color color, float direction);
    public delegate void orig_Emit_ParticleType_Entity_int_Vector2_Vector2_float(global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, global::Monocle.Entity track, int amount, Vector2 position, Vector2 positionRange, float direction);
    public delegate void hook_Emit_ParticleType_Entity_int_Vector2_Vector2_float(orig_Emit_ParticleType_Entity_int_Vector2_Vector2_float orig, global::Monocle.ParticleSystem self, global::Monocle.ParticleType type, global::Monocle.Entity track, int amount, Vector2 position, Vector2 positionRange, float direction);

    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Emit_ParticleType_Vector2>> A = new();
    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Emit_ParticleType_Vector2_float>> B = new();
    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Emit_ParticleType_Vector2_Color>> C = new();
    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Emit_ParticleType_Vector2_Color_float>> D = new();
    private static readonly List<global::Celeste.Mod.AppleEverestOwnedHook<hook_Emit_ParticleType_Entity_int_Vector2_Vector2_float>> E = new();
    private static int versionA = -1, versionB = -1, versionC = -1, versionD = -1, versionE = -1;
    private static orig_Emit_ParticleType_Vector2 originalA, activeA;
    private static orig_Emit_ParticleType_Vector2_float originalB, activeB;
    private static orig_Emit_ParticleType_Vector2_Color originalC, activeC;
    private static orig_Emit_ParticleType_Vector2_Color_float originalD, activeD;
    private static orig_Emit_ParticleType_Entity_int_Vector2_Vector2_float originalE, activeE;

    public static event hook_Emit_ParticleType_Vector2 Emit_ParticleType_Vector2 { add => global::Celeste.Mod.AppleEverestHookList.Add(A, value); remove => global::Celeste.Mod.AppleEverestHookList.Remove(A, value); }
    public static event hook_Emit_ParticleType_Vector2_float Emit_ParticleType_Vector2_float { add => global::Celeste.Mod.AppleEverestHookList.Add(B, value); remove => global::Celeste.Mod.AppleEverestHookList.Remove(B, value); }
    public static event hook_Emit_ParticleType_Vector2_Color Emit_ParticleType_Vector2_Color { add => global::Celeste.Mod.AppleEverestHookList.Add(C, value); remove => global::Celeste.Mod.AppleEverestHookList.Remove(C, value); }
    public static event hook_Emit_ParticleType_Vector2_Color_float Emit_ParticleType_Vector2_Color_float { add => global::Celeste.Mod.AppleEverestHookList.Add(D, value); remove => global::Celeste.Mod.AppleEverestHookList.Remove(D, value); }
    public static event hook_Emit_ParticleType_Entity_int_Vector2_Vector2_float Emit_ParticleType_Entity_int_Vector2_Vector2_float { add => global::Celeste.Mod.AppleEverestHookList.Add(E, value); remove => global::Celeste.Mod.AppleEverestHookList.Remove(E, value); }

    internal static void Invoke(global::Monocle.ParticleType type, Vector2 position, global::Monocle.ParticleSystem self, orig_Emit_ParticleType_Vector2 original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (versionA != version || originalA != original)
        {
            originalA = original;
            activeA = original;
            foreach (hook_Emit_ParticleType_Vector2 handler in global::Celeste.Mod.AppleEverestHookList.Active(A)) { orig_Emit_ParticleType_Vector2 next = activeA; activeA = (s, t, p) => handler(next, s, t, p); }
            versionA = version;
        }
        activeA(self, type, position);
    }
    internal static void Invoke(global::Monocle.ParticleType type, Vector2 position, float direction, global::Monocle.ParticleSystem self, orig_Emit_ParticleType_Vector2_float original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (versionB != version || originalB != original)
        {
            originalB = original;
            activeB = original;
            foreach (hook_Emit_ParticleType_Vector2_float handler in global::Celeste.Mod.AppleEverestHookList.Active(B)) { orig_Emit_ParticleType_Vector2_float next = activeB; activeB = (s, t, p, d) => handler(next, s, t, p, d); }
            versionB = version;
        }
        activeB(self, type, position, direction);
    }
    internal static void Invoke(global::Monocle.ParticleType type, Vector2 position, Color color, global::Monocle.ParticleSystem self, orig_Emit_ParticleType_Vector2_Color original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (versionC != version || originalC != original)
        {
            originalC = original;
            activeC = original;
            foreach (hook_Emit_ParticleType_Vector2_Color handler in global::Celeste.Mod.AppleEverestHookList.Active(C)) { orig_Emit_ParticleType_Vector2_Color next = activeC; activeC = (s, t, p, c) => handler(next, s, t, p, c); }
            versionC = version;
        }
        activeC(self, type, position, color);
    }
    internal static void Invoke(global::Monocle.ParticleType type, Vector2 position, Color color, float direction, global::Monocle.ParticleSystem self, orig_Emit_ParticleType_Vector2_Color_float original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (versionD != version || originalD != original)
        {
            originalD = original;
            activeD = original;
            foreach (hook_Emit_ParticleType_Vector2_Color_float handler in global::Celeste.Mod.AppleEverestHookList.Active(D)) { orig_Emit_ParticleType_Vector2_Color_float next = activeD; activeD = (s, t, p, c, d) => handler(next, s, t, p, c, d); }
            versionD = version;
        }
        activeD(self, type, position, color, direction);
    }
    internal static void Invoke(global::Monocle.ParticleType type, global::Monocle.Entity track, int amount, Vector2 position, Vector2 positionRange, float direction, global::Monocle.ParticleSystem self, orig_Emit_ParticleType_Entity_int_Vector2_Vector2_float original)
    {
        int version = global::Celeste.Mod.AppleEverestHookList.Version;
        if (versionE != version || originalE != original)
        {
            originalE = original;
            activeE = original;
            foreach (hook_Emit_ParticleType_Entity_int_Vector2_Vector2_float handler in global::Celeste.Mod.AppleEverestHookList.Active(E)) { orig_Emit_ParticleType_Entity_int_Vector2_Vector2_float next = activeE; activeE = (s, t, e, a, p, r, d) => handler(next, s, t, e, a, p, r, d); }
            versionE = version;
        }
        activeE(self, type, track, amount, position, positionRange, direction);
    }
}

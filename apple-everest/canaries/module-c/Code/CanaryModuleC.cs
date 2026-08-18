using Celeste;
using Celeste.Mod;

namespace AppleEverest.Canaries;

public sealed class CanaryHookBModule : EverestModule
{
    public override void Load()
    {
        On.Celeste.Dialog.Clean += HookDialogClean;
    }

    public override void Unload()
    {
        On.Celeste.Dialog.Clean -= HookDialogClean;
    }

    private static string HookDialogClean(On.Celeste.Dialog.orig_Clean orig, string name, Language language)
    {
        if (name != "APPLE_EVEREST_HOOK_CANARY") return orig(name, language);
        AppleEverestStaticRuntime.RecordHook("B-before");
        string result = orig(name, language);
        AppleEverestStaticRuntime.RecordHook("B-after");
        return result + " [B]";
    }
}

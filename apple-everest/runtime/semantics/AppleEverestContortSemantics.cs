#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestMadelineSpotlightModifierTrigger : Trigger
{
    private readonly Color color;
    private readonly float alpha;
    private readonly int startFade;
    private readonly int endFade;
    private readonly Ease.Easer easer;
    private readonly float duration;

    internal AppleEverestMadelineSpotlightModifierTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        // EntityDataExtensions.Color uses named XNA colours. The actual
        // Beginner trigger uses White, not a hexadecimal colour string.
        string authoredColor = data.Attr("color", "White");
        if (authoredColor is not ("White" or "FFFFFF") || data.Attr("easer", "CubeInOut") != "CubeInOut" ||
            data.Attr("neededFlags", "") != "" || data.Attr("flagsAfterInvoke", "") != "" ||
            data.Float("delay", 0f) != 0f || !data.Bool("occurOnEnter", true) ||
            data.Bool("oneUse", false) || data.Bool("persistent", false))
            throw new InvalidOperationException("spotlight trigger is outside the frozen Beginner semantics");
        color = Color.White;
        alpha = data.Float("alpha", 1f);
        startFade = data.Int("startFade", 32);
        endFade = data.Int("endFade", 64);
        duration = data.Float("duration", 1f);
        easer = Ease.CubeInOut;
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        // The distributed AbstractTrigger yields zero before invoking Routine.
        // Preserve that coroutine boundary and reacquire the current player.
        Add(new Coroutine(Invoke()));
    }

    private IEnumerator Invoke()
    {
        yield return 0f;
        Player player = Scene.Tracker.GetEntity<Player>();
        VertexLight light = player?.Light;
        if (light == null || player.Dead) yield break;
        Tween tween = Tween.Create(Tween.TweenMode.Oneshot, easer, duration, start: true);
        tween.OnUpdate = value => { if (!player.Dead) Apply(light, value.Eased); };
        tween.OnComplete = _ => { if (!player.Dead) Apply(light, 1f); };
        player.Add(tween);
    }

    private void Apply(VertexLight light, float amount)
    {
        light.Color = Color.Lerp(light.Color, color, amount);
        light.Alpha = Calc.LerpClamp(light.Alpha, alpha, amount);
        light.StartRadius = Calc.LerpClamp(light.StartRadius, startFade, amount);
        light.EndRadius = Calc.LerpClamp(light.EndRadius, endFade, amount);
    }
}

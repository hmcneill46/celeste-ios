#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestStage25KERootCanary : Entity
{
    private bool exercised;
    internal AppleEverestStage25KERootCanary(EntityData data, Vector2 offset) : base(data.Position + offset)
    { Collider = new Hitbox(Math.Max(8, data.Width), Math.Max(8, data.Height)); Depth = -100000; }
    public override void Update()
    {
        base.Update();
        AppleEverestStrawberryJamModule module = AppleEverestStrawberryJamModule.Instance;
        if (!exercised && module?.LifecycleState == 3 && CollideCheck<Player>())
        {
            exercised = true;
            string animation = AppleEverestSJJamJar.SelectAnimation(module.Save, "AppleEverest/Stage25KE", true);
            module.Session.MusicWonkyBeatIndex = 25;
            module.Session.CassetteWonkyBeatIndex = 11;
            module.Session.RainDensityData.Density = 0.5f;
            AppleEverestStaticRuntime.Log("stage25ke-root-canary=mutated jar-animation=" + animation + " typed-session=true lifecycle=3");
        }
    }
    public override void Render()
    {
        bool ready = AppleEverestStrawberryJamModule.Instance?.LifecycleState == 3;
        bool durable = AppleEverestStrawberryJamModule.Instance?.Save?.FilledJamJarSIDs.Contains("AppleEverest/Stage25KE") == true;
        Draw.Rect(X, Y, 24f, 12f, (exercised ? Color.LimeGreen : durable ? Color.Cyan : ready ? Color.Gold : Color.Red) * 0.8f);
        Draw.HollowRect(X, Y, 24f, 12f, Color.White);
    }
}

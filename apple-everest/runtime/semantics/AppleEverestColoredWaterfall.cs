#nullable disable
// Selected static implementation reviewed against PandorasBox 1.0.49.
// Package identity, authored guard and linked target patches are independently bound.
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestColoredWaterfall : Actor
{
    private static int horizontalVisiblityBuffer = 32;

    private static int verticalVisiblityBuffer = 32;

    private float height;

    private Water water;

    private Color baseColor;

    private Color surfaceColor;

    private Color fillColor;

    private Solid solid;

    private SoundSource loopingSfx;

    private SoundSource enteringSfx;

    private bool visibleOnCamera;

    public AppleEverestColoredWaterfall(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        baseColor = AppleEverestPandoraColors.Get(data.Attr("color", "#87CEFA"));
        surfaceColor = baseColor * 0.8f;
        fillColor = baseColor * 0.3f;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        base.Depth = -9999;
        base.Tag = Tags.TransitionUpdate;
        Level level = base.Scene as Level;
        bool flag = water != null && !base.Scene.CollideCheck<Solid>(new Rectangle((int)base.X, (int)(base.Y + height), 8, 16));
        height = 8f;
        while (base.Y + height < (float)level.Bounds.Bottom && (water = base.Scene.CollideFirst<Water>(new Rectangle((int)base.X, (int)(base.Y + height), 8, 8))) == null && ((solid = base.Scene.CollideFirst<Solid>(new Rectangle((int)base.X, (int)(base.Y + height), 8, 8))) == null || !solid.BlockWaterfalls))
        {
            height += 8f;
            solid = null;
        }
        Add(loopingSfx = new SoundSource());
        loopingSfx.Play("event:/env/local/waterfall_small_main");
        Add(enteringSfx = new SoundSource());
        enteringSfx.Play(flag ? "event:/env/local/waterfall_small_in_deep" : "event:/env/local/waterfall_small_in_shallow");
        enteringSfx.Position.Y = height;
        Add(new DisplacementRenderHook(RenderDisplacement));
    }

    public void RenderDisplacement()
    {
        Draw.Rect(base.X, base.Y, 8f, height, new Color(0.5f, 0.5f, 0.8f, 1f));
    }

    private void updateVisiblity(Level level)
    {
        Camera camera = level.Camera;
        bool flag = base.X < camera.Right + (float)horizontalVisiblityBuffer && base.X > camera.Left - (float)horizontalVisiblityBuffer;
        bool flag2 = base.Y < camera.Bottom + (float)verticalVisiblityBuffer && base.Y + height > camera.Top - (float)verticalVisiblityBuffer;
        visibleOnCamera = flag & flag2;
    }

    public override void Update()
    {
        Level level = base.Scene as Level;
        loopingSfx.Position.Y = Calc.Clamp(level.Camera.Position.Y + 90f, base.Y, height);
        if (base.Scene.OnInterval(0.05f))
        {
            updateVisiblity(level);
        }
        if (water != null && water.Active && water.TopSurface != null && base.Scene.OnInterval(0.3f))
        {
            water.TopSurface.DoRipple(new Vector2(base.X + 4f, water.Y), 0.75f);
        }
        if (visibleOnCamera && (water != null || solid != null))
        {
            Vector2 position = new Vector2(base.X + 4f, (float)((double)(base.Y + height) + 2.0));
            level.ParticlesFG.Emit(Water.P_Splash, 1, position, new Vector2(8f, 2f), baseColor, new Vector2(0f, -1f).Angle());
        }
        base.Update();
    }

    public override void Render()
    {
        if (!visibleOnCamera)
        {
            return;
        }
        if (water == null || water.TopSurface == null)
        {
            Draw.Rect(base.X + 1f, base.Y, 6f, height, fillColor);
            Draw.Rect(base.X - 1f, base.Y, 2f, height, surfaceColor);
            Draw.Rect(base.X + 7f, base.Y, 2f, height, surfaceColor);
            return;
        }
        Water.Surface topSurface = water.TopSurface;
        float num = height + water.TopSurface.Position.Y - water.Y;
        for (int i = 0; i < 6; i++)
        {
            Draw.Rect(base.X + (float)i + 1f, base.Y, 1f, num - topSurface.GetSurfaceHeight(new Vector2(base.X + 1f + (float)i, water.Y)), fillColor);
        }
        Draw.Rect(base.X - 1f, base.Y, 2f, num - topSurface.GetSurfaceHeight(new Vector2(base.X, water.Y)), surfaceColor);
        Draw.Rect(base.X + 7f, base.Y, 2f, num - topSurface.GetSurfaceHeight(new Vector2(base.X + 8f, water.Y)), surfaceColor);
    }
}

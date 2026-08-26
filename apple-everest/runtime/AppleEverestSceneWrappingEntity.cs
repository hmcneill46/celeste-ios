using System;
using Monocle;

namespace Celeste.Mod;

// CollabUtils2 presents the normal high-resolution Overworld UI inside the
// active Level. Keep that composition model in the closed runtime so chapter
// panels and journals retain Celeste's real animation, input, and layout.
internal class AppleEverestSceneWrappingEntity : Entity
{
    internal readonly Scene WrappedScene;
    private readonly AppleEverestSceneRenderer renderer;
    private bool initialized;

    internal event Action<Scene> OnBegin;
    internal event Action<Scene> OnEnd;

    internal AppleEverestSceneWrappingEntity(Scene scene)
    {
        WrappedScene = scene ?? throw new ArgumentNullException(nameof(scene));
        renderer = new AppleEverestSceneRenderer(scene);
    }

    private void Initialize(Scene scene)
    {
        if (initialized) return;
        initialized = true;
        WrappedScene.Begin();
        scene.RendererList.Add(renderer);
        renderer.Alloc();
        OnBegin?.Invoke(WrappedScene);
    }

    private void Uninitialize(Scene scene)
    {
        if (!initialized) return;
        initialized = false;
        WrappedScene.End();
        scene.RendererList.Remove(renderer);
        renderer.Dispose();
        OnEnd?.Invoke(WrappedScene);
    }

    public override void SceneBegin(Scene scene)
    {
        base.SceneBegin(scene);
        Initialize(scene);
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        Initialize(scene);
    }

    public override void HandleGraphicsCreate()
    {
        base.HandleGraphicsCreate();
        WrappedScene.HandleGraphicsCreate();
    }

    public override void HandleGraphicsReset()
    {
        base.HandleGraphicsReset();
        WrappedScene.HandleGraphicsReset();
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        Uninitialize(scene);
    }

    public override void SceneEnd(Scene scene)
    {
        base.SceneEnd(scene);
        Uninitialize(scene);
    }
}

// Render and update the wrapped high-resolution UI scene at the same points
// used by CollabUtils2. The temporary HUD target is two pixels larger than the
// 1920x1080 UI canvas so Celeste's page-turn and edge effects are not clipped.
internal sealed class AppleEverestSceneRenderer : Renderer
{
    private readonly Scene wrappedScene;
    private VirtualRenderTarget hudTarget;

    internal AppleEverestSceneRenderer(Scene scene)
    {
        wrappedScene = scene;
    }

    internal void Alloc()
    {
        if (hudTarget == null)
            hudTarget = VirtualContent.CreateRenderTarget("apple-everest-collab-hud", 1922, 1082);
    }

    internal void Dispose()
    {
        hudTarget?.Dispose();
        hudTarget = null;
    }

    private static VirtualRenderTarget SwapHudTarget(VirtualRenderTarget target)
    {
        VirtualRenderTarget previous = Celeste.HudTarget;
        Celeste.HudTarget = target;
        return previous;
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        bool inputDisabled = MInput.Disabled;
        MInput.Disabled = false;
        wrappedScene.BeforeUpdate();
        wrappedScene.Update();
        wrappedScene.AfterUpdate();
        MInput.Disabled = inputDisabled;
    }

    public override void BeforeRender(Scene scene)
    {
        base.BeforeRender(scene);
        VirtualRenderTarget previous = SwapHudTarget(hudTarget);
        wrappedScene.BeforeRender();
        SwapHudTarget(previous);
    }

    public override void AfterRender(Scene scene)
    {
        base.AfterRender(scene);
        VirtualRenderTarget previous = SwapHudTarget(hudTarget);
        wrappedScene.Render();
        wrappedScene.AfterRender();
        SwapHudTarget(previous);
    }
}

internal sealed class AppleEverestSceneWrappingEntity<T> : AppleEverestSceneWrappingEntity where T : Scene
{
    internal new readonly T WrappedScene;

    internal AppleEverestSceneWrappingEntity(T scene) : base(scene)
    {
        WrappedScene = scene;
    }
}

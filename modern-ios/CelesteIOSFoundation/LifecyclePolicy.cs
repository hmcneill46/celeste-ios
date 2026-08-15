namespace CelesteIOSFoundation;

public enum AppleSceneState { Disconnected, ConnectedInactive, Active, Background }

public sealed class LifecyclePolicy
{
    public AppleSceneState State { get; private set; } = AppleSceneState.Disconnected;
    public int RuntimeStartCount { get; private set; }

    public bool Connect()
    {
        if (State != AppleSceneState.Disconnected) return false;
        State = AppleSceneState.ConnectedInactive;
        RuntimeStartCount++;
        return true;
    }

    public void BecomeActive()
    {
        if (State != AppleSceneState.Disconnected) State = AppleSceneState.Active;
    }

    public void ResignActive()
    {
        if (State == AppleSceneState.Active) State = AppleSceneState.ConnectedInactive;
    }

    public void EnterBackground()
    {
        if (State != AppleSceneState.Disconnected) State = AppleSceneState.Background;
    }

    public void Disconnect() => State = AppleSceneState.Disconnected;
}

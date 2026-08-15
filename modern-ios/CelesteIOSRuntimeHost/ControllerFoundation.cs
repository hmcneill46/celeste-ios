using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CelesteIOSRuntimeHost;

internal static class ControllerFoundation
{
    internal static (float X, float Y) ReadMovement()
    {
        GamePadState state = GamePad.GetState(PlayerIndex.One);
        return state.IsConnected
            ? (state.ThumbSticks.Left.X, -state.ThumbSticks.Left.Y)
            : default;
    }

    internal static void LogInventory()
    {
        GamePadCapabilities capabilities = GamePad.GetCapabilities(PlayerIndex.One);
        RuntimeLog.Info($"controller-foundation connected={(capabilities.IsConnected ? 1 : 0)}; " +
            $"active={(capabilities.IsConnected ? "present" : "none")}; input-path=FNA-SDL");
    }
}

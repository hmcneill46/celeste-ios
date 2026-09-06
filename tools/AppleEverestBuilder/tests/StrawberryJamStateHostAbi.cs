// Host test ABI only: graphics/input are not executed by the state-codec tests.
// Serialization, schema checks, defaults and lifecycle code are linked directly
// from the same runtime source selected into the AOT product.
namespace Microsoft.Xna.Framework { }
namespace Microsoft.Xna.Framework.Input
{
    public enum Buttons { Back = 32, RightStick = 128, RightThumbstickUp = 16777216, RightThumbstickDown = 33554432, RightThumbstickLeft = 67108864, RightThumbstickRight = 134217728 }
    public enum Keys { Tab = 9, LeftShift = 160 }
}
namespace Monocle
{
    internal sealed class SpriteBank { public SpriteBank(object atlas, string xml) { } }
}
namespace Celeste
{
    internal static class GFX { internal static object Game = new(); }
    internal sealed class Level { internal Session Session = new(); }
    internal sealed class Session { internal object Area = ""; }
}
namespace Celeste.Mod
{
    internal static class GeneratedAppleEverestModuleRegistry
    {
        internal const string CoreSessionSchema = "ae9843c8fa67f56bc55f2bbfa877a2f83bb84847cf9c7b493491f5049fd950a2";
    }
    internal class EverestModuleSettings { }
    internal class EverestModuleSaveData { public int Index; }
    internal class EverestModuleSession { public int Index; }
    internal abstract class EverestModule
    {
        public object? _Settings, _SaveData, _Session;
        public virtual Type? SettingsType => null;
        public virtual Type? SaveDataType => null;
        public virtual Type? SessionType => null;
        public virtual void Load() { }
        public virtual void Initialize() { }
        public virtual void LoadContent(bool firstLoad) { }
        public virtual void Unload() { }
    }
    internal sealed record ButtonBinding(Microsoft.Xna.Framework.Input.Buttons DefaultButton,
        params Microsoft.Xna.Framework.Input.Keys[] DefaultKeys);
    internal sealed record AppleEverestModuleDurabilityAdapter(string Schema,
        Func<EverestModuleSaveData, byte[]>? SerializeSave,
        Func<byte[], int, EverestModuleSaveData>? DeserializeSave,
        Func<EverestModuleSession, byte[]>? SerializeSession,
        Func<byte[], int, EverestModuleSession>? DeserializeSession);
    internal static class AppleEverestProgressionRuntime { internal static string Sid(object area) => (string)area; }
}

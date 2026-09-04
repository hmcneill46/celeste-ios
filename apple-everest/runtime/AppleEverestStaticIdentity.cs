using Celeste;
using Monocle;

namespace Celeste.Mod;

/// <summary>
/// Typed identity attached by the generated factory registry. Static semantic
/// relationships can therefore target an exact authored custom ID and entity
/// ID without reflection or desktop helper type discovery.
/// </summary>
internal sealed class AppleEverestStaticIdentity : Component
{
    internal string CustomId { get; }
    internal EntityID EntityId { get; }

    internal AppleEverestStaticIdentity(string customId, EntityID entityId)
        : base(active: false, visible: false)
    {
        CustomId = customId;
        EntityId = entityId;
    }
}

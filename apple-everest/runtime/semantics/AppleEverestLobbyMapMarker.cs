#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal class AppleEverestLobbyMapMarker : Entity
{
	public AppleEverestLobbyMapMarker(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		Active = false;
		Visible = false;
	}
}

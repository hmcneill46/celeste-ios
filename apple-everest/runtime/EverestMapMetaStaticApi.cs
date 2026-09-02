namespace Celeste.Mod.Meta;

// Bounded pinned API needed by helper initialization. Full root-map metadata
// loading remains outside this stage; accepted static map metadata is lowered
// by the host compiler before this typed ApplyTo chain is invoked.
public class MapMeta
{
	public string Wipe { get; set; }

	public void ApplyTo(AreaData area)
	{
	}
}

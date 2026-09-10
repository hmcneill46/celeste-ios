#nullable disable
using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// ContortHelper 1.5.5 RandomSoundTrigger + its selected AbstractTrigger path.
// The authored guard remains exact. No exit cancellation, activation latch,
// player-death check, or persistent DoNotLoad write belongs to this profile.
internal sealed class AppleEverestRandomSoundTrigger : Trigger
{
    private readonly string[] audioEvents;
    private readonly bool oneUse;
    private readonly EntityID id;

    internal AppleEverestRandomSoundTrigger(EntityData data, Vector2 offset, EntityID id)
        : base(data, offset)
    {
        if (ParseList(data.Attr("neededFlags", "")).Length != 0 ||
            ParseList(data.Attr("flagsAfterInvoke", "")).Length != 0 ||
            data.Float("delay") != 0f || !data.Bool("occurOnEnter", true) || data.Bool("persistent"))
            throw new InvalidOperationException("random-sound trigger is outside the selected finite lifecycle");
        audioEvents = ParseList(data.Attr("audioEvents", ""));
        oneUse = data.Bool("oneUse");
        this.id = id;
    }

    // An empty/whitespace whole string is an empty list. Trimming comma items
    // must not discard empty items, since that changes Random.Next's bound.
    internal static string[] ParseList(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        string[] items = value.Split(',');
        for (int i = 0; i < items.Length; i++) items[i] = items[i].Trim();
        return items;
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        Add(new Coroutine(Invoke()));
    }

    private IEnumerator Invoke()
    {
        // The release's delay<=0 branch yields zero before reading Session or
        // selecting audio. Keep the scheduler boundary and even Next(1).
        yield return 0f;
        Session session = ((Level)Scene).Session;
        if (audioEvents.Length != 0)
        {
            string selected = audioEvents[Calc.Random.Next(audioEvents.Length)];
            if (!string.IsNullOrWhiteSpace(selected)) Audio.Play(selected);
        }
        if (oneUse)
        {
            Logger.Log("ContortHelper", $"Removing one-use trigger RandomSoundTrigger using '{id.ID} + 10,000,000'");
            RemoveSelf();
        }
    }
}

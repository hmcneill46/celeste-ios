using System;

namespace Microsoft.Xna.Framework.Input.Touch;

// SDL returns a compact active-finger array. This policy preserves FNA/XNA
// slots by stable SDL finger ID so array compaction cannot release or replace
// an unrelated held finger.
internal static class StableTouchSlotPolicy
{
    internal const int Empty = -1;
    internal const int Vacated = -2;

    internal static void Map(
        ReadOnlySpan<int> previousSlotIds,
        ReadOnlySpan<int> activeFingerIds,
        Span<int> nextSlotIds)
    {
        if (previousSlotIds.Length != nextSlotIds.Length || activeFingerIds.Length > nextSlotIds.Length)
            throw new ArgumentException("Invalid stable touch-slot snapshot.");

        nextSlotIds.Fill(Empty);
        Span<bool> claimed = stackalloc bool[activeFingerIds.Length];

        for (int slot = 0; slot < previousSlotIds.Length; slot++)
        {
            int previous = previousSlotIds[slot];
            if (previous == Empty) continue;
            nextSlotIds[slot] = Vacated;
            for (int finger = 0; finger < activeFingerIds.Length; finger++)
            {
                if (claimed[finger] || activeFingerIds[finger] != previous) continue;
                nextSlotIds[slot] = previous;
                claimed[finger] = true;
                break;
            }
        }

        for (int finger = 0; finger < activeFingerIds.Length; finger++)
        {
            if (claimed[finger]) continue;
            for (int slot = 0; slot < nextSlotIds.Length; slot++)
            {
                // A slot vacated during this snapshot must expose its Released
                // edge for one update. A new finger may use it next update.
                if (nextSlotIds[slot] != Empty) continue;
                nextSlotIds[slot] = activeFingerIds[finger];
                claimed[finger] = true;
                break;
            }
        }
    }
}

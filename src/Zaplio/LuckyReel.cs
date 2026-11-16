namespace Zaplio;

/// <summary>
/// "Şansıma": picks a random title or channel from the list on screen and lays out the reel that spins past and stops on it.
/// Pure logic, so the choice and the reel can be tested without a window.
/// </summary>
internal static class LuckyReel
{
    /// <summary>Cards in the reel.</summary>
    internal const int Length = 30;

    /// <summary>The winner's position; a few cards follow it so the strip does not end at the marker.</summary>
    internal const int WinnerSlot = Length - 4;

    /// <summary>
    /// A random index, avoiding the most recent picks while there is anything else to choose from,
    /// so pressing again does not land on the same thing.
    /// </summary>
    internal static int Pick(IReadOnlyList<string> keys, IReadOnlyCollection<string> recent, Random random)
    {
        if (keys.Count == 0) return -1;
        var fresh = Enumerable.Range(0, keys.Count).Where(index => !recent.Contains(keys[index])).ToArray();
        return fresh.Length > 0 ? fresh[random.Next(fresh.Length)] : random.Next(keys.Count);
    }

    /// <summary>
    /// Indexes into the list for every card of the reel. The winner sits at <see cref="WinnerSlot"/>; its neighbours are
    /// never the same item, so the stop is unambiguous, and no two neighbouring cards repeat when the list allows it.
    /// </summary>
    internal static int[] Build(int count, int winner, Random random)
    {
        if (count <= 0 || winner < 0 || winner >= count) return [];
        var reel = new int[Length];
        for (var slot = 0; slot < Length; slot++)
        {
            if (slot == WinnerSlot)
            {
                reel[slot] = winner;
                continue;
            }
            var beside = Math.Abs(slot - WinnerSlot) == 1;
            var previous = slot > 0 ? reel[slot - 1] : -1;
            reel[slot] = Choose(index => index != previous && (!beside || index != winner))
                ?? Choose(index => !beside || index != winner)
                ?? random.Next(count);
        }
        return reel;

        int? Choose(Func<int, bool> allowed)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var candidate = random.Next(count);
                if (allowed(candidate)) return candidate;
            }
            var start = random.Next(count);
            for (var step = 0; step < count; step++)
            {
                var candidate = (start + step) % count;
                if (allowed(candidate)) return candidate;
            }
            return null;
        }
    }

    /// <summary>How far the strip moves (to the left) so <paramref name="slot"/> is centred in a viewport.</summary>
    internal static double Offset(int slot, double cardWidth, double gap, double viewport) =>
        slot * (cardWidth + gap) + cardWidth / 2 - viewport / 2;
}

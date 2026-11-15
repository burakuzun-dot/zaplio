namespace Zaplio;

/// <summary>Keys for the movie/series favourites and watch-later lists.</summary>
internal static class VodLists
{
    /// <summary>
    /// "{sourceId:N}:{m|s}:{providerId}". Movies and series come from different provider id spaces, so the section is part of the key;
    /// starting with the source id lets removing a source clean its entries by prefix like every other per-source list.
    /// </summary>
    internal static string Key(Guid sourceId, LibrarySection section, long providerId) =>
        $"{sourceId:N}:{(section == LibrarySection.Series ? 's' : 'm')}:{providerId}";

    /// <summary>Adds the key when missing, removes it when present. Returns true when it is in the set afterwards.</summary>
    internal static bool Toggle(HashSet<string> set, string key)
    {
        if (set.Remove(key)) return false;
        set.Add(key);
        return true;
    }
}

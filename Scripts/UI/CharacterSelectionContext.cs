using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterSelectionContext
{
    public static CharacterCatalogEntry CurrentDetailCharacter { get; set; }

    public static int CurrentSelectPage { get; set; }

    public static List<CharacterCatalogEntry> SelectedTeam { get; } = new();

    public static bool IsSelected(CharacterCatalogEntry character)
    {
        return IndexOf(character) >= 0;
    }

    public static int IndexOf(CharacterCatalogEntry character)
    {
        if (character == null)
        {
            return -1;
        }

        string key = GetKey(character);
        return SelectedTeam.FindIndex(selected => string.Equals(GetKey(selected), key, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryToggleCharacter(CharacterCatalogEntry character, int maxTeamSize, out bool isSelectedNow)
    {
        isSelectedNow = false;
        if (character == null)
        {
            return false;
        }

        int existingIndex = IndexOf(character);
        if (existingIndex >= 0)
        {
            SelectedTeam.RemoveAt(existingIndex);
            return true;
        }

        if (SelectedTeam.Count >= maxTeamSize)
        {
            return false;
        }

        SelectedTeam.Add(character);
        isSelectedNow = true;
        return true;
    }

    public static void ClearSelection()
    {
        SelectedTeam.Clear();
    }

    public static void RebindSelection(IReadOnlyList<CharacterCatalogEntry> availableCharacters)
    {
        if (availableCharacters == null || availableCharacters.Count == 0)
        {
            SelectedTeam.Clear();
            CurrentDetailCharacter = null;
            CurrentSelectPage = 0;
            return;
        }

        var characterMap = availableCharacters
            .Where(static character => character != null)
            .GroupBy(static character => GetKey(character), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var reboundTeam = new List<CharacterCatalogEntry>();
        foreach (var selectedCharacter in SelectedTeam)
        {
            string key = GetKey(selectedCharacter);
            if (characterMap.TryGetValue(key, out var reboundCharacter))
            {
                reboundTeam.Add(reboundCharacter);
            }
        }

        SelectedTeam.Clear();
        SelectedTeam.AddRange(reboundTeam);

        if (CurrentDetailCharacter != null)
        {
            string detailKey = GetKey(CurrentDetailCharacter);
            CurrentDetailCharacter = characterMap.TryGetValue(detailKey, out var reboundDetailCharacter)
                ? reboundDetailCharacter
                : null;
        }
    }

    private static string GetKey(CharacterCatalogEntry character)
    {
        if (character == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(character.JsonPath)
            ? character.JsonPath
            : character.UnitName ?? string.Empty;
    }
}

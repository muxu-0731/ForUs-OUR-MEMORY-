using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static class CharacterDataRepository
{
    public const string CharacterDataDirectory = "res://character_data";

    private const string ExternalDataDirectoryName = "data_turn_based_combat_windows_x86_64";

    private static readonly JsonSerializerOptions CharacterJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsExportedRuntime => !OS.HasFeature("editor");

    public static List<CharacterCatalogEntry> LoadPlayerCharacters()
    {
        GD.Print($"CharacterDataRepository: LoadPlayerCharacters start. exported={IsExportedRuntime}, editorFeature={OS.HasFeature("editor")}, templateFeature={OS.HasFeature("template")}");

        foreach (string directoryPath in GetCharacterDataDirectoryCandidates())
        {
            var characters = LoadPlayerCharactersFromDirectory(directoryPath);
            if (characters.Count > 0)
            {
                GD.Print($"CharacterDataRepository: loaded {characters.Count} player characters from {directoryPath}");
                return characters;
            }

            GD.PrintErr($"CharacterDataRepository: no player characters loaded from {directoryPath}");
        }

        GD.PrintErr("CharacterDataRepository: no player characters were loaded from res:// or external fallback directories.");
        return new List<CharacterCatalogEntry>();
    }

    public static CharacterCatalogEntry LoadCharacter(string jsonPath)
    {
        CharacterConfig config = LoadCharacterConfig(jsonPath, out string resolvedJsonPath);
        return config == null ? null : new CharacterCatalogEntry(config, resolvedJsonPath);
    }

    public static List<string> LoadPlayerCharacterJsonPaths()
    {
        return LoadPlayerCharacters()
            .Select(static character => character.JsonPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    public static Texture2D LoadTextureSafe(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            GD.PrintErr($"CharacterDataRepository: texture path is empty. exported={IsExportedRuntime}");
            return null;
        }

        bool exists = ResourceLoader.Exists(resourcePath);
        GD.Print($"CharacterDataRepository: loading texture path='{resourcePath}', exists={exists}, exported={IsExportedRuntime}");
        if (!exists)
        {
            GD.PrintErr($"CharacterDataRepository: texture resource does not exist: {resourcePath}");
            return null;
        }

        Texture2D texture = ResourceLoader.Load<Texture2D>(resourcePath);
        GD.Print($"CharacterDataRepository: texture load result path='{resourcePath}', success={texture != null}");
        return texture;
    }

    public static CharacterConfig LoadCharacterConfig(string jsonPath)
    {
        return LoadCharacterConfig(jsonPath, out _);
    }

    public static CharacterConfig LoadCharacterConfig(string jsonPath, out string resolvedJsonPath)
    {
        resolvedJsonPath = NormalizePath(jsonPath);
        if (string.IsNullOrWhiteSpace(resolvedJsonPath))
        {
            GD.PrintErr("CharacterDataRepository: jsonPath is empty.");
            return null;
        }

        if (!TryReadJsonText(resolvedJsonPath, out string jsonText, out resolvedJsonPath))
        {
            return null;
        }

        CharacterConfig config = DeserializeCharacterConfig(jsonText, resolvedJsonPath);
        if (config == null)
        {
            GD.PrintErr($"CharacterDataRepository: parsed null config from {resolvedJsonPath}");
            GD.Print("Loaded <null>, skills=-1");
            return null;
        }

        LogLoadedCharacterConfig(config, resolvedJsonPath, jsonText.Length);
        return config;
    }

    private static List<CharacterCatalogEntry> LoadPlayerCharactersFromDirectory(string directoryPath)
    {
        var characters = new List<CharacterCatalogEntry>();
        string normalizedDirectoryPath = NormalizePath(directoryPath);

        using DirAccess dir = DirAccess.Open(normalizedDirectoryPath);
        if (dir == null)
        {
            GD.PrintErr($"CharacterDataRepository: failed to open character data directory '{normalizedDirectoryPath}', error={DirAccess.GetOpenError()}, exported={IsExportedRuntime}");
            return characters;
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] fileNames = dir.GetFiles();
        GD.Print($"CharacterDataRepository: scanning directory '{normalizedDirectoryPath}', fileCount={fileNames.Length}, exported={IsExportedRuntime}");

        foreach (string fileName in fileNames.OrderBy(static file => file, StringComparer.OrdinalIgnoreCase))
        {
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string jsonPath = $"{normalizedDirectoryPath.TrimEnd('/')}/{fileName}";
            CharacterCatalogEntry entry = LoadCharacter(jsonPath);
            if (entry == null || entry.Config == null || !entry.Config.IsPlayerUnit)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.UnitName))
            {
                GD.PrintErr($"CharacterDataRepository: skipped unnamed player unit from {entry.JsonPath}");
                continue;
            }

            if (!seenNames.Add(entry.UnitName))
            {
                GD.PrintErr($"CharacterDataRepository: skipped duplicate player unit name '{entry.UnitName}' from {entry.JsonPath}");
                continue;
            }

            characters.Add(entry);
        }

        return characters;
    }

    private static bool TryReadJsonText(string jsonPath, out string jsonText, out string resolvedJsonPath)
    {
        jsonText = string.Empty;
        resolvedJsonPath = NormalizePath(jsonPath);

        foreach (string candidatePath in GetJsonReadCandidates(resolvedJsonPath))
        {
            bool exists = Godot.FileAccess.FileExists(candidatePath);
            GD.Print($"CharacterDataRepository: reading JSON path='{candidatePath}', exists={exists}, exported={IsExportedRuntime}");
            if (!exists)
            {
                continue;
            }

            using Godot.FileAccess file = Godot.FileAccess.Open(candidatePath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"CharacterDataRepository: failed to open JSON '{candidatePath}', error={Godot.FileAccess.GetOpenError()}, exported={IsExportedRuntime}");
                continue;
            }

            jsonText = file.GetAsText();
            resolvedJsonPath = candidatePath;
            GD.Print($"CharacterDataRepository: JSON read ok path='{candidatePath}', length={jsonText.Length}, exported={IsExportedRuntime}");
            return true;
        }

        GD.PrintErr($"CharacterDataRepository: failed to read JSON '{jsonPath}' from res:// and external fallback paths.");
        return false;
    }

    private static IEnumerable<string> GetCharacterDataDirectoryCandidates()
    {
        yield return CharacterDataDirectory;

        foreach (string directoryPath in GetExternalFallbackDirectories("character_data"))
        {
            yield return directoryPath;
        }
    }

    private static IEnumerable<string> GetJsonReadCandidates(string jsonPath)
    {
        yield return NormalizePath(jsonPath);

        if (!jsonPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        string relativePath = jsonPath.Substring("res://".Length).TrimStart('/');
        foreach (string externalPath in GetExternalFallbackFilePaths(relativePath))
        {
            yield return externalPath;
        }
    }

    private static IEnumerable<string> GetExternalFallbackFilePaths(string relativePath)
    {
        string executableDirectory = GetExecutableDirectory();
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            yield break;
        }

        foreach (string dataRoot in GetExternalDataRoots(executableDirectory))
        {
            yield return NormalizePath($"{dataRoot.TrimEnd('/')}/{relativePath}");
        }
    }

    private static IEnumerable<string> GetExternalFallbackDirectories(string relativeDirectory)
    {
        string executableDirectory = GetExecutableDirectory();
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            yield break;
        }

        foreach (string dataRoot in GetExternalDataRoots(executableDirectory))
        {
            yield return NormalizePath($"{dataRoot.TrimEnd('/')}/{relativeDirectory}");
        }
    }

    private static IEnumerable<string> GetExternalDataRoots(string executableDirectory)
    {
        var roots = new List<string>
        {
            executableDirectory,
            $"{executableDirectory.TrimEnd('/')}/{ExternalDataDirectoryName}"
        };

        using DirAccess executableDir = DirAccess.Open(executableDirectory);
        if (executableDir != null)
        {
            foreach (string childDirectory in executableDir.GetDirectories())
            {
                if (childDirectory.StartsWith("data_", StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add($"{executableDirectory.TrimEnd('/')}/{childDirectory}");
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots.Select(NormalizePath))
        {
            if (seen.Add(root))
            {
                GD.Print($"CharacterDataRepository: external data fallback root candidate='{root}', exported={IsExportedRuntime}");
                yield return root;
            }
        }
    }

    private static string GetExecutableDirectory()
    {
        string executablePath = NormalizePath(OS.GetExecutablePath());
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return string.Empty;
        }

        int slashIndex = executablePath.LastIndexOf('/');
        if (slashIndex <= 0)
        {
            return string.Empty;
        }

        return executablePath.Substring(0, slashIndex);
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static CharacterConfig DeserializeCharacterConfig(string jsonText, string jsonPath)
    {
        if (TryDeserializeCharacterConfig(jsonText, out CharacterConfig config, out string errorMessage))
        {
            return config;
        }

        string repairedJsonText = RepairMalformedJson(jsonText);
        if (!string.Equals(repairedJsonText, jsonText, StringComparison.Ordinal) &&
            TryDeserializeCharacterConfig(repairedJsonText, out config, out string repairedErrorMessage))
        {
            GD.Print($"CharacterDataRepository: repaired malformed skill json for {jsonPath}");
            return config;
        }

        GD.PrintErr($"CharacterDataRepository: failed to parse {jsonPath}: {errorMessage}");
        return null;
    }

    private static bool TryDeserializeCharacterConfig(string jsonText, out CharacterConfig config, out string errorMessage)
    {
        config = null;
        errorMessage = string.Empty;

        try
        {
            config = JsonSerializer.Deserialize<CharacterConfig>(jsonText, CharacterJsonOptions);
            if (config == null)
            {
                errorMessage = "parsed null config";
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(jsonText);
            NormalizeCharacterConfig(config, document.RootElement);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void NormalizeCharacterConfig(CharacterConfig config, JsonElement root)
    {
        config.UnitName ??= string.Empty;
        config.TexturePath ??= string.Empty;
        config.PortraitPath ??= string.Empty;
        config.SelectionPortraitCrop = ParseSelectionPortraitCrop(root);
        config.Major ??= string.Empty;
        config.Skills = BuildNormalizedSkills(config, root);
        config.AdditionalFields ??= new Dictionary<string, JsonElement>();
    }

    private static PortraitCropConfig ParseSelectionPortraitCrop(JsonElement root)
    {
        if (!TryGetFirstProperty(
                root,
                out JsonElement cropElement,
                "SelectCardPortraitCrop",
                "SelectionPortraitCrop",
                "PortraitCrop",
                "CardPortraitCrop",
                "SelectionCardPortraitCrop"))
        {
            return null;
        }

        if (cropElement.ValueKind != JsonValueKind.Object)
        {
            GD.PushWarning("CharacterDataRepository: portrait crop field exists but is not a JSON object.");
            return null;
        }

        if (!TryGetSingle(cropElement, out float x, "X", "x") ||
            !TryGetSingle(cropElement, out float y, "Y", "y") ||
            !TryGetSingle(cropElement, out float width, "Width", "width", "W", "w") ||
            !TryGetSingle(cropElement, out float height, "Height", "height", "H", "h"))
        {
            GD.PushWarning("CharacterDataRepository: portrait crop field is missing X, Y, Width, or Height.");
            return null;
        }

        return new PortraitCropConfig
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            SourcePath = FirstString(cropElement, string.Empty, "SourcePath", "sourcePath", "Source", "source"),
            FitMode = FirstString(cropElement, string.Empty, "FitMode", "fitMode")
        };
    }

    private static List<SkillConfig> BuildNormalizedSkills(CharacterConfig config, JsonElement root)
    {
        if (TryGetFirstProperty(root, out JsonElement skillElement, "Skills", "skills", "Abilities", "abilities", "Skill", "skill"))
        {
            return ParseSkillCollection(skillElement);
        }

        return (config.Skills ?? new List<SkillConfig>())
            .Where(static skill => skill != null)
            .Select(NormalizeSkillConfig)
            .ToList();
    }

    private static List<SkillConfig> ParseSkillCollection(JsonElement skillElement)
    {
        var skills = new List<SkillConfig>();

        if (skillElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in skillElement.EnumerateArray())
            {
                SkillConfig skill = ParseSkillConfig(item);
                if (skill != null)
                {
                    skills.Add(skill);
                }
            }

            return skills;
        }

        if (skillElement.ValueKind == JsonValueKind.Object)
        {
            SkillConfig skill = ParseSkillConfig(skillElement);
            if (skill != null)
            {
                skills.Add(skill);
            }
        }

        return skills;
    }

    private static SkillConfig ParseSkillConfig(JsonElement skillElement)
    {
        if (skillElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        SkillConfig skill = null;
        try
        {
            skill = JsonSerializer.Deserialize<SkillConfig>(skillElement.GetRawText(), CharacterJsonOptions);
        }
        catch (JsonException)
        {
        }

        skill ??= new SkillConfig();
        skill.SkillId = FirstString(skillElement, skill.SkillId, "SkillId", "skillId", "id", "skill_id");
        skill.SkillName = FirstString(skillElement, skill.SkillName, "SkillName", "skillName", "Name", "name", "Title", "title");
        skill.Description = FirstString(skillElement, skill.Description, "Description", "description", "Desc", "desc", "Intro", "intro", "Effect", "effect");
        skill.Type = FirstString(skillElement, skill.Type, "Type", "type", "SkillType", "skillType", "Category", "category");
        skill.EnhancedSkillId = FirstString(skillElement, skill.EnhancedSkillId, "EnhancedSkillId", "enhancedSkillId", "enhanced_skill_id");
        skill.TriggeredBySkillId = FirstString(skillElement, skill.TriggeredBySkillId, "TriggeredBySkillId", "triggeredBySkillId", "triggered_by_skill_id");

        if (TryGetInt32(skillElement, out int energyCost, "EnergyCost", "energyCost", "energy_cost", "Energy", "energy", "Cost", "cost"))
        {
            skill.EnergyCost = energyCost;
        }

        if (TryGetBoolean(skillElement, out bool isSelectable, "IsSelectable", "isSelectable", "selectable"))
        {
            skill.IsSelectable = isSelectable;
        }

        if (TryGetBoolean(skillElement, out bool showInBattleUi, "ShowInBattleUi", "showInBattleUi", "show_in_battle_ui"))
        {
            skill.ShowInBattleUi = showInBattleUi;
        }

        return NormalizeSkillConfig(skill);
    }

    private static SkillConfig NormalizeSkillConfig(SkillConfig skill)
    {
        if (skill == null)
        {
            return null;
        }

        skill.SkillId ??= string.Empty;
        skill.SkillName ??= string.Empty;
        skill.Description ??= string.Empty;
        skill.Type ??= string.Empty;
        skill.EnhancedSkillId ??= string.Empty;
        skill.TriggeredBySkillId ??= string.Empty;
        skill.AdditionalFields ??= new Dictionary<string, JsonElement>();
        return skill;
    }

    private static string FirstString(JsonElement element, string fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out JsonElement propertyValue))
            {
                continue;
            }

            string value = GetElementString(propertyValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback ?? string.Empty;
    }

    private static bool TryGetInt32(JsonElement element, out int value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out JsonElement propertyValue))
            {
                continue;
            }

            if (propertyValue.ValueKind == JsonValueKind.Number && propertyValue.TryGetInt32(out value))
            {
                return true;
            }

            if (int.TryParse(GetElementString(propertyValue), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetBoolean(JsonElement element, out bool value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out JsonElement propertyValue))
            {
                continue;
            }

            if (propertyValue.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (propertyValue.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            string text = GetElementString(propertyValue);
            if (bool.TryParse(text, out value))
            {
                return true;
            }

            if (int.TryParse(text, out int intValue))
            {
                value = intValue != 0;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetSingle(JsonElement element, out float value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out JsonElement propertyValue))
            {
                continue;
            }

            if (propertyValue.ValueKind == JsonValueKind.Number && propertyValue.TryGetSingle(out value))
            {
                return true;
            }

            if (float.TryParse(
                    GetElementString(propertyValue),
                    System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }
        }

        value = 0f;
        return false;
    }

    private static string GetElementString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    private static bool TryGetFirstProperty(JsonElement element, out JsonElement propertyValue, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out propertyValue))
            {
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }
        }

        propertyValue = default;
        return false;
    }

    private static string RepairMalformedJson(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText))
        {
            return jsonText;
        }

        string newline = jsonText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] lines = jsonText.Replace("\r\n", "\n").Split('\n');
        bool changed = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string repairedLine = RepairUnclosedStringValue(lines[i]);
            if (!string.Equals(repairedLine, lines[i], StringComparison.Ordinal))
            {
                lines[i] = repairedLine;
                changed = true;
            }
        }

        return changed ? string.Join(newline, lines) : jsonText;
    }

    private static string RepairUnclosedStringValue(string line)
    {
        int valueStartIndex = FindStringValueStart(line);
        if (valueStartIndex < 0)
        {
            return line;
        }

        if (FindClosingQuote(line, valueStartIndex + 1) >= 0)
        {
            return line;
        }

        int insertIndex = GetStringTerminatorIndex(line);
        return insertIndex < 0 ? line : line.Insert(insertIndex, "\"");
    }

    private static int FindStringValueStart(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex < 0)
        {
            return -1;
        }

        for (int i = colonIndex + 1; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                continue;
            }

            return line[i] == '"' ? i : -1;
        }

        return -1;
    }

    private static int FindClosingQuote(string line, int startIndex)
    {
        bool escaped = false;
        for (int i = startIndex; i < line.Length; i++)
        {
            char current = line[i];
            if (current == '\\' && !escaped)
            {
                escaped = true;
                continue;
            }

            if (current == '"' && !escaped)
            {
                return i;
            }

            escaped = false;
        }

        return -1;
    }

    private static int GetStringTerminatorIndex(string line)
    {
        int index = line.Length;
        while (index > 0 && char.IsWhiteSpace(line[index - 1]))
        {
            index--;
        }

        if (index == 0)
        {
            return -1;
        }

        char last = line[index - 1];
        if (last == ',' || last == '}' || last == ']')
        {
            return index - 1;
        }

        return index;
    }

    private static void LogLoadedCharacterConfig(CharacterConfig config, string jsonPath, int jsonLength)
    {
        int skillCount = config?.Skills?.Count ?? -1;
        GD.Print($"CharacterDataRepository: loaded character json='{jsonPath}', length={jsonLength}, unit='{config?.UnitName ?? "<null>"}', TexturePath='{config?.TexturePath ?? string.Empty}', IsPlayerUnit={config?.IsPlayerUnit}, skills={skillCount}, exported={IsExportedRuntime}");

        if (config != null)
        {
            LogTextureStatus(config.TexturePath, config.UnitName, jsonPath);
            if (!string.IsNullOrWhiteSpace(config.PortraitPath))
            {
                LogTextureStatus(config.PortraitPath, config.UnitName, jsonPath);
            }
        }

        if (config?.Skills == null)
        {
            return;
        }

        foreach (SkillConfig skill in config.Skills)
        {
            if (skill == null)
            {
                GD.Print("Skill: <null>");
                continue;
            }

            GD.Print($"Skill: id={skill.SkillId}, name={skill.SkillName}, type={skill.Type}, energy={skill.EnergyCost}");
        }
    }

    private static void LogTextureStatus(string texturePath, string unitName, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            GD.PrintErr($"CharacterDataRepository: texture path is empty for unit='{unitName}', json='{jsonPath}', exported={IsExportedRuntime}");
            return;
        }

        bool exists = ResourceLoader.Exists(texturePath);
        Texture2D texture = exists ? ResourceLoader.Load<Texture2D>(texturePath) : null;
        GD.Print($"CharacterDataRepository: texture check unit='{unitName}', json='{jsonPath}', TexturePath='{texturePath}', exists={exists}, loaded={texture != null}, exported={IsExportedRuntime}");
    }
}

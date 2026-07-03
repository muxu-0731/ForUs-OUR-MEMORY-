using System.Collections.Generic;

public sealed class CharacterCatalogEntry
{
    public CharacterCatalogEntry(CharacterConfig config, string jsonPath)
    {
        Config = config;
        JsonPath = jsonPath ?? string.Empty;
    }

    public CharacterConfig Config { get; }

    public string JsonPath { get; }

    public string UnitName => Config?.UnitName ?? string.Empty;

    public string TexturePath => Config?.TexturePath ?? string.Empty;

    public string PortraitPath => Config?.PortraitPath ?? string.Empty;

    public PortraitCropConfig SelectionPortraitCrop => Config?.SelectionPortraitCrop;

    public string Major => Config?.Major ?? string.Empty;

    public float HpMax => Config?.HpMax ?? 0f;

    public float Attack => Config?.Attack ?? 0f;

    public int MaxEnergy => Config?.MaxEnergy ?? 0;

    public float CritRate => Config?.CritRate ?? 0f;

    public float CritDamage => Config?.CritDamage ?? 0f;

    public IReadOnlyList<SkillConfig> Skills => Config?.Skills ?? new List<SkillConfig>();
}

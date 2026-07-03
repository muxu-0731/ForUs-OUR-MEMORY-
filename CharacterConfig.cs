using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class PortraitCropConfig
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string FitMode { get; set; } = string.Empty;
}

public class CharacterConfig
{
    [JsonPropertyName("UnitName")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("TexturePath")]
    public string TexturePath { get; set; } = string.Empty;

    [JsonPropertyName("PortraitPath")]
    public string PortraitPath { get; set; } = string.Empty;

    [JsonIgnore]
    public PortraitCropConfig SelectionPortraitCrop { get; set; }

    [JsonPropertyName("major")]
    public string Major { get; set; } = string.Empty;

    [JsonPropertyName("Hp")]
    public float Hp { get; set; }

    [JsonPropertyName("HpMax")]
    public float HpMax { get; set; }

    [JsonPropertyName("Attack")]
    public float Attack { get; set; }

    [JsonPropertyName("CritRate")]
    public float CritRate { get; set; }

    [JsonPropertyName("CritDamage")]
    public float CritDamage { get; set; }

    [JsonPropertyName("IsPlayerUnit")]
    public bool IsPlayerUnit { get; set; }

    [JsonPropertyName("MaxEnergy")]
    public int MaxEnergy { get; set; }

    [JsonPropertyName("Skills")]
    public List<SkillConfig> Skills { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalFields { get; set; } = new();
}

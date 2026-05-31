using System.Collections.Generic;
using System.Text.Json.Serialization;

public class CharacterConfig
{
    [JsonPropertyName("UnitName")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("TexturePath")]
    public string TexturePath { get; set; } = string.Empty;

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
}

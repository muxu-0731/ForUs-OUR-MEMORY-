using System.Text.Json.Serialization;

public class SkillConfig
{
    [JsonPropertyName("SkillId")]
    public string SkillId { get; set; } = string.Empty;

    [JsonPropertyName("SkillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("EnergyCost")]
    public int EnergyCost { get; set; }
}

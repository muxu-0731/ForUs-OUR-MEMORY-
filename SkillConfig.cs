using System.Collections.Generic;
using System.Text.Json;
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

    [JsonPropertyName("EnhancedSkillId")]
    public string EnhancedSkillId { get; set; } = null;

    [JsonPropertyName("IsSelectable")]
    public bool IsSelectable { get; set; } = true;

    [JsonPropertyName("ShowInBattleUi")]
    public bool ShowInBattleUi { get; set; } = true;

    [JsonPropertyName("TriggeredBySkillId")]
    public string TriggeredBySkillId { get; set; } = null;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalFields { get; set; } = new();
}

public class SynergyAttack
{
    public string StatusId { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public float DamageMultiplier { get; set; }
    public int DotStacksOnHit { get; set; }
    public int EnergyGainOnTrigger { get; set; }
    public bool TriggerOnOtherAlliesOnly { get; set; } = true;
}

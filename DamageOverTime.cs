using System;

public class DamageOverTime
{
    public string StatusId { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int RemainingTurns { get; set; }
    public float DamageMultiplier { get; set; }
    public int StackCount { get; set; } = 1;

    public GlobalScript.BattleUnit SourceUnit { get; set; }
    public float SnapshotValue { get; set; }
    public bool CanStack { get; set; } = true;
    public int MaxStacks { get; set; } = int.MaxValue;
    public float DamageTakenAmplifyPerStack { get; set; }

    public DamageOverTime Clone()
    {
        return new DamageOverTime
        {
            StatusId = StatusId,
            StatusName = StatusName,
            RemainingTurns = RemainingTurns,
            DamageMultiplier = DamageMultiplier,
            StackCount = StackCount,
            SourceUnit = SourceUnit,
            SnapshotValue = SnapshotValue,
            CanStack = CanStack,
            MaxStacks = MaxStacks,
            DamageTakenAmplifyPerStack = DamageTakenAmplifyPerStack
        };
    }
}

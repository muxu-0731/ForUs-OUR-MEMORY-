using Godot;
using System.Collections.Generic;

public static class DotGlobalManager
{
    public const string ReviewStatusId = "review";
    public const string GuHenStatusId = "guhen";
    public const string MiaminYouAreDoneStatusId = "miamin_you_are_done";
    public const string MiaminSelfDoubtStatusId = "miamin_self_doubt";

    public static DamageOverTime CreateReviewDot(GlobalScript.BattleUnit sourceUnit, int stackCount = 1)
    {
        return new DamageOverTime
        {
            StatusId = ReviewStatusId,
            StatusName = "锐评",
            RemainingTurns = 3,
            DamageMultiplier = 0.015f,
            StackCount = Mathf.Max(1, stackCount),
            SourceUnit = sourceUnit,
            SnapshotValue = sourceUnit?.HpMax ?? 0f,
            CanStack = true,
            MaxStacks = 10,
            DamageTakenAmplifyPerStack = 0.05f
        };
    }

    public static DamageOverTime CreateGuHenDot(GlobalScript.BattleUnit sourceUnit)
    {
        return new DamageOverTime
        {
            StatusId = GuHenStatusId,
            StatusName = "锢痕",
            RemainingTurns = 3,
            DamageMultiplier = 0.2f,
            StackCount = 1,
            SourceUnit = sourceUnit,
            SnapshotValue = sourceUnit?.Attack ?? 0f,
            CanStack = false,
            MaxStacks = 1,
            DamageTakenAmplifyPerStack = 0f
        };
    }

    public static DamageOverTime CreateMiaminYouAreDoneDot(GlobalScript.BattleUnit sourceUnit, int stackCount = 1)
    {
        return new DamageOverTime
        {
            StatusId = MiaminYouAreDoneStatusId,
            StatusName = "你丸了",
            RemainingTurns = 3,
            DamageMultiplier = 0.1f,
            StackCount = Mathf.Max(1, stackCount),
            SourceUnit = sourceUnit,
            SnapshotValue = sourceUnit?.Attack ?? 0f,
            CanStack = true,
            MaxStacks = 3,
            DamageTakenAmplifyPerStack = 0f
        };
    }

    public static DamageOverTime CreateMiaminSelfDoubtStatus(GlobalScript.BattleUnit sourceUnit)
    {
        return new DamageOverTime
        {
            StatusId = MiaminSelfDoubtStatusId,
            StatusName = "自我怀疑",
            RemainingTurns = 5,
            DamageMultiplier = 0f,
            StackCount = 1,
            SourceUnit = sourceUnit,
            SnapshotValue = 0f,
            CanStack = false,
            MaxStacks = 1,
            DamageTakenAmplifyPerStack = 0f,
            DotDamageTakenMultiplier = 0.5f
        };
    }

    public static void ApplyDot(GlobalScript.BattleUnit target, DamageOverTime dot)
    {
        if (target == null || dot == null)
        {
            GD.PrintErr("DoT 施加失败：目标或状态为空。");
            return;
        }

        target.DamageOverTimeEffects ??= new List<DamageOverTime>();
        var existing = GetDot(target, dot.StatusId);
        if (existing == null)
        {
            target.DamageOverTimeEffects.Add(dot.Clone());
            GD.Print($"【DoT施加】{target.UnitName} 获得 {dot.StatusName}，持续 {dot.RemainingTurns} 回合，层数 {dot.StackCount}");
            return;
        }

        existing.SourceUnit = dot.SourceUnit;
        existing.SnapshotValue = dot.SnapshotValue;
        existing.DamageMultiplier = dot.DamageMultiplier;
        existing.DamageTakenAmplifyPerStack = dot.DamageTakenAmplifyPerStack;
        existing.DotDamageTakenMultiplier = dot.DotDamageTakenMultiplier;
        existing.CanStack = dot.CanStack;
        existing.MaxStacks = dot.MaxStacks;

        if (dot.CanStack)
        {
            AddStacks(target, dot.StatusId, dot.StackCount, dot.MaxStacks, dot.RemainingTurns);
        }
        else
        {
            RefreshDuration(target, dot.StatusId, dot.RemainingTurns);
            GD.Print($"【DoT刷新】{target.UnitName} 的 {dot.StatusName} 刷新至 {dot.RemainingTurns} 回合");
        }
    }

    public static void RefreshDuration(GlobalScript.BattleUnit target, string statusId, int turns)
    {
        var dot = GetDot(target, statusId);
        if (dot == null)
        {
            GD.PrintErr($"DoT 刷新失败：{target?.UnitName} 不存在状态 {statusId}");
            return;
        }

        dot.RemainingTurns = Mathf.Max(0, turns);
    }

    public static void AddStacks(GlobalScript.BattleUnit target, string statusId, int stacksToAdd, int maxStacks, int refreshTurns)
    {
        var dot = GetDot(target, statusId);
        if (dot == null)
        {
            GD.PrintErr($"DoT 叠加失败：{target?.UnitName} 不存在状态 {statusId}");
            return;
        }

        dot.StackCount = Mathf.Clamp(dot.StackCount + stacksToAdd, 1, Mathf.Max(1, maxStacks));
        dot.RemainingTurns = Mathf.Max(dot.RemainingTurns, refreshTurns);
        GD.Print($"【DoT叠加】{target.UnitName} 的 {dot.StatusName} 当前 {dot.StackCount} 层，持续 {dot.RemainingTurns} 回合");
    }

    public static float ResolveSingleDotDamage(GlobalScript global, GlobalScript.BattleUnit target, DamageOverTime dot, bool reduceDuration)
    {
        if (global == null || target == null || dot == null || target.IsDead)
        {
            return 0f;
        }

        int stacks = Mathf.Max(1, dot.StackCount);
        float damage = Mathf.Max(0f, dot.SnapshotValue * dot.DamageMultiplier * stacks);
        float dotDamageTakenBonus = GetDotDamageTakenBonus(target);
        if (damage > 0f && dotDamageTakenBonus > 0f)
        {
            float beforeBonus = damage;
            damage *= 1f + dotDamageTakenBonus;
            GD.Print($"【持续伤害加深】{target.UnitName} 受到的持续伤害提高{dotDamageTakenBonus:P0}，{beforeBonus:0.0} → {damage:0.0}");
        }

        if (damage <= 0f)
        {
            GD.Print($"【DoT结算】{target.UnitName} 的 {dot.StatusName} 伤害为 0，跳过");
        }
        else
        {
            GD.Print($"【DoT结算】{target.UnitName} 触发 {dot.StatusName}，层数 {stacks}，伤害 {damage:0.0}");
            global.TakeDamage(target, damage, isDotDamage: true);
            global.TriggerMiaminDotHealPassive(target, damage);
        }

        if (reduceDuration)
        {
            dot.RemainingTurns = Mathf.Max(0, dot.RemainingTurns - 1);
            if (dot.RemainingTurns <= 0)
            {
                RemoveDot(target, dot.StatusId);
                GD.Print($"【DoT移除】{target.UnitName} 的 {dot.StatusName} 已结束");
            }
        }

        return damage;
    }

    public static void ClearDots(GlobalScript.BattleUnit target, string statusId = null)
    {
        if (target?.DamageOverTimeEffects == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(statusId))
        {
            target.DamageOverTimeEffects.Clear();
            GD.Print($"【DoT清空】{target.UnitName} 的所有持续伤害已清空");
            return;
        }

        RemoveDot(target, statusId);
        GD.Print($"【DoT清空】{target.UnitName} 的 {statusId} 已清空");
    }

    public static void SpreadDots(GlobalScript global, GlobalScript.BattleUnit sourceTarget, IEnumerable<GlobalScript.BattleUnit> adjacentTargets)
    {
        if (global == null || sourceTarget == null || adjacentTargets == null)
        {
            GD.PrintErr("DoT 扩散失败：参数为空。");
            return;
        }

        if (sourceTarget.DamageOverTimeEffects == null || sourceTarget.DamageOverTimeEffects.Count == 0)
        {
            GD.Print($"【DoT扩散】{sourceTarget.UnitName} 身上没有可扩散的持续伤害");
            return;
        }

        foreach (var adjacentTarget in adjacentTargets)
        {
            if (adjacentTarget == null || adjacentTarget.IsDead || adjacentTarget == sourceTarget)
            {
                continue;
            }

            foreach (var dot in sourceTarget.DamageOverTimeEffects)
            {
                ApplyDot(adjacentTarget, dot.Clone());
            }

            GD.Print($"【DoT扩散】{sourceTarget.UnitName} 的持续伤害已扩散到 {adjacentTarget.UnitName}");
        }
    }

    public static void ExplodeDots(GlobalScript global, IEnumerable<GlobalScript.BattleUnit> targets)
    {
        if (global == null || targets == null)
        {
            GD.PrintErr("DoT 引爆失败：参数为空。");
            return;
        }

        foreach (var target in targets)
        {
            if (target == null || target.IsDead || target.DamageOverTimeEffects == null)
            {
                continue;
            }

            foreach (var dot in target.DamageOverTimeEffects.ToArray())
            {
                GD.Print($"【DoT引爆】{target.UnitName} 的 {dot.StatusName} 被引爆");
                ResolveSingleDotDamage(global, target, dot, reduceDuration: false);
            }
        }
    }

    public static void ResolveTurnStartDots(GlobalScript global, GlobalScript.BattleUnit unit)
    {
        if (global == null || unit == null || unit.IsDead || unit.DamageOverTimeEffects == null || unit.DamageOverTimeEffects.Count == 0)
        {
            return;
        }

        foreach (var dot in unit.DamageOverTimeEffects.ToArray())
        {
            if (unit.IsDead)
            {
                break;
            }

            ResolveSingleDotDamage(global, unit, dot, reduceDuration: true);
        }
    }

    public static bool HasTurnStartDamageToResolve(GlobalScript.BattleUnit unit)
    {
        if (unit == null || unit.IsDead || unit.DamageOverTimeEffects == null)
        {
            return false;
        }

        foreach (var dot in unit.DamageOverTimeEffects)
        {
            if (dot == null || dot.RemainingTurns <= 0 || dot.StackCount <= 0)
            {
                continue;
            }

            if (dot.DamageMultiplier > 0f)
            {
                return true;
            }
        }

        return false;
    }

    public static float GetDamageTakenBonus(GlobalScript.BattleUnit target)
    {
        if (target?.DamageOverTimeEffects == null)
        {
            return 0f;
        }

        float bonus = 0f;
        foreach (var dot in target.DamageOverTimeEffects)
        {
            bonus += Mathf.Max(0, dot.DamageTakenAmplifyPerStack) * Mathf.Max(0, dot.StackCount);
        }
        return bonus;
    }

    public static float GetDotDamageTakenBonus(GlobalScript.BattleUnit target)
    {
        if (target?.DamageOverTimeEffects == null)
        {
            return 0f;
        }

        float bonus = 0f;
        foreach (var dot in target.DamageOverTimeEffects)
        {
            bonus += Mathf.Max(0f, dot.DotDamageTakenMultiplier);
        }
        return bonus;
    }

    public static DamageOverTime GetDot(GlobalScript.BattleUnit target, string statusId)
    {
        if (target?.DamageOverTimeEffects == null || string.IsNullOrEmpty(statusId))
        {
            return null;
        }

        return target.DamageOverTimeEffects.Find(dot => dot != null && dot.StatusId == statusId);
    }

    private static void RemoveDot(GlobalScript.BattleUnit target, string statusId)
    {
        if (target?.DamageOverTimeEffects == null || string.IsNullOrEmpty(statusId))
        {
            return;
        }

        target.DamageOverTimeEffects.RemoveAll(dot => dot != null && dot.StatusId == statusId);
    }
}

using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class SynergyGlobalManager
{
    public const string ChickenSynergyAttackId = "chicken_coop_attack";

    public static SynergyAttack CreateChickenDefaultSynergy()
    {
        return new SynergyAttack
        {
            StatusId = ChickenSynergyAttackId,
            StatusName = "游戏锐评官",
            DamageMultiplier = 0.02f,
            DotStacksOnHit = 1,
            EnergyGainOnTrigger = 5,
            TriggerOnOtherAlliesOnly = true
        };
    }

    public static void RegisterSynergyAttack(GlobalScript.BattleUnit owner, SynergyAttack synergyAttack)
    {
        if (owner == null || synergyAttack == null)
        {
            GD.PrintErr("默契攻击注册失败：拥有者或配置为空。");
            return;
        }

        owner.SynergyAttacks ??= new List<SynergyAttack>();
        owner.SynergyAttacks.RemoveAll(item => item != null && item.StatusId == synergyAttack.StatusId);
        owner.SynergyAttacks.Add(synergyAttack);
        GD.Print($"【默契攻击注册】{owner.UnitName} 注册词条：{synergyAttack.StatusName}");
    }

    public static async Task TriggerOnAllyAttackHit(GlobalScript global, GlobalScript.BattleUnit attacker, GlobalScript.BattleUnit target)
    {
        if (global == null || attacker == null || target == null || target.IsDead)
        {
            return;
        }

        foreach (var owner in global.PlayerTeam)
        {
            if (owner == null || owner.IsDead || owner.SynergyAttacks == null || owner.SynergyAttacks.Count == 0)
            {
                continue;
            }

            foreach (var synergyAttack in owner.SynergyAttacks)
            {
                if (synergyAttack == null)
                {
                    continue;
                }

                if (synergyAttack.TriggerOnOtherAlliesOnly && owner == attacker)
                {
                    continue;
                }

                GD.Print($"【默契攻击触发】{owner.UnitName} 触发 {synergyAttack.StatusName}");
                await TriggerSingleSynergyAttack(global, owner, synergyAttack, target);
            }
        }
    }

    private static async Task TriggerSingleSynergyAttack(
        GlobalScript global,
        GlobalScript.BattleUnit owner,
        SynergyAttack synergyAttack,
        GlobalScript.BattleUnit target)
    {
        if (global == null || owner == null || synergyAttack == null || target == null || target.IsDead)
        {
            return;
        }

        await global.ToSignal(global.GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        Ui.Instance?.BroadcastSkillName(synergyAttack.StatusName, true);

        float baseDamage = owner.HpMax * synergyAttack.DamageMultiplier;
        float finalDamage = baseDamage;
        bool isCrit = false;

        float critRate = owner.GetFinalCritRate();
        float roll = (float)GlobalScript.GlobalRandom.NextDouble();
        if (roll <= critRate)
        {
            isCrit = true;
            finalDamage *= owner.GetFinalCritDamage();
        }

        if (target.Hardened)
        {
            finalDamage /= 2f;
        }

        global.TakeDamage(target, finalDamage, isCrit: isCrit);

        if (synergyAttack.DotStacksOnHit > 0)
        {
            global.ApplyReview(owner, target, synergyAttack.DotStacksOnHit);
        }

        if (synergyAttack.EnergyGainOnTrigger > 0)
        {
            global.AddEnergy(owner, synergyAttack.EnergyGainOnTrigger, synergyAttack.StatusName);
        }

        global.CheckBattleEnd();
        await global.ToSignal(global.GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
    }
}

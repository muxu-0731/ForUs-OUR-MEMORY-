using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class SynergyGlobalManager
{
    public const string ChickenSynergyAttackId = "chicken_coop_attack";
    private const string ChickenCoopProjectileScenePath = "res://ChickenCoopProjectile.tscn";
    private const float ProjectileTravelDuration = 0.32f;
    private const int EffectTopZIndex = 1200;
    private const float ProjectileStartDistance = 42f;
    private const float ProjectileEndDistance = 34f;
    private static readonly Vector2 ProjectileVerticalOffset = new Vector2(0f, -26f);
    private static readonly Color ExplosionFlashColor = new Color(0.84f, 0.98f, 1f, 0.95f);
    private static readonly Vector2 ProjectileGlowMinScale = new Vector2(5.6f, 5.6f);
    private static readonly Vector2 ProjectileGlowMaxScale = new Vector2(7.2f, 7.2f);
    private static readonly Vector2 ExplosionFlashStartScale = new Vector2(2.5f, 2.5f);
    private static readonly Vector2 ExplosionFlashEndScale = new Vector2(7.5f, 7.5f);

    private static readonly PackedScene ChickenCoopProjectileScene = GD.Load<PackedScene>(ChickenCoopProjectileScenePath);

    public static SynergyAttack CreateChickenDefaultSynergy()
    {
        return new SynergyAttack
        {
            StatusId = ChickenSynergyAttackId,
            StatusName = "默契攻击",
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

        await PlayChickenCoopProjectile(global, owner, target);

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

        global.TakeDamage(target, finalDamage, isCrit: isCrit, sourceUnit: owner);

        if (synergyAttack.DotStacksOnHit > 0)
        {
            global.ApplyReview(owner, target, synergyAttack.DotStacksOnHit);
        }

        if (synergyAttack.EnergyGainOnTrigger > 0)
        {
            global.AddEnergy(owner, synergyAttack.EnergyGainOnTrigger, synergyAttack.StatusName);
        }

        global.CheckBattleEnd();
    }

    private static async Task PlayChickenCoopProjectile(
        GlobalScript global,
        GlobalScript.BattleUnit owner,
        GlobalScript.BattleUnit target)
    {
        if (global == null || owner?.BindNode == null || target?.BindNode == null)
        {
            return;
        }

        if (ChickenCoopProjectileScene == null)
        {
            GD.PrintErr($"默契攻击特效加载失败：{ChickenCoopProjectileScenePath}");
            return;
        }

        var projectileRoot = ChickenCoopProjectileScene.Instantiate<Node2D>();
        if (projectileRoot == null)
        {
            return;
        }

        var projectileGlow = projectileRoot.GetNodeOrNull<Sprite2D>("ProjectileGlow");
        var projectileSprite = projectileRoot.GetNodeOrNull<Sprite2D>("ProjectileSprite");
        var explosionFlash = projectileRoot.GetNodeOrNull<Sprite2D>("ExplosionFlash");
        var explosionParticles = projectileRoot.GetNodeOrNull<GpuParticles2D>("ExplosionParticles");

        Node effectParent = GetEffectParent(global);
        effectParent.AddChild(projectileRoot);

        projectileRoot.TopLevel = true;
        projectileRoot.ZAsRelative = false;
        projectileRoot.ZIndex = EffectTopZIndex;

        Vector2 startPos = owner.BindNode.GlobalPosition;
        Vector2 targetPos = target.BindNode.GlobalPosition;
        Vector2 direction = (targetPos - startPos).Normalized();
        if (direction == Vector2.Zero)
        {
            direction = Vector2.Right;
        }

        startPos += ProjectileVerticalOffset + direction * ProjectileStartDistance;
        targetPos += ProjectileVerticalOffset - direction * ProjectileEndDistance;
        projectileRoot.GlobalPosition = startPos;

        if (projectileGlow != null)
        {
            projectileGlow.Visible = true;
            projectileGlow.Position = Vector2.Zero;
            projectileGlow.Scale = ProjectileGlowMinScale;
            projectileGlow.Modulate = new Color(projectileGlow.Modulate, 0.45f);
        }

        if (projectileSprite != null)
        {
            projectileSprite.Visible = true;
            projectileSprite.Position = Vector2.Zero;
            projectileSprite.Modulate = new Color(projectileSprite.Modulate, 1f);
        }

        if (explosionFlash != null)
        {
            explosionFlash.Visible = false;
            explosionFlash.Position = Vector2.Zero;
            explosionFlash.Modulate = ExplosionFlashColor;
            explosionFlash.Scale = ExplosionFlashStartScale;
        }

        if (explosionParticles != null)
        {
            explosionParticles.Visible = false;
            explosionParticles.Emitting = false;
            explosionParticles.Position = Vector2.Zero;
        }

        Tween pulseTween = null;
        if (projectileGlow != null)
        {
            pulseTween = global.CreateTween();
            pulseTween.SetLoops();
            pulseTween.TweenProperty(projectileGlow, "scale", ProjectileGlowMaxScale, 0.14f);
            pulseTween.TweenProperty(projectileGlow, "scale", ProjectileGlowMinScale, 0.14f);
        }

        Tween flyTween = global.CreateTween();
        flyTween.TweenProperty(projectileRoot, "global_position", targetPos, ProjectileTravelDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        await global.ToSignal(flyTween, Tween.SignalName.Finished);
        pulseTween?.Kill();

        if (projectileGlow != null)
        {
            projectileGlow.Visible = false;
        }

        if (projectileSprite != null)
        {
            projectileSprite.Visible = false;
        }

        if (explosionFlash != null)
        {
            explosionFlash.Visible = true;
            var flashTween = global.CreateTween();
            flashTween.TweenProperty(explosionFlash, "scale", ExplosionFlashEndScale, 0.18f);
            flashTween.Parallel().TweenProperty(explosionFlash, "modulate:a", 0f, 0.18f);
        }

        if (explosionParticles != null)
        {
            explosionParticles.Visible = true;
            explosionParticles.Restart();
            explosionParticles.Emitting = true;
            await global.ToSignal(
                global.GetTree().CreateTimer(Mathf.Max(0.2f, explosionParticles.Lifetime)),
                SceneTreeTimer.SignalName.Timeout);
            explosionParticles.Emitting = false;
        }
        else
        {
            await global.ToSignal(global.GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
        }

        projectileRoot.QueueFree();
    }

    private static Node GetEffectParent(GlobalScript global)
    {
        return global.GetTree().CurrentScene?.GetNodeOrNull<Node>("BattleEffectLayer/BattleEffects")
            ?? (Node)ParticleEffectManager.Instance
            ?? global.GetTree().CurrentScene
            ?? global.GetTree().Root;
    }
}

using Godot;
using System;
using System.Collections.Generic;

public partial class GlobalScript : Node
{
    // 全局单例
    public static GlobalScript Instance { get; private set; }
    // 全局唯一随机数实例
    public static readonly Random GlobalRandom = new Random();

    // 技能类型枚举
    public enum SkillType
    {
        Passive,
        Normal,
        Special,
        Ultimate
    }

    // 技能数据结构
    public class SkillData
    {
        public string SkillId;
        public string SkillName;
        public string Description;
        public SkillType Type;
    }

    // 战斗单位类
    public class BattleUnit
    {
        public string UnitName;
        public string TexturePath;
        public float Hp;
        public float HpMax;
        public float Attack;
        public float CritRate;        // 基础暴击率
        public float CritDamage;      // 基础暴击伤害倍率
        public float Defense;

        // ✅ 新增：小鸡-锐评Debuff
        public int ReviewStacks;      // 锐评层数（最多10）
        public int ReviewTurns;       // 锐评持续回合
        public float ReviewDotDamage; // 锐评每层DOT伤害（小鸡2.5%最大生命值）
        public BattleUnit ReviewCaster; // 锐评施加者（小鸡）

        // ✅ 新增：小鸡-禁疗Debuff
        public bool IsHealBlocked;
        public int HealBlockTurns;

        // ✅ 新增：小鸡-防御降低Debuff
        public float DefenseDownPercent; // 防御降低百分比（0~1）
        public int DefenseDownTurns;

        // 获取实际防御（考虑防御降低）
        public float GetEffectiveDefense()
        {
            return Defense * (1 - DefenseDownPercent);
        }

        // 获取伤害减免
        public float GetDamageReduction()
        {
            float effectiveDef = GetEffectiveDefense();
            if (effectiveDef <= 0) return 0f;
            return Mathf.Min(effectiveDef / (effectiveDef + 200), 0.8f); // 减免上限80%
        }

        // 向死而生buff
        public float ExtraCritRateBuff = 0f;
        public float ExtraCritDamageBuff = 0f;
        public int DeadLiveBuffTurns = 0;

        // 该角色拥有的技能列表
        public List<SkillData> Skills = new List<SkillData>();

        // 通用状态效果
        public int AbyssTurns;
        public float AbyssBaseDamage;
        public int AbyssStacks;
        public int AbyssMaxStacks;
        public bool Hardened;
        public int HardenedTurns;
        public bool Cursed;
        public int CursedTurns;
        public bool ExtraTurn;

        public bool IsPlayerUnit;
        public Node2D BindNode;
        public bool IsDead => Hp <= 0;

        public bool HasRegularCustomerMark; // 是否持有熟客印记
        public int RegularCustomerMarkTurns; // 印记剩余持续回合
        public float ExtraMaxHpFromMark;     // 印记带来的额外最大生命值（外卖猫30%自身最大生命值）


        public BattleUnit()
        {
            CritDamage = 1.5f;
            AbyssBaseDamage = 5f;
            AbyssMaxStacks = 3;

            HasRegularCustomerMark = false;
            RegularCustomerMarkTurns = 0;
            ExtraMaxHpFromMark = 0f;

            Defense = 0f;

            // 初始化小鸡Debuff
            ReviewStacks = 0;
            ReviewTurns = 0;
            ReviewDotDamage = 0f;
            ReviewCaster = null;
            IsHealBlocked = false;
            HealBlockTurns = 0;
            DefenseDownPercent = 0f;
            DefenseDownTurns = 0;
        }

        // 获取最终暴击率（基础+buff，保底0）
        public float GetFinalCritRate()
        {
            return Mathf.Max(0f, CritRate + ExtraCritRateBuff);
        }

        // 获取最终暴击伤害（基础+buff，保底100%）
        public float GetFinalCritDamage()
        {
            return Mathf.Max(1f, CritDamage + ExtraCritDamageBuff);
        }
    }

    // 队伍数据
    public List<BattleUnit> PlayerTeam = new List<BattleUnit>();
    public List<BattleUnit> EnemyTeam = new List<BattleUnit>();

    // 战斗回合状态
    public enum BattleState
    {
        Waiting,
        PlayerTurn,
        EnemyTurn,
        BattleEnd
    }
    public BattleState CurrentState = BattleState.Waiting;
    public int CurrentActingUnitIndex = 0;
    public BattleUnit CurrentActingUnit;

    // 敌人技能模板
    public enum EnemySkillType
    {
        NormalAttack,
        HeavyAttack,
        Heal,
        Harden,
        Curse
    }

    public struct EnemyData
    {
        public string Name;
        public string TexturePath;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public List<EnemySkillType> SkillPool;
    }
    public List<EnemyData> AllEnemies = new List<EnemyData>();
    public int CurrentEnemyIndex = 0;

    // 单例初始化
    public override void _Ready()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;
        SetProcessMode(ProcessModeEnum.Always);

        InitAllEnemyTemplates();
        InitPlayerTeam();
        InitEnemyTeam();
        StartPlayerTurn();
    }

    #region 数据初始化
    // 初始化玩家队伍+技能
    private void InitPlayerTeam()
    {
        PlayerTeam.Clear();

        // 1号：外卖猫
        var takeawayCat = new BattleUnit
        {
            UnitName = "外卖猫",
            TexturePath = "res://2dres/char/poor-kid1.png",
            Hp = 150f,
            HpMax = 150f,
            Attack = 5f,
            CritRate = 0.15f,
            CritDamage = 1.5f,
            IsPlayerUnit = true
        };
        takeawayCat.Skills.Add(new SkillData
        {
            SkillId = "cat_passive",
            SkillName = "回头客",
            Description = "外卖猫释放技能时会为目标施加熟客印记。持有熟客印记的友方角色将会提高【30%外卖猫自身最大生命值】的最大生命值，且损失生命值后，外卖猫还会额外为其回复【5%外卖猫自身最大生命值】的血量。熟客印记最多持续三个回合，期间再次叠加可刷新持续时间。",
            Type = SkillType.Passive
        });
        takeawayCat.Skills.Add(new SkillData
        {
            SkillId = "cat_normal",
            SkillName = "普通攻击",
            Description = "对目标造成【5%自身最大生命值】的伤害",
            Type = SkillType.Normal
        });
        takeawayCat.Skills.Add(new SkillData
        {
            SkillId = "cat_special",
            SkillName = "喵送外卖，请签收",
            Description = "为全体友方角色回复【10%外卖猫自身最大生命值】的血量。",
            Type = SkillType.Special
        });
        takeawayCat.Skills.Add(new SkillData
        {
            SkillId = "cat_ult",
            SkillName = "爆冲！喵手回春",
            Description = "为全体友方角色回复【30%外卖猫自身最大生命值】的血量，对敌人造成【50%本次总回复量】的伤害。",
            Type = SkillType.Ultimate
        });
        PlayerTeam.Add(takeawayCat);

        // 2号：小鸡
        var chicken = new BattleUnit
        {
            UnitName = "小鸡",
            TexturePath = "res://sucaibao/GameAssets/player.png",
            Hp = 150f,
            HpMax = 150f,
            Attack = 5f,
            CritRate = 0.08f,
            CritDamage = 1.5f,
            IsPlayerUnit = true
        };
        chicken.Skills.Add(new SkillData
        {
            SkillId = "chicken_passive",
            SkillName = "游戏锐评官",
            Description = "友方其他角色释放攻击并对命中敌人时，小鸡释放一次【默契攻击】，对敌人造成【2%自身最大生命值】的伤害。小鸡每次攻击都会使敌人陷入一层【锐评】，每层【锐评】都会使敌人受到的所有伤害提高5%，与此同时被视为持续伤害，在敌人回合开始时，对该敌人造成【1.5%自身最大生命值】的伤害。最多叠加十层，持续三个回合，叠加可额外刷新持续时间。",
            Type = SkillType.Passive
        });
        chicken.Skills.Add(new SkillData
        {
            SkillId = "chicken_normal",
            SkillName = "普通攻击",
            Description = "对目标造成5%自身最大生命值的伤害",
            Type = SkillType.Normal
        });
        chicken.Skills.Add(new SkillData
        {
            SkillId = "chicken_special",
            SkillName = "沉浸式体验？",
            Description = "对敌人造成【5%自身最大生命值】的伤害，额外施加两层【锐评】，并使该敌人在接下来的三回合内禁疗。",
            Type = SkillType.Special
        });
        chicken.Skills.Add(new SkillData
        {
            SkillId = "chicken_ult",
            SkillName = "轻松过关！",
            Description = "对敌人造成【30%自身最大生命值】的伤害，并使该敌人立即陷入十层【锐评】,同时额外在接下来的三回合内降低该敌人60%防御力。",
            Type = SkillType.Ultimate
        });
        PlayerTeam.Add(chicken);

        // 3号：邪恶兔
        var evilRabbit = new BattleUnit
        {
            UnitName = "邪恶兔",
            TexturePath = "res://2dres/char/1.png",
            Hp = 200f,
            HpMax = 200f,
            Attack = 10f,
            CritRate = 0.25f,
            CritDamage = 1.5f,
            IsPlayerUnit = true
        };
        // ✅ 被动描述更新：增加上限说明
        evilRabbit.Skills.Add(new SkillData
        {
            SkillId = "rabbit_passive",
            SkillName = "向死而生",
            Description = "每当自身当前生命值减少或增加1%，提高自身2%的暴击伤害与1%暴击率，持续3个回合。暴击率加成最多50%，暴击伤害加成最多100%，触发时刷新持续时间。",
            Type = SkillType.Passive
        });
        evilRabbit.Skills.Add(new SkillData
        {
            SkillId = "rabbit_normal",
            SkillName = "普通攻击",
            Description = "对目标造成【5%自身最大生命值】的伤害。",
            Type = SkillType.Normal
        });
        // 燃魂技能描述
        evilRabbit.Skills.Add(new SkillData
        {
            SkillId = "rabbit_special",
            SkillName = "燃魂",
            Description = "消耗全体友方角色【各10%该角色最大生命值】的血量（不会导致友方角色死亡，最多将生命值降低为1），随后对目标造成【50%本次消耗血量总和】伤害。",
            Type = SkillType.Special
        });
        evilRabbit.Skills.Add(new SkillData
        {
            SkillId = "rabbit_ult",
            SkillName = "血魇",
            Description = "对目标造成【20%自身最大生命值】的伤害，随后回复自身【20%最大生命值】的血量。",
            Type = SkillType.Ultimate
        });
        PlayerTeam.Add(evilRabbit);
    }

    // 初始化敌人队伍
    private void InitEnemyTeam()
    {
        EnemyTeam.Clear();
        CurrentEnemyIndex = 0;
        SpawnNextEnemy();
    }

    // 生成下一个敌人
    public void SpawnNextEnemy()
    {
        if (CurrentEnemyIndex >= AllEnemies.Count) return;
        var template = AllEnemies[CurrentEnemyIndex];

        var newEnemy = new BattleUnit
        {
            UnitName = template.Name,
            TexturePath = template.TexturePath,
            Hp = template.MaxHp,
            HpMax = template.MaxHp,
            Attack = template.Attack,
            Defense = template.Defense,
            IsPlayerUnit = false
        };

        EnemyTeam.Clear();
        EnemyTeam.Add(newEnemy);
        CurrentEnemyIndex++;

        if (Ui.Instance != null)
        {
            Ui.Instance.OnNewEnemySpawned(newEnemy);
        }
        GD.Print($"新敌人【{newEnemy.UnitName}】登场！");
    }

    // 敌人属性
    private void InitAllEnemyTemplates()
    {
        // 1. 骷髅蛮兵
        AllEnemies.Add(new EnemyData
        {
            Name = "骷髅蛮兵",
            TexturePath = "res://sucaibao/monsters/8.png",
            MaxHp = 300f,
            Attack = 25f,
            Defense = 50f,
            SkillPool = new List<EnemySkillType>
            { EnemySkillType.NormalAttack, EnemySkillType.NormalAttack, EnemySkillType.HeavyAttack, EnemySkillType.Heal }
        });

        // 2. 石化女妖
        AllEnemies.Add(new EnemyData
        {
            Name = "石化女妖",
            TexturePath = "res://sucaibao/monsters/3.png",
            MaxHp = 600f,
            Attack = 40f,
            Defense = 100f,
            SkillPool = new List<EnemySkillType>
            { EnemySkillType.NormalAttack, EnemySkillType.HeavyAttack, EnemySkillType.Harden, EnemySkillType.Curse }
        });

        // 3. 昔日先驱
        AllEnemies.Add(new EnemyData
        {
            Name = "昔日先驱",
            TexturePath = "res://sucaibao/monsters/11.png",
            MaxHp = 900f,
            Attack = 55f,
            Defense = 150f,
            SkillPool = new List<EnemySkillType>
            { EnemySkillType.NormalAttack, EnemySkillType.HeavyAttack, EnemySkillType.Heal, EnemySkillType.Harden }
        });
    }
    #endregion

    #region 战斗核心通用方法
    // ✅ 核心修改：受伤方法（修复防御减免bug + 锐评增伤）
    public void TakeDamage(BattleUnit target, float damage, bool isFriendlyBurnSoul = false)
    {
        if (target.IsDead) return;

        // 记录受伤前的生命值比例
        float beforeHpPercent = target.Hp / target.HpMax;

        // 1. 锐评增伤（每层5%）
        float damageAfterReview = damage;
        if (target.ReviewStacks > 0)
        {
            float reviewBonus = target.ReviewStacks * 0.05f;
            damageAfterReview = damage * (1 + reviewBonus);
            GD.Print($"【锐评增伤】{target.UnitName} 有{target.ReviewStacks}层锐评，增伤{reviewBonus:P0}，原始伤害:{damage:0.0} → 增伤后:{damageAfterReview:0.0}");
        }

        // 2. 防御减免（修复：使用防御后的伤害计算）
        float damageAfterDefense = damageAfterReview;
        if (target.GetEffectiveDefense() > 0)
        {
            float reduction = target.GetDamageReduction();
            damageAfterDefense = damageAfterReview * (1 - reduction);
            GD.Print($"【防御减免】{target.UnitName} 减免{(reduction * 100):0.00}%，原始伤害：{damageAfterReview:0.0} → 防御后：{damageAfterDefense:0.0}");
        }

        // 3. 硬化减伤
        float finalDamage = target.Hardened ? damageAfterDefense / 2 : damageAfterDefense;
        float newHp = target.Hp - finalDamage;

        // 燃魂友方保护：最低1血
        if (isFriendlyBurnSoul && target.IsPlayerUnit)
        {
            newHp = Mathf.Max(1f, newHp);
            GD.Print($"【燃魂保护】{target.UnitName}血量最低保留1点，扣血后血量：{newHp:0.0}");
        }
        else
        {
            newHp = Mathf.Max(0f, newHp);
        }

        target.Hp = newHp;

        // 熟客印记回复（目标有印记时，扣血后回复5%外卖猫最大生命值）
        if (target.HasRegularCustomerMark)
        {
            // 找到外卖猫
            var takeawayCat = PlayerTeam.Find(u => u.UnitName == "外卖猫" && !u.IsDead);
            if (takeawayCat != null)
            {
                float healValue = takeawayCat.HpMax * 0.05f;
                Heal(target, healValue);
                GD.Print($"【熟客印记】{target.UnitName}损失血量后，回复{healValue:0}点血量（5%外卖猫最大生命值）");
            }
        }

        // 触发邪恶兔被动
        if (target.IsPlayerUnit && target.UnitName == "邪恶兔")
        {
            TriggerDeadLivePassive(target, beforeHpPercent, target.Hp / target.HpMax);
        }

        // 触发血条动画
        if (target.IsPlayerUnit)
        {
            // 玩家单位，找对应Player节点更新血条
            var playerNode = target.BindNode as Player;
            playerNode?.UpdateHp();
        }
        else
        {
            // 敌人单位，找Enemy节点更新血条
            var enemyNode = target.BindNode as Enemy;
            enemyNode?.UpdateHp();
        }

        GD.Print($"{target.UnitName}受到{finalDamage:0.0}点伤害，剩余血量：{target.Hp}/{target.HpMax}");
    }

    // ✅ 核心修改：治疗方法（禁疗检查）
    public void Heal(BattleUnit target, float healValue)
    {
        if (target.IsDead) return;

        // 禁疗检查
        if (target.IsHealBlocked)
        {
            GD.Print($"【禁疗】{target.UnitName} 被禁疗，无法回复！");
            return;
        }

        // 记录治疗前的生命值比例
        float beforeHpPercent = target.Hp / target.HpMax;

        // 结算治疗
        target.Hp = Mathf.Min(target.HpMax, target.Hp + healValue);

        // 触发邪恶兔被动
        if (target.IsPlayerUnit && target.UnitName == "邪恶兔")
        {
            TriggerDeadLivePassive(target, beforeHpPercent, target.Hp / target.HpMax);
        }

        GD.Print($"{target.UnitName} 回复了 {healValue:0.0} 点血量");
    }

    // ✅ 新增：施加锐评Debuff
    public void ApplyReview(BattleUnit caster, BattleUnit target, int stacks)
    {
        if (target == null || target.IsDead || caster == null) return;

        target.ReviewStacks = Mathf.Min(target.ReviewStacks + stacks, 10);
        target.ReviewTurns = 3; // 刷新持续时间
        target.ReviewDotDamage = caster.HpMax * 0.015f; // 1.5%小鸡最大生命值
        target.ReviewCaster = caster;

        GD.Print($"【锐评】{target.UnitName} 获得 {stacks} 层锐评，当前层数：{target.ReviewStacks}/10，持续3回合");
    }

    // ✅ 新增：施加禁疗Debuff
    public void ApplyHealBlock(BattleUnit target, int turns)
    {
        if (target == null || target.IsDead) return;
        target.IsHealBlocked = true;
        target.HealBlockTurns = turns;
        GD.Print($"【禁疗】{target.UnitName} 被禁疗 {turns} 回合");
    }

    // 施加防御降低Debuff
    public void ApplyDefenseDown(BattleUnit target, float percent, int turns)
    {
        if (target == null || target.IsDead) return;
        target.DefenseDownPercent = percent;
        target.DefenseDownTurns = turns;
        GD.Print($"【防御降低】{target.UnitName} 防御降低 {percent:P0}，持续 {turns} 回合");
    }

    // 小鸡默契攻击（不占回合）
    public async void TriggerCoopAttack(BattleUnit target)
    {
        var chicken = PlayerTeam.Find(u => u.UnitName == "小鸡" && !u.IsDead);
        if (chicken == null || target == null || target.IsDead) return;

        GD.Print($"【默契攻击】小鸡触发！");
        await ToSignal(GetTree().CreateTimer(0.15f), "timeout");

        // 计算伤害：2%小鸡最大生命值
        float baseDamage = chicken.HpMax * 0.02f;
        float finalDamage = baseDamage;
        bool isCrit = false;

        // 暴击判定
        float critRate = chicken.GetFinalCritRate();
        float roll = (float)GlobalRandom.NextDouble();
        if (roll <= critRate)
        {
            isCrit = true;
            finalDamage *= chicken.GetFinalCritDamage();
        }

        // 硬化减伤
        if (target.Hardened) finalDamage /= 2;

        // 结算伤害
        TakeDamage(target, finalDamage);

        // 施加1层锐评
        ApplyReview(chicken, target, 1);

        // UI提示
        if (Ui.Instance != null)
        {
            string tip = $"🐔 小鸡【默契攻击】！\n对 {target.UnitName} 造成 {finalDamage:0.0} 点伤害！";
            if (isCrit) tip = "💥 暴击！！" + tip;
            Ui.Instance.SetBattleTip(tip);
        }

        CheckBattleEnd();
    }

    // 向死而生被动逻辑：新增暴击率/暴击伤害上限
    private void TriggerDeadLivePassive(BattleUnit rabbit, float beforePercent, float afterPercent)
    {
        float hpChangePercent = Mathf.Abs(afterPercent - beforePercent) * 100;
        if (hpChangePercent < 0.01f) return;

        // 每1%变化：+2%暴击伤害、+1%暴击率
        float addCritDmg = hpChangePercent * 0.02f;
        float addCritRate = hpChangePercent * 0.01f;

        rabbit.ExtraCritDamageBuff += addCritDmg;
        rabbit.ExtraCritRateBuff += addCritRate;
        rabbit.DeadLiveBuffTurns = 3;

        // ✅ 硬上限：暴击率加成 ≤ 50%（0.5），暴击伤害加成 ≤ 100%（1.0）
        rabbit.ExtraCritRateBuff = Mathf.Min(rabbit.ExtraCritRateBuff, 0.5f);
        rabbit.ExtraCritDamageBuff = Mathf.Min(rabbit.ExtraCritDamageBuff, 1.0f);

        GD.Print($"【向死而生触发】生命值变化{hpChangePercent:0.00}%");
        GD.Print($"新增加成：暴击率+{addCritRate * 100:0.00}%，暴击伤害+{addCritDmg * 100:0.00}%");
        GD.Print($"当前总加成：暴击率{rabbit.ExtraCritRateBuff * 100:0.00}% / 上限50%，暴击伤害{rabbit.ExtraCritDamageBuff * 100:0.00}% / 上限100%");
    }

    // 应用熟客印记（外卖猫被动）
    public void ApplyRegularCustomerMark(BattleUnit caster, BattleUnit target)
    {
        if (caster.UnitName != "外卖猫" || target == null || target.IsDead) return;

        // 计算额外最大生命值（30%外卖猫自身最大生命值）
        float extraMaxHp = caster.HpMax * 0.3f;

        // 首次添加印记：增加最大生命值
        if (!target.HasRegularCustomerMark)
        {
            target.ExtraMaxHpFromMark = extraMaxHp;
            target.HpMax += extraMaxHp;
            // 同步当前血量（避免最大生命值增加后血量比例异常）
            target.Hp = Mathf.Min(target.Hp + extraMaxHp, target.HpMax);
            GD.Print($"【熟客印记】{target.UnitName}获得{extraMaxHp:0}点额外最大生命值，当前最大生命值：{target.HpMax:0}");
        }

        // 刷新印记持续时间（最多3回合）
        target.HasRegularCustomerMark = true;
        target.RegularCustomerMarkTurns = 3;
        GD.Print($"【熟客印记】{target.UnitName}的印记持续时间刷新为3回合");

        // 被动效果：损失生命值后回复5%外卖猫最大生命值
        // （注：这里在添加印记时直接触发一次回复，后续血量变化时也会触发）
        float healValue = caster.HpMax * 0.05f;
        Heal(target, healValue);
        GD.Print($"【熟客印记】{target.UnitName}回复{healValue:0}点血量（5%外卖猫最大生命值）");
    }

    // 获取存活的玩家列表
    public List<BattleUnit> GetAlivePlayers()
    {
        var alive = new List<BattleUnit>();
        foreach (var unit in PlayerTeam) if (!unit.IsDead) alive.Add(unit);
        return alive;
    }

    // 获取存活的敌人列表
    public List<BattleUnit> GetAliveEnemies()
    {
        var alive = new List<BattleUnit>();
        foreach (var unit in EnemyTeam) if (!unit.IsDead) alive.Add(unit);
        return alive;
    }

    // 战斗结束判断
    public bool CheckBattleEnd()
    {
        bool allPlayerDead = GetAlivePlayers().Count == 0;
        bool currentEnemyDead = GetAliveEnemies().Count == 0;

        if (allPlayerDead)
        {
            CurrentState = BattleState.BattleEnd;
            return true;
        }

        if (currentEnemyDead)
        {
            if (CurrentEnemyIndex < AllEnemies.Count)
            {
                SpawnNextEnemy();
                return false;
            }
            else
            {
                CurrentState = BattleState.BattleEnd;
                return true;
            }
        }

        return false;
    }

    // 回合结束状态处理（新增小鸡Debuff回合管理）
    public void OnRoundEndProcessStatus()
    {
        // 处理所有玩家的状态
        foreach (var unit in PlayerTeam)
        {
            if (unit.IsDead) continue;

            // 处理邪恶兔的被动buff
            if (unit.UnitName == "邪恶兔")
            {
                if (unit.DeadLiveBuffTurns > 0)
                {
                    unit.DeadLiveBuffTurns--;
                    if (unit.DeadLiveBuffTurns <= 0)
                    {
                        unit.ExtraCritDamageBuff = 0f;
                        unit.ExtraCritRateBuff = 0f;
                        GD.Print("【向死而生】buff已过期，所有加成清零");
                    }
                    else
                    {
                        GD.Print($"【向死而生】buff剩余回合：{unit.DeadLiveBuffTurns}，当前加成：暴击率{unit.GetFinalCritRate() * 100:0.00}%，暴击伤害{unit.GetFinalCritDamage() * 100:0.00}%");
                    }
                }
            }

            // 熟客印记回合递减
            if (unit.HasRegularCustomerMark)
            {
                unit.RegularCustomerMarkTurns--;
                if (unit.RegularCustomerMarkTurns <= 0)
                {
                    // 移除印记：恢复原有最大生命值
                    unit.HpMax -= unit.ExtraMaxHpFromMark;
                    unit.Hp = Mathf.Min(unit.Hp, unit.HpMax); // 防止血量超过新的最大生命值
                    unit.HasRegularCustomerMark = false;
                    unit.ExtraMaxHpFromMark = 0f;
                    GD.Print($"【熟客印记】{unit.UnitName}的印记过期，最大生命值恢复为：{unit.HpMax:0}");
                }
                else
                {
                    GD.Print($"【熟客印记】{unit.UnitName}的印记剩余回合：{unit.RegularCustomerMarkTurns}");
                }
            }

            // 其他通用状态
            if (unit.HardenedTurns > 0) { unit.HardenedTurns--; if (unit.HardenedTurns <= 0) unit.Hardened = false; }
            if (unit.CursedTurns > 0) { unit.CursedTurns--; if (unit.CursedTurns <= 0) unit.Cursed = false; }
        }

        // 处理敌人状态
        foreach (var unit in EnemyTeam)
        {
            if (unit.IsDead) continue;

            // ✅ 新增：锐评持续回合
            if (unit.ReviewTurns > 0)
            {
                unit.ReviewTurns--;
                if (unit.ReviewTurns <= 0)
                {
                    unit.ReviewStacks = 0;
                    unit.ReviewDotDamage = 0f;
                    unit.ReviewCaster = null;
                    GD.Print($"【锐评过期】{unit.UnitName} 的锐评层数清零");
                }
            }

            // ✅ 新增：禁疗持续回合
            if (unit.HealBlockTurns > 0)
            {
                unit.HealBlockTurns--;
                if (unit.HealBlockTurns <= 0)
                {
                    unit.IsHealBlocked = false;
                    GD.Print($"【禁疗过期】{unit.UnitName} 的禁疗结束");
                }
            }

            // ✅ 新增：防御降低持续回合
            if (unit.DefenseDownTurns > 0)
            {
                unit.DefenseDownTurns--;
                if (unit.DefenseDownTurns <= 0)
                {
                    unit.DefenseDownPercent = 0f;
                    GD.Print($"【防御降低过期】{unit.UnitName} 的防御恢复正常");
                }
            }

            // 原有的深渊状态
            if (unit.AbyssTurns > 0)
            {
                TakeDamage(unit, unit.AbyssBaseDamage * unit.AbyssStacks);
                unit.AbyssTurns--;
                if (unit.AbyssTurns <= 0) unit.AbyssStacks = 0;
            }
            if (unit.HardenedTurns > 0) { unit.HardenedTurns--; if (unit.HardenedTurns <= 0) unit.Hardened = false; }
        }
    }
    #endregion

    #region 回合流转逻辑
    // 开始玩家大回合
    public void StartPlayerTurn()
    {
        CurrentState = BattleState.PlayerTurn;
        CurrentActingUnitIndex = 0;
        FindNextAlivePlayer();
    }

    // 找下一个存活的玩家
    private void FindNextAlivePlayer()
    {
        while (CurrentActingUnitIndex < PlayerTeam.Count)
        {
            var unit = PlayerTeam[CurrentActingUnitIndex];
            if (!unit.IsDead)
            {
                CurrentActingUnit = unit;
                Ui.Instance?.OnSinglePlayerTurnStart(unit);
                return;
            }
            CurrentActingUnitIndex++;
        }

        // 所有玩家行动完，进入敌人回合
        StartEnemyTurn();
    }

    // 单个玩家行动结束
    public void OnSinglePlayerActionEnd()
    {
        CurrentActingUnitIndex++;
        FindNextAlivePlayer();
    }

    // 敌人回合开始（先结算锐评DOT）
    public void StartEnemyTurn()
    {
        CurrentState = BattleState.EnemyTurn;
        CurrentActingUnitIndex = 0;

        //敌人回合开始时，结算锐评持续伤害
        foreach (var enemy in EnemyTeam)
        {
            if (enemy.IsDead || enemy.ReviewStacks <= 0 || enemy.ReviewCaster == null) continue;

            float dotDamage = enemy.ReviewDotDamage * enemy.ReviewStacks;
            GD.Print($"【锐评DOT】{enemy.UnitName} 受到 {enemy.ReviewStacks} 层锐评伤害，共 {dotDamage:0.0}");
            TakeDamage(enemy, dotDamage);
        }

        if (CheckBattleEnd())
        {
            // 如果敌人被DOT打死，直接结束战斗/刷新boss，不执行后续敌人AI
            return;
        }

        FindNextAliveEnemy();
    }

    // 找下一个存活的敌人
    private void FindNextAliveEnemy()
    {
        while (CurrentActingUnitIndex < EnemyTeam.Count)
        {
            var unit = EnemyTeam[CurrentActingUnitIndex];
            if (!unit.IsDead)
            {
                CurrentActingUnit = unit;
                Ui.Instance?.StartEnemyAI(unit);
                return;
            }
            CurrentActingUnitIndex++;
        }

        if (CheckBattleEnd())
        {
            return;
        }

        // 所有敌人行动完，回到玩家回合
        OnRoundEndProcessStatus();
        StartPlayerTurn();
    }

    // 单个敌人行动结束
    public void OnSingleEnemyActionEnd()
    {
        CurrentActingUnitIndex++;
        FindNextAliveEnemy();
    }
    #endregion
}
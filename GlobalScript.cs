using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class GlobalScript : Node
{
    private const string EvilRabbitJsonPath = "res://character_data/evil_rabbit.json";
    private const string TakeawayCatJsonPath = "res://character_data/takeaway_cat.json";
    private const string ChickenJsonPath = "res://character_data/chicken.json";
    private const string AppleQueenJsonPath = "res://character_data/apple_queen.json";

    private static readonly JsonSerializerOptions CharacterJsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

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
        public int EnergyCost;
    }

    public class SelectedCharacterData
    {
        public string CharacterName;
        public string TexturePath;

        public SelectedCharacterData()
        {
        }

        public SelectedCharacterData(string characterName, string texturePath)
        {
            CharacterName = characterName;
            TexturePath = texturePath;
        }
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
        public int CurrentEnergy { get; set; }
        public int MaxEnergy { get; set; }
        public List<DamageOverTime> DamageOverTimeEffects = new List<DamageOverTime>();
        public List<SynergyAttack> SynergyAttacks = new List<SynergyAttack>();

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
            CurrentEnergy = 0;
            MaxEnergy = 0;

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
    public List<SelectedCharacterData> SelectedTeam = new List<SelectedCharacterData>();

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

        ResetBattleFromSelectedTeam();
    }

    #region 数据初始化
    public BattleUnit LoadCharacterFromJson(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            GD.PrintErr("角色加载失败：jsonPath 为空。");
            return null;
        }

        if (!Godot.FileAccess.FileExists(jsonPath))
        {
            GD.PrintErr($"角色配置文件不存在：{jsonPath}");
            return null;
        }

        try
        {
            using var file = Godot.FileAccess.Open(jsonPath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"角色配置文件打开失败：{jsonPath}，错误码：{Godot.FileAccess.GetOpenError()}");
                return null;
            }

            string jsonText = file.GetAsText();
            var config = JsonSerializer.Deserialize<CharacterConfig>(jsonText, CharacterJsonOptions);
            if (config == null)
            {
                GD.PrintErr($"角色配置解析结果为空：{jsonPath}");
                return null;
            }

            var unit = new BattleUnit
            {
                UnitName = config.UnitName,
                TexturePath = config.TexturePath,
                Hp = config.Hp,
                HpMax = config.HpMax,
                Attack = config.Attack,
                CritRate = config.CritRate,
                CritDamage = config.CritDamage,
                IsPlayerUnit = config.IsPlayerUnit,
                MaxEnergy = Mathf.Max(0, config.MaxEnergy),
                CurrentEnergy = 0
            };

            if (config.Skills != null)
            {
                foreach (var skillConfig in config.Skills)
                {
                    if (skillConfig == null)
                    {
                        continue;
                    }

                    if (!Enum.TryParse(skillConfig.Type, true, out SkillType skillType))
                    {
                        GD.PrintErr($"角色技能类型解析失败：{jsonPath} -> {skillConfig.SkillId} / {skillConfig.Type}");
                        continue;
                    }

                    unit.Skills.Add(new SkillData
                    {
                        SkillId = skillConfig.SkillId,
                        SkillName = skillConfig.SkillName,
                        Description = skillConfig.Description,
                        Type = skillType,
                        EnergyCost = skillConfig.EnergyCost
                    });
                }
            }

            return unit;
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"角色配置 JSON 解析失败：{jsonPath}，异常：{ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"角色配置加载异常：{jsonPath}，异常：{ex}");
            return null;
        }
    }

    // 初始化玩家队伍+技能
    private void InitPlayerTeam()
    {
        PlayerTeam.Clear();

        string[] characterJsonPaths =
        {
            EvilRabbitJsonPath,
            TakeawayCatJsonPath,
            ChickenJsonPath,
            AppleQueenJsonPath
        };

        foreach (string jsonPath in characterJsonPaths)
        {
            var battleUnit = LoadCharacterFromJson(jsonPath);
            if (battleUnit == null)
            {
                continue;
            }

            PlayerTeam.Add(battleUnit);
        }
    }

    public void EnsureDefaultSelectedTeam()
    {
        if (SelectedTeam.Count == 3)
        {
            return;
        }

        SelectedTeam.Clear();
        SelectedTeam.Add(new SelectedCharacterData("外卖猫", "res://character_picture/deliverycat.png"));
        SelectedTeam.Add(new SelectedCharacterData("邪恶兔", "res://character_picture/evilrabbit.png"));
        SelectedTeam.Add(new SelectedCharacterData("小鸡", "res://character_picture/chicken.png"));
    }

    public void ResetBattleFromSelectedTeam()
    {
        if (AllEnemies.Count == 0)
        {
            InitAllEnemyTemplates();
        }

        InitPlayerTeam();
        EnsureDefaultSelectedTeam();
        ApplySelectedTeamToPlayerTeam();
        InitializeReusableStatusSystems();
        InitEnemyTeam();

        CurrentState = BattleState.Waiting;
        CurrentActingUnitIndex = 0;
        CurrentActingUnit = null;
        StartPlayerTurn();
    }

    private void ApplySelectedTeamToPlayerTeam()
    {
        if (SelectedTeam.Count != 3 || PlayerTeam.Count == 0)
        {
            return;
        }

        var templates = new Dictionary<string, BattleUnit>();
        var orderedTeam = new List<BattleUnit>();

        foreach (var template in PlayerTeam)
        {
            if (!templates.ContainsKey(template.UnitName))
            {
                templates.Add(template.UnitName, template);
            }
        }

        foreach (var selectedCharacter in SelectedTeam)
        {
            if (!templates.TryGetValue(selectedCharacter.CharacterName, out var templateUnit))
            {
                GD.PrintErr($"未找到已加载的角色模板：{selectedCharacter.CharacterName}");
                continue;
            }

            orderedTeam.Add(CloneBattleUnit(templateUnit, selectedCharacter.TexturePath));
        }

        if (orderedTeam.Count == 3)
        {
            PlayerTeam = orderedTeam;
        }
    }

    private void InitializeReusableStatusSystems()
    {
        foreach (var unit in PlayerTeam)
        {
            if (unit == null)
            {
                continue;
            }

            unit.DamageOverTimeEffects = new List<DamageOverTime>();
            unit.SynergyAttacks = new List<SynergyAttack>();
            unit.ReviewStacks = 0;
            unit.ReviewTurns = 0;
            unit.ReviewDotDamage = 0f;
            unit.ReviewCaster = null;
        }

        var chicken = PlayerTeam.Find(unit => unit != null && unit.UnitName == "小鸡");
        if (chicken != null)
        {
            SynergyGlobalManager.RegisterSynergyAttack(chicken, SynergyGlobalManager.CreateChickenDefaultSynergy());
        }
    }

    private BattleUnit CloneBattleUnit(BattleUnit sourceUnit, string texturePath)
    {
        var cloneUnit = new BattleUnit
        {
            UnitName = sourceUnit.UnitName,
            TexturePath = string.IsNullOrEmpty(texturePath) ? sourceUnit.TexturePath : texturePath,
            Hp = sourceUnit.Hp,
            HpMax = sourceUnit.HpMax,
            Attack = sourceUnit.Attack,
            CritRate = sourceUnit.CritRate,
            CritDamage = sourceUnit.CritDamage,
            Defense = sourceUnit.Defense,
            IsPlayerUnit = sourceUnit.IsPlayerUnit,
            MaxEnergy = Mathf.Max(0, sourceUnit.MaxEnergy),
            CurrentEnergy = Mathf.Clamp(sourceUnit.CurrentEnergy, 0, Mathf.Max(0, sourceUnit.MaxEnergy))
        };

        foreach (var skill in sourceUnit.Skills)
        {
            cloneUnit.Skills.Add(new SkillData
            {
                SkillId = skill.SkillId,
                SkillName = skill.SkillName,
                Description = skill.Description,
                Type = skill.Type,
                EnergyCost = skill.EnergyCost
            });
        }

        return cloneUnit;
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

        // 1. 全局持续伤害增伤（如小鸡锐评）
        float damageAfterReview = damage;
        float dotDamageBonus = DotGlobalManager.GetDamageTakenBonus(target);
        if (dotDamageBonus > 0f)
        {
            damageAfterReview = damage * (1 + dotDamageBonus);
            GD.Print($"【持续伤害增伤】{target.UnitName} 当前增伤{dotDamageBonus:P0}，原始伤害:{damage:0.0} → 增伤后:{damageAfterReview:0.0}");
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

    public bool CanCastUltimate(BattleUnit caster)
    {
        if (caster == null)
        {
            GD.PrintErr("能量校验失败：施法者为空。");
            return false;
        }

        int maxEnergy = Mathf.Max(0, caster.MaxEnergy);
        int currentEnergy = Mathf.Clamp(caster.CurrentEnergy, 0, maxEnergy);
        caster.CurrentEnergy = currentEnergy;
        caster.MaxEnergy = maxEnergy;
        return currentEnergy >= maxEnergy;
    }

    public void ApplySkillEnergy(BattleUnit caster, SkillData skill)
    {
        if (caster == null || skill == null)
        {
            GD.PrintErr("能量结算失败：角色或技能为空。");
            return;
        }

        int maxEnergy = Mathf.Max(0, caster.MaxEnergy);
        int beforeEnergy = Mathf.Clamp(caster.CurrentEnergy, 0, maxEnergy);
        int afterEnergy = Mathf.Clamp(beforeEnergy - skill.EnergyCost, 0, maxEnergy);

        caster.MaxEnergy = maxEnergy;
        caster.CurrentEnergy = afterEnergy;

        if (skill.Type == SkillType.Ultimate)
        {
            caster.CurrentEnergy = 0;
            GD.Print($"【能量】{caster.UnitName} 释放大招后清空能量：{beforeEnergy}/{maxEnergy} -> 0/{maxEnergy}");
            return;
        }

        GD.Print($"【能量】{caster.UnitName} 使用 {skill.SkillName} 后能量变化：{beforeEnergy}/{maxEnergy} -> {caster.CurrentEnergy}/{maxEnergy}（EnergyCost={skill.EnergyCost}）");
    }

    public void AddEnergy(BattleUnit unit, int amount, string source)
    {
        if (unit == null)
        {
            GD.PrintErr($"能量增加失败：来源 {source} 的目标角色为空。");
            return;
        }

        int maxEnergy = Mathf.Max(0, unit.MaxEnergy);
        int beforeEnergy = Mathf.Clamp(unit.CurrentEnergy, 0, maxEnergy);
        int afterEnergy = Mathf.Clamp(beforeEnergy + amount, 0, maxEnergy);

        unit.MaxEnergy = maxEnergy;
        unit.CurrentEnergy = afterEnergy;

        GD.Print($"【能量】{unit.UnitName} 因 {source} 获得能量：{beforeEnergy}/{maxEnergy} -> {afterEnergy}/{maxEnergy}");
    }

    // ✅ 新增：施加锐评Debuff
    public void ApplyReview(BattleUnit caster, BattleUnit target, int stacks)
    {
        if (target == null || target.IsDead || caster == null) return;

        DotGlobalManager.ApplyDot(target, DotGlobalManager.CreateReviewDot(caster, stacks));
        var reviewDot = DotGlobalManager.GetDot(target, DotGlobalManager.ReviewStatusId);
        if (reviewDot != null)
        {
            target.ReviewStacks = reviewDot.StackCount;
            target.ReviewTurns = reviewDot.RemainingTurns;
            target.ReviewDotDamage = reviewDot.SnapshotValue * reviewDot.DamageMultiplier;
            target.ReviewCaster = caster;
            GD.Print($"【锐评】{target.UnitName} 获得 {stacks} 层锐评，当前层数：{reviewDot.StackCount}/10，持续3回合");
        }
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

    public void ApplyAppleQueenGuHen(BattleUnit caster, BattleUnit target)
    {
        if (caster == null || target == null || target.IsDead)
        {
            GD.PrintErr("苹果大王被动施加失败：施法者或目标无效。");
            return;
        }

        DotGlobalManager.ApplyDot(target, DotGlobalManager.CreateGuHenDot(caster));
    }

    public List<BattleUnit> GetAdjacentEnemies(BattleUnit centerTarget)
    {
        var adjacentEnemies = new List<BattleUnit>();
        if (centerTarget == null)
        {
            return adjacentEnemies;
        }

        int targetIndex = EnemyTeam.IndexOf(centerTarget);
        if (targetIndex < 0)
        {
            return adjacentEnemies;
        }

        if (targetIndex - 1 >= 0)
        {
            var left = EnemyTeam[targetIndex - 1];
            if (left != null && !left.IsDead)
            {
                adjacentEnemies.Add(left);
            }
        }

        if (targetIndex + 1 < EnemyTeam.Count)
        {
            var right = EnemyTeam[targetIndex + 1];
            if (right != null && !right.IsDead)
            {
                adjacentEnemies.Add(right);
            }
        }

        return adjacentEnemies;
    }

    // 小鸡默契攻击（不占回合）
    public void TriggerCoopAttack(BattleUnit target)
    {
        SynergyGlobalManager.TriggerOnAllyAttackHit(this, CurrentActingUnit, target);
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

    private bool ResolveTurnStartStatuses(BattleUnit unit)
    {
        if (unit == null || unit.IsDead)
        {
            return false;
        }

        DotGlobalManager.ResolveTurnStartDots(this, unit);

        var reviewDot = DotGlobalManager.GetDot(unit, DotGlobalManager.ReviewStatusId);
        unit.ReviewStacks = reviewDot?.StackCount ?? 0;
        unit.ReviewTurns = reviewDot?.RemainingTurns ?? 0;
        unit.ReviewDotDamage = reviewDot == null ? 0f : reviewDot.SnapshotValue * reviewDot.DamageMultiplier;
        unit.ReviewCaster = reviewDot?.SourceUnit;

        if (unit.IsDead)
        {
            GD.Print($"【回合开始】{unit.UnitName} 因持续伤害倒下");
            return true;
        }

        return false;
    }

    // 找下一个存活的玩家
    private void FindNextAlivePlayer()
    {
        while (CurrentActingUnitIndex < PlayerTeam.Count)
        {
            var unit = PlayerTeam[CurrentActingUnitIndex];
            if (!unit.IsDead)
            {
                if (ResolveTurnStartStatuses(unit))
                {
                    if (CheckBattleEnd())
                    {
                        return;
                    }

                    CurrentActingUnitIndex++;
                    continue;
                }

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
                if (ResolveTurnStartStatuses(unit))
                {
                    if (CheckBattleEnd())
                    {
                        return;
                    }

                    CurrentActingUnitIndex++;
                    continue;
                }

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

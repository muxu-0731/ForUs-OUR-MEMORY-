using Godot;
using System;
using System.Collections.Generic;

public partial class GlobalScript : Node
{
    public const int MaxPartySize = 4;
    public const int InvalidPartySlotIndex = -1;

    private const string EvilRabbitJsonPath = "res://character_data/evil_rabbit.json";
    private const string TakeawayCatJsonPath = "res://character_data/takeaway_cat.json";
    private const string ChickenJsonPath = "res://character_data/chicken.json";
    private const string AppleQueenJsonPath = "res://character_data/apple_queen.json";
    private const string MiaminJsonPath = "res://character_data/miamin.json";
    private const string ChaosFateDiceJsonPath = "res://character_data/chaos_fate_dice.json";
    private static readonly string[] DefaultSelectedTeamJsonPaths =
    {
        AppleQueenJsonPath,
        ChickenJsonPath,
        EvilRabbitJsonPath,
        TakeawayCatJsonPath
    };

    public const string ChaosFateDiceUnitName = "无序命骰";
    private const int ChaosOrderValueMax = 6;
    private const float ChaosOrderCritRatePerStack = 0.05f;
    private const float ChaosOrderCritDamagePerStack = 0.08f;
    private const float ChaosTeamCritDamagePerOrder = 0.10f;
    private const float ChaosTeamCritDamageCap = 1.0f;
    private const int ChaosTeamCritDamageBuffDuration = 3;
    private const float TurnStartDotPreResolveDelaySeconds = 1.0f;
    private const float TurnStartDotResolveDisplaySeconds = 0.5f;
    private const float TurnStartDotPostResolveDelaySeconds = 0.5f;

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
        Ultimate,
        EnhancedSpecial
    }

    // 技能数据结构
    public class SkillData
    {
        public string SkillId;
        public string SkillName;
        public string Description;
        public SkillType Type;
        public int EnergyCost;
        public string EnhancedSkillId;
        public bool IsSelectable = true;
        public bool ShowInBattleUi = true;
        public string TriggeredBySkillId;
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
        public enum PlayerSlotType
        {
            FormalPartyMember,
            Summon
        }

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
        public int ChaosOrderValue = 0;
        public float ChaosTeamCritDamageBuff = 0f;
        public int ChaosTeamCritDamageBuffTurns = 0;

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
        public Vector2 WorldPosition;
        public Vector2 AnchorWorldPosition;
        public PlayerSlotType SlotType = PlayerSlotType.FormalPartyMember;
        public int PartySlotIndex = InvalidPartySlotIndex;
        public int OwnerPartySlotIndex = InvalidPartySlotIndex;
        public bool IsDead => Hp <= 0;
        public bool IsSummon => SlotType == PlayerSlotType.Summon;

        public int ResolvePlayerTrackIndex()
        {
            return IsSummon ? OwnerPartySlotIndex : PartySlotIndex;
        }

        public bool HasRegularCustomerMark; // 是否持有熟客印记
        public int RegularCustomerMarkTurns; // 印记剩余持续回合
        public float ExtraMaxHpFromMark;     // 印记带来的额外最大生命值（外卖猫30%自身最大生命值）

        public bool MiaminFatalProtectionUsed; // 迈阿密被动：每局最多触发一次
        public bool MiaminNoMeatballState;     // 迈阿密被动：【没丸呢】锁血状态


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
            MiaminFatalProtectionUsed = false;
            MiaminNoMeatballState = false;
        }

        // 获取最终暴击率（基础+buff，保底0）
        public float GetFinalCritRate()
        {
            return Mathf.Max(0f, CritRate + ExtraCritRateBuff + ChaosOrderValue * ChaosOrderCritRatePerStack);
        }

        // 获取最终暴击伤害（基础+buff，保底100%）
        public float GetFinalCritDamage()
        {
            return Mathf.Max(1f, CritDamage + ExtraCritDamageBuff + ChaosTeamCritDamageBuff + ChaosOrderValue * ChaosOrderCritDamagePerStack);
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
        TurnStartStatusResolve,
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
    private TurnIndicatorEffect _currentTurnIndicator;
    private ulong _turnIndicatorRequestId = 0;

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

        try
        {
            CharacterConfig config = CharacterDataRepository.LoadCharacterConfig(jsonPath, out string resolvedJsonPath);
            if (config == null)
            {
                GD.PrintErr($"角色配置解析结果为空：{jsonPath}");
                return null;
            }

            GD.Print($"GlobalScript: creating battle unit from json='{resolvedJsonPath}', TexturePath='{config.TexturePath}', exported={CharacterDataRepository.IsExportedRuntime}");

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
                        GD.PrintErr($"角色技能类型解析失败：{resolvedJsonPath} -> {skillConfig.SkillId} / {skillConfig.Type}");
                        continue;
                    }

                    unit.Skills.Add(new SkillData
                    {
                        SkillId = skillConfig.SkillId,
                        SkillName = skillConfig.SkillName,
                        Description = skillConfig.Description,
                        Type = skillType,
                        EnergyCost = skillConfig.EnergyCost,
                        EnhancedSkillId = skillConfig.EnhancedSkillId,
                        IsSelectable = skillConfig.IsSelectable,
                        ShowInBattleUi = skillConfig.ShowInBattleUi,
                        TriggeredBySkillId = skillConfig.TriggeredBySkillId
                    });
                }
            }

            return unit;
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

        var characterJsonPaths = CharacterDataRepository.LoadPlayerCharacterJsonPaths();
        if (characterJsonPaths.Count == 0)
        {
            characterJsonPaths = new List<string>
            {
                EvilRabbitJsonPath,
                TakeawayCatJsonPath,
                ChickenJsonPath,
                AppleQueenJsonPath,
                MiaminJsonPath,
                ChaosFateDiceJsonPath
            };
        }

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
        if (SelectedTeam.Count == MaxPartySize)
        {
            return;
        }

        BuildDefaultSelectedTeam();
    }

    private void BuildDefaultSelectedTeam()
    {
        SelectedTeam.Clear();

        foreach (string jsonPath in DefaultSelectedTeamJsonPaths)
        {
            var unit = LoadCharacterFromJson(jsonPath);
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitName) || string.IsNullOrWhiteSpace(unit.TexturePath))
            {
                continue;
            }

            if (SelectedTeam.Exists(character => character.CharacterName == unit.UnitName))
            {
                continue;
            }

            SelectedTeam.Add(new SelectedCharacterData(unit.UnitName, unit.TexturePath));
            if (SelectedTeam.Count >= MaxPartySize)
            {
                break;
            }
        }
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
        UpdateTurnIndicator(null);
        StartPlayerTurn();
    }

    private void ApplySelectedTeamToPlayerTeam()
    {
        if (PlayerTeam.Count == 0)
        {
            return;
        }

        if (!TryCreateOrderedSelectedTeam(out var orderedTeam))
        {
            GD.PrintErr("SelectedTeam 无效，将回退到默认四人队伍。");
            BuildDefaultSelectedTeam();

            if (!TryCreateOrderedSelectedTeam(out orderedTeam))
            {
                GD.PrintErr("默认四人队伍构建失败，无法应用出战队伍。");
                return;
            }
        }

        PlayerTeam = orderedTeam;
    }

    private bool TryCreateOrderedSelectedTeam(out List<BattleUnit> orderedTeam)
    {
        var templates = new Dictionary<string, BattleUnit>();
        var selectedNames = new HashSet<string>();
        orderedTeam = new List<BattleUnit>();

        if (SelectedTeam.Count != MaxPartySize)
        {
            return false;
        }

        foreach (var template in PlayerTeam)
        {
            if (!templates.ContainsKey(template.UnitName))
            {
                templates.Add(template.UnitName, template);
            }
        }

        foreach (var selectedCharacter in SelectedTeam)
        {
            if (selectedCharacter == null || string.IsNullOrWhiteSpace(selectedCharacter.CharacterName))
            {
                return false;
            }

            if (!selectedNames.Add(selectedCharacter.CharacterName))
            {
                return false;
            }

            if (!templates.TryGetValue(selectedCharacter.CharacterName, out var templateUnit))
            {
                GD.PrintErr($"未找到已加载的角色模板：{selectedCharacter.CharacterName}");
                return false;
            }

            int slotIndex = orderedTeam.Count;
            var orderedUnit = CloneBattleUnit(templateUnit, selectedCharacter.TexturePath);
            orderedUnit.SlotType = BattleUnit.PlayerSlotType.FormalPartyMember;
            orderedUnit.PartySlotIndex = slotIndex;
            orderedUnit.OwnerPartySlotIndex = InvalidPartySlotIndex;
            orderedTeam.Add(orderedUnit);
        }

        return orderedTeam.Count == MaxPartySize;
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
            unit.MiaminFatalProtectionUsed = false;
            unit.MiaminNoMeatballState = false;
            unit.ChaosOrderValue = 0;
            unit.ChaosTeamCritDamageBuff = 0f;
            unit.ChaosTeamCritDamageBuffTurns = 0;
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
            CurrentEnergy = Mathf.Clamp(sourceUnit.CurrentEnergy, 0, Mathf.Max(0, sourceUnit.MaxEnergy)),
            SlotType = sourceUnit.SlotType,
            PartySlotIndex = sourceUnit.PartySlotIndex,
            OwnerPartySlotIndex = sourceUnit.OwnerPartySlotIndex,
            MiaminFatalProtectionUsed = false,
            MiaminNoMeatballState = false
        };

        foreach (var skill in sourceUnit.Skills)
        {
            cloneUnit.Skills.Add(new SkillData
            {
                SkillId = skill.SkillId,
                SkillName = skill.SkillName,
                Description = skill.Description,
                Type = skill.Type,
                EnergyCost = skill.EnergyCost,
                EnhancedSkillId = skill.EnhancedSkillId,
                IsSelectable = skill.IsSelectable,
                ShowInBattleUi = skill.ShowInBattleUi,
                TriggeredBySkillId = skill.TriggeredBySkillId
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
    public void TakeDamage(BattleUnit target, float damage, bool isFriendlyBurnSoul = false, bool isCrit = false, BattleUnit sourceUnit = null, bool isDotDamage = false)
    {
        if (target.IsDead) return;

        // 记录受伤前的生命值比例
        float beforeHpPercent = target.Hp / target.HpMax;

        // 1. 全局易伤。DOT 也可以吃目标自己身上的通用易伤，但不吃施法者侧因素。
        float damageAfterReview = damage;
        float damageTakenBonus = DotGlobalManager.GetDamageTakenBonus(target);
        if (damageTakenBonus > 0f)
        {
            damageAfterReview = damage * (1 + damageTakenBonus);
            GD.Print($"【伤害易伤】{target.UnitName} 当前易伤{damageTakenBonus:P0}，原始伤害:{damage:0.0} → 易伤后:{damageAfterReview:0.0}");
        }

        if (!isDotDamage && target.IsPlayerUnit && target.UnitName == "迈阿密" && sourceUnit != null && !sourceUnit.IsPlayerUnit)
        {
            var youAreDone = DotGlobalManager.GetDot(sourceUnit, DotGlobalManager.MiaminYouAreDoneStatusId);
            int stacks = Mathf.Clamp(youAreDone?.StackCount ?? 0, 0, 3);
            if (stacks > 0)
            {
                float reduction = Mathf.Clamp(stacks * 0.2f, 0f, 0.95f);
                float beforeReduction = damageAfterReview;
                damageAfterReview *= 1f - reduction;
                GD.Print($"【你丸了】{sourceUnit.UnitName} 对迈阿密造成的伤害降低{reduction:P0}，{beforeReduction:0.0} → {damageAfterReview:0.0}");
            }
        }

        // 2. 防御减免（修复：使用防御后的伤害计算）
        float damageAfterDefense = damageAfterReview;
        if (target.GetEffectiveDefense() > 0)
        {
            float reduction = target.GetDamageReduction();
            damageAfterDefense = damageAfterReview * (1 - reduction);
            GD.Print($"【防御减免】{target.UnitName} 减免{(reduction * 100):0.00}%，原始伤害：{damageAfterReview:0.0} → 防御后：{damageAfterDefense:0.0}");
        }

        // 3. 硬化减伤。DOT 是否被减伤，取决于目标自己身上的效果，因此这里统一按目标状态结算。
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

        if (target.IsPlayerUnit && target.UnitName == "迈阿密")
        {
            if (target.MiaminNoMeatballState && newHp <= 0f)
            {
                newHp = 1f;
                GD.Print("【没丸呢】迈阿密处于锁血状态，生命值保持为1点");
            }
            else if (newHp <= 0f && !target.MiaminFatalProtectionUsed)
            {
                target.MiaminFatalProtectionUsed = true;
                target.MiaminNoMeatballState = true;
                newHp = 1f;
                GD.Print("【玩丸了？】迈阿密受到致命伤害，进入【没丸呢】状态并锁定生命值为1点");
            }
        }

        target.Hp = newHp;

        if (IsChaosFateDice(target) && !Mathf.IsEqualApprox(target.Hp / Mathf.Max(1f, target.HpMax), beforeHpPercent))
        {
            AddChaosOrderValue(target, 1, "生命值下降");
        }

        if (finalDamage > 0f && DamagePopfxManager.Instance != null && target.BindNode != null)
        {
            Vector2 popfxPosition = target.BindNode.GlobalPosition;
            DamagePopfxManager.Instance.SpawnDamageText(Mathf.RoundToInt(finalDamage), isCrit, popfxPosition);
        }

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

        healValue = Mathf.Max(0f, healValue);

        // 记录治疗前的生命值比例
        float beforeHpPercent = target.Hp / target.HpMax;

        // 结算治疗
        target.Hp = Mathf.Min(target.HpMax, target.Hp + healValue);

        if (IsChaosFateDice(target) && !Mathf.IsEqualApprox(target.Hp / Mathf.Max(1f, target.HpMax), beforeHpPercent))
        {
            AddChaosOrderValue(target, 1, "生命值上升");
        }

        int countedHeal = Mathf.RoundToInt(healValue);
        if (countedHeal > 0 && DamagePopfxManager.Instance != null && target.BindNode != null)
        {
            Vector2 popfxPosition = target.BindNode.GlobalPosition;
            DamagePopfxManager.Instance.SpawnHealText(countedHeal, popfxPosition);
        }

        // 触发邪恶兔被动
        if (target.IsPlayerUnit && target.UnitName == "邪恶兔")
        {
            TriggerDeadLivePassive(target, beforeHpPercent, target.Hp / target.HpMax);
        }

        RefreshUnitHpDisplay(target);
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

    public void ApplyMiaminYouAreDone(BattleUnit caster, BattleUnit target, int stacks = 1)
    {
        if (caster == null || target == null || target.IsDead)
        {
            return;
        }

        DotGlobalManager.ApplyDot(target, DotGlobalManager.CreateMiaminYouAreDoneDot(caster, stacks));
    }

    public void ApplyMiaminSelfDoubt(BattleUnit caster, BattleUnit target)
    {
        if (caster == null || target == null || target.IsDead)
        {
            return;
        }

        DotGlobalManager.ApplyDot(target, DotGlobalManager.CreateMiaminSelfDoubtStatus(caster));
    }

    public BattleUnit GetAliveMiamin()
    {
        return PlayerTeam.Find(unit => unit != null && unit.UnitName == "迈阿密" && !unit.IsDead);
    }

    public void TriggerMiaminDotHealPassive(BattleUnit dotTarget, float dotDamage)
    {
        if (dotTarget == null || dotTarget.IsPlayerUnit || dotDamage <= 0f)
        {
            return;
        }

        var miamin = GetAliveMiamin();
        if (miamin == null)
        {
            return;
        }

        foreach (var ally in GetAlivePlayers())
        {
            Heal(ally, ally.HpMax * 0.01f);
        }

        GD.Print("【玩丸了？】敌人受到持续伤害，迈阿密使全体队友恢复各自最大生命值的1%");
    }

    private void ResolveMiaminNoMeatballState(BattleUnit unit)
    {
        if (unit == null || unit.UnitName != "迈阿密" || !unit.MiaminNoMeatballState)
        {
            return;
        }

        unit.MiaminNoMeatballState = false;
        float targetHp = Mathf.Max(1f, unit.HpMax * 0.5f);
        float healValue = Mathf.Max(0f, targetHp - unit.Hp);
        if (healValue > 0f)
        {
            Heal(unit, healValue);
        }

        unit.Hp = Mathf.Max(unit.Hp, targetHp);
        RefreshUnitHpDisplay(unit);
        GD.Print("【没丸呢】状态结束，迈阿密恢复至最大生命值的50%");
    }

    private void RefreshUnitHpDisplay(BattleUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        if (unit.IsPlayerUnit)
        {
            var playerNode = unit.BindNode as Player;
            playerNode?.UpdateHp();
        }
        else
        {
            var enemyNode = unit.BindNode as Enemy;
            enemyNode?.UpdateHp();
        }
    }

    public bool IsChaosFateDice(BattleUnit unit)
    {
        return unit != null && unit.IsPlayerUnit && unit.UnitName == ChaosFateDiceUnitName;
    }

    public SkillData FindSkillById(BattleUnit unit, string skillId)
    {
        if (unit == null || string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        return unit.Skills.Find(skill => skill != null && skill.SkillId == skillId);
    }

    public int GetChaosOrderValue(BattleUnit unit)
    {
        return unit == null ? 0 : Mathf.Clamp(unit.ChaosOrderValue, 0, ChaosOrderValueMax);
    }

    public int AddChaosOrderValue(BattleUnit unit, int amount, string reason = null)
    {
        if (!IsChaosFateDice(unit) || amount <= 0)
        {
            return 0;
        }

        int before = GetChaosOrderValue(unit);
        unit.ChaosOrderValue = Mathf.Clamp(before + amount, 0, ChaosOrderValueMax);
        int gained = unit.ChaosOrderValue - before;

        if (gained > 0)
        {
            GD.Print($"【有序值】{unit.UnitName} 因 {reason ?? "生命值变化"} 获得 {gained} 点有序值，当前 {unit.ChaosOrderValue}/{ChaosOrderValueMax}");
        }

        return gained;
    }

    public void SetChaosOrderValue(BattleUnit unit, int value, string reason = null)
    {
        if (!IsChaosFateDice(unit))
        {
            return;
        }

        unit.ChaosOrderValue = Mathf.Clamp(value, 0, ChaosOrderValueMax);
        GD.Print($"【有序值】{unit.UnitName} 的有序值被设置为 {unit.ChaosOrderValue}/{ChaosOrderValueMax}，原因：{reason ?? "未说明"}");
    }

    public int ClearChaosOrderValue(BattleUnit unit, string reason = null)
    {
        if (!IsChaosFateDice(unit))
        {
            return 0;
        }

        int cleared = GetChaosOrderValue(unit);
        unit.ChaosOrderValue = 0;
        GD.Print($"【有序值】{unit.UnitName} 清空了 {cleared} 点有序值，原因：{reason ?? "未说明"}");
        return cleared;
    }

    public float ApplyNonLethalSelfHpCost(BattleUnit unit, float hpCost, string reason = null)
    {
        if (unit == null || unit.IsDead || hpCost <= 0f)
        {
            return 0f;
        }

        float actualCost = Mathf.Min(hpCost, Mathf.Max(0f, unit.Hp - 1f));
        if (actualCost <= 0f)
        {
            return 0f;
        }

        TakeDamage(unit, actualCost, isFriendlyBurnSoul: unit.IsPlayerUnit, sourceUnit: unit);
        GD.Print($"【生命代价】{unit.UnitName} 因 {reason ?? "技能效果"} 实际失去 {actualCost:0.0} 点生命");
        return actualCost;
    }

    public float ApplyNonLethalCurrentHpCost(BattleUnit unit, float percent, string reason = null)
    {
        if (unit == null || unit.IsDead || percent <= 0f)
        {
            return 0f;
        }

        return ApplyNonLethalSelfHpCost(unit, unit.Hp * percent, reason);
    }

    public void ApplyChaosTeamCritDamageBuff(int clearedOrderValue)
    {
        if (clearedOrderValue <= 0)
        {
            return;
        }

        float bonus = clearedOrderValue * ChaosTeamCritDamagePerOrder;
        foreach (var ally in GetAlivePlayers())
        {
            float before = ally.ChaosTeamCritDamageBuff;
            ally.ChaosTeamCritDamageBuff = Mathf.Min(ChaosTeamCritDamageCap, ally.ChaosTeamCritDamageBuff + bonus);
            if (ally.ChaosTeamCritDamageBuff > before)
            {
                ally.ChaosTeamCritDamageBuffTurns = ChaosTeamCritDamageBuffDuration;
            }
        }

        GD.Print($"【稳稳拿下！】全队获得 {bonus * 100:0}% 暴击伤害，持续 {ChaosTeamCritDamageBuffDuration} 回合");
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
    public System.Threading.Tasks.Task TriggerCoopAttack(BattleUnit target)
    {
        return SynergyGlobalManager.TriggerOnAllyAttackHit(this, CurrentActingUnit, target);
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
            float beforeHpPercent = target.Hp / target.HpMax;
            target.ExtraMaxHpFromMark = extraMaxHp;
            target.HpMax += extraMaxHp;
            // 同步当前血量（避免最大生命值增加后血量比例异常）
            target.Hp = Mathf.Min(target.Hp + extraMaxHp, target.HpMax);

            if (extraMaxHp > 0f && DamagePopfxManager.Instance != null && target.BindNode != null)
            {
                Vector2 popfxPosition = target.BindNode.GlobalPosition;
                DamagePopfxManager.Instance.SpawnHealText(Mathf.RoundToInt(extraMaxHp), popfxPosition);
            }

            if (target.IsPlayerUnit && target.UnitName == "邪恶兔")
            {
                TriggerDeadLivePassive(target, beforeHpPercent, target.Hp / target.HpMax);
            }

            if (target.IsPlayerUnit)
            {
                var playerNode = target.BindNode as Player;
                playerNode?.UpdateHp();
            }
            else
            {
                var enemyNode = target.BindNode as Enemy;
                enemyNode?.UpdateHp();
            }

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
            UpdateTurnIndicator(null);
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
                UpdateTurnIndicator(null);
                return true;
            }
        }

        return false;
    }

    private async void UpdateTurnIndicator(Node2D currentCharacter)
    {
        _turnIndicatorRequestId++;
        ulong requestId = _turnIndicatorRequestId;

        if (GodotObject.IsInstanceValid(_currentTurnIndicator))
        {
            _currentTurnIndicator.QueueFree();
            _currentTurnIndicator = null;
        }

        if (!GodotObject.IsInstanceValid(currentCharacter) || !currentCharacter.IsInsideTree())
        {
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (requestId != _turnIndicatorRequestId)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(currentCharacter) || !currentCharacter.IsInsideTree())
        {
            return;
        }

        Node parentNode = currentCharacter.GetParent();
        if (!GodotObject.IsInstanceValid(parentNode) || !parentNode.IsInsideTree())
        {
            return;
        }

        var indicator = new TurnIndicatorEffect();
        indicator.Configure(currentCharacter, CalculateTurnIndicatorRadius(currentCharacter));
        parentNode.AddChild(indicator);
        _currentTurnIndicator = indicator;
    }

    private float CalculateTurnIndicatorRadius(Node2D currentCharacter)
    {
        if (currentCharacter is Player player)
        {
            Rect2 bounds = player.GetDisplayBoundsGlobal();
            return Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.38f;
        }

        if (currentCharacter is Enemy enemy)
        {
            Rect2 bounds = enemy.GetDisplayBoundsGlobal();
            return Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.33f;
        }

        if (currentCharacter is Sprite2D sprite && sprite.Texture != null)
        {
            Vector2 textureSize = sprite.Texture.GetSize() * sprite.GlobalScale.Abs();
            return Mathf.Max(textureSize.X, textureSize.Y) * 0.35f;
        }

        return 80f;
    }

    public void RefreshCurrentTurnIndicator()
    {
        if (CurrentState == BattleState.BattleEnd || CurrentActingUnit == null || CurrentActingUnit.IsDead)
        {
            UpdateTurnIndicator(null);
            return;
        }

        UpdateTurnIndicator(CurrentActingUnit.BindNode);
    }

    private bool CheckBattleEndAndNotifyUi()
    {
        if (!CheckBattleEnd())
        {
            return false;
        }

        Ui.Instance?.HandleBattleEnd();
        return true;
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
            if (unit.ChaosTeamCritDamageBuffTurns > 0)
            {
                unit.ChaosTeamCritDamageBuffTurns--;
                if (unit.ChaosTeamCritDamageBuffTurns <= 0)
                {
                    unit.ChaosTeamCritDamageBuff = 0f;
                    GD.Print($"【暴击伤害增益结束】{unit.UnitName} 通过【稳稳拿下！】获得的暴击伤害加成已消失");
                }
            }
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

        ResolveMiaminNoMeatballState(unit);

        if (unit.IsDead)
        {
            GD.Print($"【回合开始】{unit.UnitName} 因持续伤害倒下");
            return true;
        }

        return false;
    }

    private void BeginUnitActionTurn(BattleUnit unit)
    {
        if (unit == null || unit.IsDead)
        {
            return;
        }

        CurrentActingUnit = unit;
        UpdateTurnIndicator(unit.BindNode);

        if (unit.IsPlayerUnit)
        {
            CurrentState = BattleState.PlayerTurn;
            Ui.Instance?.OnSinglePlayerTurnStart(unit);
            return;
        }

        CurrentState = BattleState.EnemyTurn;
        Ui.Instance?.StartEnemyAI(unit);
    }

    private async void StartTurnStartStatusResolvePhase(BattleUnit unit)
    {
        if (unit == null || unit.IsDead)
        {
            return;
        }

        CurrentActingUnit = unit;
        CurrentState = BattleState.TurnStartStatusResolve;
        UpdateTurnIndicator(unit.BindNode);
        Ui.Instance?.OnTurnStartStatusResolve(unit);

        await ToSignal(GetTree().CreateTimer(TurnStartDotPreResolveDelaySeconds), SceneTreeTimer.SignalName.Timeout);

        bool unitDiedFromStatus = ResolveTurnStartStatuses(unit);
        await ToSignal(GetTree().CreateTimer(TurnStartDotResolveDisplaySeconds), SceneTreeTimer.SignalName.Timeout);

        if (unitDiedFromStatus)
        {
            if (CheckBattleEndAndNotifyUi())
            {
                return;
            }

            CurrentActingUnitIndex++;
            CurrentActingUnit = null;

            if (unit.IsPlayerUnit)
            {
                FindNextAlivePlayer();
            }
            else
            {
                FindNextAliveEnemy();
            }
            return;
        }

        await ToSignal(GetTree().CreateTimer(TurnStartDotPostResolveDelaySeconds), SceneTreeTimer.SignalName.Timeout);
        BeginUnitActionTurn(unit);
    }

    // 找下一个存活的玩家
    private void FindNextAlivePlayer()
    {
        while (CurrentActingUnitIndex < PlayerTeam.Count)
        {
            var unit = PlayerTeam[CurrentActingUnitIndex];
            if (!unit.IsDead)
            {
                if (DotGlobalManager.HasTurnStartDamageToResolve(unit))
                {
                    StartTurnStartStatusResolvePhase(unit);
                    return;
                }

                if (ResolveTurnStartStatuses(unit))
                {
                    if (CheckBattleEndAndNotifyUi())
                    {
                        return;
                    }

                    CurrentActingUnit = null;
                    CurrentActingUnitIndex++;
                    continue;
                }

                BeginUnitActionTurn(unit);
                return;
            }
            CurrentActingUnitIndex++;
        }

        // 所有玩家行动完，进入敌人回合
        UpdateTurnIndicator(null);
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
                if (DotGlobalManager.HasTurnStartDamageToResolve(unit))
                {
                    StartTurnStartStatusResolvePhase(unit);
                    return;
                }

                if (ResolveTurnStartStatuses(unit))
                {
                    if (CheckBattleEndAndNotifyUi())
                    {
                        return;
                    }

                    CurrentActingUnit = null;
                    CurrentActingUnitIndex++;
                    continue;
                }

                BeginUnitActionTurn(unit);
                return;
            }
            CurrentActingUnitIndex++;
        }

        if (CheckBattleEnd())
        {
            return;
        }

        // 所有敌人行动完，回到玩家回合
        UpdateTurnIndicator(null);
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

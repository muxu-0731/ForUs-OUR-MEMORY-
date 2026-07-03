using Godot;
using System;
using System.Text;

public partial class Ui : Control
{
    public static Ui Instance { get; private set; }
    private const float DefaultActionEndDelaySeconds = 1.0f;
    private const float CoopAttackActionEndDelaySeconds = 0.05f;

    // UI节点
    private Button _skillButton;
    private Control _skillsContainer;
    private Button _passiveBtn;
    private Button _normalAttackBtn;
    private Button _specialSkillBtn;
    private Button _ultimateBtn;

    private SkillTooltip _skillTooltip;
    private Label _battleTipLabel;
    private Label _remainEnemyLabel;
    private Label _attrLabel;
    private PanelContainer _skillNotifier;
    private Label _notifierLabel;
    private Tween _notifierTween;


    private readonly PackedScene _skillTooltipScene = GD.Load<PackedScene>("res://skill_tooltip.tscn");

    // 当前角色的技能缓存
    private GlobalScript.SkillData _currentPassive;
    private GlobalScript.SkillData _currentNormal;
    private GlobalScript.SkillData _currentSpecial;
    private GlobalScript.SkillData _currentUltimate;

    private bool _currentPlayerHasActed = false;
    private bool _isResolvingSkill = false;

    public override void _Ready()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;

        // 获取所有节点
        _skillButton = GetNodeOrNull<Button>("skill");
        _skillsContainer = GetNodeOrNull<Control>("skill/SkillsContainer");
        _passiveBtn = GetNodeOrNull<Button>("skill/SkillsContainer/passive_skill");
        _normalAttackBtn = GetNodeOrNull<Button>("skill/SkillsContainer/normalattack");
        _specialSkillBtn = GetNodeOrNull<Button>("skill/SkillsContainer/special_skill");
        _ultimateBtn = GetNodeOrNull<Button>("skill/SkillsContainer/ultimate_ability");
        _battleTipLabel = GetNodeOrNull<Label>("Label");
        _remainEnemyLabel = GetNodeOrNull<Label>("enemynumber");
        _attrLabel = GetNodeOrNull<Label>("playershuxing");
        _skillNotifier = GetNodeOrNull<PanelContainer>("SkillNotifier");
        _notifierLabel = GetNodeOrNull<Label>("SkillNotifier/NotifierLabel");


        // 实例化自定义提示框
        _skillTooltip = _skillTooltipScene.Instantiate<SkillTooltip>();
        // ✅ 用方法名延迟执行添加+隐藏操作
        CallDeferred(nameof(AddAndHideTooltip));

        // 绑定按钮点击事件（原有逻辑不变）
        if (_skillButton != null) _skillButton.Pressed += OnSkillBtnClick;
        if (_normalAttackBtn != null) _normalAttackBtn.Pressed += OnNormalAttackCast;
        if (_specialSkillBtn != null) _specialSkillBtn.Pressed += OnSpecialSkillCast;
        if (_ultimateBtn != null) _ultimateBtn.Pressed += OnUltimateCast;

        // 绑定鼠标悬停事件（逻辑优化，保留原有绑定方式）
        if (_passiveBtn != null)
        {
            _passiveBtn.MouseEntered += () => OnSkillHover(_currentPassive);
            _passiveBtn.MouseExited += OnSkillHoverEnd;
        }
        if (_normalAttackBtn != null)
        {
            _normalAttackBtn.MouseEntered += () => OnSkillHover(_currentNormal);
            _normalAttackBtn.MouseExited += OnSkillHoverEnd;
        }
        if (_specialSkillBtn != null)
        {
            _specialSkillBtn.MouseEntered += () => OnSkillHover(_currentSpecial);
            _specialSkillBtn.MouseExited += OnSkillHoverEnd;
        }
        if (_ultimateBtn != null)
        {
            _ultimateBtn.MouseEntered += () => OnSkillHover(_currentUltimate);
            _ultimateBtn.MouseExited += OnSkillHoverEnd;
        }

        // 初始状态（原有逻辑，移除旧Label相关）
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        if (_attrLabel != null) _attrLabel.Visible = false;
        if (_battleTipLabel != null)
        {
            _battleTipLabel.Visible = false;
            _battleTipLabel.Text = string.Empty;
        }
        if (_skillNotifier != null)
        {
            _skillNotifier.Visible = false;
            _skillNotifier.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        // 延迟初始化检查
        GetTree().CreateTimer(0.1f).Timeout += () =>
        {
            var global = GlobalScript.Instance;
            if (global != null && global.CurrentActingUnit != null && global.CurrentState == GlobalScript.BattleState.PlayerTurn)
            {
                OnSinglePlayerTurnStart(global.CurrentActingUnit);
            }
        };
    }

    public override void _Process(double delta)
    {
        var global = GlobalScript.Instance;
        if (global == null) return;

        // 按钮禁用逻辑（原有不变）
        bool isPlayerTurn = global.CurrentState == GlobalScript.BattleState.PlayerTurn;
        bool shouldDisable = !isPlayerTurn || _currentPlayerHasActed || _isResolvingSkill;

        if (_passiveBtn != null) _passiveBtn.Disabled = true;
        if (_normalAttackBtn != null) _normalAttackBtn.Disabled = shouldDisable;
        if (_specialSkillBtn != null) _specialSkillBtn.Disabled = shouldDisable;
        if (_ultimateBtn != null) _ultimateBtn.Disabled = shouldDisable;
        if (_skillButton != null) _skillButton.Disabled = !isPlayerTurn;

        // 刷新剩余敌人数（原有不变）
        if (_remainEnemyLabel != null)
        {
            int remaining = global.AllEnemies.Count - global.CurrentEnemyIndex;
            if (global.GetAliveEnemies().Count > 0) remaining += 1;
            _remainEnemyLabel.Text = $"剩余敌人数量：{remaining}";
        }

        // 玩家回合实时刷新属性面板（原有不变）
        if (isPlayerTurn && global.CurrentActingUnit != null && !global.CurrentActingUnit.IsDead)
        {
            RefreshAttrLabel(global.CurrentActingUnit);
            AppendChaosExtraAttrLabel(global.CurrentActingUnit);
        }
    }

    private void AddAndHideTooltip()
    {
        if (_skillTooltip == null) return;
        // 先添加到根节点
        GetTree().Root.AddChild(_skillTooltip);
        // 再隐藏
        _skillTooltip.Hide();
    }

    #region 回合控制&技能UI刷新
    // 切换角色时更新技能（原有不变）
    public void OnSinglePlayerTurnStart(GlobalScript.BattleUnit playerUnit)
    {
        _currentPlayerHasActed = false;
        _isResolvingSkill = false;
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        // ========== 核心修改4：隐藏自定义提示框 ==========
        if (_skillTooltip != null) _skillTooltip.Hide();

        // 显示属性面板
        if (_attrLabel != null)
        {
            _attrLabel.Visible = true;
            RefreshAttrLabel(playerUnit);
            AppendChaosExtraAttrLabel(playerUnit);
        }

        // 提取当前角色的技能
        _currentPassive = playerUnit.Skills.Find(s => s.Type == GlobalScript.SkillType.Passive);
        _currentNormal = FindBattleSkill(playerUnit, GlobalScript.SkillType.Normal);
        _currentSpecial = FindBattleSkill(playerUnit, GlobalScript.SkillType.Special);
        _currentUltimate = FindBattleSkill(playerUnit, GlobalScript.SkillType.Ultimate);

        // 更新按钮文本
        UpdatePassiveButton(_passiveBtn, _currentPassive);
        UpdateSkillButton(_normalAttackBtn, _currentNormal);
        UpdateSkillButton(_specialSkillBtn, _currentSpecial);
        UpdateSkillButton(_ultimateBtn, _currentUltimate);

        // 更新回合提示
        if (_battleTipLabel != null)
        {
            _battleTipLabel.Text = $"当前回合：{playerUnit.UnitName}，请选择技能！";
        }
    }

    // 新敌人登场时调用（原有不变）
    public void OnTurnStartStatusResolve(GlobalScript.BattleUnit unit)
    {
        _currentPlayerHasActed = false;
        _isResolvingSkill = false;

        if (_skillsContainer != null) _skillsContainer.Visible = false;
        if (_skillTooltip != null) _skillTooltip.Hide();
        if (_attrLabel != null) _attrLabel.Visible = false;

        if (_battleTipLabel != null && unit != null)
        {
            _battleTipLabel.Text = $"{unit.UnitName} 的持续伤害结算中...";
        }
    }

    public void BroadcastSkillName(string skillName, bool isPlayerTurn)
    {
        if (_skillNotifier == null || _notifierLabel == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_notifierTween))
        {
            _notifierTween.Kill();
        }

        _notifierLabel.Text = skillName;
        _notifierLabel.Modulate = isPlayerTurn ? Colors.White : new Color(1f, 0.25f, 0.25f);
        _skillNotifier.Visible = true;
        _skillNotifier.Modulate = new Color(1f, 1f, 1f, 0f);

        _notifierTween = CreateTween();
        _notifierTween.TweenProperty(_skillNotifier, "modulate:a", 1f, 0.2f);
        _notifierTween.TweenInterval(3.0f);
        _notifierTween.TweenProperty(_skillNotifier, "modulate:a", 0f, 0.3f);
        _notifierTween.TweenCallback(Callable.From(() =>
        {
            if (_skillNotifier != null)
            {
                _skillNotifier.Visible = false;
            }
        }));
    }

    public void OnNewEnemySpawned(GlobalScript.BattleUnit newEnemy)
    {
        var enemyNode = GetTree().Root.GetNodeOrNull<Enemy>("Main/enemy");
        enemyNode?.RefreshEnemyData(newEnemy);

        if (_battleTipLabel != null)
        {
            _battleTipLabel.Text = $"新敌人【{newEnemy.UnitName}】登场！";
        }
        _currentPlayerHasActed = false;
        _isResolvingSkill = false;
    }

    // ========== 核心修改5：替换悬停显示逻辑为自定义提示框 ==========
    private void OnSkillHover(GlobalScript.SkillData skill)
    {
        if (skill == null || _skillTooltip == null) return;

        // 调用自定义提示框的方法，自动处理文本换行、位置防溢出
        _skillTooltip.SetSkillData(skill);
        _skillTooltip.Show();
    }

    // ========== 核心修改6：替换悬停结束逻辑 ==========
    private void OnSkillHoverEnd()
    {
        if (_skillTooltip != null)
        {
            _skillTooltip.Hide();
        }
    }

    // ✅ 新增：供全局调用的设置提示方法
    public void SetBattleTip(string text)
    {
        if (_battleTipLabel != null)
        {
            _battleTipLabel.Visible = false;
            _battleTipLabel.Text = string.Empty;
        }
    }
    #endregion

    #region 技能释放逻辑
    private void OnSkillBtnClick()
    {
        if (_skillsContainer != null)
            _skillsContainer.Visible = !_skillsContainer.Visible;
    }

    private async void OnNormalAttackCast()
    {
        var global = GlobalScript.Instance;
        if (!CanCastSkill(global) || _currentNormal == null) return;

        _currentPlayerHasActed = true;
        _isResolvingSkill = true;
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        BroadcastSkillName(_currentNormal.SkillName, true);

        var caster = global.CurrentActingUnit;
        if (global.IsChaosFateDice(caster))
        {
            await ExecuteChaosFateDiceNormalAttack(global, caster);
            return;
        }

        var enemies = global.GetAliveEnemies();
        if (enemies.Count == 0)
        {
            _isResolvingSkill = false;
            EndCurrentPlayerAction(global);
            return;
        }
        var target = enemies[0];

        await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

        float baseDamage = caster.UnitName == "苹果大王" || caster.UnitName == "迈阿密"
            ? caster.Attack * 0.05f
            : caster.HpMax * 0.05f;
        float finalDamage = baseDamage;
        bool isCrit = false;

        float finalCritRate = caster.GetFinalCritRate();
        float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
        GD.Print($"【{caster.UnitName}普通攻击暴击判定】");
        GD.Print($"当前暴击率：{finalCritRate * 100:0.00}%，随机到的数值：{randomValue * 100:0.00}%");

        if (randomValue <= finalCritRate)
        {
            isCrit = true;
            finalDamage *= caster.GetFinalCritDamage();
            GD.Print($"✅ 暴击触发！伤害倍率：{caster.GetFinalCritDamage():0.00}倍，暴击后伤害：{finalDamage:0.0}");
        }
        else
        {
            GD.Print("❌ 未触发暴击");
        }

        if (target.Hardened)
        {
            finalDamage /= 2;
            GD.Print($"敌人硬化生效，伤害减半，最终伤害：{finalDamage:0.0}");
        }

        global.TakeDamage(target, finalDamage, isCrit: isCrit);
        global.ApplySkillEnergy(caster, _currentNormal);
        global.GainFocus(1);

        // ✅ 新增：小鸡普攻加1层锐评
        if (caster.UnitName == "小鸡")
        {
            global.ApplyReview(caster, target, 1);
        }
        else if (caster.UnitName == "苹果大王")
        {
            global.ApplyAppleQueenGuHen(caster, target);
        }

        string tip = $"{caster.UnitName}的{_currentNormal.SkillName}命中！";
        tip += $"对{target.UnitName}造成{finalDamage:0.0}点伤害！";
        if (isCrit) tip = "💥 暴击！！" + tip;
        if (target.Hardened) tip += "\n敌人的硬化减免了一半伤害！";
        if (_battleTipLabel != null) _battleTipLabel.Text = tip;

        // ✅ 新增：非小鸡角色攻击后，触发小鸡默契攻击
        if (caster.UnitName != "小鸡")
        {
            await global.TriggerCoopAttack(target);
        }

        FinishTurnConsumingAction(global, usedCoopAttack: caster.UnitName != "灏忛浮");
    }

    private async void OnSpecialSkillCast()
    {
        var global = GlobalScript.Instance;
        if (!CanCastSkill(global) || _currentSpecial == null) return;

        var caster = global.CurrentActingUnit;
        if (global.IsChaosFateDice(caster))
        {
            _currentPlayerHasActed = true;
            _isResolvingSkill = true;
            if (_skillsContainer != null) _skillsContainer.Visible = false;
            BroadcastSkillName(_currentSpecial.SkillName, true);
            await ExecuteChaosFateDiceSpecial(global, caster);
            return;
        }

        if (!global.HasEnoughFocus(1))
        {
            if (_battleTipLabel != null)
            {
                _battleTipLabel.Text = "专注力不足，无法释放特殊技能";
            }
            return;
        }

        _currentPlayerHasActed = true;
        _isResolvingSkill = true;
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        BroadcastSkillName(_currentSpecial.SkillName, true);

        var alivePlayers = global.GetAlivePlayers();
        var enemies = global.GetAliveEnemies(); // ✅ 方法开头只声明一次enemies，所有分支共用
        string tip = "";
        bool skillExecuted = false;

        // ✅ 外卖猫特殊技能逻辑
        if (caster.UnitName == "外卖猫")
        {
            global.SpendFocus(1);
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            float healPerFriend = caster.HpMax * 0.1f;
            tip = $"{caster.UnitName}释放{_currentSpecial.SkillName}！\n";
            foreach (var friend in alivePlayers)
            {
                global.Heal(friend, healPerFriend);
                global.ApplyRegularCustomerMark(caster, friend);
                tip += $"{friend.UnitName}回复{healPerFriend:0}点血量！\n";
            }
            skillExecuted = true;
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            // ✅ 外卖猫放完技能，触发小鸡默契攻击
            if (enemies.Count > 0) await global.TriggerCoopAttack(enemies[0]);
        }
        // ✅ 邪恶兔燃魂逻辑（直接用开头的enemies，不再重复声明）
        else if (caster.UnitName == "邪恶兔")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                EndCurrentPlayerAction(global);
                return;
            }
            var target = enemies[0];
            global.SpendFocus(1);

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            float totalCostHp = 0f;
            string costTip = "";

            foreach (var player in alivePlayers)
            {
                float costHp = player.HpMax * 0.1f;
                totalCostHp += costHp;
                global.TakeDamage(player, costHp, isFriendlyBurnSoul: true);
                costTip += $"{player.UnitName}消耗了{costHp:0}点生命值（最低保留1点）！\n";

                // ✅ 完整恢复：给每个扣血的友方角色都播放燃魂启动特效
                if (ParticleEffectManager.Instance != null && player.BindNode != null)
                {
                    Vector2 playerPos = player.BindNode.GlobalPosition;
                    _ = ParticleEffectManager.Instance.PlayBurningSoul(playerPos);
                    GD.Print($"🔥 给 {player.UnitName} 播放燃魂特效，位置：{playerPos}");
                }
            }
            GD.Print($"燃魂总消耗血量：{totalCostHp:0}，基础伤害：{totalCostHp * 0.5f:0}");

            await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

            float baseDamage = totalCostHp * 0.5f;
            float finalDamage = baseDamage;
            bool isCrit = false;

            float finalCritRate = caster.GetFinalCritRate();
            float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
            GD.Print($"【{caster.UnitName}燃魂暴击判定】");
            GD.Print($"当前暴击率：{finalCritRate * 100:0.00}%，随机到的数值：{randomValue * 100:0.00}%");

            if (randomValue <= finalCritRate)
            {
                isCrit = true;
                finalDamage *= caster.GetFinalCritDamage();
                GD.Print($"✅ 暴击触发！伤害倍率：{caster.GetFinalCritDamage():0.00}倍，暴击后伤害：{finalDamage:0.0}");
            }
            else
            {
                GD.Print("❌ 未触发暴击");
            }

            if (target.Hardened)
            {
                finalDamage /= 2;
                GD.Print($"敌人硬化生效，伤害减半，最终伤害：{finalDamage:0.0}");
            }

            // ✅ 完整恢复：燃魂命中特效
            if (ParticleEffectManager.Instance != null && target.BindNode != null)
            {
                Vector2 targetPos = target.BindNode.GlobalPosition;
                _ = ParticleEffectManager.Instance.PlayBurningSoul(targetPos);
                GD.Print($"💥 燃魂命中特效播放：{targetPos}");
            }
            else
            {
                GD.PushWarning("⚠️ 燃魂命中特效播放失败：特效管理器未初始化或目标节点不存在");
            }

            global.TakeDamage(target, finalDamage, isCrit: isCrit);
            skillExecuted = true;

            tip = costTip;
            if (isCrit) tip += "💥 暴击！！";
            tip += $"{caster.UnitName}的{_currentSpecial.SkillName}命中！";
            tip += $"对{target.UnitName}造成{finalDamage:0.0}点伤害！";
            if (target.Hardened) tip += "\n敌人的硬化减免了一半伤害！";
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            // ✅ 邪恶兔放完技能，触发小鸡默契攻击
            await global.TriggerCoopAttack(target);
        }
        // ✅ 小鸡特殊技能逻辑
        else if (caster.UnitName == "小鸡")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                EndCurrentPlayerAction(global);
                return;
            }
            var target = enemies[0];
            global.SpendFocus(1);

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            // 5%最大生命值伤害
            float baseDamage = caster.HpMax * 0.05f;
            float finalDamage = baseDamage;
            bool isCrit = false;

            float finalCritRate = caster.GetFinalCritRate();
            float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
            if (randomValue <= finalCritRate)
            {
                isCrit = true;
                finalDamage *= caster.GetFinalCritDamage();
            }

            if (target.Hardened) finalDamage /= 2;

            global.TakeDamage(target, finalDamage, isCrit: isCrit);
            skillExecuted = true;

            // 额外2层锐评 + 禁疗3回合
            global.ApplyReview(caster, target, 2);
            global.ApplyHealBlock(target, 3);

            tip = $"{caster.UnitName}的{_currentSpecial.SkillName}命中！";
            tip += $"\n对{target.UnitName}造成{finalDamage:0.0}点伤害！";
            tip += $"\n额外施加2层锐评！";
            tip += $"\n敌人被禁疗3回合！";
            if (isCrit) tip = "💥 暴击！！" + tip;
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;
        }
        else if (caster.UnitName == "苹果大王")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                EndCurrentPlayerAction(global);
                return;
            }

            var target = enemies[0];
            global.SpendFocus(1);

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            float baseDamage = caster.Attack * 0.3f;
            float finalDamage = baseDamage;
            bool isCrit = false;

            float finalCritRate = caster.GetFinalCritRate();
            float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
            if (randomValue <= finalCritRate)
            {
                isCrit = true;
                finalDamage *= caster.GetFinalCritDamage();
            }

            if (target.Hardened) finalDamage /= 2;

            global.TakeDamage(target, finalDamage, isCrit: isCrit);
            global.ApplyAppleQueenGuHen(caster, target);
            DotGlobalManager.SpreadDots(global, target, global.GetAdjacentEnemies(target));
            skillExecuted = true;

            tip = $"{caster.UnitName}的{_currentSpecial.SkillName}命中！";
            tip += $"\n对{target.UnitName}造成{finalDamage:0.0}点伤害！";
            tip += "\n目标被施加【锢痕】！";
            tip += "\n目标身上的持续伤害已向相邻敌人扩散！";
            if (isCrit) tip = "💥 暴击！！" + tip;
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            await global.TriggerCoopAttack(target);
        }
        else if (caster.UnitName == "迈阿密")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                EndCurrentPlayerAction(global);
                return;
            }

            global.SpendFocus(1);
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            tip = $"{caster.UnitName}释放{_currentSpecial.SkillName}！\n";
            foreach (var enemy in enemies)
            {
                float finalDamage = caster.Attack * 0.1f;
                bool isCrit = false;

                float finalCritRate = caster.GetFinalCritRate();
                float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
                if (randomValue <= finalCritRate)
                {
                    isCrit = true;
                    finalDamage *= caster.GetFinalCritDamage();
                }

                if (enemy.Hardened)
                {
                    finalDamage /= 2f;
                }

                global.TakeDamage(enemy, finalDamage, isCrit: isCrit);
                global.ApplyMiaminYouAreDone(caster, enemy, 1);
                tip += $"{enemy.UnitName}受到{finalDamage:0.0}点伤害并被施加1层【你丸了】！\n";
                if (isCrit)
                {
                    tip = "💥 暴击！！\n" + tip;
                }
            }

            skillExecuted = true;
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            await global.TriggerCoopAttack(enemies[0]);
        }
        else
        {
            tip = "该角色暂未实装特殊技能！";
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;
        }

        if (skillExecuted)
        {
            global.ApplySkillEnergy(caster, _currentSpecial);
        }

        FinishTurnConsumingAction(global, usedCoopAttack: true);
    }

    private async void OnUltimateCast()
    {
        var global = GlobalScript.Instance;
        if (!CanCastSkill(global) || _currentUltimate == null) return;

        var caster = global.CurrentActingUnit;
        if (!global.CanCastUltimate(caster))
        {
            GD.Print("能量不足，无法释放大招");
            if (_battleTipLabel != null)
            {
                _battleTipLabel.Text = "能量不足，无法释放大招";
            }
            return;
        }

        if (global.IsChaosFateDice(caster))
        {
            _isResolvingSkill = true;
            if (_skillsContainer != null) _skillsContainer.Visible = false;
            BroadcastSkillName(_currentUltimate.SkillName, true);
            await ExecuteChaosFateDiceUltimate(global, caster);
            return;
        }

        _isResolvingSkill = true;
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        BroadcastSkillName(_currentUltimate.SkillName, true);

        var alivePlayers = global.GetAlivePlayers();
        var enemies = global.GetAliveEnemies();
        string tip = "";

        // ✅ 外卖猫大招逻辑
        if (caster.UnitName == "外卖猫")
        {
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            // 第一步：全体友方回复30%外卖猫最大生命值
            float healPerFriend = caster.HpMax * 0.3f;
            float totalHeal = 0f;
            tip = $"{caster.UnitName}释放{_currentUltimate.SkillName}！\n";
            foreach (var friend in alivePlayers)
            {
                global.Heal(friend, healPerFriend);
                // 触发被动：给友方加熟客印记
                global.ApplyRegularCustomerMark(caster, friend);
                totalHeal += healPerFriend;
                tip += $"{friend.UnitName}回复{healPerFriend:0}点血量！\n";
            }

            await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

            // 第二步：对敌人造成50%总回复量的伤害
            if (enemies.Count > 0)
            {
                var target = enemies[0];
                float damage = totalHeal * 0.5f;
                float finalDamage = damage;
                bool isCrit = false;

                // 暴击判定
                float finalCritRate = caster.GetFinalCritRate();
                float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
                if (randomValue <= finalCritRate)
                {
                    isCrit = true;
                    finalDamage *= caster.GetFinalCritDamage();
                    tip += "💥 暴击！！";
                }

                if (target.Hardened)
                {
                    finalDamage /= 2;
                    tip += $"敌人{target.UnitName}硬化生效，伤害减半！\n";
                }

                global.TakeDamage(target, finalDamage, isCrit: isCrit);
                tip += $"对{target.UnitName}造成{finalDamage:0.0}点伤害（50%总回复量）！";

                // ✅ 外卖猫放完大招，触发小鸡默契攻击
                await global.TriggerCoopAttack(target);
            }

            global.ApplySkillEnergy(caster, _currentUltimate);
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;
            ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
            return;
        }
        // ✅ 邪恶兔大招逻辑
        // ✅ 邪恶兔大招逻辑（完整恢复特效）
        else if (caster.UnitName == "邪恶兔")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                return;
            }
            var target = enemies[0];

            // 1. 先等待一小段前摇
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            // 2. 先计算好伤害数值（但不扣血）
            float baseDamage = caster.HpMax * 0.2f;
            float finalDamage = baseDamage;
            bool isCrit = false;

            float finalCritRate = caster.GetFinalCritRate();
            float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
            GD.Print($"【{caster.UnitName}血魇暴击判定】");
            GD.Print($"当前暴击率：{finalCritRate * 100:0.00}%，随机到的数值：{randomValue * 100:0.00}%");

            if (randomValue <= finalCritRate)
            {
                isCrit = true;
                finalDamage *= caster.GetFinalCritDamage();
                GD.Print($"✅ 暴击触发！伤害倍率：{caster.GetFinalCritDamage():0.00}倍，暴击后伤害：{finalDamage:0.0}");
            }
            else
            {
                GD.Print("❌ 未触发暴击");
            }

            if (target.Hardened)
            {
                finalDamage /= 2;
                GD.Print($"敌人硬化生效，伤害减半，最终伤害：{finalDamage:0.0}");
            }

            // ✅ 完整恢复：血魇特效播放
            if (caster.BindNode != null && target.BindNode != null && ParticleEffectManager.Instance != null)
            {
                GD.Print("🎮 准备播放血魇特效...");
                await ParticleEffectManager.Instance.PlayBloodNightmare(caster.BindNode.GlobalPosition, target.BindNode.GlobalPosition);
                GD.Print("🎮 血魇特效播放完成！");
            }
            else
            {
                GD.PushError($"❌ 特效跳过！caster.BindNode={caster.BindNode != null}, target.BindNode={target.BindNode != null}, 特效管理器={ParticleEffectManager.Instance != null}");
            }

            // ✅ 特效完全播完后，再结算伤害
            GD.Print("血魇特效播放完成，开始结算伤害");
            global.TakeDamage(target, finalDamage, isCrit: isCrit);

            // 3. 伤害结算后，再执行吸血
            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
            float healValue = caster.HpMax * 0.2f;
            global.Heal(caster, healValue);

            // 4. 刷新UI提示
            tip = "";
            if (isCrit) tip += "💥 暴击！！";
            tip += $"{caster.UnitName}的{_currentUltimate.SkillName}命中！";
            tip += $"对{target.UnitName}造成{finalDamage:0.0}点伤害！";
            if (target.Hardened) tip += "\n敌人的硬化减免了一半伤害！";
            tip += $"\n回复了自身{healValue:0}点生命值！";
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            // ✅ 邪恶兔放完大招，触发小鸡默契攻击
            await global.TriggerCoopAttack(target);

            global.ApplySkillEnergy(caster, _currentUltimate);

            ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
            return;
        }
        // ✅ 小鸡大招逻辑
        else if (caster.UnitName == "小鸡")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                return;
            }
            var target = enemies[0];

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            // 30%最大生命值伤害
            float baseDamage = caster.HpMax * 0.3f;
            float finalDamage = baseDamage;
            bool isCrit = false;

            float finalCritRate = caster.GetFinalCritRate();
            float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
            if (randomValue <= finalCritRate)
            {
                isCrit = true;
                finalDamage *= caster.GetFinalCritDamage();
            }

            if (target.Hardened) finalDamage /= 2;

            global.TakeDamage(target, finalDamage, isCrit: isCrit);

            // 立即10层锐评 + 降低60%防御3回合
            global.ApplyReview(caster, target, 10);
            global.ApplyDefenseDown(target, 0.6f, 3);

            tip = $"{caster.UnitName}的{_currentUltimate.SkillName}命中！";
            tip += $"\n对{target.UnitName}造成{finalDamage:0.0}点伤害！";
            tip += $"\n敌人立即陷入10层锐评！";
            tip += $"\n敌人防御力降低60%，持续3回合！";
            if (isCrit) tip = "💥 暴击！！" + tip;
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            global.ApplySkillEnergy(caster, _currentUltimate);
            ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
            return;
        }
        else if (caster.UnitName == "苹果大王")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                return;
            }

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            tip = $"{caster.UnitName}释放{_currentUltimate.SkillName}！\n";
            foreach (var enemy in enemies)
            {
                float finalDamage = caster.Attack * 0.5f;
                bool isCrit = false;

                float finalCritRate = caster.GetFinalCritRate();
                float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
                if (randomValue <= finalCritRate)
                {
                    isCrit = true;
                    finalDamage *= caster.GetFinalCritDamage();
                }

                if (enemy.Hardened)
                {
                    finalDamage /= 2f;
                }

                global.TakeDamage(enemy, finalDamage, isCrit: isCrit);
                global.ApplyAppleQueenGuHen(caster, enemy);
                tip += $"{enemy.UnitName}受到{finalDamage:0.0}点伤害并被施加【锢痕】！\n";
                if (isCrit)
                {
                    tip = "💥 暴击！！\n" + tip;
                }
            }

            DotGlobalManager.ExplodeDots(global, enemies);
            global.ApplySkillEnergy(caster, _currentUltimate);
            await global.TriggerCoopAttack(enemies[0]);

            tip += "场上所有敌人的持续伤害已被引爆！";
            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
            return;
        }
        else if (caster.UnitName == "迈阿密")
        {
            if (enemies.Count == 0)
            {
                _isResolvingSkill = false;
                return;
            }

            await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

            tip = $"{caster.UnitName}释放{_currentUltimate.SkillName}！\n";
            foreach (var enemy in enemies)
            {
                float finalDamage = caster.Attack * 0.3f;
                bool isCrit = false;

                float finalCritRate = caster.GetFinalCritRate();
                float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
                if (randomValue <= finalCritRate)
                {
                    isCrit = true;
                    finalDamage *= caster.GetFinalCritDamage();
                }

                if (enemy.Hardened)
                {
                    finalDamage /= 2f;
                }

                global.TakeDamage(enemy, finalDamage, isCrit: isCrit);
                global.ApplyMiaminSelfDoubt(caster, enemy);
                tip += $"{enemy.UnitName}受到{finalDamage:0.0}点伤害并陷入【自我怀疑】！\n";
                if (isCrit)
                {
                    tip = "💥 暴击！！\n" + tip;
                }
            }

            global.ApplySkillEnergy(caster, _currentUltimate);
            await global.TriggerCoopAttack(enemies[0]);

            if (_battleTipLabel != null) _battleTipLabel.Text = tip;

            ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
            return;
        }
    }
    #endregion

    #region 敌人AI逻辑（完全保留原有代码，无修改）
    public async void StartEnemyAI(GlobalScript.BattleUnit enemy)
    {
        var global = GlobalScript.Instance;
        if (global == null) return;

        // 敌人回合隐藏UI
        if (_attrLabel != null) _attrLabel.Visible = false;
        if (_skillTooltip != null) _skillTooltip.Hide();
        if (_skillsContainer != null) _skillsContainer.Visible = false;

        if (_battleTipLabel != null)
            _battleTipLabel.Text = $"{enemy.UnitName}的回合...";
        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

        var alivePlayers = global.GetAlivePlayers();
        if (alivePlayers.Count == 0)
        {
            global.OnSingleEnemyActionEnd();
            return;
        }

        var target = SelectEnemyTarget(global, enemy, alivePlayers);
        GD.Print($"【敌人AI】{enemy.UnitName}随机选择了{target.UnitName}作为目标！");

        var rand = GlobalScript.GlobalRandom;

        if (enemy.AbyssTurns > 0 && enemy.AbyssStacks > 0)
        {
            float abyssDmg = enemy.AbyssStacks * enemy.AbyssBaseDamage;
            global.TakeDamage(enemy, abyssDmg);
            if (_battleTipLabel != null)
                _battleTipLabel.Text = $"渊噬对{enemy.UnitName}造成了{abyssDmg}点伤害！";
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

            if (enemy.IsDead)
            {
                global.OnSingleEnemyActionEnd();
                return;
            }
        }

        int randomSkill = rand.Next(0, 4);
        switch (randomSkill)
        {
            case 0:
                BroadcastSkillName("普通攻击", false);
                global.TakeDamage(target, enemy.Attack * 1f, sourceUnit: enemy);
                TryApplyMiaminRetaliatoryStack(global, enemy, target);
                if (_battleTipLabel != null)
                    _battleTipLabel.Text = $"{enemy.UnitName}发动普通攻击！";
                break;
            case 1:
                BroadcastSkillName("重击", false);
                global.TakeDamage(target, enemy.Attack * 1.6f, sourceUnit: enemy);
                TryApplyMiaminRetaliatoryStack(global, enemy, target);
                if (_battleTipLabel != null)
                    _battleTipLabel.Text = $"{enemy.UnitName}发动重击！";
                break;
            case 2:
                BroadcastSkillName("硬化", false);
                enemy.Hardened = true;
                enemy.HardenedTurns = 2;
                if (_battleTipLabel != null)
                    _battleTipLabel.Text = $"{enemy.UnitName}开启硬化皮肤，受到的伤害减半！";
                break;
            case 3:
                BroadcastSkillName("自我愈合", false);
                global.Heal(enemy, enemy.HpMax * 0.1f);
                if (_battleTipLabel != null)
                    _battleTipLabel.Text = $"{enemy.UnitName}使用自我愈合，恢复了生命值！";
                break;
        }

        await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

        if (global.CheckBattleEnd())
        {
            OnBattleEnd(global);
            return;
        }

        global.OnSingleEnemyActionEnd();
    }
    #endregion

    #region 内部辅助方法（完全保留原有代码，仅修改战斗结束时的提示框隐藏）
    private bool CanCastSkill(GlobalScript global)
    {
        return global != null
            && global.CurrentState == GlobalScript.BattleState.PlayerTurn
            && !_currentPlayerHasActed
            && !_isResolvingSkill
            && global.CurrentActingUnit != null;
    }

    private GlobalScript.BattleUnit SelectEnemyTarget(GlobalScript global, GlobalScript.BattleUnit enemy, System.Collections.Generic.List<GlobalScript.BattleUnit> alivePlayers)
    {
        var miamin = global.GetAliveMiamin();
        bool hasYouAreDone = DotGlobalManager.GetDot(enemy, DotGlobalManager.MiaminYouAreDoneStatusId) != null;
        if (hasYouAreDone && miamin != null && alivePlayers.Contains(miamin) && GlobalScript.GlobalRandom.NextDouble() < 0.7)
        {
            GD.Print($"【你丸了】{enemy.UnitName}更容易将迈阿密作为攻击目标");
            return miamin;
        }

        int randomTargetIndex = GlobalScript.GlobalRandom.Next(0, alivePlayers.Count);
        return alivePlayers[randomTargetIndex];
    }

    private void TryApplyMiaminRetaliatoryStack(GlobalScript global, GlobalScript.BattleUnit enemy, GlobalScript.BattleUnit target)
    {
        if (target == null || target.UnitName != "迈阿密")
        {
            return;
        }

        var youAreDone = DotGlobalManager.GetDot(enemy, DotGlobalManager.MiaminYouAreDoneStatusId);
        if (youAreDone == null)
        {
            return;
        }

        global.ApplyMiaminYouAreDone(youAreDone.SourceUnit ?? target, enemy, 1);
        GD.Print($"【你丸了】{enemy.UnitName}攻击迈阿密，自身额外叠加1层【你丸了】");
    }

    private void CheckBattleAndContinue(GlobalScript global, bool usedCoopAttack = false)
    {
        if (global.CheckBattleEnd())
        {
            OnBattleEnd(global);
            return;
        }
        EndCurrentPlayerAction(global, usedCoopAttack ? CoopAttackActionEndDelaySeconds : DefaultActionEndDelaySeconds);
    }

    private void FinishTurnConsumingAction(GlobalScript global, bool usedCoopAttack = false)
    {
        _isResolvingSkill = false;
        CheckBattleAndContinue(global, usedCoopAttack);
    }

    private void ReturnToCurrentPlayerTurnAfterUltimate(GlobalScript global, GlobalScript.BattleUnit caster)
    {
        if (global.CheckBattleEnd())
        {
            OnBattleEnd(global);
            return;
        }

        _isResolvingSkill = false;
        _currentPlayerHasActed = false;

        if (_skillsContainer != null)
        {
            _skillsContainer.Visible = false;
        }

        if (global.CurrentState == GlobalScript.BattleState.PlayerTurn && global.CurrentActingUnit == caster && _attrLabel != null)
        {
            _attrLabel.Visible = true;
            RefreshAttrLabel(caster);
        }
    }

    private void EndCurrentPlayerAction(GlobalScript global, float delaySeconds = DefaultActionEndDelaySeconds)
    {
        GetTree().CreateTimer(delaySeconds).Timeout += () =>
        {
            global.OnSinglePlayerActionEnd();
        };
    }

    private void OnBattleEnd(GlobalScript global)
    {
        _isResolvingSkill = false;
        if (GodotObject.IsInstanceValid(_notifierTween))
        {
            _notifierTween.Kill();
        }
        if (_skillNotifier != null)
        {
            _skillNotifier.Visible = false;
            _skillNotifier.Modulate = new Color(1f, 1f, 1f, 0f);
        }
        // 禁用所有按钮
        if (_skillButton != null) _skillButton.Disabled = true;
        if (_normalAttackBtn != null) _normalAttackBtn.Disabled = true;
        if (_specialSkillBtn != null) _specialSkillBtn.Disabled = true;
        if (_ultimateBtn != null) _ultimateBtn.Disabled = true;
        if (_skillsContainer != null) _skillsContainer.Visible = false;
        // ========== 核心修改8：战斗结束隐藏自定义提示框 ==========
        if (_skillTooltip != null) _skillTooltip.Hide();

        // 隐藏属性面板
        if (_attrLabel != null) _attrLabel.Visible = false;

        // 战斗结果提示
        if (_battleTipLabel != null)
        {
            if (global.GetAlivePlayers().Count == 0)
                _battleTipLabel.Text = "战斗失败，你的队伍被击败了！";
            else
                _battleTipLabel.Text = "🏆 恭喜！你的队伍击败了所有敌人，获得最终胜利！";
        }
    }

    public void HandleBattleEnd()
    {
        var global = GlobalScript.Instance;
        if (global == null)
        {
            return;
        }

        OnBattleEnd(global);
    }

    private void RefreshAttrLabel(GlobalScript.BattleUnit playerUnit)
    {
        if (_attrLabel == null || playerUnit == null) return;

        float finalCritRate = playerUnit.GetFinalCritRate() * 100;
        float finalCritDamage = playerUnit.GetFinalCritDamage() * 100;

        _attrLabel.Text =
$@"最大生命值：{playerUnit.HpMax:0}
当前生命值：{Mathf.Ceil(playerUnit.Hp):0}
攻击力：{playerUnit.Attack:0}
暴击率：{finalCritRate:0}%
暴击伤害：{finalCritDamage:0}%";
    }

    private GlobalScript.SkillData FindBattleSkill(GlobalScript.BattleUnit unit, GlobalScript.SkillType type)
    {
        return unit?.Skills.Find(skill =>
            skill != null &&
            skill.Type == type &&
            skill.Type != GlobalScript.SkillType.Passive &&
            skill.Type != GlobalScript.SkillType.EnhancedSpecial &&
            skill.IsSelectable &&
            skill.ShowInBattleUi);
    }

    private void UpdateSkillButton(Button button, GlobalScript.SkillData skill)
    {
        if (button == null)
        {
            return;
        }

        button.Visible = skill != null;
        button.Disabled = skill == null;
        button.Modulate = Colors.White;
        if (skill != null)
        {
            button.Text = skill.SkillName;
        }
    }

    private void UpdatePassiveButton(Button button, GlobalScript.SkillData skill)
    {
        if (button == null)
        {
            return;
        }

        button.Visible = skill != null;
        button.Disabled = true;
        button.Modulate = new Color(1f, 1f, 1f, 0.6f);
        if (skill != null)
        {
            button.Text = skill.SkillName;
        }
    }

    private void AppendChaosExtraAttrLabel(GlobalScript.BattleUnit playerUnit)
    {
        if (_attrLabel == null || playerUnit == null || GlobalScript.Instance == null)
        {
            return;
        }

        var builder = new StringBuilder(_attrLabel.Text ?? string.Empty);
        if (GlobalScript.Instance.IsChaosFateDice(playerUnit))
        {
            builder.AppendLine();
            builder.Append($"鏈夊簭鍊硷細{GlobalScript.Instance.GetChaosOrderValue(playerUnit)}/6");
        }

        if (playerUnit.ChaosTeamCritDamageBuff > 0f)
        {
            builder.AppendLine();
            float buffPercent = playerUnit.ChaosTeamCritDamageBuff * 100f;
            builder.Append("鍏ㄩ槦鏆村嚮浼ゅ鍔犳垚锛歿" + buffPercent.ToString("0") + "% (" + playerUnit.ChaosTeamCritDamageBuffTurns + "鍥炲悎)");
        }

        _attrLabel.Text = builder.ToString();
    }

    private bool TryGetPrimaryEnemyTarget(GlobalScript global, out GlobalScript.BattleUnit target)
    {
        target = null;
        var enemies = global.GetAliveEnemies();
        if (enemies.Count <= 0)
        {
            _isResolvingSkill = false;
            EndCurrentPlayerAction(global);
            return false;
        }

        target = enemies[0];
        return true;
    }

    private (float FinalDamage, bool IsCrit) RollCritDamage(GlobalScript.BattleUnit caster, float baseDamage)
    {
        float finalDamage = baseDamage;
        bool isCrit = false;
        float critRate = caster.GetFinalCritRate();
        float randomValue = (float)GlobalScript.GlobalRandom.NextDouble();
        if (randomValue <= critRate)
        {
            isCrit = true;
            finalDamage *= caster.GetFinalCritDamage();
        }

        return (finalDamage, isCrit);
    }

    private async System.Threading.Tasks.Task ExecuteChaosFateDiceNormalAttack(GlobalScript global, GlobalScript.BattleUnit caster)
    {
        if (!TryGetPrimaryEnemyTarget(global, out var target))
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

        var result = RollCritDamage(caster, caster.HpMax * 0.05f);
        global.TakeDamage(target, result.FinalDamage, isCrit: result.IsCrit, sourceUnit: caster);
        global.ApplySkillEnergy(caster, _currentNormal);
        global.GainFocus(1);

        string tip = $"{caster.UnitName}的{_currentNormal.SkillName}命中，对{target.UnitName}造成{result.FinalDamage:0.0}点伤害！";
        if (result.IsCrit) tip = "暴击！" + tip;
        if (_battleTipLabel != null) _battleTipLabel.Text = tip;

        await global.TriggerCoopAttack(target);
        FinishTurnConsumingAction(global, usedCoopAttack: true);
    }

    private async System.Threading.Tasks.Task ExecuteChaosFateDiceSpecial(GlobalScript global, GlobalScript.BattleUnit caster)
    {
        if (!TryGetPrimaryEnemyTarget(global, out var target))
        {
            return;
        }

        global.ApplySkillEnergy(caster, _currentSpecial);
        float selfCost = global.ApplyNonLethalCurrentHpCost(caster, 0.05f, _currentSpecial.SkillName);

        await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

        int orderValue = global.GetChaosOrderValue(caster);
        float enhancedChance = Mathf.Clamp(0.2f + orderValue * 0.1f, 0f, 1f);
        bool triggerEnhanced = (float)GlobalScript.GlobalRandom.NextDouble() <= enhancedChance;

        if (triggerEnhanced)
        {
            var enhancedSkill = global.FindSkillById(caster, _currentSpecial.EnhancedSkillId);
            if (enhancedSkill != null)
            {
                BroadcastSkillName(enhancedSkill.SkillName, true);
                await ExecuteChaosFateDiceEnhancedSpecial(global, caster, target, enhancedSkill, selfCost, enhancedChance);
                return;
            }
        }

        var result = RollCritDamage(caster, caster.HpMax * 0.20f);
        global.TakeDamage(target, result.FinalDamage, isCrit: result.IsCrit, sourceUnit: caster);

        string tip = $"{caster.UnitName}先消耗了{selfCost:0.0}点生命值，随后使用{_currentSpecial.SkillName}对{target.UnitName}造成{result.FinalDamage:0.0}点伤害！";
        tip += "\n当前有序值：" + orderValue + "/6，稳稳拿下！触发概率：" + (enhancedChance * 100).ToString("0") + "%";
        if (result.IsCrit) tip = "暴击！\n" + tip;
        if (_battleTipLabel != null) _battleTipLabel.Text = tip;

        await global.TriggerCoopAttack(target);
        FinishTurnConsumingAction(global, usedCoopAttack: true);
    }

    private async System.Threading.Tasks.Task ExecuteChaosFateDiceEnhancedSpecial(
        GlobalScript global,
        GlobalScript.BattleUnit caster,
        GlobalScript.BattleUnit target,
        GlobalScript.SkillData enhancedSkill,
        float selfCost,
        float enhancedChance)
    {
        await ToSignal(GetTree().CreateTimer(0.15f), "timeout");

        var result = RollCritDamage(caster, caster.HpMax * 0.40f);
        global.TakeDamage(target, result.FinalDamage, isCrit: result.IsCrit, sourceUnit: caster);

        int cleared = global.ClearChaosOrderValue(caster, enhancedSkill.SkillName);
        if (cleared > 0)
        {
            global.ApplyChaosTeamCritDamageBuff(cleared);
        }

        string tip = $"{caster.UnitName}的{_currentSpecial.SkillName}触发了{enhancedSkill.SkillName}！";
        tip += $"\n先消耗了{selfCost:0.0}点生命值，再对{target.UnitName}造成{result.FinalDamage:0.0}点伤害！";
        tip += "\n触发时概率：" + (enhancedChance * 100).ToString("0") + "%，清空了" + cleared + "点有序值。";
        if (cleared > 0)
        {
            tip += "\n全队获得" + (cleared * 10) + "%暴击伤害加成，持续3回合。";
        }
        if (result.IsCrit) tip = "暴击！\n" + tip;
        if (_battleTipLabel != null) _battleTipLabel.Text = tip;

        await global.TriggerCoopAttack(target);
        FinishTurnConsumingAction(global);
    }

    private async System.Threading.Tasks.Task ExecuteChaosFateDiceUltimate(GlobalScript global, GlobalScript.BattleUnit caster)
    {
        var allies = global.GetAlivePlayers();
        var enemies = global.GetAliveEnemies();
        if (enemies.Count <= 0)
        {
            _isResolvingSkill = false;
            return;
        }

        global.ApplySkillEnergy(caster, _currentUltimate);
        var tipBuilder = new StringBuilder();
        tipBuilder.AppendLine($"{caster.UnitName}释放{_currentUltimate.SkillName}！");

        foreach (var ally in allies)
        {
            float cost = global.ApplyNonLethalCurrentHpCost(ally, 0.15f, _currentUltimate.SkillName);
            tipBuilder.AppendLine($"{ally.UnitName}消耗了{cost:0.0}点当前生命值（最低保留1点）");
        }

        global.SetChaosOrderValue(caster, 6, _currentUltimate.SkillName);
        tipBuilder.AppendLine($"{caster.UnitName}立即获得6点有序值。");

        await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

        bool anyCrit = false;
        foreach (var enemy in enemies)
        {
            var result = RollCritDamage(caster, caster.HpMax * 0.25f);
            anyCrit |= result.IsCrit;
            global.TakeDamage(enemy, result.FinalDamage, isCrit: result.IsCrit, sourceUnit: caster);
            tipBuilder.AppendLine($"{enemy.UnitName}受到{result.FinalDamage:0.0}点伤害！");
        }

        if (_battleTipLabel != null)
        {
            _battleTipLabel.Text = anyCrit ? "暴击！\n" + tipBuilder : tipBuilder.ToString();
        }

        await global.TriggerCoopAttack(enemies[0]);
        ReturnToCurrentPlayerTurnAfterUltimate(global, caster);
    }
    #endregion
}

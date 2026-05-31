using Godot;

public partial class Player : Sprite2D
{
    [Export] private int _playerTeamIndex = 0;

    // 血条节点（路径和你的场景树完全匹配）
    private ProgressBar _hpActual;
    private ProgressBar _hpVisual;
    private Label _hpLabel;
    private GlobalScript.BattleUnit _bindUnit;
    private Tween _hpTween;
    private float _animationDuration = 0.5f; // 动画时长可自定义

    public override void _Ready()
    {
        // ✅ 路径完全匹配你的场景树，大小写、层级完全一致
        _hpActual = GetNodeOrNull<ProgressBar>("HpBar/hp_actual");
        _hpVisual = GetNodeOrNull<ProgressBar>("HpBar/hp_visual");
        _hpLabel = GetNodeOrNull<Label>("HpBar/playerhpLabel");

        // 报错检查，方便你定位问题
        if (_hpActual == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/hp_actual 节点！");
        if (_hpVisual == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/hp_visual 节点！");
        if (_hpLabel == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/playerhpLabel 节点！");

        BindUnitData();
    }

    public override void _Process(double delta)
    {
        if (_bindUnit == null)
        {
            BindUnitData();
            return;
        }

        // 同步实际血条数值
        if (_hpActual != null)
        {
            _hpActual.MaxValue = _bindUnit.HpMax;
            _hpActual.Value = _bindUnit.Hp;
        }

        // ✅ 血量文本显示：当前/最大，取整
        if (_hpLabel != null)
        {
            _hpLabel.Text = $"{Mathf.Ceil(_bindUnit.Hp)}/{Mathf.Ceil(_bindUnit.HpMax)}";
        }

        // 触发血条动画
        if (_hpVisual != null && _hpActual != null && !Mathf.IsEqualApprox((float)_hpVisual.Value, (float)_hpActual.Value))
        {
            PlayHpAnimation((float)_hpActual.Value);
        }

        // 死亡隐藏
        if (_bindUnit.IsDead)
        {
            Visible = false;
            if (_hpActual != null) _hpActual.Visible = false;
            if (_hpVisual != null) _hpVisual.Visible = false;
            if (_hpLabel != null) _hpLabel.Visible = false;
        }
        else
        {
            Visible = true;
            if (_hpVisual != null) _hpVisual.Visible = true;
            if (_hpLabel != null) _hpLabel.Visible = true;
        }
    }

    // 血条缓动动画
    private void PlayHpAnimation(float targetHp)
    {
        if (_hpVisual == null || _hpActual == null) return;

        // 停止旧动画避免冲突
        if (_hpTween != null && _hpTween.IsRunning())
        {
            _hpTween.Kill();
        }

        // 同步最大值
        _hpVisual.MaxValue = _hpActual.MaxValue;

        // 创建动画
        _hpTween = CreateTween();
        _hpTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _hpTween.TweenProperty(_hpVisual, "value", targetHp, _animationDuration);
    }

    // 外部调用更新血条
    public void UpdateHp()
    {
        // 动画在_Process自动触发，这里留空保证兼容
    }

    // 绑定战斗数据
    private void BindUnitData()
    {
        var global = GlobalScript.Instance;
        if (global == null || global.PlayerTeam.Count <= _playerTeamIndex) return;

        _bindUnit = global.PlayerTeam[_playerTeamIndex];
        _bindUnit.BindNode = this;

        // 加载角色贴图
        if (!string.IsNullOrEmpty(_bindUnit.TexturePath))
        {
            Texture = GD.Load<Texture2D>(_bindUnit.TexturePath);
        }

        GD.Print($"玩家【{_bindUnit.UnitName}】数据绑定完成，队伍索引：{_playerTeamIndex}");
    }
}
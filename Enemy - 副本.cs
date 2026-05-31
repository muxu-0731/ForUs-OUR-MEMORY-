using Godot;

public partial class Enemy : Sprite2D
{
    [Export] private int _enemyTeamIndex = 0;

    // 血条节点（路径完全匹配敌人场景结构）
    private ProgressBar _hpActual;
    private ProgressBar _hpVisual;
    private Label _hpLabel;
    private GlobalScript.BattleUnit _bindUnit;
    private Tween _hpTween;
    private float _animationDuration = 0.5f;

    public override void _Ready()
    {
        // ✅ 路径完全匹配敌人的节点结构
        _hpActual = GetNodeOrNull<ProgressBar>("hpbar/hp_actual");
        _hpVisual = GetNodeOrNull<ProgressBar>("hpbar/hp_visual");
        _hpLabel = GetNodeOrNull<Label>("hpbar/hplabel");

        // 报错检查
        if (_hpActual == null) GD.PrintErr("敌人：找不到 hpbar/hp_actual 节点！");
        if (_hpVisual == null) GD.PrintErr("敌人：找不到 hpbar/hp_visual 节点！");
        if (_hpLabel == null) GD.PrintErr("敌人：找不到 hpbar/hplabel 节点！");

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

        if (_hpTween != null && _hpTween.IsRunning())
        {
            _hpTween.Kill();
        }

        _hpVisual.MaxValue = _hpActual.MaxValue;

        _hpTween = CreateTween();
        _hpTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _hpTween.TweenProperty(_hpVisual, "value", targetHp, _animationDuration);
    }

    // 新敌人登场刷新数据
    public void RefreshEnemyData(GlobalScript.BattleUnit newEnemyData)
    {
        _bindUnit = newEnemyData;
        _bindUnit.BindNode = this;

        Visible = true;
        if (_hpVisual != null) _hpVisual.Visible = true;
        if (_hpLabel != null) _hpLabel.Visible = true;

        if (!string.IsNullOrEmpty(newEnemyData.TexturePath))
        {
            Texture = GD.Load<Texture2D>(newEnemyData.TexturePath);
        }

        GD.Print($"敌人节点刷新为：{newEnemyData.UnitName}");
    }

    // 外部调用更新血条
    public void UpdateHp()
    {
        // 动画在_Process自动触发
    }

    // 绑定战斗数据
    private void BindUnitData()
    {
        var global = GlobalScript.Instance;
        if (global == null || global.EnemyTeam.Count <= _enemyTeamIndex) return;

        _bindUnit = global.EnemyTeam[_enemyTeamIndex];
        _bindUnit.BindNode = this;

        if (!string.IsNullOrEmpty(_bindUnit.TexturePath))
        {
            Texture = GD.Load<Texture2D>(_bindUnit.TexturePath);
        }

        GD.Print($"敌人【{_bindUnit.UnitName}】数据绑定完成");
    }
}
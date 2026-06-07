using Godot;

public partial class Player : Sprite2D
{
    [Export] private int _playerTeamIndex = 0;

    private const int SpriteZIndex = 1;
    private const int HpBarZIndex = 2;
    private const float FixedHpBarWidth = 180f;
    private const float FixedHpBarHeight = 30f;
    private const int HpLabelFontSize = 16;
    private const float HpBarVerticalMargin = 10f;

    private Control _hpBarRoot;
    private ProgressBar _hpActual;
    private ProgressBar _hpVisual;
    private Label _hpLabel;
    private GlobalScript.BattleUnit _bindUnit;
    private Tween _hpTween;
    private float _animationDuration = 0.5f;
    private Vector2 _baseScale;

    public GlobalScript.BattleUnit BoundUnit => _bindUnit;

    public Rect2 HpBarGlobalRect
    {
        get
        {
            if (_hpBarRoot == null)
            {
                return new Rect2(GlobalPosition, Vector2.Zero);
            }

            return new Rect2(_hpBarRoot.GlobalPosition, new Vector2(FixedHpBarWidth, FixedHpBarHeight));
        }
    }

    public override void _Ready()
    {
        _baseScale = Scale;
        TextureFilter = TextureFilterEnum.Nearest;
        Centered = true;
        ZIndex = SpriteZIndex;
        ZAsRelative = false;

        _hpBarRoot = GetNodeOrNull<Control>("HpBar");
        _hpActual = GetNodeOrNull<ProgressBar>("HpBar/hp_actual");
        _hpVisual = GetNodeOrNull<ProgressBar>("HpBar/hp_visual");
        _hpLabel = GetNodeOrNull<Label>("HpBar/playerhpLabel");

        if (_hpBarRoot != null)
        {
            _hpBarRoot.TopLevel = false;
            _hpBarRoot.ZIndex = HpBarZIndex;
            _hpBarRoot.ZAsRelative = false;
            _hpBarRoot.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        }

        if (_hpBarRoot == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar 节点。");
        if (_hpActual == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/hp_actual 节点。");
        if (_hpVisual == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/hp_visual 节点。");
        if (_hpLabel == null) GD.PrintErr($"玩家{_playerTeamIndex}：找不到 HpBar/playerhpLabel 节点。");

        UpdateHpBarLayout();
        BindUnitData();
    }

    public override void _Process(double delta)
    {
        if (_bindUnit == null)
        {
            BindUnitData();
            return;
        }

        if (_hpActual != null)
        {
            _hpActual.MaxValue = _bindUnit.HpMax;
            _hpActual.Value = _bindUnit.Hp;
        }

        if (_hpLabel != null)
        {
            _hpLabel.Text = $"{Mathf.Ceil(_bindUnit.Hp)}/{Mathf.Ceil(_bindUnit.HpMax)}";
        }

        if (_hpVisual != null && _hpActual != null && !Mathf.IsEqualApprox((float)_hpVisual.Value, (float)_hpActual.Value))
        {
            PlayHpAnimation((float)_hpActual.Value);
        }

        UpdateHpBarPosition();

        if (_bindUnit.IsDead)
        {
            Visible = false;
            if (_hpBarRoot != null) _hpBarRoot.Visible = false;
            if (_hpActual != null) _hpActual.Visible = false;
            if (_hpVisual != null) _hpVisual.Visible = false;
            if (_hpLabel != null) _hpLabel.Visible = false;
        }
        else
        {
            Visible = true;
            if (_hpBarRoot != null) _hpBarRoot.Visible = true;
            if (_hpVisual != null) _hpVisual.Visible = true;
            if (_hpLabel != null) _hpLabel.Visible = true;
        }
    }

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

    public void UpdateHp()
    {
    }

    private void BindUnitData()
    {
        var global = GlobalScript.Instance;
        if (global == null || global.PlayerTeam.Count <= _playerTeamIndex) return;

        _bindUnit = global.PlayerTeam[_playerTeamIndex];
        _bindUnit.BindNode = this;

        if (!string.IsNullOrEmpty(_bindUnit.TexturePath))
        {
            var texture = GD.Load<Texture2D>(_bindUnit.TexturePath);
            if (texture == null)
            {
                GD.PrintErr($"玩家【{_bindUnit.UnitName}】贴图加载失败：{_bindUnit.TexturePath}");
            }
            else
            {
                Texture = texture;
                ApplyTextureDisplay(texture);
                UpdateHpBarLayout();
                UpdateHpBarPosition();
            }
        }

        global.RefreshCurrentTurnIndicator();
        GD.Print($"玩家【{_bindUnit.UnitName}】数据绑定完成，队伍索引：{_playerTeamIndex}");
    }

    private void ApplyTextureDisplay(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        Vector2 textureSize = texture.GetSize();
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            GD.PrintErr($"玩家【{_bindUnit?.UnitName}】贴图尺寸异常：{textureSize}");
            Scale = _baseScale;
            return;
        }

        Scale = _baseScale;
    }

    private void UpdateHpBarPosition()
    {
        if (_hpBarRoot == null || Texture == null)
        {
            return;
        }

        Vector2 safeScale = GetSafeAbsScale();
        Vector2 textureSize = Texture.GetSize();

        _hpBarRoot.Scale = new Vector2(1f / safeScale.X, 1f / safeScale.Y);
        _hpBarRoot.Position = new Vector2(
            -FixedHpBarWidth * 0.5f / safeScale.X,
            -textureSize.Y * 0.5f - (HpBarVerticalMargin + FixedHpBarHeight) / safeScale.Y);
    }

    private void UpdateHpBarLayout()
    {
        if (_hpBarRoot == null || _hpActual == null || _hpVisual == null || _hpLabel == null)
        {
            return;
        }

        _hpBarRoot.Size = new Vector2(FixedHpBarWidth, FixedHpBarHeight);

        _hpActual.Position = Vector2.Zero;
        _hpActual.Size = _hpBarRoot.Size;
        _hpActual.ShowPercentage = false;

        _hpVisual.Position = Vector2.Zero;
        _hpVisual.Size = _hpBarRoot.Size;
        _hpVisual.ShowPercentage = false;

        _hpLabel.Position = Vector2.Zero;
        _hpLabel.Size = _hpBarRoot.Size;
        _hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _hpLabel.VerticalAlignment = VerticalAlignment.Center;
        _hpLabel.ClipText = true;
        _hpLabel.AddThemeFontSizeOverride("font_size", HpLabelFontSize);
    }

    private Vector2 GetSafeAbsScale()
    {
        return new Vector2(
            Mathf.Max(Mathf.Abs(Scale.X), 0.001f),
            Mathf.Max(Mathf.Abs(Scale.Y), 0.001f));
    }
}

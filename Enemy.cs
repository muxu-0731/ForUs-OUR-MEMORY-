using Godot;

public partial class Enemy : Sprite2D
{
    [Export] private int _enemyTeamIndex = 0;

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
    private BattleHpBarController _hpBarController;
    private float _lastDisplayedHp = -1f;
    private float _lastDisplayedHpMax = -1f;
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

        _hpBarRoot = GetNodeOrNull<Control>("hpbar");
        _hpActual = GetNodeOrNull<ProgressBar>("hpbar/hp_actual");
        _hpVisual = GetNodeOrNull<ProgressBar>("hpbar/hp_visual");
        _hpLabel = GetNodeOrNull<Label>("hpbar/hplabel");

        if (_hpBarRoot != null)
        {
            _hpBarRoot.TopLevel = false;
            _hpBarRoot.ZIndex = HpBarZIndex;
            _hpBarRoot.ZAsRelative = false;
            _hpBarRoot.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        }

        if (_hpBarRoot == null) GD.PrintErr("Enemy: missing hpbar node.");
        if (_hpActual == null) GD.PrintErr("Enemy: missing hpbar/hp_actual node.");
        if (_hpVisual == null) GD.PrintErr("Enemy: missing hpbar/hp_visual node.");
        if (_hpLabel == null) GD.PrintErr("Enemy: missing hpbar/hplabel node.");

        UpdateHpBarLayout();
        InitializeHpBarController();
        BindUnitData();
    }

    public override void _Process(double delta)
    {
        if (_bindUnit == null)
        {
            BindUnitData();
            return;
        }

        RefreshAttachedUi();

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
            if (_hpActual != null) _hpActual.Visible = true;
            if (_hpVisual != null) _hpVisual.Visible = true;
            if (_hpLabel != null) _hpLabel.Visible = true;
        }
    }

    private void InitializeHpBarController()
    {
        if (_hpBarRoot == null || _hpActual == null || _hpVisual == null)
        {
            return;
        }

        _hpBarController = new BattleHpBarController(
            this,
            _hpBarRoot,
            _hpActual,
            _hpVisual,
            _hpLabel,
            BattleHpBarController.CreateEnemyPalette());
    }

    public void RefreshEnemyData(GlobalScript.BattleUnit newEnemyData)
    {
        _bindUnit = newEnemyData;
        _bindUnit.BindNode = this;

        Visible = true;
        if (_hpActual != null) _hpActual.Visible = true;
        if (_hpVisual != null) _hpVisual.Visible = true;
        if (_hpLabel != null) _hpLabel.Visible = true;

        if (!string.IsNullOrEmpty(newEnemyData.TexturePath))
        {
            Texture = GD.Load<Texture2D>(newEnemyData.TexturePath);
            UpdateHpBarLayout();
            RefreshAttachedUi();
        }

        SyncHpBarImmediate();
        UpdateBoundUnitWorldAnchors();

        GlobalScript.Instance?.RefreshCurrentTurnIndicator();
        Main.Instance?.RefreshBattleLayout();
        GD.Print($"Enemy node refreshed: {newEnemyData.UnitName}");
    }

    public void UpdateHp()
    {
        if (_bindUnit == null || _hpBarController == null)
        {
            return;
        }

        float hpMax = Mathf.Max(1f, _bindUnit.HpMax);
        float newHp = Mathf.Clamp(_bindUnit.Hp, 0f, hpMax);

        if (_lastDisplayedHp < 0f || _lastDisplayedHpMax <= 0f)
        {
            _hpBarController.ApplyImmediate(newHp, hpMax);
        }
        else
        {
            _hpBarController.AnimateHpChange(_lastDisplayedHp, newHp, hpMax);
        }

        _lastDisplayedHp = newHp;
        _lastDisplayedHpMax = hpMax;
    }

    private void BindUnitData()
    {
        var global = GlobalScript.Instance;
        if (global == null || global.EnemyTeam.Count <= _enemyTeamIndex)
        {
            return;
        }

        _bindUnit = global.EnemyTeam[_enemyTeamIndex];
        _bindUnit.BindNode = this;

        if (!string.IsNullOrEmpty(_bindUnit.TexturePath))
        {
            Texture = GD.Load<Texture2D>(_bindUnit.TexturePath);
            UpdateHpBarLayout();
            RefreshAttachedUi();
        }

        SyncHpBarImmediate();
        UpdateBoundUnitWorldAnchors();

        global.RefreshCurrentTurnIndicator();
        GD.Print($"Enemy bound: {_bindUnit.UnitName}");
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

    public void RefreshAttachedUi()
    {
        UpdateHpBarPosition();

        if (GetNodeOrNull<EnergyBar>("EnergyBar") is EnergyBar energyBar)
        {
            energyBar.RefreshLayout();
        }

        UpdateBoundUnitWorldAnchors();
    }

    public Rect2 GetDisplayBoundsLocal()
    {
        if (Texture == null)
        {
            return new Rect2(Vector2.Zero, Vector2.Zero);
        }

        Vector2 textureSize = Texture.GetSize();
        Vector2 drawOrigin = -textureSize * 0.5f + Offset;
        return new Rect2(drawOrigin, textureSize);
    }

    public Rect2 GetDisplayBoundsGlobal()
    {
        return BattleViewUtility.TransformRectToGlobal(this, GetDisplayBoundsLocal());
    }

    public Vector2 GetFootAnchorLocal()
    {
        return BattleViewUtility.GetBottomCenter(GetDisplayBoundsLocal());
    }

    public Vector2 GetFootAnchorGlobal()
    {
        return ToGlobal(GetFootAnchorLocal());
    }

    public Vector2 GetSelectionAnchorGlobal()
    {
        Rect2 bounds = GetDisplayBoundsGlobal();
        return new Vector2(bounds.GetCenter().X, bounds.End.Y - Mathf.Min(18f, bounds.Size.Y * 0.06f));
    }

    public void SetFormationFootPosition(Vector2 worldFootPosition)
    {
        Vector2 currentFoot = GetFootAnchorGlobal();
        GlobalPosition += worldFootPosition - currentFoot;
        RefreshAttachedUi();
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

    private void SyncHpBarImmediate()
    {
        if (_bindUnit == null || _hpBarController == null)
        {
            return;
        }

        float hpMax = Mathf.Max(1f, _bindUnit.HpMax);
        float hp = Mathf.Clamp(_bindUnit.Hp, 0f, hpMax);
        _hpBarController.ApplyImmediate(hp, hpMax);
        _lastDisplayedHp = hp;
        _lastDisplayedHpMax = hpMax;
    }

    private Vector2 GetSafeAbsScale()
    {
        return new Vector2(
            Mathf.Max(Mathf.Abs(Scale.X), 0.001f),
            Mathf.Max(Mathf.Abs(Scale.Y), 0.001f));
    }

    private void UpdateBoundUnitWorldAnchors()
    {
        if (_bindUnit == null)
        {
            return;
        }

        _bindUnit.WorldPosition = GlobalPosition;
        _bindUnit.AnchorWorldPosition = GetFootAnchorGlobal();
    }
}

using Godot;

public partial class Player : Sprite2D
{
    [Export] private int _playerTeamIndex = 0;

    private const int SpriteZIndex = 1;
    private const float TargetPlayerVisualHeight = 132f;
    private const float MinPlayerVisualScale = 0.05f;
    private const float MaxPlayerVisualScale = 3.0f;
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
    private Vector2 _baseOffset;
    private Vector2 _attachedUiScreenOffset = Vector2.Zero;
    private Rect2 _visibleContentPixelBounds = new Rect2();
    private bool _hasVisibleContentBounds;

    public GlobalScript.BattleUnit BoundUnit => _bindUnit;
    public int PlayerTeamIndex => _playerTeamIndex;
    public Vector2 AttachedUiScreenOffset => _attachedUiScreenOffset;

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
        _baseOffset = Offset;
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

        if (_hpBarRoot == null) GD.PrintErr($"Player {_playerTeamIndex}: missing HpBar node.");
        if (_hpActual == null) GD.PrintErr($"Player {_playerTeamIndex}: missing HpBar/hp_actual node.");
        if (_hpVisual == null) GD.PrintErr($"Player {_playerTeamIndex}: missing HpBar/hp_visual node.");
        if (_hpLabel == null) GD.PrintErr($"Player {_playerTeamIndex}: missing HpBar/playerhpLabel node.");

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
            BattleHpBarController.CreatePlayerPalette());
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
        if (global == null || global.PlayerTeam.Count <= _playerTeamIndex)
        {
            return;
        }

        _bindUnit = global.PlayerTeam[_playerTeamIndex];
        _bindUnit.BindNode = this;

        if (!string.IsNullOrEmpty(_bindUnit.TexturePath))
        {
            var texture = GD.Load<Texture2D>(_bindUnit.TexturePath);
            if (texture == null)
            {
                GD.PrintErr($"Player [{_bindUnit.UnitName}] failed to load texture: {_bindUnit.TexturePath}");
            }
            else
            {
                Texture = texture;
                ApplyTextureDisplay(texture);
                UpdateHpBarLayout();
                RefreshAttachedUi();
            }
        }

        SyncHpBarImmediate();
        UpdateBoundUnitWorldAnchors();

        global.RefreshCurrentTurnIndicator();
        GD.Print($"Player [{_bindUnit.UnitName}] bound successfully at team index {_playerTeamIndex}.");
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
            GD.PrintErr($"Player [{_bindUnit?.UnitName}] texture size is invalid: {textureSize}");
            Scale = _baseScale;
            Offset = _baseOffset;
            CacheFullTextureBounds(textureSize);
            return;
        }

        Offset = _baseOffset;

        if (!BattleSpriteDisplayUtility.TryGetVisibleContentMetrics(texture, out var visibleMetrics))
        {
            GD.PrintErr($"Player [{_bindUnit?.UnitName}] visible sprite bounds not found, fallback to base scale.");
            Scale = _baseScale;
            CacheFullTextureBounds(textureSize);
            return;
        }

        float uniformScale = BattleSpriteDisplayUtility.CalculateUniformScale(
            visibleMetrics.VisibleHeight,
            TargetPlayerVisualHeight,
            MinPlayerVisualScale,
            MaxPlayerVisualScale);

        float scaleSignX = _baseScale.X < 0f ? -1f : 1f;
        float scaleSignY = _baseScale.Y < 0f ? -1f : 1f;

        Scale = new Vector2(scaleSignX * uniformScale, scaleSignY * uniformScale);
        Offset = _baseOffset + new Vector2(0f, visibleMetrics.BottomInset);
        CacheVisibleBounds(visibleMetrics);
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

    public void SetFormationFootPosition(Vector2 worldFootPosition)
    {
        Vector2 currentFoot = GetFootAnchorGlobal();
        GlobalPosition += worldFootPosition - currentFoot;
        RefreshAttachedUi();
    }

    public void SetAttachedUiScreenOffset(Vector2 screenOffset)
    {
        _attachedUiScreenOffset = screenOffset;
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
        return new Vector2(bounds.GetCenter().X, bounds.End.Y - Mathf.Min(12f, bounds.Size.Y * 0.08f));
    }

    private void UpdateHpBarPosition()
    {
        if (_hpBarRoot == null || Texture == null)
        {
            return;
        }

        Vector2 safeScale = GetSafeAbsScale();
        Rect2 displayBounds = GetDisplayBoundsLocal();
        float displayCenterX = displayBounds.Position.X + displayBounds.Size.X * 0.5f;

        _hpBarRoot.Scale = new Vector2(1f / safeScale.X, 1f / safeScale.Y);
        _hpBarRoot.Position = new Vector2(
            displayCenterX - FixedHpBarWidth * 0.5f / safeScale.X,
            displayBounds.Position.Y - (HpBarVerticalMargin + FixedHpBarHeight) / safeScale.Y)
            + new Vector2(_attachedUiScreenOffset.X / safeScale.X, _attachedUiScreenOffset.Y / safeScale.Y);
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

    public Rect2 GetDisplayBoundsLocal()
    {
        if (Texture == null)
        {
            return new Rect2(Vector2.Zero, Vector2.Zero);
        }

        Vector2 textureSize = Texture.GetSize();
        Rect2 pixelBounds = _hasVisibleContentBounds
            ? _visibleContentPixelBounds
            : new Rect2(Vector2.Zero, textureSize);

        Vector2 drawOrigin = -textureSize * 0.5f + Offset;
        return new Rect2(drawOrigin + pixelBounds.Position, pixelBounds.Size);
    }

    private void CacheVisibleBounds(BattleSpriteDisplayUtility.VisibleContentMetrics visibleMetrics)
    {
        _visibleContentPixelBounds = new Rect2(
            visibleMetrics.PixelBounds.Position.X,
            visibleMetrics.PixelBounds.Position.Y,
            visibleMetrics.PixelBounds.Size.X,
            visibleMetrics.PixelBounds.Size.Y);
        _hasVisibleContentBounds = true;
    }

    private void CacheFullTextureBounds(Vector2 textureSize)
    {
        _visibleContentPixelBounds = new Rect2(Vector2.Zero, textureSize);
        _hasVisibleContentBounds = false;
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
}

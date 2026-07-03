using Godot;

public sealed class BattleHpBarController
{
    public readonly struct Palette
    {
        public Palette(
            Color mainFill,
            Color damageGhostFill,
            Color healPreviewFill,
            Color backgroundFill,
            Color borderColor,
            Color damageFlashColor,
            Color healFlashColor,
            Color labelTint)
        {
            MainFill = mainFill;
            DamageGhostFill = damageGhostFill;
            HealPreviewFill = healPreviewFill;
            BackgroundFill = backgroundFill;
            BorderColor = borderColor;
            DamageFlashColor = damageFlashColor;
            HealFlashColor = healFlashColor;
            LabelTint = labelTint;
        }

        public Color MainFill { get; }
        public Color DamageGhostFill { get; }
        public Color HealPreviewFill { get; }
        public Color BackgroundFill { get; }
        public Color BorderColor { get; }
        public Color DamageFlashColor { get; }
        public Color HealFlashColor { get; }
        public Color LabelTint { get; }
    }

    private const float DamageMainDuration = 0.12f;
    private const float DamageDelay = 0.30f;
    private const float DamageGhostDuration = 0.52f;
    private const float HealDuration = 0.32f;
    private const float HealPreviewFadeDuration = 0.24f;
    private const float FlashDuration = 0.16f;
    private const float LabelPulseDuration = 0.18f;
    private const float MinVisibleDelta = 0.5f;
    private const float BarCornerRadius = 6.0f;
    private const int BorderWidth = 1;

    private readonly Node _owner;
    private readonly Control _root;
    private readonly Panel _backgroundPanel;
    private readonly ProgressBar _mainBar;
    private readonly ProgressBar _damageGhostBar;
    private readonly ProgressBar _healPreviewBar;
    private readonly ColorRect _flashOverlay;
    private readonly Label _label;
    private readonly Palette _palette;

    private Tween _mainTween;
    private Tween _damageGhostTween;
    private Tween _healPreviewTween;
    private Tween _flashTween;
    private Tween _labelTween;
    private float _lastHp = -1f;
    private float _lastHpMax = -1f;

    public BattleHpBarController(
        Node owner,
        Control root,
        ProgressBar mainBar,
        ProgressBar damageGhostBar,
        Label label,
        Palette palette)
    {
        _owner = owner;
        _root = root;
        _mainBar = mainBar;
        _damageGhostBar = damageGhostBar;
        _label = label;
        _palette = palette;

        _backgroundPanel = new Panel
        {
            Name = "hp_background",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _healPreviewBar = new ProgressBar
        {
            Name = "hp_preview",
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _flashOverlay = new ColorRect
        {
            Name = "HpFlashOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = palette.DamageFlashColor,
            Visible = false
        };

        ConfigureVisualTree();
        ApplyTheme();
    }

    public static Palette CreatePlayerPalette()
    {
        return new Palette(
            new Color(0.30f, 0.96f, 0.80f, 1.0f),
            new Color(1.0f, 0.88f, 0.86f, 0.98f),
            new Color(0.86f, 1.0f, 0.97f, 0.98f),
            new Color(0.07f, 0.11f, 0.16f, 0.78f),
            new Color(0.90f, 0.98f, 1.0f, 0.54f),
            new Color(1.0f, 0.82f, 0.82f, 0.34f),
            new Color(0.82f, 1.0f, 0.94f, 0.30f),
            new Color(0.92f, 1.0f, 0.98f, 1.0f));
    }

    public static Palette CreateEnemyPalette()
    {
        return new Palette(
            new Color(1.0f, 0.36f, 0.46f, 1.0f),
            new Color(1.0f, 0.90f, 0.82f, 0.96f),
            new Color(1.0f, 0.70f, 0.78f, 0.92f),
            new Color(0.10f, 0.08f, 0.12f, 0.74f),
            new Color(1.0f, 0.88f, 0.92f, 0.42f),
            new Color(1.0f, 0.82f, 0.84f, 0.30f),
            new Color(1.0f, 0.72f, 0.78f, 0.26f),
            new Color(1.0f, 0.94f, 0.96f, 1.0f));
    }

    public void ApplyImmediate(float hp, float hpMax)
    {
        float clampedMaxHp = Mathf.Max(1f, hpMax);
        float clampedHp = Mathf.Clamp(hp, 0f, clampedMaxHp);

        KillTweens();
        ConfigureBarState(_mainBar, clampedHp, clampedMaxHp, 1f);
        ConfigureBarState(_damageGhostBar, clampedHp, clampedMaxHp, 1f);
        ConfigureBarState(_healPreviewBar, clampedHp, clampedMaxHp, 0f);
        _backgroundPanel.Visible = true;
        _healPreviewBar.Visible = false;
        _flashOverlay.Visible = false;
        _flashOverlay.Modulate = new Color(1f, 1f, 1f, 0f);
        ResetLabelAppearance();
        UpdateLabel(clampedHp, clampedMaxHp);

        _lastHp = clampedHp;
        _lastHpMax = clampedMaxHp;
    }

    public void AnimateHpChange(float oldHp, float newHp, float hpMax)
    {
        float clampedMaxHp = Mathf.Max(1f, hpMax);
        float clampedOldHp = Mathf.Clamp(oldHp, 0f, clampedMaxHp);
        float clampedNewHp = Mathf.Clamp(newHp, 0f, clampedMaxHp);

        if (_lastHp < 0f || _lastHpMax <= 0f)
        {
            ApplyImmediate(clampedNewHp, clampedMaxHp);
            return;
        }

        if (Mathf.IsEqualApprox(clampedOldHp, clampedNewHp) && Mathf.IsEqualApprox(_lastHpMax, clampedMaxHp))
        {
            ApplyImmediate(clampedNewHp, clampedMaxHp);
            return;
        }

        float currentMainHp = ClampBarValue(_mainBar, clampedMaxHp);
        float currentGhostHp = ClampBarValue(_damageGhostBar, clampedMaxHp);
        float currentPreviewHp = ClampBarValue(_healPreviewBar, clampedMaxHp);

        KillTweens();
        UpdateLabel(clampedNewHp, clampedMaxHp);

        ConfigureBarState(_mainBar, currentMainHp, clampedMaxHp, 1f);
        ConfigureBarState(_damageGhostBar, currentGhostHp, clampedMaxHp, 1f);
        ConfigureBarState(_healPreviewBar, currentPreviewHp, clampedMaxHp, 0f);

        float visualStartHp = Mathf.Clamp(currentMainHp, 0f, clampedMaxHp);
        float visibleDelta = Mathf.Abs(clampedNewHp - visualStartHp);

        if (clampedNewHp < visualStartHp)
        {
            PlayDamageAnimation(visualStartHp, clampedNewHp, clampedMaxHp, visibleDelta);
        }
        else if (clampedNewHp > visualStartHp)
        {
            PlayHealAnimation(visualStartHp, clampedNewHp, clampedMaxHp, visibleDelta);
        }
        else
        {
            ApplyImmediate(clampedNewHp, clampedMaxHp);
        }

        _lastHp = clampedNewHp;
        _lastHpMax = clampedMaxHp;
    }

    private void PlayDamageAnimation(float startHp, float targetHp, float hpMax, float visibleDelta)
    {
        ConfigureBarState(_mainBar, startHp, hpMax, 1f);
        ConfigureBarState(_damageGhostBar, Mathf.Max(startHp, ClampBarValue(_damageGhostBar, hpMax)), hpMax, 1f);
        ConfigureBarState(_healPreviewBar, targetHp, hpMax, 0f);
        _healPreviewBar.Visible = false;

        _mainTween = CreateTween();
        _mainTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _mainTween.TweenProperty(_mainBar, "value", targetHp, DamageMainDuration);

        _damageGhostTween = CreateTween();
        _damageGhostTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _damageGhostTween.TweenInterval(DamageDelay);
        _damageGhostTween.TweenProperty(_damageGhostBar, "value", targetHp, DamageGhostDuration);

        TriggerFlash(_palette.DamageFlashColor, visibleDelta);
        PulseLabel(_palette.DamageGhostFill);
    }

    private void PlayHealAnimation(float startHp, float targetHp, float hpMax, float visibleDelta)
    {
        ConfigureBarState(_mainBar, startHp, hpMax, 1f);
        ConfigureBarState(_damageGhostBar, startHp, hpMax, 1f);
        ConfigureBarState(_healPreviewBar, targetHp, hpMax, 1f);
        _healPreviewBar.Visible = true;

        _mainTween = CreateTween();
        _mainTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _mainTween.TweenProperty(_mainBar, "value", targetHp, HealDuration);

        _healPreviewTween = CreateTween();
        _healPreviewTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _healPreviewTween.TweenProperty(_healPreviewBar, "modulate:a", 1f, FlashDuration);
        _healPreviewTween.TweenInterval(Mathf.Max(0.05f, HealDuration - FlashDuration));
        _healPreviewTween.TweenProperty(_healPreviewBar, "modulate:a", 0f, HealPreviewFadeDuration);
        _healPreviewTween.TweenCallback(Callable.From(() =>
        {
            if (_healPreviewBar != null)
            {
                _healPreviewBar.Visible = false;
                _healPreviewBar.Value = targetHp;
            }
        }));

        TriggerFlash(_palette.HealFlashColor, visibleDelta);
        PulseLabel(_palette.HealPreviewFill);
    }

    private void TriggerFlash(Color flashColor, float visibleDelta)
    {
        if (visibleDelta < MinVisibleDelta)
        {
            return;
        }

        if (_flashOverlay == null)
        {
            return;
        }

        _flashOverlay.Visible = true;
        _flashOverlay.Color = flashColor;
        _flashOverlay.Modulate = new Color(1f, 1f, 1f, 0f);

        _flashTween = CreateTween();
        _flashTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _flashTween.TweenProperty(_flashOverlay, "modulate:a", 1f, FlashDuration);
        _flashTween.TweenProperty(_flashOverlay, "modulate:a", 0f, FlashDuration);
        _flashTween.TweenCallback(Callable.From(() =>
        {
            if (_flashOverlay != null)
            {
                _flashOverlay.Visible = false;
            }
        }));
    }

    private void PulseLabel(Color pulseColor)
    {
        if (_label == null)
        {
            return;
        }

        KillTween(ref _labelTween);

        _label.Modulate = pulseColor;
        _labelTween = CreateTween();
        _labelTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _labelTween.TweenProperty(_label, "modulate", _palette.LabelTint, LabelPulseDuration);
    }

    private void ConfigureVisualTree()
    {
        _backgroundPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _backgroundPanel.OffsetLeft = 0f;
        _backgroundPanel.OffsetTop = 0f;
        _backgroundPanel.OffsetRight = 0f;
        _backgroundPanel.OffsetBottom = 0f;

        ConfigureBarLayout(_mainBar);
        ConfigureBarLayout(_damageGhostBar);
        ConfigureBarLayout(_healPreviewBar);

        _flashOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _flashOverlay.OffsetLeft = 0f;
        _flashOverlay.OffsetTop = 0f;
        _flashOverlay.OffsetRight = 0f;
        _flashOverlay.OffsetBottom = 0f;

        _root.AddChild(_backgroundPanel);
        _root.AddChild(_healPreviewBar);
        _root.AddChild(_flashOverlay);

        _root.MoveChild(_backgroundPanel, 0);
        _root.MoveChild(_damageGhostBar, 1);
        _root.MoveChild(_healPreviewBar, 2);
        _root.MoveChild(_mainBar, 3);
        _root.MoveChild(_flashOverlay, 4);
        if (_label != null)
        {
            _root.MoveChild(_label, 5);
        }
    }

    private void ApplyTheme()
    {
        _backgroundPanel.AddThemeStyleboxOverride("panel", CreateBackgroundStyle(_palette.BackgroundFill, _palette.BorderColor));

        _mainBar.AddThemeStyleboxOverride("fill", CreateFillStyle(_palette.MainFill));
        _mainBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());

        _damageGhostBar.AddThemeStyleboxOverride("fill", CreateFillStyle(_palette.DamageGhostFill));
        _damageGhostBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());

        _healPreviewBar.AddThemeStyleboxOverride("fill", CreateFillStyle(_palette.HealPreviewFill));
        _healPreviewBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());

        ResetLabelAppearance();
    }

    private void ConfigureBarLayout(ProgressBar bar)
    {
        if (bar == null)
        {
            return;
        }

        bar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bar.OffsetLeft = 0f;
        bar.OffsetTop = 0f;
        bar.OffsetRight = 0f;
        bar.OffsetBottom = 0f;
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        bar.ShowPercentage = false;
        bar.MouseFilter = Control.MouseFilterEnum.Ignore;
        bar.Visible = true;
    }

    private void ConfigureBarState(ProgressBar bar, float value, float maxValue, float alpha)
    {
        if (bar == null)
        {
            return;
        }

        bar.MaxValue = Mathf.Max(1f, maxValue);
        bar.Value = Mathf.Clamp(value, 0f, maxValue);
        bar.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private float ClampBarValue(ProgressBar bar, float maxValue)
    {
        if (bar == null)
        {
            return 0f;
        }

        return Mathf.Clamp((float)bar.Value, 0f, Mathf.Max(1f, maxValue));
    }

    private void UpdateLabel(float hp, float hpMax)
    {
        if (_label == null)
        {
            return;
        }

        _label.Text = $"{Mathf.Ceil(hp)}/{Mathf.Ceil(hpMax)}";
    }

    private void ResetLabelAppearance()
    {
        if (_label == null)
        {
            return;
        }

        _label.Modulate = _palette.LabelTint;
    }

    private StyleBoxFlat CreateFillStyle(Color fillColor)
    {
        return new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusTopLeft = (int)BarCornerRadius,
            CornerRadiusTopRight = (int)BarCornerRadius,
            CornerRadiusBottomLeft = (int)BarCornerRadius,
            CornerRadiusBottomRight = (int)BarCornerRadius,
            ShadowColor = new Color(fillColor.R, fillColor.G, fillColor.B, 0.28f),
            ShadowSize = 3
        };
    }

    private StyleBoxFlat CreateBackgroundStyle(Color backgroundColor, Color borderColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = borderColor,
            BorderWidthLeft = BorderWidth,
            BorderWidthTop = BorderWidth,
            BorderWidthRight = BorderWidth,
            BorderWidthBottom = BorderWidth,
            CornerRadiusTopLeft = (int)BarCornerRadius,
            CornerRadiusTopRight = (int)BarCornerRadius,
            CornerRadiusBottomLeft = (int)BarCornerRadius,
            CornerRadiusBottomRight = (int)BarCornerRadius
        };
    }

    private Tween CreateTween()
    {
        return _owner?.CreateTween();
    }

    private void KillTweens()
    {
        KillTween(ref _mainTween);
        KillTween(ref _damageGhostTween);
        KillTween(ref _healPreviewTween);
        KillTween(ref _flashTween);
        KillTween(ref _labelTween);
    }

    private void KillTween(ref Tween tween)
    {
        if (GodotObject.IsInstanceValid(tween))
        {
            tween.Kill();
        }

        tween = null;
    }
}

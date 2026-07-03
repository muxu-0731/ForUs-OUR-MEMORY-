using Godot;

public partial class EnergyBar : Control
{
    private const int EnergyBarZIndex = 3;
    private const float EnergyMargin = 6f;
    private const float BarWidth = 12f;
    private const float BarHeight = 70f;
    private const float TextGap = 6f;
    private const int EnergyTextFontSize = 14;

    private static readonly Color FillColor = new Color("#FFD700");
    private static readonly Color BackgroundColor = new Color("#333333");
    private static readonly Color BorderColor = new Color("#FFF3B0");
    private static readonly Color TextColor = new Color("#FFF7CC");

    private int _currentEnergy;
    private int _maxEnergy;
    private bool _missingBindLogged;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = false;
        ZIndex = EnergyBarZIndex;
        ZAsRelative = false;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Size = new Vector2(72f, 70f);
        CustomMinimumSize = Size;
        Visible = false;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (GetParent() is not Node2D)
        {
            if (!_missingBindLogged)
            {
                GD.PrintErr("EnergyBar bind failed: parent node is not Node2D.");
                _missingBindLogged = true;
            }

            Visible = false;
            return;
        }

        var unit = ResolveBoundUnit();
        if (unit == null)
        {
            Visible = false;
            return;
        }

        if (unit.MaxEnergy <= 0 || unit.IsDead)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _missingBindLogged = false;

        int maxEnergy = Mathf.Max(0, unit.MaxEnergy);
        int currentEnergy = Mathf.Clamp(unit.CurrentEnergy, 0, maxEnergy);
        if (currentEnergy != unit.CurrentEnergy)
        {
            GD.PrintErr($"EnergyBar detected out-of-range energy for {unit.UnitName}: {unit.CurrentEnergy}/{maxEnergy} -> {currentEnergy}/{maxEnergy}");
            unit.CurrentEnergy = currentEnergy;
        }

        if (_currentEnergy != currentEnergy || _maxEnergy != maxEnergy)
        {
            _currentEnergy = currentEnergy;
            _maxEnergy = maxEnergy;
            QueueRedraw();
        }

        UpdateEnergyBarLayout();
    }

    public void RefreshLayout()
    {
        if (!IsInsideTree())
        {
            return;
        }

        UpdateEnergyBarLayout();
    }

    public override void _Draw()
    {
        if (_maxEnergy <= 0)
        {
            return;
        }

        var barRect = new Rect2(Vector2.Zero, new Vector2(BarWidth, BarHeight));
        DrawRect(barRect, BackgroundColor);
        DrawRect(barRect, BorderColor, false, 1.5f);

        float fillRatio = Mathf.Clamp((float)_currentEnergy / _maxEnergy, 0f, 1f);
        float fillHeight = BarHeight * fillRatio;
        var fillRect = new Rect2(0f, BarHeight - fillHeight, BarWidth, fillHeight);
        DrawRect(fillRect, FillColor);

        var font = GetThemeDefaultFont();
        if (font == null)
        {
            return;
        }

        string energyText = $"{_currentEnergy}/{_maxEnergy}";
        float textX = BarWidth + TextGap;
        float textBaseline = font.GetAscent(EnergyTextFontSize);
        DrawString(font, new Vector2(textX, textBaseline), energyText, HorizontalAlignment.Left, -1, EnergyTextFontSize, TextColor);
    }

    private GlobalScript.BattleUnit ResolveBoundUnit()
    {
        GlobalScript.BattleUnit unit = null;

        if (GetParent() is Player player)
        {
            unit = player.BoundUnit;
        }
        else if (GetParent() is Enemy enemy)
        {
            unit = enemy.BoundUnit;
        }

        if (unit == null && !_missingBindLogged)
        {
            GD.PrintErr($"EnergyBar bind failed: parent {GetParent()?.Name} has no BattleUnit.");
            _missingBindLogged = true;
        }

        return unit;
    }

    private void UpdateEnergyBarLayout()
    {
        if (GetParent() is not Sprite2D sprite || sprite.Texture == null)
        {
            if (!_missingBindLogged)
            {
                GD.PrintErr("EnergyBar layout failed: parent is not a Sprite2D with a texture.");
                _missingBindLogged = true;
            }

            Visible = false;
            return;
        }

        var font = GetThemeDefaultFont();
        float textWidth = font == null
            ? 48f
            : font.GetStringSize($"{_currentEnergy}/{_maxEnergy}", HorizontalAlignment.Left, -1, EnergyTextFontSize).X;
        float contentHeight = font == null
            ? BarHeight
            : Mathf.Max(BarHeight, font.GetHeight(EnergyTextFontSize));

        Size = new Vector2(BarWidth + TextGap + textWidth, contentHeight);
        CustomMinimumSize = Size;

        Vector2 safeScale = new Vector2(
            Mathf.Max(Mathf.Abs(sprite.Scale.X), 0.001f),
            Mathf.Max(Mathf.Abs(sprite.Scale.Y), 0.001f));
        Rect2 displayBounds = ResolveDisplayBounds(sprite);
        Vector2 uiOffset = ResolveAttachedUiScreenOffset();

        // Keep the energy bar at a fixed screen size while anchoring it to the sprite's visual bounds.
        Scale = new Vector2(1f / safeScale.X, 1f / safeScale.Y);
        Position = new Vector2(
            displayBounds.Position.X + displayBounds.Size.X + EnergyMargin / safeScale.X,
            displayBounds.Position.Y + (displayBounds.Size.Y - contentHeight / safeScale.Y) * 0.5f)
            + new Vector2(uiOffset.X / safeScale.X, uiOffset.Y / safeScale.Y);
    }

    private Rect2 ResolveDisplayBounds(Sprite2D sprite)
    {
        if (sprite is Player player)
        {
            return player.GetDisplayBoundsLocal();
        }

        Vector2 textureSize = sprite.Texture.GetSize();
        Vector2 drawOrigin = -textureSize * 0.5f + sprite.Offset;
        return new Rect2(drawOrigin, textureSize);
    }

    private Vector2 ResolveAttachedUiScreenOffset()
    {
        if (GetParent() is Player player)
        {
            return player.AttachedUiScreenOffset;
        }

        return Vector2.Zero;
    }
}

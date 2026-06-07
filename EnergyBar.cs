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
                GD.PrintErr("EnergyBar 绑定失败：父节点不是 Node2D。");
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
            GD.PrintErr($"EnergyBar 检测到能量越界，已自动修正：{unit.UnitName} {unit.CurrentEnergy}/{maxEnergy} -> {currentEnergy}/{maxEnergy}");
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
            GD.PrintErr($"EnergyBar 绑定失败：父节点 {GetParent()?.Name} 尚未绑定 BattleUnit。");
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
                GD.PrintErr("EnergyBar 布局失败：父节点不是带贴图的 Sprite2D。");
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

        // 能量条固定挂在角色右下角外侧 5px，条体尺寸始终保持 12x70。
        Scale = new Vector2(1f / safeScale.X, 1f / safeScale.Y);
        Position = new Vector2(
            sprite.Texture.GetSize().X * 0.5f + EnergyMargin / safeScale.X,
            -contentHeight * 0.5f / safeScale.Y);
    }
}

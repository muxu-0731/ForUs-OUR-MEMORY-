using Godot;

public partial class SkillTooltip : PanelContainer
{
    // 私有字段
    private VBoxContainer _content;
    private Label _titleLabel;
    private Label _descLabel;
    private ScrollContainer _descScroll;

    // 常量
    private const float MaxTooltipWidth = 320f;
    private const float SafeViewportMargin = 18f;
    private const float MinScrollableDescHeight = 96f;
    private readonly Vector2 _mouseOffset = new Vector2(18, 18);

    public override void _Ready()
    {
        // 获取节点（路径和你场景保持一致）
        _content = GetNode<VBoxContainer>("MarginContainer/Content");
        _titleLabel = GetNode<Label>("MarginContainer/Content/TitleLabel");
        _descLabel = GetNode<Label>("MarginContainer/Content/DescLabel");

        // 固定最大宽度，强制文字按宽度换行
        _titleLabel.CustomMinimumSize = new Vector2(MaxTooltipWidth - 20, 0);
        _descLabel.CustomMinimumSize = new Vector2(MaxTooltipWidth - 20, 0);
        _titleLabel.MaxLinesVisible = -1;
        _descLabel.MaxLinesVisible = -1;

        EnsureDescriptionScrollContainer();

        // 初始状态
        Hide();
        ZIndex = 999;
    }

    // 核心方法：设置技能数据
    public void SetSkillData(GlobalScript.SkillData skillData)
    {
        if (skillData == null)
        {
            Hide();
            return;
        }

        // 设置文本内容
        string typeText = GetSkillTypeText(skillData.Type);
        _titleLabel.Text = $"【{typeText}】{skillData.SkillName}";
        _descLabel.Text = skillData.Description;

        Show();
        _ = RefreshLayoutAsync();
    }

    private void EnsureDescriptionScrollContainer()
    {
        if (_descLabel.GetParent() is ScrollContainer existingScroll)
        {
            _descScroll = existingScroll;
            return;
        }

        var originalParent = _descLabel.GetParent();
        int originalIndex = _descLabel.GetIndex();

        _descScroll = new ScrollContainer
        {
            Name = "DescScrollContainer",
            CustomMinimumSize = new Vector2(MaxTooltipWidth - 20, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            ClipContents = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };

        originalParent.RemoveChild(_descLabel);
        originalParent.AddChild(_descScroll);
        originalParent.MoveChild(_descScroll, originalIndex);
        _descScroll.AddChild(_descLabel);

        _descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _descLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
    }

    private async System.Threading.Tasks.Task RefreshLayoutAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!Visible || _descScroll == null || _descLabel == null || _titleLabel == null)
        {
            return;
        }

        float textWidth = MaxTooltipWidth - 20;
        _titleLabel.CustomMinimumSize = new Vector2(textWidth, 0);
        _descLabel.CustomMinimumSize = new Vector2(textWidth, 0);

        float fullDescHeight = Mathf.Max(MinScrollableDescHeight, _descLabel.GetCombinedMinimumSize().Y);
        _descScroll.CustomMinimumSize = new Vector2(textWidth, fullDescHeight);

        Size = GetCombinedMinimumSize();

        float maxTooltipHeight = Mathf.Max(MinScrollableDescHeight, GetViewportRect().Size.Y - SafeViewportMargin * 2f);
        if (Size.Y > maxTooltipHeight)
        {
            float overflow = Size.Y - maxTooltipHeight;
            float cappedDescHeight = Mathf.Max(MinScrollableDescHeight, fullDescHeight - overflow);
            _descScroll.CustomMinimumSize = new Vector2(textWidth, cappedDescHeight);
            Size = GetCombinedMinimumSize();
        }

        UpdateTooltipPosition(GetGlobalMousePosition());
    }

    // 重写Hide方法，隐藏时复位滚动位置，避免下次显示停留在旧阅读位置
    public new void Hide()
    {
        if (_descScroll != null)
        {
            _descScroll.ScrollVertical = 0;
        }
        base.Hide();
    }

    // 防溢出位置更新逻辑
    public void UpdateTooltipPosition(Vector2 mousePosition)
    {
        if (!Visible) return;

        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 minSize = GetCombinedMinimumSize();
        Vector2 tooltipSize = new Vector2(Mathf.Max(Size.X, minSize.X), Mathf.Max(Size.Y, minSize.Y));

        // 初始位置：鼠标+偏移，避免挡住鼠标
        Vector2 targetPos = mousePosition + _mouseOffset;

        // 右边界防溢出：超出就往左挪
        if (targetPos.X + tooltipSize.X > viewportSize.X)
            targetPos.X = mousePosition.X - tooltipSize.X - _mouseOffset.X;
        // 下边界防溢出：超出就往上挪
        if (targetPos.Y + tooltipSize.Y > viewportSize.Y)
            targetPos.Y = mousePosition.Y - tooltipSize.Y - _mouseOffset.Y;

        // 左/上边界保底，不超出窗口边缘
        targetPos.X = Mathf.Max(0, targetPos.X);
        targetPos.Y = Mathf.Max(0, targetPos.Y);

        // 当说明框接近窗口尺寸时，强制压回可见区域
        targetPos.X = Mathf.Min(targetPos.X, Mathf.Max(0, viewportSize.X - tooltipSize.X));
        targetPos.Y = Mathf.Min(targetPos.Y, Mathf.Max(0, viewportSize.Y - tooltipSize.Y));

        // 应用最终位置
        GlobalPosition = targetPos;
    }

    // 鼠标移动时实时更新提示框位置
    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventMouseMotion mouseMotion)
        {
            UpdateTooltipPosition(mouseMotion.GlobalPosition);
        }
    }

    // 技能类型枚举转中文文本
    private string GetSkillTypeText(GlobalScript.SkillType type)
    {
        return type switch
        {
            GlobalScript.SkillType.Passive => "被动技能",
            GlobalScript.SkillType.Normal => "普通攻击",
            GlobalScript.SkillType.Special => "特殊技能",
            GlobalScript.SkillType.Ultimate => "大招",
            _ => "未知类型"
        };
    }
}

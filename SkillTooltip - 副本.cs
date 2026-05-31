using Godot;

public partial class SkillTooltip : PanelContainer
{
    // 私有字段
    private Label _titleLabel;
    private Label _descLabel;

    // 常量
    private const float MaxTooltipWidth = 320f;
    private readonly Vector2 _mouseOffset = new Vector2(18, 18);

    // ✅ 新增：信号绑定状态标记，彻底解决无效解绑报错
    private bool _isResizedSignalConnected = false;

    public override void _Ready()
    {
        // 获取节点（路径和你场景保持一致）
        _titleLabel = GetNode<Label>("MarginContainer/Content/TitleLabel");
        _descLabel = GetNode<Label>("MarginContainer/Content/DescLabel");

        // Godot 4 正确的自动换行写法
        _titleLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _descLabel.AutowrapMode = TextServer.AutowrapMode.Word;

        // 固定最大宽度，强制文字按宽度换行
        _titleLabel.CustomMinimumSize = new Vector2(MaxTooltipWidth - 20, 0);
        _descLabel.CustomMinimumSize = new Vector2(MaxTooltipWidth - 20, 0);

        // 初始状态
        Hide();
        ZIndex = 999;
    }

    // 核心方法：设置技能数据
    public void SetSkillData(GlobalScript.SkillData skillData)
    {
        // 先安全解绑残留信号（只有绑定过才解绑）
        SafeDisconnectResizedSignal();

        if (skillData == null)
        {
            Hide();
            return;
        }

        // 设置文本内容
        string typeText = GetSkillTypeText(skillData.Type);
        _titleLabel.Text = $"【{typeText}】{skillData.SkillName}";
        _descLabel.Text = skillData.Description;

        // 安全绑定信号：先确保没有残留，再绑定
        SafeDisconnectResizedSignal();
        Resized += OnLayoutReady;
        _isResizedSignalConnected = true;

        Show();
    }

    // 布局完成后执行位置更新
    private void OnLayoutReady()
    {
        // 执行完立刻安全解绑
        SafeDisconnectResizedSignal();
        UpdateTooltipPosition(GetGlobalMousePosition());
    }

    // ✅ 新增：安全解绑方法，只有绑定过才执行解绑，杜绝报错
    private void SafeDisconnectResizedSignal()
    {
        if (_isResizedSignalConnected)
        {
            Resized -= OnLayoutReady;
            _isResizedSignalConnected = false;
        }
    }

    // 重写Hide方法，隐藏时安全清理信号
    public new void Hide()
    {
        SafeDisconnectResizedSignal();
        base.Hide();
    }

    // 节点释放时兜底清理，彻底杜绝残留
    protected override void Dispose(bool disposing)
    {
        SafeDisconnectResizedSignal();
        base.Dispose(disposing);
    }

    // 防溢出位置更新逻辑
    public void UpdateTooltipPosition(Vector2 mousePosition)
    {
        if (!Visible) return;

        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 tooltipSize = GetCombinedMinimumSize();

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
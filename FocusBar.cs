using Godot;

public partial class FocusBar : Control
{
    private const int DotCount = 5;
    private const float DotRadius = 17.5f;
    private const float DotSpacing = 12f;

    private static readonly Color ActiveColor = new Color("#4A90E2");
    private static readonly Color InactiveColor = new Color("#333333");

    private int _lastFocusValue = -1;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -111.5f;
        OffsetTop = 12f;
        OffsetRight = 111.5f;
        OffsetBottom = 47f;
        CustomMinimumSize = new Vector2(223f, 35f);

        GlobalScript.Instance?.ResetFocus();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        int focusValue = GlobalScript.Instance?.FocusPoints ?? GlobalScript.FocusStart;
        if (focusValue != _lastFocusValue)
        {
            _lastFocusValue = focusValue;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        int focusValue = GlobalScript.Instance?.FocusPoints ?? GlobalScript.FocusStart;

        for (int i = 0; i < DotCount; i++)
        {
            float x = DotRadius + (DotRadius * 2f + DotSpacing) * i;
            Vector2 center = new Vector2(x, DotRadius);
            DrawCircle(center, DotRadius, i < focusValue ? ActiveColor : InactiveColor);
        }
    }
}

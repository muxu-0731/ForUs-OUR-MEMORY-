using Godot;

public partial class TurnIndicatorEffect : Node2D
{
    private float _time = 0f;
    private float _radius = 65f;
    private float _rotationSpeed = 1.2f;

    public void Configure(float radius)
    {
        _radius = Mathf.Max(radius, 24f);
        QueueRedraw();
    }

    public override void _Ready()
    {
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = 1000;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        Rotation += _rotationSpeed * (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float breatheOffset = Mathf.Sin(_time * 2.5f) * 4f;
        float currentRadius = _radius + breatheOffset;

        Color ringColor = new Color(0.1f, 0.75f, 1f, 0.8f);
        Color innerRingColor = new Color(0.1f, 0.75f, 1f, 0.28f);
        Color dotColor = new Color(0.1f, 0.85f, 1f, 1f);

        DrawArc(Vector2.Zero, currentRadius, 0, Mathf.Pi * 2, 96, ringColor, 4f, true);
        DrawArc(Vector2.Zero, currentRadius - 10f, 0, Mathf.Pi * 2, 96, innerRingColor, 2f, true);

        for (int i = 0; i < 4; i++)
        {
            float angle = (Mathf.Pi / 2) * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawCircle(dir * currentRadius, 5f, dotColor);
        }
    }
}

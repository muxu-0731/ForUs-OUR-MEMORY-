using Godot;

public partial class DamagePopfx : Node2D
{
    private static readonly Color DamageColor = new Color(0.92f, 0.2f, 0.2f);
    private static readonly Color HealColor = new Color(0.25f, 0.9f, 0.35f);

    private Label _damageLabel;

    public void Initialize(int amount, bool isCrit, bool isHeal, Vector2 spawnGlobalPos, Vector2 startOffset, Vector2 travelOffset)
    {
        _damageLabel = GetNode<Label>("DamageLabel");
        _damageLabel.Text = isCrit ? $"{amount}!" : $"{amount}";

        _damageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _damageLabel.VerticalAlignment = VerticalAlignment.Center;
        _damageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _damageLabel.CustomMinimumSize = new Vector2(220, 56);
        _damageLabel.Position = new Vector2(-110, -28);

        Visible = true;
        _damageLabel.Visible = true;
        Modulate = Colors.White;
        _damageLabel.Modulate = isHeal ? HealColor : DamageColor;

        Vector2 startPos = spawnGlobalPos + startOffset;
        Vector2 peakPos = startPos + travelOffset;
        Vector2 floatPos = peakPos + new Vector2(travelOffset.X * 0.15f, -10f);
        Vector2 fallPos = floatPos + new Vector2(travelOffset.X * 0.2f, 80f);

        Vector2 startScale;
        Vector2 targetScale;

        if (isHeal)
        {
            startScale = new Vector2(1.5f, 1.5f);
            targetScale = new Vector2(1.1f, 1.1f);
        }
        else if (isCrit)
        {
            startScale = new Vector2(3.0f, 3.0f);
            targetScale = new Vector2(1.8f, 1.8f);
        }
        else
        {
            startScale = new Vector2(1.4f, 1.4f);
            targetScale = new Vector2(1.0f, 1.0f);
        }

        GlobalPosition = startPos;
        Scale = startScale;

        Tween moveTween = CreateTween();
        moveTween.TweenProperty(this, "global_position", peakPos, 0.15f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        moveTween.Parallel().TweenProperty(this, "scale", targetScale, 0.15f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        moveTween.Chain().TweenProperty(this, "global_position", floatPos, 1.2f)
            .SetTrans(Tween.TransitionType.Linear);

        moveTween.Chain().TweenProperty(this, "global_position", fallPos, 0.65f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);

        Tween lifeTween = CreateTween();
        lifeTween.TweenInterval(1.35f);
        lifeTween.Chain().TweenProperty(_damageLabel, "modulate:a", 0f, 0.5f);
        lifeTween.Parallel().TweenProperty(this, "modulate:a", 0f, 0.5f);

        lifeTween.Chain().TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(_damageLabel))
            {
                _damageLabel.Text = "";
                _damageLabel.Visible = false;
            }

            Visible = false;
            QueueFree();
        }));
    }
}

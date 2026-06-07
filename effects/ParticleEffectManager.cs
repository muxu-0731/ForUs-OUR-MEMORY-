using Godot;
using System.Threading.Tasks;

public partial class ParticleEffectManager : Node2D
{
    private const int EffectTopZIndex = 1200;

    public static ParticleEffectManager Instance { get; private set; }

    private GpuParticles2D _bloodNightmareSoulBurst;
    private Sprite2D _bloodNightmareBigSphere;
    private GpuParticles2D _bloodNightmareExplosion;
    private PackedScene _burningSoulScene;

    private readonly float _sphereRiseHeight = 50f;
    private readonly float _riseDuration = 1.0f;
    private readonly float _rushDuration = 0.3f;

    public override void _Ready()
    {
        if (Instance != null)
        {
            QueueFree();
            return;
        }

        Instance = this;
        SetProcessMode(ProcessModeEnum.Always);
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = EffectTopZIndex;

        _bloodNightmareSoulBurst = GetNodeOrNull<GpuParticles2D>("BloodNightmare_SoulBurst");
        _bloodNightmareBigSphere = GetNodeOrNull<Sprite2D>("BloodNightmare_BigSphere");
        _bloodNightmareExplosion = GetNodeOrNull<GpuParticles2D>("BloodNightmare_Explosion");
        _burningSoulScene = GD.Load<PackedScene>("res://effects/burning_soul_effect.tscn");

        ConfigureTopLayer(_bloodNightmareSoulBurst);
        ConfigureTopLayer(_bloodNightmareBigSphere);
        ConfigureTopLayer(_bloodNightmareExplosion);

        HideAllEffects();
    }

    private void HideAllEffects()
    {
        if (_bloodNightmareSoulBurst != null)
        {
            _bloodNightmareSoulBurst.Hide();
            _bloodNightmareSoulBurst.Emitting = false;
        }

        _bloodNightmareBigSphere?.Hide();

        if (_bloodNightmareExplosion != null)
        {
            _bloodNightmareExplosion.Hide();
            _bloodNightmareExplosion.Emitting = false;
        }
    }

    public async Task PlayBloodNightmare(Vector2 casterPos, Vector2 targetPos)
    {
        try
        {
            if (_bloodNightmareSoulBurst == null || _bloodNightmareBigSphere == null || _bloodNightmareExplosion == null)
            {
                GD.PushError("Blood nightmare effect nodes are missing.");
                return;
            }

            _bloodNightmareSoulBurst.Show();
            _bloodNightmareSoulBurst.GlobalPosition = casterPos;
            _bloodNightmareSoulBurst.Restart();
            _bloodNightmareSoulBurst.Emitting = true;

            _bloodNightmareBigSphere.Show();
            _bloodNightmareBigSphere.GlobalPosition = casterPos;
            Vector2 riseTargetPos = casterPos + new Vector2(0f, -_sphereRiseHeight);

            Tween riseTween = CreateTween();
            riseTween.TweenProperty(_bloodNightmareBigSphere, "global_position", riseTargetPos, _riseDuration);
            riseTween.SetEase(Tween.EaseType.Out);

            await ToSignal(riseTween, Tween.SignalName.Finished);
            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

            _bloodNightmareSoulBurst.Hide();
            _bloodNightmareSoulBurst.Emitting = false;

            Tween rushTween = CreateTween();
            rushTween.TweenProperty(_bloodNightmareBigSphere, "global_position", targetPos, _rushDuration);
            rushTween.SetEase(Tween.EaseType.In);

            await ToSignal(rushTween, Tween.SignalName.Finished);
            _bloodNightmareBigSphere.Hide();

            _bloodNightmareExplosion.Show();
            _bloodNightmareExplosion.GlobalPosition = targetPos;
            _bloodNightmareExplosion.Restart();
            _bloodNightmareExplosion.Emitting = true;

            await ToSignal(GetTree().CreateTimer(_bloodNightmareExplosion.Lifetime), SceneTreeTimer.SignalName.Timeout);
            _bloodNightmareExplosion.Hide();
            _bloodNightmareExplosion.Emitting = false;
        }
        catch (System.Exception e)
        {
            GD.PrintErr("Blood nightmare effect failed: ", e.Message);
            HideAllEffects();
        }
    }

    public async Task PlayBurningSoul(Vector2 playPos)
    {
        try
        {
            if (_burningSoulScene == null)
            {
                GD.PushError("Missing burning soul effect scene: res://effects/burning_soul_effect.tscn");
                return;
            }

            GpuParticles2D newEffect = _burningSoulScene.Instantiate<GpuParticles2D>();
            ConfigureTopLayer(newEffect);
            GetTree().Root.AddChild(newEffect);
            newEffect.GlobalPosition = playPos;
            newEffect.Emitting = true;
            newEffect.Restart();

            await ToSignal(GetTree().CreateTimer(newEffect.Lifetime), SceneTreeTimer.SignalName.Timeout);
            newEffect.QueueFree();
        }
        catch (System.Exception e)
        {
            GD.PrintErr("Burning soul effect failed: ", e.Message);
        }
    }

    private void ConfigureTopLayer(CanvasItem effectNode)
    {
        if (effectNode == null)
        {
            return;
        }

        effectNode.TopLevel = true;
        effectNode.ZAsRelative = false;
        effectNode.ZIndex = EffectTopZIndex;
    }
}

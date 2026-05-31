using Godot;
using System.Threading.Tasks;

public partial class ParticleEffectManager : Node2D
{
    // 全局单例（确保特效管理器唯一）
    public static ParticleEffectManager Instance { get; private set; }

    // 血魇特效节点缓存（复用节点，无需动态创建）
    private GpuParticles2D _bloodNightmareSoulBurst;
    private Sprite2D _bloodNightmareBigSphere;
    private GpuParticles2D _bloodNightmareExplosion;

    // 燃魂特效场景（用于动态实例化多个特效，支持多角色同时播放）
    private PackedScene _burningSoulScene;

    // 血魇特效时序参数
    private readonly float _sphereRiseHeight = 50f;
    private readonly float _riseDuration = 1.0f;
    private readonly float _rushDuration = 0.3f;
    private readonly float _burstDuration = 0.6f;

    public override void _Ready()
    {
        // 单例逻辑：防止重复创建
        if (Instance != null)
        {
            QueueFree();
            return;
        }
        Instance = this;
        SetProcessMode(ProcessModeEnum.Always);

        // 调试：打印所有子节点，方便排查节点名称/类型问题
        GD.Print("=== ParticleEffectManager 子节点列表 ===");
        foreach (var child in GetChildren())
        {
            GD.Print($"名称：{child.Name} | 类型：{child.GetType().FullName}");
        }

        // 绑定血魇特效节点（复用节点）
        _bloodNightmareSoulBurst = GetNodeOrNull<GpuParticles2D>("BloodNightmare_SoulBurst");
        _bloodNightmareBigSphere = GetNodeOrNull<Sprite2D>("BloodNightmare_BigSphere");
        _bloodNightmareExplosion = GetNodeOrNull<GpuParticles2D>("BloodNightmare_Explosion");

        // 🎯 核心修正：加载燃魂特效场景（路径改为你提供的 res://effects/burning_soul_effect.tscn）
        _burningSoulScene = GD.Load<PackedScene>("res://effects/burning_soul_effect.tscn");

        // 打印加载状态，方便排查
        GD.Print($"📌 血魇节点加载状态：");
        GD.Print($"   SoulBurst: {_bloodNightmareSoulBurst != null}");
        GD.Print($"   BigSphere: {_bloodNightmareBigSphere != null}");
        GD.Print($"   Explosion: {_bloodNightmareExplosion != null}");
        GD.Print($"📌 燃魂场景加载状态：{_burningSoulScene != null}");

        // 隐藏初始特效
        HideAllEffects();
        GD.Print("✅ 粒子特效管理器初始化完成！");
    }

    /// <summary>
    /// 隐藏所有复用的血魇特效节点（燃魂为动态创建，无需隐藏）
    /// </summary>
    private void HideAllEffects()
    {
        _bloodNightmareSoulBurst?.Hide();
        _bloodNightmareBigSphere?.Hide();
        _bloodNightmareExplosion?.Hide();
    }

    /// <summary>
    /// 播放血魇特效（复用节点，单体特效）
    /// </summary>
    /// <param name="casterPos">施法者位置</param>
    /// <param name="targetPos">目标位置</param>
    public async Task PlayBloodNightmare(Vector2 casterPos, Vector2 targetPos)
    {
        try
        {
            // 空值检查：避免调用空节点
            if (_bloodNightmareSoulBurst == null || _bloodNightmareBigSphere == null || _bloodNightmareExplosion == null)
            {
                GD.PushError("❌ 血魇特效节点未找到，无法播放！");
                return;
            }

            // 阶段1：球形扩散粒子
            _bloodNightmareSoulBurst.Show();
            _bloodNightmareSoulBurst.Position = casterPos;
            _bloodNightmareSoulBurst.Restart();
            _bloodNightmareSoulBurst.Emitting = true;
            GD.Print("血魇特效：阶段1-球形扩散播放");

            // 阶段2：红球升空
            _bloodNightmareBigSphere.Show();
            _bloodNightmareBigSphere.Position = casterPos;
            Vector2 riseTargetPos = new Vector2(casterPos.X, casterPos.Y - _sphereRiseHeight);

            var riseTween = CreateTween();
            riseTween.TweenProperty(_bloodNightmareBigSphere, "position", riseTargetPos, _riseDuration);
            riseTween.SetEase(Tween.EaseType.Out);
            GD.Print("血魇特效：阶段2-红球升空");

            await ToSignal(riseTween, "finished");
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

            _bloodNightmareSoulBurst.Hide();
            _bloodNightmareSoulBurst.Emitting = false;

            // 阶段3：红球冲刺
            var rushTween = CreateTween();
            rushTween.TweenProperty(_bloodNightmareBigSphere, "position", targetPos, _rushDuration);
            rushTween.SetEase(Tween.EaseType.In);
            GD.Print("血魇特效：阶段3-红球冲刺目标");

            await ToSignal(rushTween, "finished");
            _bloodNightmareBigSphere.Hide();

            // 阶段4：命中爆炸
            _bloodNightmareExplosion.Show();
            _bloodNightmareExplosion.Position = targetPos;
            _bloodNightmareExplosion.Restart();
            _bloodNightmareExplosion.Emitting = true;
            GD.Print("血魇特效：阶段4-命中爆炸");

            await ToSignal(GetTree().CreateTimer(_bloodNightmareExplosion.Lifetime), "timeout");
            _bloodNightmareExplosion.Hide();
            _bloodNightmareExplosion.Emitting = false;

            GD.Print("✅ 血魇特效播放完成！");
        }
        catch (System.Exception e)
        {
            GD.PrintErr("❌ 血魇特效播放失败：", e.Message);
            HideAllEffects();
        }
    }

    /// <summary>
    /// 播放燃魂特效（动态实例化，支持多角色同时播放）
    /// </summary>
    /// <param name="playPos">特效播放位置</param>
    public async Task PlayBurningSoul(Vector2 playPos)
    {
        try
        {
            // 空值检查：场景未加载则返回
            if (_burningSoulScene == null)
            {
                GD.PushError("❌ 燃魂特效场景未加载！请检查路径：res://effects/burning_soul_effect.tscn");
                return;
            }

            GD.Print($"🔥 燃魂特效触发，位置：{playPos}");

            // 1. 动态实例化新的粒子节点（每次播放都创建新节点，支持多位置同时播放）
            var newEffect = _burningSoulScene.Instantiate<GpuParticles2D>();

            // 2. 添加到场景根节点（避免随其他节点移动）
            GetTree().Root.AddChild(newEffect);

            // 3. 设置特效播放位置
            newEffect.GlobalPosition = playPos;

            // 4. 修复笔误：newEmitting → newEffect.Emitting
            newEffect.Emitting = true;
            newEffect.Restart();

            // 5. 等待特效播放完毕（根据粒子生命周期自动等待）
            await ToSignal(GetTree().CreateTimer(newEffect.Lifetime), "timeout");

            // 6. 播放完成后自动销毁节点，避免内存泄漏
            newEffect.QueueFree();

            GD.Print("✅ 燃魂特效播放完成并自动销毁！");
        }
        catch (System.Exception e)
        {
            GD.PrintErr("❌ 燃魂特效播放失败：", e.Message);
        }
    }
}
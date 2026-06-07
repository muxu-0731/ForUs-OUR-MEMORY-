using Godot;
using System.Collections.Generic;

public partial class DamagePopfxManager : Node
{
    private readonly struct SpawnPattern
    {
        public SpawnPattern(Vector2 startOffset, Vector2 travelOffset)
        {
            StartOffset = startOffset;
            TravelOffset = travelOffset;
        }

        public Vector2 StartOffset { get; }
        public Vector2 TravelOffset { get; }
    }

    private sealed class SpawnBucket
    {
        public int NextIndex;
        public ulong LastTick;
    }

    private static readonly SpawnPattern[] SpawnPatterns =
    {
        new SpawnPattern(new Vector2(-18f, -6f), new Vector2(-58f, -108f)),
        new SpawnPattern(new Vector2(18f, -6f), new Vector2(58f, -108f)),
        new SpawnPattern(new Vector2(-28f, -18f), new Vector2(-82f, -92f)),
        new SpawnPattern(new Vector2(28f, -18f), new Vector2(82f, -92f)),
        new SpawnPattern(new Vector2(0f, -26f), new Vector2(0f, -122f)),
        new SpawnPattern(new Vector2(-10f, -34f), new Vector2(-42f, -132f)),
        new SpawnPattern(new Vector2(10f, -34f), new Vector2(42f, -132f))
    };

    public static DamagePopfxManager Instance { get; private set; }

    private readonly Dictionary<Vector2I, SpawnBucket> _spawnBuckets = new();
    private PackedScene _popfxScene;

    public override void _Ready()
    {
        Instance = this;

        const string scenePath = "res://DamagePopfx.tscn";
        if (ResourceLoader.Exists(scenePath))
        {
            _popfxScene = GD.Load<PackedScene>(scenePath);
        }
        else
        {
            GD.PrintErr("[DamagePopfxManager] Missing DamagePopfx.tscn in project root.");
        }
    }

    public void SpawnDamageText(int amount, bool isCrit, Vector2 globalPosition)
    {
        SpawnNumberText(amount, isCrit, false, globalPosition);
    }

    public void SpawnHealText(int amount, Vector2 globalPosition)
    {
        SpawnNumberText(amount, false, true, globalPosition);
    }

    private void SpawnNumberText(int amount, bool isCrit, bool isHeal, Vector2 globalPosition)
    {
        if (_popfxScene == null)
        {
            return;
        }

        if (_popfxScene.Instantiate() is not DamagePopfx popfxInstance)
        {
            return;
        }

        popfxInstance.ZAsRelative = false;
        popfxInstance.ZIndex = 999;

        GetTree().CurrentScene.AddChild(popfxInstance);

        SpawnPattern pattern = GetNextPattern(globalPosition);
        popfxInstance.Initialize(amount, isCrit, isHeal, globalPosition, pattern.StartOffset, pattern.TravelOffset);
    }

    private SpawnPattern GetNextPattern(Vector2 globalPosition)
    {
        Vector2I key = new Vector2I(
            Mathf.RoundToInt(globalPosition.X / 24f),
            Mathf.RoundToInt(globalPosition.Y / 24f));

        ulong now = Time.GetTicksMsec();
        if (!_spawnBuckets.TryGetValue(key, out SpawnBucket bucket) || now - bucket.LastTick > 900)
        {
            bucket = new SpawnBucket();
            _spawnBuckets[key] = bucket;
        }

        bucket.LastTick = now;
        int patternIndex = bucket.NextIndex % SpawnPatterns.Length;
        bucket.NextIndex++;
        return SpawnPatterns[patternIndex];
    }
}

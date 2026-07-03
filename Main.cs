using Godot;
using System.Collections.Generic;

public partial class Main : Node2D
{
    private const float TopSafeMargin = 96f;
    private const float BottomSafeMargin = 24f;
    private const float SideSafeMargin = 24f;
    private const float PlayerAreaRightPercent = 0.58f;
    private const float EnemyAreaLeftPercent = 0.58f;

    public static Main Instance { get; private set; }

    private Sprite2D _background;
    private CanvasLayer _canvasLayer;
    private Ui _battleUi;
    private Label _enemyNameLabel;
    private readonly List<Player> _playerViews = new();
    private readonly List<Enemy> _enemyViews = new();

    public override void _EnterTree()
    {
        if (GlobalScript.Instance != null)
        {
            GlobalScript.Instance.ResetBattleFromSelectedTeam();
        }
    }

    public override void _Ready()
    {
        Instance = this;
        _background = GetNodeOrNull<Sprite2D>("Background");
        _canvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
        _battleUi = GetNodeOrNull<Ui>("CanvasLayer/ui");
        _enemyNameLabel = GetNodeOrNull<Label>("EnemyNameLabel");

        if (_background == null)
        {
            GD.PrintErr("Main: missing Background Sprite2D node.");
        }
        else
        {
            // Keep the battle background pinned to the bottom-most draw layer.
            _background.ZAsRelative = false;
            _background.ZIndex = -100;
            _background.Visible = true;
        }

        if (_canvasLayer == null)
        {
            GD.PrintErr("Main: missing CanvasLayer node.");
        }
        else
        {
            // Battle UI must render in front of world sprites and the background.
            _canvasLayer.Layer = 1;
        }

        if (_battleUi == null)
        {
            GD.PrintErr("Main: missing CanvasLayer/ui node.");
        }

        CollectBattleViews();
        RefreshBattleLayout();

        if (GetViewport() != null)
        {
            GetViewport().SizeChanged += OnViewportSizeChanged;
        }
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Process(double delta)
    {
        UpdateEnemyNameLabel();
    }

    public void RefreshBattleLayout()
    {
        if (!IsInsideTree())
        {
            return;
        }

        CollectBattleViews();
        ApplyPlayerTrackFormation(_playerViews, BattleFormationLibrary.PlayerPartyTracks, BuildPlayerSafeArea());
        ApplyFormation(_enemyViews, BattleFormationLibrary.EnemyParty, BuildEnemySafeArea());
        RefreshAttachedUi();
        UpdateEnemyNameLabel();
        GlobalScript.Instance?.RefreshCurrentTurnIndicator();
    }

    private void CollectBattleViews()
    {
        _playerViews.Clear();
        _enemyViews.Clear();

        foreach (Node child in GetChildren())
        {
            if (child is Player player)
            {
                _playerViews.Add(player);
            }
            else if (child is Enemy enemy)
            {
                _enemyViews.Add(enemy);
            }
        }

        _playerViews.Sort((left, right) => left.GetIndex().CompareTo(right.GetIndex()));
        _enemyViews.Sort((left, right) => left.GetIndex().CompareTo(right.GetIndex()));
    }

    private void ApplyFormation(IReadOnlyList<Player> views, BattleFormation formation, Rect2 safeArea)
    {
        IReadOnlyList<FormationSlot> slots = formation.ResolveSlots(views.Count);
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        int slotCount = Mathf.Min(views.Count, slots.Count);

        for (int i = 0; i < slotCount; i++)
        {
            Player player = views[i];
            if (player == null || !player.IsInsideTree())
            {
                continue;
            }

            player.SetFormationFootPosition(slots[i].ResolveWorldPosition(viewportSize));
            ClampViewIntoSafeArea(player, player.GetDisplayBoundsGlobal(), safeArea);
        }
    }

    private void ApplyPlayerTrackFormation(IReadOnlyList<Player> views, PlayerTrackFormation formation, Rect2 safeArea)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        foreach (Player player in views)
        {
            if (player == null || !player.IsInsideTree())
            {
                continue;
            }

            if (!TryResolvePlayerPlacement(player, formation, viewportSize, out FormationResolvedPlacement placement))
            {
                player.SetAttachedUiScreenOffset(Vector2.Zero);
                continue;
            }

            player.SetAttachedUiScreenOffset(placement.UiOffset);
            player.SetFormationFootPosition(placement.WorldFootPosition);
            ClampViewIntoSafeArea(player, player.GetDisplayBoundsGlobal(), safeArea);
        }
    }

    private void ApplyFormation(IReadOnlyList<Enemy> views, BattleFormation formation, Rect2 safeArea)
    {
        IReadOnlyList<FormationSlot> slots = formation.ResolveSlots(views.Count);
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        int slotCount = Mathf.Min(views.Count, slots.Count);

        for (int i = 0; i < slotCount; i++)
        {
            Enemy enemy = views[i];
            if (enemy == null || !enemy.IsInsideTree())
            {
                continue;
            }

            enemy.SetFormationFootPosition(slots[i].ResolveWorldPosition(viewportSize));
            ClampViewIntoSafeArea(enemy, enemy.GetDisplayBoundsGlobal(), safeArea);
        }
    }

    private void ClampViewIntoSafeArea(Player player, Rect2 bounds, Rect2 safeArea)
    {
        Vector2 correction = BattleViewUtility.ClampOffsetInside(bounds, safeArea);
        if (correction == Vector2.Zero)
        {
            return;
        }

        player.GlobalPosition += correction;
        player.RefreshAttachedUi();
    }

    private bool TryResolvePlayerPlacement(
        Player player,
        PlayerTrackFormation formation,
        Vector2 viewportSize,
        out FormationResolvedPlacement placement)
    {
        int partySlotIndex = ResolvePlayerTrackIndex(player);
        PlayerTrackOccupantKind occupantKind = ResolvePlayerTrackOccupantKind(player);
        return formation.TryResolvePlacement(partySlotIndex, occupantKind, viewportSize, out placement);
    }

    private int ResolvePlayerTrackIndex(Player player)
    {
        var unit = player?.BoundUnit;
        if (unit != null)
        {
            int trackIndex = unit.ResolvePlayerTrackIndex();
            if (trackIndex >= 0 && trackIndex < GlobalScript.MaxPartySize)
            {
                return trackIndex;
            }
        }

        return player?.PlayerTeamIndex ?? GlobalScript.InvalidPartySlotIndex;
    }

    private static PlayerTrackOccupantKind ResolvePlayerTrackOccupantKind(Player player)
    {
        return player?.BoundUnit != null && player.BoundUnit.IsSummon
            ? PlayerTrackOccupantKind.Summon
            : PlayerTrackOccupantKind.PrimaryActor;
    }

    private void ClampViewIntoSafeArea(Enemy enemy, Rect2 bounds, Rect2 safeArea)
    {
        Vector2 correction = BattleViewUtility.ClampOffsetInside(bounds, safeArea);
        if (correction == Vector2.Zero)
        {
            return;
        }

        enemy.GlobalPosition += correction;
        enemy.RefreshAttachedUi();
    }

    private void RefreshAttachedUi()
    {
        foreach (Player player in _playerViews)
        {
            player?.RefreshAttachedUi();
        }

        foreach (Enemy enemy in _enemyViews)
        {
            enemy?.RefreshAttachedUi();
        }
    }

    private Rect2 BuildPlayerSafeArea()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float width = Mathf.Max(0f, viewportSize.X * PlayerAreaRightPercent - SideSafeMargin * 2f);
        float height = Mathf.Max(0f, viewportSize.Y - TopSafeMargin - BottomSafeMargin);
        return new Rect2(SideSafeMargin, TopSafeMargin, width, height);
    }

    private Rect2 BuildEnemySafeArea()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float left = viewportSize.X * EnemyAreaLeftPercent;
        float width = Mathf.Max(0f, viewportSize.X - left - SideSafeMargin);
        float height = Mathf.Max(0f, viewportSize.Y - TopSafeMargin - BottomSafeMargin);
        return new Rect2(left, TopSafeMargin, width, height);
    }

    private void UpdateEnemyNameLabel()
    {
        if (_enemyNameLabel == null)
        {
            return;
        }

        Enemy currentEnemy = null;
        foreach (Enemy enemy in _enemyViews)
        {
            if (enemy?.BoundUnit != null && !enemy.BoundUnit.IsDead)
            {
                currentEnemy = enemy;
                break;
            }
        }

        if (currentEnemy?.BoundUnit == null)
        {
            _enemyNameLabel.Visible = false;
            return;
        }

        Rect2 enemyBounds = currentEnemy.GetDisplayBoundsGlobal();
        _enemyNameLabel.Visible = true;
        _enemyNameLabel.Text = currentEnemy.BoundUnit.UnitName;
        _enemyNameLabel.ResetSize();
        _enemyNameLabel.Position = new Vector2(
            enemyBounds.GetCenter().X - _enemyNameLabel.Size.X * 0.5f,
            enemyBounds.Position.Y - _enemyNameLabel.Size.Y - 14f);
    }

    private void OnViewportSizeChanged()
    {
        CallDeferred(nameof(RefreshBattleLayout));
    }
}

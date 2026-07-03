using Godot;
using System;
using System.Collections.Generic;

public enum BattleFormationKind
{
    PlayerParty,
    EnemyParty,
    TemporaryAlly
}

public enum PlayerTrackOccupantKind
{
    PrimaryActor,
    Summon
}

public readonly struct FormationSlot
{
    public FormationSlot(float viewportX, float viewportY)
    {
        ViewportPercent = new Vector2(viewportX, viewportY);
    }

    public Vector2 ViewportPercent { get; }

    public Vector2 ResolveWorldPosition(Vector2 viewportSize)
    {
        return new Vector2(
            viewportSize.X * ViewportPercent.X,
            viewportSize.Y * ViewportPercent.Y);
    }
}

public readonly struct FormationResolvedPlacement
{
    public FormationResolvedPlacement(Vector2 worldFootPosition, Vector2 uiOffset)
    {
        WorldFootPosition = worldFootPosition;
        UiOffset = uiOffset;
    }

    public Vector2 WorldFootPosition { get; }
    public Vector2 UiOffset { get; }
}

public readonly struct PlayerTrackSlot
{
    public PlayerTrackSlot(FormationSlot formationSlot, Vector2 uiOffset)
    {
        FormationSlot = formationSlot;
        UiOffset = uiOffset;
    }

    public FormationSlot FormationSlot { get; }
    public Vector2 UiOffset { get; }

    public FormationResolvedPlacement ResolvePlacement(Vector2 viewportSize)
    {
        return new FormationResolvedPlacement(
            FormationSlot.ResolveWorldPosition(viewportSize),
            UiOffset);
    }
}

public readonly struct PlayerTrackLayout
{
    public PlayerTrackLayout(int partySlotIndex, PlayerTrackSlot primarySlot, PlayerTrackSlot summonSlot)
    {
        PartySlotIndex = partySlotIndex;
        PrimarySlot = primarySlot;
        SummonSlot = summonSlot;
    }

    public int PartySlotIndex { get; }
    public PlayerTrackSlot PrimarySlot { get; }
    public PlayerTrackSlot SummonSlot { get; }

    public PlayerTrackSlot GetSlot(PlayerTrackOccupantKind occupantKind)
    {
        return occupantKind == PlayerTrackOccupantKind.Summon
            ? SummonSlot
            : PrimarySlot;
    }
}

public sealed class PlayerTrackFormation
{
    private readonly PlayerTrackLayout[] _tracks;

    public PlayerTrackFormation(PlayerTrackLayout[] tracks)
    {
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
    }

    public int TrackCount => _tracks.Length;

    public bool TryResolvePlacement(
        int partySlotIndex,
        PlayerTrackOccupantKind occupantKind,
        Vector2 viewportSize,
        out FormationResolvedPlacement placement)
    {
        if (!TryGetTrack(partySlotIndex, out PlayerTrackLayout track))
        {
            placement = default;
            return false;
        }

        placement = track.GetSlot(occupantKind).ResolvePlacement(viewportSize);
        return true;
    }

    public bool TryGetTrack(int partySlotIndex, out PlayerTrackLayout track)
    {
        if (partySlotIndex < 0 || partySlotIndex >= _tracks.Length)
        {
            track = default;
            return false;
        }

        track = _tracks[partySlotIndex];
        return true;
    }
}

public sealed class BattleFormation
{
    private readonly Dictionary<int, FormationSlot[]> _layouts;
    private readonly FormationSlot[] _fallbackSlots;

    public BattleFormation(BattleFormationKind kind, Dictionary<int, FormationSlot[]> layouts, FormationSlot[] fallbackSlots)
    {
        Kind = kind;
        _layouts = layouts ?? throw new ArgumentNullException(nameof(layouts));
        _fallbackSlots = fallbackSlots ?? Array.Empty<FormationSlot>();
    }

    public BattleFormationKind Kind { get; }

    public IReadOnlyList<FormationSlot> ResolveSlots(int unitCount)
    {
        if (unitCount <= 0)
        {
            return Array.Empty<FormationSlot>();
        }

        if (_layouts.TryGetValue(unitCount, out FormationSlot[] slots) && slots.Length > 0)
        {
            return slots;
        }

        return _fallbackSlots;
    }
}

public static class BattleFormationLibrary
{
    private static readonly FormationSlot[] EnemyFallbackSlots =
    {
        new FormationSlot(0.80f, 0.60f),
        new FormationSlot(0.89f, 0.72f),
        new FormationSlot(0.69f, 0.74f)
    };

    private static readonly FormationSlot[] TemporaryAllyFallbackSlots =
    {
        new FormationSlot(0.28f, 0.86f),
        new FormationSlot(0.38f, 0.84f)
    };

    private static readonly PlayerTrackLayout[] PlayerTrackLayouts =
    {
        new PlayerTrackLayout(
            0,
            new PlayerTrackSlot(new FormationSlot(0.14f, 0.61f), new Vector2(-8f, 0f)),
            new PlayerTrackSlot(new FormationSlot(0.20f, 0.70f), new Vector2(24f, 18f))),
        new PlayerTrackLayout(
            1,
            new PlayerTrackSlot(new FormationSlot(0.25f, 0.77f), new Vector2(0f, 0f)),
            new PlayerTrackSlot(new FormationSlot(0.31f, 0.85f), new Vector2(26f, 18f))),
        new PlayerTrackLayout(
            2,
            new PlayerTrackSlot(new FormationSlot(0.36f, 0.57f), new Vector2(0f, -2f)),
            new PlayerTrackSlot(new FormationSlot(0.42f, 0.66f), new Vector2(26f, 18f))),
        new PlayerTrackLayout(
            3,
            new PlayerTrackSlot(new FormationSlot(0.47f, 0.76f), new Vector2(0f, 0f)),
            new PlayerTrackSlot(new FormationSlot(0.53f, 0.84f), new Vector2(26f, 20f)))
    };

    public static PlayerTrackFormation PlayerPartyTracks { get; } = new PlayerTrackFormation(PlayerTrackLayouts);

    public static BattleFormation EnemyParty { get; } = new BattleFormation(
        BattleFormationKind.EnemyParty,
        new Dictionary<int, FormationSlot[]>
        {
            [1] = new[]
            {
                new FormationSlot(0.82f, 0.64f)
            },
            [2] = new[]
            {
                new FormationSlot(0.76f, 0.58f),
                new FormationSlot(0.88f, 0.72f)
            },
            [3] = EnemyFallbackSlots
        },
        EnemyFallbackSlots);

    // Temporary allies stay separate from the 4-track formal party.
    // Future owner-bound summons should use PlayerPartyTracks and their owner's summon sub-slot instead.
    public static BattleFormation TemporaryAlly { get; } = new BattleFormation(
        BattleFormationKind.TemporaryAlly,
        new Dictionary<int, FormationSlot[]>
        {
            [1] = new[]
            {
                new FormationSlot(0.31f, 0.84f)
            },
            [2] = TemporaryAllyFallbackSlots
        },
        TemporaryAllyFallbackSlots);
}

using Godot;

namespace ModularFighter.Core;

public sealed class FighterHitLogEntry
{
	public ulong PhysicsFrame { get; init; }
	public string AttackerName { get; init; } = "";
	public string DefenderName { get; init; } = "";
	public string MoveName { get; init; } = "";
	public string HitboxTag { get; init; } = "";
	public string HurtboxTag { get; init; } = "";
	public Vector2 HitPoint { get; init; }
	public Rect2 HitboxWorldRect { get; init; }
	public Rect2 HurtboxWorldRect { get; init; }
	public FighterBoxAttribute HitboxAttributes { get; init; }
	public FighterBoxAttribute HurtboxAttributes { get; init; }
	public FighterAttackLevel AttackLevel { get; init; }
	public int HitboxPriority { get; init; }
	public int AttackFrame { get; init; }
	public int HitstunFrames { get; init; }
	/// <summary>Attacker freeze, traditionally called hitlag.</summary>
	public int HitstopFrames { get; init; }
	/// <summary>Defender freeze; normally equal to hitlag unless a contact rule splits them.</summary>
	public int DefenderHitstopFrames { get; init; }
	public float Pushback { get; init; }
	public bool CounterHit { get; init; }
	public bool Projectile { get; init; }
}

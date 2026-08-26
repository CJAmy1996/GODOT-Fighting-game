using Godot;

namespace ModularFighter.Core;

/// <summary>
/// One collision contract for every fighter:
/// Godot physics collides fighters with stage geometry only. Fighter-to-fighter
/// spacing is resolved by pushboxes, and attacks use hitbox/hurtbox rectangles.
/// </summary>
public static class FighterCollisionPolicy
{
	public const uint StagePhysicsLayer = 1u;
	public const uint FighterBodyLayer = 1u << 1;

	public static void Apply(FighterController fighter)
	{
		if (!GodotObject.IsInstanceValid(fighter)) return;
		fighter.CollisionLayer = FighterBodyLayer;
		fighter.CollisionMask = StagePhysicsLayer;
	}

	public static bool IsNormalized(FighterController fighter) =>
		GodotObject.IsInstanceValid(fighter) &&
		fighter.CollisionLayer == FighterBodyLayer &&
		fighter.CollisionMask == StagePhysicsLayer;
}

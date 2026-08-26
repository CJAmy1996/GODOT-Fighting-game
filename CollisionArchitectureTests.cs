using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

/// <summary>Startup invariants for the normalized fighting-game collision split.</summary>
public partial class CollisionArchitectureTests : Node
{
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }

	public override void _Ready()
	{
		try
		{
			FighterController first = GetNode<FighterController>(FighterOnePath);
			FighterController second = GetNode<FighterController>(FighterTwoPath);
			ValidateFighter(first, "Player");
			ValidateFighter(second, "Opponent");
			if (first.TeamId == 0 || second.TeamId == 0 || first.IsSameTeam(second))
				throw new InvalidOperationException($"Opponents require distinct nonzero teams; got {first.TeamId} and {second.TeamId}.");
			GD.Print("Collision architecture tests passed: stage-only bodies, custom push/hit/hurt boxes, distinct teams.");
		}
		catch (Exception exception)
		{
			GD.PushError(exception.Message);
			GetTree().Quit(1);
		}
	}

	private static void ValidateFighter(FighterController fighter, string label)
	{
		if (!FighterCollisionPolicy.IsNormalized(fighter))
			throw new InvalidOperationException($"{label} violates fighter collision policy: layer={fighter.CollisionLayer}, mask={fighter.CollisionMask}.");
		if ((fighter.CollisionMask & FighterCollisionPolicy.FighterBodyLayer) != 0)
			throw new InvalidOperationException($"{label} uses physical fighter-to-fighter collision; spacing must use pushboxes.");
		if (fighter.PushboxLocal.Size.X <= 0f || fighter.PushboxLocal.Size.Y <= 0f)
			throw new InvalidOperationException($"{label} requires a positive pushbox.");
		if (fighter.HurtboxLocal.Size.X <= 0f || fighter.HurtboxLocal.Size.Y <= 0f)
			throw new InvalidOperationException($"{label} requires a positive fallback hurtbox.");
		if (!fighter.ParticipatesInPointCollision)
			throw new InvalidOperationException($"{label} must start as an active point fighter.");
	}
}

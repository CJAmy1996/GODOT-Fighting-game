using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

public partial class SuperJumpCommitRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			var fighter = new FighterController();
			fighter.RefreshAirJumpResourcesForSuperJump();
			fighter.Velocity = new Vector2(0f, -1265f);
			var normalJump = new JumpAbility { ReleaseVelocityMultiplier = 0.1f };
			var runtime = new AbilityRuntime { BoolValue = true, FramesRemaining = 10 };

			bool remainsActive = normalJump.Tick(fighter, runtime, 1f / 60f);
			Expect(!remainsActive, "released super-jump incorrectly retained normal variable-height state");
			Expect(Mathf.IsEqualApprox(fighter.Velocity.Y, -1265f),
				$"frame-1 release cut committed super-jump speed to {fighter.Velocity.Y:0.##}");
			Expect(!fighter.IsInShortHopRoute, "frame-1 release marked the super jump as a short hop");

			fighter.Free();
			GD.Print("SUPER_JUMP_COMMIT_TEST_PASS frame1_release=full_height short_hop=false");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"SUPER_JUMP_COMMIT_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

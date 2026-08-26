using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class HitReactionControllerRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			HitReactionController controller = new();
			controller.BeginHitReaction(12, FighterHitState.Hitstun, startedCrouching: true);
			Expect(controller.State.HitState == FighterHitState.Hitstun, "ordinary hit state changed");
			Expect(controller.State.HitstunFramesLeft == 12, "ordinary hit frame count changed");
			Expect(controller.State.HitReactionStartedCrouching, "crouch reaction snapshot changed");
			Expect(controller.State.HitReactionSerial == 1, "hit reaction serial changed");

			float pushback = controller.BeginBlockstun(0, 100f, GuardReactionStrength.None,
				SpecialReactionKind.GuardPullbackWeak, crouchBlocking: true);
			Expect(controller.State.HitState == FighterHitState.Blockstun, "block state changed");
			Expect(controller.State.HitstunFramesLeft == 1, "blockstun minimum changed");
			Expect(controller.State.GuardReactionStrength == GuardReactionStrength.Medium,
				"default guard reaction changed");
			Expect(controller.State.SpecialReaction == SpecialReactionKind.GuardPullbackWeak,
				"special guard reaction changed");
			Expect(controller.State.IsCrouchBlocking, "crouch block snapshot changed");
			Expect(controller.State.BlockReactionSerial == 1, "block reaction serial changed");
			Expect(Mathf.IsEqualApprox(pushback, -100f), "guard pullback direction changed");

			Vector2 firstJuggleVelocity = controller.BeginJuggleReaction(20, 40f, -220f,
				startedCrouching: false);
			Expect(controller.State.HitState == FighterHitState.Juggle && controller.State.JuggleHitCount == 1,
				"fresh juggle transition changed");
			controller.IncrementGroundNormalJuggleHitCount();
			controller.BeginJuggleReaction(18, 35f, -180f, startedCrouching: false);
			Expect(controller.State.JuggleHitCount == 2 && controller.State.GroundNormalJuggleHitCount == 1,
				"continuing juggle counters changed");
			Expect(firstJuggleVelocity == new Vector2(40f, -220f), "juggle velocity changed");
			Expect(HitReactionController.ResolveLaunchVelocity(50f, 600f) == new Vector2(50f, -600f),
				"launch velocity changed");
			Expect(HitReactionController.ResolveAirPopVelocity(new Vector2(10f, -300f), 45f, 200f) ==
				new Vector2(45f, -300f), "air-pop velocity preservation changed");
			Expect(HitReactionController.ResolveAirSpikeVelocity(new Vector2(10f, -100f), 55f, 420f) ==
				new Vector2(55f, 420f), "air-spike velocity changed");
			Expect(HitReactionController.ResolveInitialKnockdownState(KnockdownType.WallBounce, true) ==
				FighterHitState.WallBounce, "wall-bounce classification changed");
			Expect(HitReactionController.ResolveInitialKnockdownState(KnockdownType.HardKnockdown, true) ==
				FighterHitState.GroundedKnockdown, "grounded knockdown classification changed");
			controller.State.KnockdownType = KnockdownType.HardKnockdown;
			controller.BeginWakeup(3);
			Expect(controller.State.WakeupFramesLeft == 3 && controller.State.HitState == FighterHitState.None &&
				controller.State.KnockdownType == KnockdownType.None, "wakeup initialization changed");
			controller.TickWakeup();
			controller.TickWakeup();
			controller.TickWakeup();
			Expect(controller.State.WakeupFramesLeft == 0 && controller.State.ActiveWakeupTotalFrames == 0,
				"wakeup countdown changed");
			Expect(controller.BeginKnockdown(KnockdownType.None) == KnockdownType.AirKnockdown,
				"default knockdown type changed");
			controller.BeginWallSplat(WallBounceReactionStrength.None, -3);
			Expect(controller.State.WallBounceStrength == WallBounceReactionStrength.Strong &&
				controller.State.PendingWallSplatKnockdown && controller.State.WallSplatDirection == -1,
				"wall-splat initialization changed");
			controller.ConfigureGroundBounce(GroundBounceReactionStrength.None, 500f, intoJuggle: true);
			Expect(controller.State.GroundBounceStrength == GroundBounceReactionStrength.Medium &&
				Mathf.IsEqualApprox(controller.State.PendingGroundBounceSpeed, 500f) &&
				controller.State.PendingGroundBounceIntoJuggle, "ground-bounce initialization changed");
			GroundBounceLandingTransition bounceLanding = controller.ResolveGroundBounceLanding(420f);
			Expect(Mathf.IsEqualApprox(bounceLanding.BounceSpeed, 500f) && bounceLanding.IntoJuggle &&
				controller.State.HitState == FighterHitState.Juggle, "ground-bounce landing changed");
			controller.State.HitstunFramesLeft = 1;
			controller.State.HitState = FighterHitState.Juggle;
			Expect(controller.TickHitstun(airborneReactionMustPersist: true, groundedKnockdownHasWakeup: false) ==
				HitstunTickResult.PersistedUntilLanding && controller.State.HitstunFramesLeft == 1,
				"air reaction persistence changed");
			controller.ClearRecoveredReaction();
			Expect(controller.State.HitState == FighterHitState.None && controller.State.HitstunFramesLeft == 0 &&
				controller.State.JuggleHitCount == 0, "reaction recovery clearing changed");

			GD.Print("HIT_REACTION_CONTROLLER_TEST_PASS hit=12 block_min=1 pullback=-100 juggle=1>2 launch_pop_spike=preserved");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"HIT_REACTION_CONTROLLER_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

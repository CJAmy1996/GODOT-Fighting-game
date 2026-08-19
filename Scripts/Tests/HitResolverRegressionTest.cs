using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class HitResolverRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			HitstunResolution groundedLight = HitResolver.ResolveHitstun(BaseHitstunRequest with
			{
				GroundedNormalStrength = GroundedNormalStrength.Light,
				CounterHit = true
			});
			Expect(groundedLight.AuthoredBaseHitstun == 9 && groundedLight.BaseHitstun == 12 &&
				groundedLight.AppliedHitstun == 16,
				$"ground light resolution changed ({groundedLight})");

			HitstunResolution airNormal = HitResolver.ResolveHitstun(BaseHitstunRequest with
			{
				AuthoredBaseHitstun = 12,
				AttackerStartedAirborne = true,
				DefenderWasGrounded = false,
				CurrentAttackIsNormal = true,
				AirToAirNormalAdjustment = -2
			});
			Expect(airNormal.AppliedHitstun == 18, $"air-normal modifiers changed ({airNormal.AppliedHitstun})");

			PushbackResolution pushback = HitResolver.ResolvePushback(new PushbackResolutionRequest(
				IsLauncher: false, LauncherPushback: 0f, IsAirborneLightNormal: false,
				AirLightPushback: 50f, BasePushback: 100f, AttackerStartedAirborne: false,
				AirAttackMultiplier: 0.25f, DefenderWasGrounded: false,
				DefenderIsAirborneJuggle: true, DefenderJuggleHitCount: 3,
				JuggleDistanceScalePerHit: 0.1f, MaximumJuggleDistanceScale: 1.55f,
				GroundToAirMultiplier: 0.5f, GroundedNormalContinuingJuggle: true,
				GroundNormalJuggleMultiplier: 0.65f));
			Expect(Mathf.IsEqualApprox(pushback.AppliedPushback, 42.25f) &&
				pushback.GroundedNormalContinuesJuggle, $"juggle pushback order changed ({pushback.AppliedPushback})");

			Expect(HitResolver.ResolveBlockstun(-1, 9, instantBlock: true) == 3,
				"instant-block rounding/fallback changed");
			Expect(Mathf.IsEqualApprox(HitResolver.ResolveJuggleBounceSpeed(220f, 3, 24f, 60f), 172f),
				"juggle bounce decay changed");
			Expect(HitResolver.SelectReaction(BaseReactionRequest with
			{
				IsLauncher = true,
				RequestsKnockdown = true
			}) == ResolvedHitReaction.Launcher, "launcher no longer precedes knockdown");
			Expect(HitResolver.SelectReaction(BaseReactionRequest with
			{
				DefenderWasGrounded = false,
				DefenderAlreadyInJuggle = true
			}) == ResolvedHitReaction.ContinuingJuggle, "continuing juggle selection changed");

			GD.Print("HIT_RESOLVER_TEST_PASS grounded_light=12 counter=16 air_normal=18 pushback_order=42.25 instant_block=3 bounce=172 reaction_precedence=preserved");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"HIT_RESOLVER_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static HitstunResolutionRequest BaseHitstunRequest => new(
		AuthoredBaseHitstun: 9,
		JumpingHeavyHitGroundedDefender: false,
		JumpingHeavyGroundedHitstun: 12,
		GroundedNormalStrength: GroundedNormalStrength.None,
		GroundedLightHitstun: 12,
		GroundedMediumHitstun: 14,
		GroundedHeavyHitstun: 16,
		CounterHit: false,
		CounterHitBonus: 4,
		AttackerStartedAirborne: false,
		DefenderWasGrounded: true,
		CurrentAttackIsNormal: true,
		AirToAirBonus: 8,
		AirToAirNormalAdjustment: 0);

	private static HitReactionSelectionRequest BaseReactionRequest => new(
		HasBlowAway: false,
		IsLauncher: false,
		HasUnguardedSpecialReaction: false,
		AuthoredHitReaction: HitReactionKind.Normal,
		RequestsKnockdown: false,
		FinalSuperRush: false,
		FinalSuperKnockdown: false,
		DefenderWasGrounded: true,
		AttackerStartedAirborne: false,
		IsHeavyAttack: false,
		DefenderAlreadyInJuggle: false);

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

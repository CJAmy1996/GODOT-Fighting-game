using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class UniversalCombatBaselineRegressionTest : Node
{
	private static readonly string[] FighterScenes =
	{
		"res://Scenes/Characters/KungFuMan.tscn",
		"res://Scenes/Characters/SanzoKongoumaru.tscn",
		"res://Scenes/Characters/MechaHeita.tscn",
		"res://Scenes/Characters/Agito.tscn",
		"res://Scenes/Characters/Daigo.tscn",
		"res://Scenes/Characters/Heita.tscn",
		"res://Scenes/Characters/Kamui.tscn",
		"res://Scenes/Characters/Kinako.tscn",
		"res://Scenes/Characters/Kunagi.tscn",
		"res://Scenes/Characters/Rouga.tscn",
		"res://Scenes/Characters/Senna.tscn"
	};

	public override void _Ready()
	{
		try
		{
			foreach (string path in FighterScenes)
			{
				FighterController fighter = GD.Load<PackedScene>(path).Instantiate<FighterController>();
				Expect(fighter.LightAttackHitstunFrames == 10 && fighter.HeavyAttackHitstunFrames == 14,
					$"{path} overrides the universal 10f/14f normal hitstun baseline");
				Expect(fighter.LightAttackHitstopFrames == 5 && fighter.HeavyAttackHitstopFrames == 13 &&
					Mathf.IsEqualApprox(fighter.AirLightHitstopMultiplier, 1f),
					$"{path} overrides the universal light/heavy contact pacing");
				Expect(Mathf.IsEqualApprox(fighter.AirToGroundHitstopMomentumScale, 0.85f) &&
					fighter.ResolveLandingLagFramesForCurrentAirTime(11) == 2,
					$"{path} overrides universal jump-in motion or non-flight landing recovery");
				Expect(Mathf.IsEqualApprox(fighter.GroundedNonLauncherHitstopMultiplier, 0.85f),
					$"{path} overrides the universal grounded-normal hitstop pace");
				if (path.EndsWith("Kamui.tscn", StringComparison.Ordinal))
				{
					NormalMoveData sweep = fighter.Definition.NormalMoves.FindRule(
						FighterController.CrouchingHeavyKickName, true, false);
					Expect(sweep?.EffectAnimationName == "crouching_heavy_sword_effect" &&
						Mathf.IsEqualApprox(sweep.EffectRotationDegrees, 90f),
						"Kamui crouching HK does not resolve to the authored 90-degree sweep sword");
					NormalMoveData crouchingHeavyPunch = fighter.Definition.NormalMoves.FindRule(
						FighterController.CrouchingHeavyPunchName, true, false);
					Expect(crouchingHeavyPunch?.EffectSpriteFrames == null &&
						string.IsNullOrWhiteSpace(crouchingHeavyPunch.EffectAnimationName),
						"Kamui crouching HP still spawns the detached sword asset");
					NormalMoveData backPunch = fighter.Definition.NormalMoves.FindRule(
						FighterController.BackLightPunchName, false, false);
					Expect(backPunch?.EffectAnimationName == "medium_punch_effect" &&
						Mathf.IsEqualApprox(backPunch.EffectVelocity.X, 240f) &&
						Mathf.IsEqualApprox(backPunch.EffectHorizontalDecelerationPerFrame, 40f) &&
						backPunch.EffectFadeStartFrame == 4 &&
						Mathf.IsEqualApprox(backPunch.EffectOpacityLossPerFrame, 15f) &&
						backPunch.EffectScaleFromFacingBackEdge,
						"Kamui back+LP does not resolve to the source-authored moving/fading MP asset");
					NormalMoveData crouchingLightKick = fighter.Definition.NormalMoves.FindRule("LIGHT KICK", true, false);
					Expect(crouchingLightKick?.BoxTimeline != null &&
						Array.TrueForAll(crouchingLightKick.BoxTimeline, box => box?.HitSparkScene == null),
						"Kamui crouching LK still uses sword/slash contact feedback");
					SpecialMoveData ringFireball = fighter.Definition.SpecialMoves.FindMove(
						"KAMUI RING FIREBALL", false, false);
					SpecialMoveData ikazuchi = fighter.Definition.SpecialMoves.FindMove(
						"IKAZUCHI LIGHT", false, false);
					Expect(ikazuchi?.ProjectileVisualAdditiveBlend == true &&
						ikazuchi.ProjectileVisualBlackKey && ikazuchi.ProjectileVisualOpacityFrames.Length == 2 &&
						ikazuchi.ProjectileHitStartFrame == 0 && ikazuchi.ProjectilePersistsVisuallyAfterFinalHit &&
						ikazuchi.StartupFrames == 22 && ikazuchi.ProjectileAnimationName == "ikazuchi_active" &&
						ikazuchi.ProjectileLifetimeFrames == 52 &&
						ikazuchi.ProjectileVisualStartScale == new Vector2(2f, 6f) &&
						ikazuchi.ProjectileVisualScale == new Vector2(1f, 6f) &&
						ikazuchi.ProjectileVisualScaleEndFrame == 2,
						"Kamui Ikazuchi does not use the source SE/color rendering program");
					Expect(ringFireball?.ProjectileTrailCount == 6 && ringFireball.ProjectileTrailFrameSpacing == 4 &&
						ringFireball.StartupFrames == 10 && ringFireball.ActiveFrames == 1 &&
						ringFireball.RecoveryFrames == 47 && ringFireball.ProjectileSpawnOffset == new Vector2(136f, -114f) &&
						Mathf.IsEqualApprox(ringFireball.ProjectileSpeed, 620f) &&
						Mathf.IsZeroApprox(ringFireball.ProjectileSpeedDeltaPerFrame) &&
						Mathf.IsEqualApprox(ringFireball.ProjectileTrailOpacity, 1f) &&
						Mathf.IsEqualApprox(ringFireball.ProjectileTrailScaleStep, 0.05f) &&
						ringFireball.ProjectileTrailLifetimeFrames == 30 &&
						Mathf.IsEqualApprox(ringFireball.ProjectileTrailOpacityLossPerFrame, 10f) &&
						ringFireball.ProjectileVisualAdditiveBlend && ringFireball.ProjectileVisualBlackKey,
						"Kamui fireball does not match source actions 152/153");
					NormalMoveData airHeavyKick = fighter.Definition.NormalMoves.FindRule("HEAVY KICK", false, true);
					NormalMoveData airHeavyPunch = fighter.Definition.NormalMoves.FindRule(
						FighterController.AirHeavyPunchName, false, true);
					NormalMoveData standingLight = fighter.Definition.NormalMoves.FindRule("LIGHT PUNCH", false, false);
					NormalMoveData standingMedium = fighter.Definition.NormalMoves.FindRule("MEDIUM PUNCH", false, false);
					Expect(standingLight?.CanChainToMedium == true && standingLight.ChainEarliestActiveFramesLeft == 3 &&
						standingMedium?.CanChainToHeavy == true && standingMedium.ChainEarliestActiveFramesLeft == 3,
						"Kamui normal chain is not light -> medium -> heavy");
					var kamuiSuperJump = GD.Load<ModularFighter.Movement.SuperJumpAbility>(
						"res://Data/Characters/BigBangBeatRevolve/Kamui/kamui_super_jump.tres");
					Expect(kamuiSuperJump != null && Mathf.IsEqualApprox(kamuiSuperJump.InitialSpeed, 1300f) &&
						Mathf.IsEqualApprox(kamuiSuperJump.ForwardSpeed, 340f),
						"Kamui super jump cannot chase the launched opponent");
					Expect(!fighter.Definition.AllowLegacyFallbackMoves &&
						airHeavyKick?.AnimationName == "standing_heavy_kick" && airHeavyKick.SwordSlashSound >= 0 &&
						airHeavyPunch?.AnimationName == "forward_heavy_punch" && airHeavyPunch.SwordSlashSound >= 0 &&
						airHeavyPunch.BoxTimeline != null && Array.FindAll(airHeavyPunch.BoxTimeline,
							box => box?.Kind == FighterBoxKind.Hitbox).Length == 2,
						"Kamui still uses prototype air heavies or legacy Kung Fu Man fallbacks");
					SuperMoveData forwardLightning = Array.Find(fighter.Definition.SuperMoves,
						move => move?.AttackName == "SUPER SEVEN IKAZUCHI FORWARD");
					SuperMoveData backLightning = Array.Find(fighter.Definition.SuperMoves,
						move => move?.AttackName == "SUPER SEVEN IKAZUCHI BACK");
					Expect(forwardLightning?.ProjectileCount == 7 &&
						forwardLightning.ProjectileVisualAdditiveBlend && forwardLightning.ProjectileVisualBlackKey &&
						forwardLightning.ProjectileHitStartFrame == 16 && forwardLightning.ProjectilePersistsVisuallyAfterFinalHit &&
						Mathf.IsEqualApprox(forwardLightning.ProjectileVolleyHorizontalSpacing, 180f) &&
						forwardLightning.ProjectileVolleyScreenCarry && forwardLightning.ProjectileVolleyFinalOnlyKnockdown &&
						forwardLightning.FinalHitKnocksDown && forwardLightning.FinalKnockdownType == KnockdownType.HardKnockdown &&
						forwardLightning.ProjectilePlaysElectricitySound && forwardLightning.ProjectileElectrocutesDefender &&
						backLightning?.ProjectileCount == 7 && backLightning.ProjectileTargetsOpponent &&
						backLightning.ProjectileVisualAdditiveBlend && backLightning.ProjectileVisualBlackKey &&
						backLightning.ProjectileHitStartFrame == 16 && backLightning.ProjectilePersistsVisuallyAfterFinalHit &&
						Mathf.IsEqualApprox(backLightning.ProjectileVolleyHorizontalSpacing, -180f) &&
						backLightning.ProjectileVolleyScreenCarry && backLightning.ProjectileVolleyFinalOnlyKnockdown &&
						backLightning.FinalHitKnocksDown && backLightning.FinalKnockdownType == KnockdownType.HardKnockdown &&
						backLightning.ProjectilePlaysElectricitySound && backLightning.ProjectileElectrocutesDefender,
						"Kamui seven-bolt directional lightning supers are not configured");
				}
				fighter.Free();
			}
			GD.Print("UNIVERSAL_COMBAT_BASELINE_TEST_PASS roster=11 hitstun=10f/14f grounded-hitstop=85% air-light=full-paced jump-in=through-motion non-flight-landing<=2f");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"UNIVERSAL_COMBAT_BASELINE_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

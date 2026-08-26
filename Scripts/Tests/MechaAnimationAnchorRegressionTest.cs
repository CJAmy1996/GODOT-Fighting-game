using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

public partial class MechaAnimationAnchorRegressionTest : Node
{
	private static readonly Vector2[] StandingLightPunch =
	{
		new(0, 0), new(1, 0), new(14, 0), new(11, 0), new(0, 0), new(0, 0)
	};
	private static readonly Vector2[] CrouchingLightPunch =
	{
		new(0, 0), new(2, 0), new(20, 1), new(18, 1), new(5, 0), new(-1, 0)
	};
	private static readonly Vector2[] CrouchingHeavyPunch =
	{
		new(0, 0), new(18.5f, 0), new(21, 0), new(27, 0), new(24, 0),
		new(22, 0), new(22, 0), new(15.5f, 0), new(7, 1), new(4, 0)
	};
	private static readonly Vector2[] StandingMediumPunch =
	{
		new(0, 0), new(-5, -5), new(4, -5), new(21, -5), new(19, -5),
		new(13, -5), new(3.5f, -5), new(3, -4), new(0, 0)
	};
	private static readonly Vector2[] CrouchingMediumKick =
	{
		new(0, 0), new(0, 0), new(4, 3), new(3, 1.5f), new(2, 0), new(-1, 0), new(-3, 0)
	};
	private static readonly Vector2[] JumpingLightKick =
	{
		new(0, 0), new(3.5f, 15), new(2.5f, 15.5f), new(-4, 14), new(-6.5f, 16), new(-7, 3.5f)
	};
	private static readonly Vector2[] JumpingMediumPunch =
	{
		new(0, 0), new(1, 2), new(-4.5f, 7), new(-1.5f, 17.5f), new(-10, 23),
		new(-11, 24), new(-15, 23), new(-7.5f, 30), new(-3.5f, 13), new(-3.5f, -7)
	};
	private static readonly Vector2[] JumpingHeavyPunch =
	{
		new(0, 0), new(-3, 41.5f), new(-4, 42.5f), new(2, 21.5f), new(13.5f, 16.5f),
		new(11.5f, 16.5f), new(0.5f, 17.5f), new(-1.5f, 25), new(0, 14.5f), new(0.5f, 6.5f)
	};
	private static readonly Vector2[] JumpingHeavyKick =
	{
		new(0, 0), new(34, -29.5f), new(31, -29.5f), new(31, -29.5f), new(31, -29.5f),
		new(31, -29.5f), new(31, -29.5f), new(21, -28.5f), new(12, -12.5f), new(8, -10.5f),
		new(6, -2), new(0, 0), new(4.5f, 1.5f)
	};
	private static readonly Vector2[] StandingHeavyPunch =
	{
		new(0.5f, -9), new(-0.5f, 0), new(-1.5f, 0), new(-5.5f, -5), new(-1.5f, -5), new(8, -5),
		new(39, -8.5f), new(33.5f, -9), new(33.5f, -9), new(33.5f, -9), new(33.5f, -9), new(33.5f, -9),
		new(33.5f, -9), new(22.5f, -4), new(4.5f, -4), new(3, -2), new(-1.5f, 0), new(-1.5f, 0)
	};
	private static readonly Vector2[] JumpingMediumKick =
	{
		new(0, 0), new(-1, 1), new(-2, 0), new(11.5f, 11), new(12, 9), new(12, 9),
		new(6.5f, -12.5f), new(3.5f, -12.5f), new(0, -30), new(1, -27), new(0, -11),
		new(0, 17), new(0, 17.5f)
	};

	public override void _Ready()
	{
		FighterDefinition definition = GD.Load<FighterDefinition>(
			"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_definition.tres");
		SpriteFrames frames = GD.Load<SpriteFrames>(
			"res://Assets/TestFighter/BigBangBeatRevolve/MechaHeita/m_heita_sprite_frames.tres");
		int failures = 0;
		failures += Validate(definition?.NormalMoves?.FindRule("LIGHT PUNCH", false, false),
			StandingLightPunch, "standing LP");
		failures += Validate(definition?.NormalMoves?.FindRule("LIGHT PUNCH", true, false),
			CrouchingLightPunch, "crouching LP");
		NormalMoveData crouchingHeavyPunch = definition?.NormalMoves?.FindRule("HEAVY PUNCH CROUCHING", true, false);
		failures += Validate(crouchingHeavyPunch, CrouchingHeavyPunch, "crouching HP");
		failures += ValidateCrouchingHeavyPunchLauncher(crouchingHeavyPunch);
		failures += ValidateNative(definition?.NormalMoves?.FindRule(FighterController.BackLightPunchName, false, false),
			frames, StandingMediumPunch, "standing medium punch");
		failures += ValidateNative(definition?.NormalMoves?.FindRule(FighterController.CrouchingMediumKickName, true, false),
			frames, CrouchingMediumKick, "crouching medium kick");
		failures += ValidateNative(definition?.NormalMoves?.FindRule("LIGHT PUNCH", false, true),
			frames, CrouchingLightPunch, "jumping light punch");
		failures += ValidateNative(definition?.NormalMoves?.FindRule("LIGHT KICK", false, true),
			frames, JumpingLightKick, "jumping light kick");
		failures += ValidateNative(definition?.NormalMoves?.FindRule(FighterController.AirBackLightPunchName, false, true),
			frames, JumpingMediumPunch, "jumping medium punch");
		failures += ValidateNative(definition?.NormalMoves?.FindRule(FighterController.AirHeavyPunchName, false, true),
			frames, JumpingHeavyPunch, "jumping heavy punch");
		failures += Validate(definition?.NormalMoves?.FindRule("HEAVY KICK", false, true),
			JumpingHeavyKick, "jumping heavy kick");
		failures += ValidateNative(definition?.NormalMoves?.FindRule("HEAVY PUNCH", false, false),
			frames, StandingHeavyPunch, "standing HP");
		failures += ValidateStandingHeavyExplosion(definition?.NormalMoves?.FindRule("HEAVY PUNCH", false, false));
		failures += ValidateNative(definition?.NormalMoves?.FindRule(FighterController.AirBackLightKickName, false, true),
			frames, JumpingMediumKick, "jumping medium kick");
		failures += ValidateOpticBeam(definition?.SpecialMoves?.FindMove("LIGHT OPTIC BLAST", false, false), 5, 18,
			"light optic blast");
		failures += ValidateMissileExplosion(definition?.SpecialMoves?.FindMove("LIGHT MECHA MISSILE", false, false),
			"light missile");
		failures += ValidateMissileExplosion(definition?.SpecialMoves?.FindMove("HEAVY MECHA MISSILE", false, false),
			"heavy missile");
		failures += ValidateAirNormalCancels(definition);
		SuperJumpAbility superJump = GD.Load<SuperJumpAbility>(
			"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_super_jump.tres");
		if (superJump?.InitialSpeed != 1440f)
		{
			GD.PushError("Mecha anchor regression: super jump lost its launcher-chase height");
			failures++;
		}
		SuperMoveData fullFire = Array.Find(definition?.SuperMoves ?? Array.Empty<SuperMoveData>(),
			move => move?.AttackName == "SUPER FULL FIRE");
		failures += ValidateFullFireExplosion(fullFire);
		if (failures == 0)
			GD.Print("MECHA_ANIMATION_ANCHOR_TEST_PASS");
		GetTree().Quit(failures);
	}

	private static int ValidateOpticBeam(SpecialMoveData move, int hits, int lifetime, string label)
	{
		SpriteFrames frames = move?.ProjectileSpriteFrames;
		if (move is { Projectile: true, ProjectileSpeed: 0f, ProjectileAnchoredToOwner: true,
			ProjectileDirectionalHitbox: true, ProjectileHitCooldownFrames: 3 } &&
			move.ProjectileHitCount == hits && move.ProjectileLifetimeFrames == lifetime &&
			move.ProjectileHitboxLocal.Position.X == 0f && move.ProjectileHitboxLocal.Size.X == 620f &&
			frames != null && frames.HasAnimation("optic_blast") &&
			frames.GetFrameCount("optic_blast") == 2 && frames.GetAnimationLoop("optic_blast")) return 0;
		GD.PushError($"Mecha anchor regression: {label} lost its anchored multi-hit optic-beam setup");
		return 1;
	}

	private static int ValidateMissileExplosion(SpecialMoveData move, string label)
	{
		bool lightMissileJuggle = label != "light missile" ||
			move is { StartupFrames: 15, Launches: true, LaunchGroundedOnly: true, LaunchSpeed: 760f,
				LaunchPushback: 85f, LaunchHitstunFrames: 38, ProjectileSpawnOffset: { X: -16f, Y: -57f },
				ProjectilePathTravelFrames: 60 } && move.ProjectilePath?.PointCount == 6;
		bool heavyMissileOrigin = label != "heavy missile" ||
			move is { StartupFrames: 25, ProjectileSpawnOffset: { X: -19f, Y: -71f },
				ProjectilePathTravelFrames: 60 } && move.ProjectilePath?.PointCount == 6;
		if (move is { Projectile: true, ProjectileImpactAdditiveBlend: true, ProjectileImpactBlackKey: true,
			ProjectileImpactBlackensDefender: true, ProjectileImpactBlackSilhouetteFrames: 8 } &&
			lightMissileJuggle && heavyMissileOrigin &&
			move.ProjectileImpactAnimationName == "system_explosion" &&
			move.ProjectileImpactVisualOffset == Vector2.Zero &&
			move.ProjectileImpactDefenderFireSpriteFrames == move.ProjectileImpactSpriteFrames &&
			move.ProjectileImpactDefenderFireAnimationName == "system_burn_flame") return 0;
		GD.PushError($"Mecha anchor regression: {label} lost its contact-centered system explosion/burn presentation");
		return 1;
	}

	private static int ValidateAirNormalCancels(FighterDefinition definition)
	{
		int failures = 0;
		foreach (NormalMoveData move in definition?.NormalMoves?.Rules ?? System.Array.Empty<NormalMoveData>())
		{
			if (move?.Stance != NormalMoveStance.Airborne || move.CanChainToSpecial) continue;
			GD.PushError($"Mecha anchor regression: airborne normal '{move.AttackName}' lost special/super cancels");
			failures++;
		}
		return failures;
	}

	private static int ValidateFullFireExplosion(SuperMoveData move)
	{
		if (move is { Projectile: true, ProjectileImpactAdditiveBlend: true, ProjectileImpactBlackKey: true,
			ProjectileImpactBlackensDefender: true, ProjectileImpactBlackSilhouetteFrames: 8 } &&
			move.CommandInput is { Buttons: MotionAttackButton.AnyPunch,
				ButtonMatchMode: MotionButtonMatchMode.AllSelectedButtons } &&
			move.CommandInput.Motion?.MotionName == "Double Quarter Circle Forward" &&
			move.ProjectileImpactAnimationName == "system_explosion" &&
			move.ProjectileImpactVisualOffset == Vector2.Zero &&
			move.ProjectileImpactDefenderFireSpriteFrames == move.ProjectileImpactSpriteFrames &&
			move.ProjectileImpactDefenderFireAnimationName == "system_burn_flame") return 0;
		GD.PushError("Mecha anchor regression: full fire lost QCFx2+LP+HP or its missile explosion/burn presentation");
		return 1;
	}

	private int ValidateStandingHeavyExplosion(NormalMoveData move)
	{
		SpriteFrames frames = move?.EffectSpriteFrames;
		bool sourceAnimations = frames != null &&
			frames.HasAnimation("system_explosion") && frames.GetFrameCount("system_explosion") == 9 &&
			AttackDrawingTimeline.GetAuthoredTicks(frames, "system_explosion") == 18 &&
			frames.HasAnimation("system_burn_flame") && frames.GetFrameCount("system_burn_flame") == 15 &&
			AttackDrawingTimeline.GetAuthoredTicks(frames, "system_burn_flame") == 27;
		var effectProbe = new MoveVisualEffect();
		AddChild(effectProbe);
		effectProbe.Initialize(frames, "system_explosion", 1, Vector2.One, Vector2.Zero, true, true);
		AnimatedSprite2D effectSprite = effectProbe.GetChildCount() > 0
			? effectProbe.GetChild(0) as AnimatedSprite2D
			: null;
		bool blackKeyMaterial = effectSprite?.Material is ShaderMaterial;
		effectProbe.QueueFree();
		if (move is { EffectSpawnOnHitContact: true, EffectRequiresFullCharge: true,
			EffectAdditiveBlend: true, EffectBlackKey: true, EffectBlackensDefender: true,
			EffectBlackSilhouetteFrames: 8 } && move.EffectVisualOffset == Vector2.Zero &&
			move.EffectSpawnOffset == Vector2.Zero &&
			move.EffectAnimationName == "system_explosion" &&
			move.EffectDefenderFireAnimationName == "system_burn_flame" && sourceAnimations && blackKeyMaterial) return 0;
		GD.PushError("Mecha anchor regression: charged standing HP lost its contact-centered system explosion/burn presentation");
		return 1;
	}

	private static int ValidateCrouchingHeavyPunchLauncher(NormalMoveData move)
	{
		if (move is { Launches: true, LaunchSpeed: 1265f, LaunchPushback: 180f,
			LaunchHitstunFrames: 30, JumpCancelWindowFrames: 30,
			ChaseJumpSpeed: 1440f, ChaseForwardSpeed: 360f }) return 0;
		GD.PushError("Mecha anchor regression: down+HP is no longer a standard jump-cancelable launcher");
		return 1;
	}

	private static int ValidateNative(NormalMoveData move, SpriteFrames frames, Vector2[] expected, string label)
	{
		int failures = Validate(move, expected, label);
		if (move == null) return failures;
		if (move.AnimationSourceTimeline.Length > 0)
		{
			GD.PushError($"Mecha anchor regression: {label} overrides the original authored drawing timing");
			failures++;
		}
		int moveTicks = move.StartupFrames + move.ActiveFrames + move.RecoveryFrames;
		int sourceTicks = AttackDrawingTimeline.GetAuthoredTicks(frames, move.AnimationName);
		if (sourceTicks < moveTicks)
		{
			GD.PushError($"Mecha anchor regression: {label} has only {sourceTicks} source ticks for a {moveTicks}-tick move");
			failures++;
		}
		return failures;
	}

	private static int Validate(NormalMoveData move, Vector2[] expected, string label)
	{
		Vector2[] actual = move?.AnimationDrawingOffsets ?? Array.Empty<Vector2>();
		if (actual.Length == expected.Length)
		{
			for (int index = 0; index < expected.Length; index++)
				if (!actual[index].IsEqualApprox(expected[index]))
				{
					GD.PushError($"Mecha anchor regression: {label} drawing {index} is {actual[index]}, expected {expected[index]}");
					return 1;
				}
			return 0;
		}
		GD.PushError($"Mecha anchor regression: {label} has {actual.Length} anchors, expected {expected.Length}");
		return 1;
	}
}

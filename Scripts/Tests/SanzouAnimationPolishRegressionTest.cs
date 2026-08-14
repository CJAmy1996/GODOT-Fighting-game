using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Exact 60 Hz source-tick and big-body movement contract for Sanzou.</summary>
public partial class SanzouAnimationPolishRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			Validate();
			GD.Print("SANZOU ANIMATION POLISH TEST PASSED: move loops, tails, movement, and shake settings match the 60 Hz specification.");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU ANIMATION POLISH TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private void Validate()
	{
		var definition = ResourceLoader.Load<FighterDefinition>("res://Data/Characters/Sanzo/sanzo_kongoumaru.tres");
		var frames = ResourceLoader.Load<SpriteFrames>("res://Assets/TestFighter/Sanzo/sanzo_sprite_frames.tres");
		Expect(definition != null && frames != null, "Sanzou resources failed to load");

		NormalMoveData jab = Normal(definition, "LIGHT PUNCH", NormalMoveStance.Standing);
		ExpectTimeline(jab, 0, 1, 2, 3, 4, 5, 4, 5, 4, 5);
		ExpectDrawingsMatch(frames, jab);

		NormalMoveData crouchLightKick = Normal(definition, "LIGHT KICK", NormalMoveStance.Crouching);
		ExpectSlice(crouchLightKick.AnimationSourceTimeline, 8, 7, 6, 5, 4, 3, 2, 1, 0);
		Expect(crouchLightKick.AnimationSourceTimeline.Length == 29, "crouch LK timeline is not 29 ticks");

		NormalMoveData heavyPunch = Normal(definition, "HEAVY PUNCH", NormalMoveStance.Standing);
		Expect(heavyPunch.AnimationSourceTimeline.Skip(15).Take(4).All(frame => frame == 16),
			"standing HP does not hold source tick 16 for all four active ticks");
		for (int frame = 19; frame < 48; frame++)
			Expect(heavyPunch.AnimationSourceTimeline[frame] == (frame % 2 == 1 ? 17 : 18),
				$"standing HP recovery tick {frame} is not the 17/18 loop");

		NormalMoveData airHeavyKick = Normal(definition, "HEAVY KICK", NormalMoveStance.Airborne);
		Expect(airHeavyKick.ActiveFrames == 8, "air HK is not active for eight ticks");
		Expect(airHeavyKick.AnimationTailName == "fall" && airHeavyKick.AnimationTailStartFrame == 15,
			"air HK does not hand off to fall after frame 14");
		for (int frame = 7; frame <= 14; frame++)
			Expect(airHeavyKick.AnimationSourceTimeline[frame] == (frame % 2 == 1 ? 7 : 8),
				$"air HK active tick {frame} is not the 7/8 loop");
		Expect(!definition.NormalMoves.Rules.Any(move => move?.AttackName == "HEAVY KICK AIR UP"),
			"deleted Heavy Kick Up is still present");

		NormalMoveData sweep = Normal(definition, FighterController.CrouchingHeavyKickName, NormalMoveStance.Crouching);
		Expect(sweep.StartupFrames == 10, "sweep startup was not expanded to ten clean animation ticks");
		Expect(sweep.RecoveryFrames == 6 && sweep.AnimationSourceTimeline.Length == 21,
			"sweep contains recovery padding or an extra animation cycle");
		ExpectSlice(sweep.AnimationSourceTimeline, 0, 0, 0, 6, 6, 12, 12, 18, 18, 24, 24);
		ExpectSlice(sweep.AnimationSourceTimeline, 10, 30, 30, 30, 30, 30, 30, 24, 18, 12, 6, 0);

		NormalMoveData backLightPunch = Normal(definition, FighterController.BackLightPunchName, NormalMoveStance.Standing);
		ExpectAlternating(backLightPunch.AnimationSourceTimeline, 8, 8, 10, 11, "back LP");

		foreach (string qcf in new[] { FighterController.QcfPowerPunchLightName, FighterController.QcfPowerPunchHeavyName })
		{
			SpecialMoveData move = Special(definition, qcf);
			ExpectAlternating(move.AnimationSourceTimeline, 15, 8, 17, 19, qcf);
			ExpectSlice(move.AnimationSourceTimeline, 23, 21, 21, 22, 22, 23, 23, 28, 28, 33, 33);
		}

		SpecialMoveData rekka = Special(definition, FighterController.QcfPowerPunchRekkaName);
		Expect(rekka.ActiveFrames == 7, "rekka does not use half of its seven 11/12 cycles as active time");
		ExpectAlternating(rekka.AnimationSourceTimeline, 8, 14, 11, 12, "rekka");

		SpecialMoveData blockReflector = Special(definition, FighterController.BlockReflectorName);
		for (int cycle = 0; cycle < 4; cycle++)
			ExpectSlice(blockReflector.AnimationSourceTimeline, 6 + cycle * 3, 6, 7, 8);

		SpecialMoveData superReflector = Special(definition, FighterController.SanzoSuperReflectorName);
		Expect(superReflector.StartupFrames == 12, "super reflector dramatic pre-start is not twelve ticks");
		for (int cycle = 0; cycle < 4; cycle++)
			ExpectSlice(superReflector.AnimationSourceTimeline, cycle * 3, 6, 7, 8);

		Expect(Mathf.IsEqualApprox(definition.Tuning.WalkSpeed, 165f), "walk speed is not halved to 165");
		Expect(Mathf.IsEqualApprox(definition.Tuning.Gravity, 1807f), "jump gravity is not reduced by 35 percent");
		var neutralJump = ResourceLoader.Load<ModularFighter.Movement.JumpAbility>("res://Data/Characters/Sanzo/sanzo_neutral_jump.tres");
		var superJump = ResourceLoader.Load<ModularFighter.Movement.SuperJumpAbility>("res://Data/Characters/Sanzo/sanzo_super_jump.tres");
		var forwardDash = ResourceLoader.Load<ModularFighter.Movement.DashAbility>("res://Data/Characters/Sanzo/sanzo_forward_short_hop.tres");
		var backDash = ResourceLoader.Load<ModularFighter.Movement.DashAbility>("res://Data/Characters/Sanzo/sanzo_backdash.tres");
		Expect(Mathf.IsEqualApprox(neutralJump.InitialSpeed, 643.5f), "neutral jump launch is not reduced by 35 percent");
		NormalMoveData crouchingLauncher = Normal(definition, FighterController.CrouchingHeavyPunchName, NormalMoveStance.Crouching);
		Expect(Mathf.IsEqualApprox(superJump.InitialSpeed, crouchingLauncher.ChaseJumpSpeed),
			"raw super jump does not match the down+HP chase super-jump height");
		Expect(Mathf.IsEqualApprox(forwardDash.Speed, 295f) && Mathf.IsEqualApprox(backDash.Speed, 250f),
			"forward/back dash speeds are not halved");

		SpecialMoveData stomp = Special(definition, FighterController.StompSpecialName);
		float riseSeconds = stomp.ForceDownwardStartFrame / 60f;
		float stompHeight = stomp.SelfLaunchSpeed * riseSeconds - 0.5f * definition.Tuning.Gravity * riseSeconds * riseSeconds;
		float normalJumpHeight = neutralJump.InitialSpeed * neutralJump.InitialSpeed / (2f * definition.Tuning.Gravity);
		Expect(stompHeight >= normalJumpHeight * 2f,
			$"stomp height {stompHeight:0.0} is not at least twice normal jump height {normalJumpHeight:0.0}");
		Expect(stomp.ActiveFrames == 120 && stomp.ForceDownwardStartFrame == 32,
			"stomp does not preserve a long high-rise/descent active window");
		Expect(Mathf.IsEqualApprox(stomp.ForceDownwardSpeed, 380f) &&
			Mathf.IsEqualApprox(stomp.ForceDownwardTerminalSpeed, 480f),
			"stomp descent is not the slower controlled fall");
		Expect(stomp.RiseAnimationSourceCycle.SequenceEqual(new[] { 2, 3 }) &&
			stomp.DescentAnimationSourceCycle.SequenceEqual(new[] { 7, 8, 9 }),
			"stomp rise/descent drawing cycles are wrong");
		Expect(stomp.LandingRecoveryFrames == 12 && stomp.LandingAnimationSourceTimeline.Length == 12,
			"stomp landing animation is not a full twelve ticks");
		FighterBoxFrame stompHit = stomp.BoxTimeline.First(box => box?.Tag == "stomp-finisher");
		Expect(stompHit.StartFrame == 32 && stompHit.EndFrame < 0 && stompHit.GroundBounceIntoJuggle,
			"stomp hitbox does not remain active through landing into juggle");
		Expect(Mathf.IsEqualApprox(stompHit.GroundBounceSpeed, 340f) && stompHit.Pushback <= 35f,
			"stomp ground bounce is not small and close to Sanzou");

		Expect(definition.Gauges.MaxLife == 1200 && definition.Gauges.StartingLife == 1200,
			"Sanzou life is not full at 1200");
		Expect(definition.Gauges.LifeColor.G > 0.9f && definition.Gauges.LifeColor.R < 0.3f,
			"Sanzou life bar is not green");
		var hud = new CombatHud();
		Expect(hud.LifeBarTopOffset == 136f && hud.ComboCounterOffset == 48f,
			"life-bar height or under-bar combo counter anchor is wrong");
		hud.Free();

		var scene = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn");
		var fighter = scene.Instantiate<SpriteTestFighter>();
		Expect(fighter.HeavyWalkFootstepShake && fighter.HeavyLandingShake,
			"Sanzou walk/landing shakes are not enabled");
		Expect(fighter.HeavyWalkShakeStrength >= 4.5f && fighter.HeavyLandingShakeStrength >= 5.25f,
			"Sanzou shake strengths are not visibly heavy");
		fighter.Free();
	}

	private static NormalMoveData Normal(FighterDefinition definition, string name, NormalMoveStance stance) =>
		definition.NormalMoves.Rules.FirstOrDefault(move => move != null && move.AttackName == name && move.Stance == stance)
		?? throw new InvalidOperationException($"missing normal {name} ({stance})");

	private static SpecialMoveData Special(FighterDefinition definition, string name) =>
		definition.SpecialMoves.Moves.FirstOrDefault(move => move != null && move.AttackName == name)
		?? throw new InvalidOperationException($"missing special {name}");

	private static void ExpectDrawingsMatch(SpriteFrames frames, NormalMoveData move)
	{
		for (int frame = 0; frame < move.AnimationSourceTimeline.Length; frame++)
		{
			int resolved = AttackDrawingTimeline.Resolve(frames, move.AnimationName, frame,
				move.StartupFrames, move.ActiveFrames, move.RecoveryFrames, false, move.AnimationSourceTimeline);
			int direct = AttackDrawingTimeline.ResolveSourceTick(frames, move.AnimationName, move.AnimationSourceTimeline[frame]);
			Expect(resolved == direct, $"{move.AttackName} frame {frame} resolved drawing {resolved}, expected {direct}");
		}
	}

	private static void ExpectTimeline(NormalMoveData move, params int[] expected) =>
		Expect(move.AnimationSourceTimeline.SequenceEqual(expected), $"{move.AttackName} source timeline differs");

	private static void ExpectAlternating(int[] timeline, int start, int count, int first, int second, string label)
	{
		for (int offset = 0; offset < count; offset++)
			Expect(timeline[start + offset] == (offset % 2 == 0 ? first : second),
				$"{label} tick {start + offset} breaks the {first}/{second} loop");
	}

	private static void ExpectSlice(int[] timeline, int start, params int[] expected)
	{
		for (int offset = 0; offset < expected.Length; offset++)
			Expect(start + offset < timeline.Length && timeline[start + offset] == expected[offset],
				$"timeline tick {start + offset} expected source {expected[offset]}");
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

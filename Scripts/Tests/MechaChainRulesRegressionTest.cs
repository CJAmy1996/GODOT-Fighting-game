using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class MechaChainRulesRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			NormalMoveSet moves = GD.Load<NormalMoveSet>(
				"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_normal_moves.tres");
			CheckStance(moves, airborne: false,
				"LIGHT PUNCH", "LIGHT KICK", "MEDIUM PUNCH BACK", "CROUCHING MEDIUM KICK");
			CheckStance(moves, airborne: true,
				"LIGHT PUNCH", "LIGHT KICK", FighterController.AirBackLightPunchName,
				FighterController.AirBackLightKickName);
			FighterDefinition definition = GD.Load<FighterDefinition>(
				"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_definition.tres");
			CheckCharacterCancelRules(definition, moves);
			GD.Print("MECHA_CHAIN_RULES_TEST_PASS ground+air lights=all-lights>medium>heavy mediums=reciprocal-once>heavy lk+mk=jump-cancel all_normals=special-cancel buffer=10f");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"MECHA_CHAIN_RULES_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void CheckCharacterCancelRules(FighterDefinition definition, NormalMoveSet moves)
	{
		Expect(definition?.Tuning?.InputBufferFrames == 8,
			"Mecha's attack/special buffer is not the authored 8 frames");
		Expect(definition.Tuning.SpecialCancelBufferFrames == 10,
			"Mecha's motion-special cancel buffer is not the authored 10 frames");
		foreach (string move in new[] { "LIGHT KICK", "CROUCHING MEDIUM KICK", FighterController.AirBackLightKickName,
			"MEDIUM PUNCH BACK", FighterController.AirBackLightPunchName })
		{
			bool found = false;
			foreach (CancelRule rule in definition.CancelRules)
			{
				if (rule != null && rule.Allows(move, "JUMP", CancelKind.Jump, true, true,
					0, 0, 0))
				{
					found = true;
					break;
				}
			}
			Expect(found, $"{move} is missing its on-contact jump cancel");
		}
		NormalMoveData airLightKick = moves.FindRule("LIGHT KICK", false, true);
		NormalMoveData airMediumKick = moves.FindRule(FighterController.AirBackLightKickName, false, true);
		Expect(airLightKick?.PreserveAirborneTargetVelocity == true,
			"air LK does not preserve the defender's vertical velocity");
		Expect(airLightKick.HitstunFrames == 12,
			"air LK does not have the requested base 12-frame hitstun");
		Expect(airLightKick.RepeatLightPunchChainTarget == FighterController.AirBackLightPunchName,
			"air LK lost its LP-to-MP route");
		Expect(airMediumKick?.PreserveAirborneTargetVelocity == true,
			"air MK does not preserve airborne velocity like the original air LK reaction");

		foreach (NormalMoveData move in moves.Rules)
		{
			if (move == null || move.AttackName is "THROW" or "BACK THROW") continue;
			bool found = false;
			foreach (CancelRule rule in definition.CancelRules)
			{
				if (rule != null && rule.Allows(move.AttackName, "ANY AUTHORED SPECIAL",
					CancelKind.Special, true, true, 0, 0, 0))
				{
					found = true;
					break;
				}
			}
			Expect(found, $"{move.AttackName} is not covered by the character-level normal-to-special rule");
		}
	}

	private static void CheckStance(NormalMoveSet moves, bool airborne,
		string lightPunchName, string lightKickName, string mediumPunchName, string mediumKickName)
	{
		NormalMoveData lightPunch = moves.FindRule(lightPunchName, false, airborne);
		NormalMoveData lightKick = moves.FindRule(lightKickName, false, airborne);
		NormalMoveData mediumPunch = moves.FindRule(mediumPunchName, false, airborne);
		NormalMoveData mediumKick = moves.FindRule(mediumKickName, !airborne, airborne);
		Expect(lightPunch != null && lightKick != null && mediumPunch != null && mediumKick != null,
			$"{(airborne ? "air" : "ground")} chain move lookup failed");

		foreach (NormalMoveData light in new[] { lightPunch, lightKick })
		{
			Expect(light.MaxUsesPerCombo == (airborne ? 1 : 0),
				$"{light.AttackName} has the wrong {(airborne ? "air" : "ground")} repeat limit");
			Expect(light.CancelWindowStartFrame == 0,
				$"{light.AttackName} chain window still waits until active frames end");
			Expect(light.AllowsChainTo(lightPunchName, false, airborne) &&
				light.AllowsChainTo(lightKickName, false, airborne) &&
				light.AllowsChainTo(mediumPunchName, false, airborne) &&
				light.AllowsChainTo(mediumKickName, !airborne, airborne) &&
				light.AllowsChainTo("HEAVY PUNCH", false, airborne),
				$"{light.AttackName} does not route through all lights, mediums, and heavies");
		}
		if (airborne)
			Expect(lightKick.RepeatLightKickChainTarget == mediumKickName,
				"air LK repeat does not route into air MK");

		Expect(mediumPunch.MaxUsesPerCombo == 1 && mediumKick.MaxUsesPerCombo == 1,
			"medium normals are not capped at one use each per combo");
		Expect(mediumPunch.CancelWindowStartFrame == 0 && mediumKick.CancelWindowStartFrame == 0 &&
			mediumPunch.AllowsChainTo(mediumKickName, !airborne, airborne) &&
			mediumKick.AllowsChainTo(mediumPunchName, false, airborne) &&
			mediumPunch.AllowsChainTo("HEAVY KICK", false, airborne) &&
			mediumKick.AllowsChainTo("HEAVY PUNCH", false, airborne),
			"medium normals are not reciprocal hit chains into heavy");
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

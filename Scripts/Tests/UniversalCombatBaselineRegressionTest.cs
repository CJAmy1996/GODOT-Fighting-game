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
				fighter.Free();
			}
			GD.Print("UNIVERSAL_COMBAT_BASELINE_TEST_PASS roster=11 hitstun=10f/14f hitstop=5f/13f air-light=full-paced jump-in=through-motion non-flight-landing<=2f");
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

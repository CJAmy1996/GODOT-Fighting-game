using System;
using System.IO;
using System.Reflection;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

public partial class BigBangSourcePortRegressionTest : Node
{
	private static readonly (string Stem, string Slug)[] ImportedFighters =
	{
		("Agito", "agito"), ("Daigo", "daigo"), ("Heita", "heita"),
		("Kamui", "kamui"), ("Kinako", "kinako"), ("Kunagi", "kunagi"),
		("MechaHeita", "m_heita"), ("Rouga", "rouga"), ("Senna", "senna")
	};

	public override void _Ready()
	{
		try
		{
			foreach ((string stem, string slug) in ImportedFighters)
			{
				FighterDefinition definition = GD.Load<FighterDefinition>(
					$"res://Data/Characters/BigBangBeatRevolve/{stem}/{slug}_definition.tres");
				Expect(definition?.SuperPortrait != null,
					$"{stem} did not load its archive-authored universal super portrait");
			}

			FighterDefinition mecha = GD.Load<FighterDefinition>(
				"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_definition.tres");
			SpecialMoveData ground = mecha.SpecialMoves.FindMove("HELICOPTER DP", false, false);
			SpecialMoveData air = mecha.SpecialMoves.FindMove("LIGHT HELICOPTER DP", false, true);
			Expect(ground?.AnimationName == "anim_130" && ground.Stance == NormalMoveStance.Standing,
				"grounded DP is not source visual 130");
			Expect(ground.SelfLaunchStartFrame == 9 && ground.SelfLaunchUsesFacing &&
				Mathf.IsEqualApprox(ground.SelfHorizontalSpeed, 1000f) &&
				Mathf.IsEqualApprox(ground.SelfLaunchSpeed, 800f) &&
				Mathf.IsEqualApprox(ground.SelfHorizontalDeceleration, 2400f),
				"grounded DP does not preserve M 1000,-800,-40/tick on frame 9");
			Expect(ground.ActiveFrames == 27 && ground.RecoveryFrames == 0 &&
				ground.AnimationSourceTimeline?.Length == 36 && ground.AnimationSourceTimeline[^1] == 11 &&
				Array.FindAll(ground.BoxTimeline, box => box?.Kind == FighterBoxKind.Hitbox).Length == 13 &&
				Array.TrueForAll(ground.BoxTimeline, box => box == null || box.EndFrame <= 35),
				"grounded DP does not keep all 13 hits inside the helicopter drawings before natural fall");
			Expect(ground.HitSparkScene == null,
				"grounded DP blood must be selected by its source FA hit group, not the entire move");
			for (int hit = 1; hit <= 8; hit++)
			{
				FighterBoxFrame box = FindBox(ground, $"helicopter-hit-{hit}");
				Expect(box?.HitSparkScene?.ResourcePath == "res://Effects/BigBangBloodHitSpark.tscn",
					$"grounded DP source blood mapping is missing from middle hit {hit}");
			}
			for (int hit = 9; hit <= 13; hit++)
			{
				Expect(FindBox(ground, $"helicopter-hit-{hit}")?.HitSparkScene == null,
					$"grounded DP final FA group incorrectly uses blood on hit {hit}");
			}
			Expect(air?.AnimationName == "anim_132" && air.Stance == NormalMoveStance.Airborne &&
				air.CommandInput?.AirOnly == true && !air.CommandInput.GroundOnly,
				"airborne DP is not source visual 132 or is not air-only");
			Expect(air.SelfLaunchStartFrame == 9 && air.SelfLaunchUsesFacing &&
				Mathf.IsZeroApprox(air.SelfHorizontalSpeed) && Mathf.IsEqualApprox(air.SelfLaunchSpeed, 800f),
				"airborne DP does not preserve M 0,-800 on frame 9");
			Expect(air.HitSparkScene == null,
				"airborne DP incorrectly inherited grounded action 130's common blood effect");

			// Exercise the actual C# launch resolver, not just the .tres values.
			var motionProbe = new FighterController();
			FieldInfo currentSpecial = typeof(FighterController).GetField("_currentSpecialMove",
				BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo currentMove = typeof(FighterController).GetField("_currentMoveData",
				BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo contactSpark = typeof(FighterController).GetField("_currentContactHitSparkScene",
				BindingFlags.Instance | BindingFlags.NonPublic);
			MethodInfo applyLaunch = typeof(FighterController).GetMethod("ApplyCurrentSpecialSelfLaunch",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Expect(currentSpecial != null && currentMove != null && contactSpark != null && applyLaunch != null,
				"C# source DP resolver or per-contact spark resolver is missing");
			motionProbe.SetFacing(-1);
			currentSpecial.SetValue(motionProbe, ground);
			applyLaunch.Invoke(motionProbe, new object[] { ground.AttackName });
			Expect(motionProbe.Velocity.IsEqualApprox(new Vector2(-1000f, -800f)),
				$"grounded DP C# velocity resolved to {motionProbe.Velocity}");
			currentSpecial.SetValue(motionProbe, air);
			applyLaunch.Invoke(motionProbe, new object[] { air.AttackName });
			Expect(motionProbe.Velocity.IsEqualApprox(new Vector2(0f, -800f)),
				$"airborne DP C# velocity resolved to {motionProbe.Velocity}");
			currentMove.SetValue(motionProbe, ground);
			contactSpark.SetValue(motionProbe, FindBox(ground, "helicopter-hit-1").HitSparkScene);
			Expect(motionProbe.CurrentHitSparkScene?.ResourcePath == "res://Effects/BigBangBloodHitSpark.tscn",
				"C# contact resolver did not select the grounded DP spin hit's blood resource");
			contactSpark.SetValue(motionProbe, FindBox(ground, "helicopter-hit-9").HitSparkScene);
			Expect(motionProbe.CurrentHitSparkScene == null,
				"C# contact resolver leaked blood into the grounded DP finishing FA group");
			motionProbe.Free();

			for (ulong index = 0; index < 2; index++)
			{
				string backdrop = VersusStageRules.ChooseHyperComboBackdropPath(index);
				Expect(File.Exists(ProjectSettings.GlobalizePath(backdrop)),
					$"universal galaxy backdrop is missing: {backdrop}");
			}

			string actionCatalogPath = ProjectSettings.GlobalizePath(
				"res://Assets/Effects/BigBangCommon/common_animation_catalog.csv");
			string resourceCatalogPath = ProjectSettings.GlobalizePath(
				"res://Assets/Effects/BigBangCommon/common_resource_usage.csv");
			Expect(File.Exists(actionCatalogPath) && File.Exists(resourceCatalogPath),
				"common source catalogs were not generated");
			Expect(ResourceLoader.Exists("res://Effects/BigBangSuperCancelEffect.tscn"),
				"universal BBB special-impact/super-cancel composite scene is missing");
			var activationEffect = GD.Load<PackedScene>("res://Effects/BigBangSuperCancelEffect.tscn")
				.Instantiate<BigBangSuperCancelEffect>();
			activationEffect.ConfigureScreenCoverage(new Vector2(860f, 486f), new Vector2(-120f, 80f));
			AddChild(activationEffect);
			Sprite2D activationLightning = activationEffect.GetNodeOrNull<Sprite2D>("InnerLightning");
			Sprite2D activationOuterLightning = activationEffect.GetNodeOrNull<Sprite2D>("OuterLightning");
			Sprite2D activationCore = activationEffect.GetNodeOrNull<Sprite2D>("ImpactRing");
			Vector2 innerConnection = activationLightning.Position +
				new Vector2(-activationLightning.Texture.GetWidth(), activationLightning.Texture.GetHeight()) *
				activationLightning.Scale * 0.5f;
			Vector2 outerConnection = activationOuterLightning.Position +
				new Vector2(activationOuterLightning.Texture.GetWidth(), -activationOuterLightning.Texture.GetHeight()) *
				activationOuterLightning.Scale * 0.5f;
			Expect(activationEffect.CoverageWorldSize == new Vector2(860f, 486f) &&
				activationLightning != null && activationLightning.Scale.X > 1f &&
				activationOuterLightning is { FlipH: true } &&
				innerConnection.IsEqualApprox(new Vector2(-120f, 80f)) &&
				outerConnection.IsEqualApprox(new Vector2(-120f, 80f)) &&
				activationCore?.Position == new Vector2(-120f, 80f) &&
				activationEffect.GetNodeOrNull<Sprite2D>("InnerLightningOpposite") == null &&
				activationEffect.GetNodeOrNull<Sprite2D>("OuterLightningQuarterTurn") == null,
				"super activation does not join exactly two diagonal lightning sheets at the fighter-centered core");
			activationEffect.QueueFree();
			string catalog = File.ReadAllText(actionCatalogPath);
			Expect(catalog.Contains("common_section_020") && catalog.Contains("192 193 194 195 196 197 198 199") &&
				catalog.Contains("common_section_026") && catalog.Contains("200 201 202 203 204 205 206 207 208 209 210"),
				"common catalog does not contain exact guard and blood mappings");

			GD.Print("BIGBANG_SOURCE_PORT_TEST_PASS portraits=9 galaxies=2 guard=192-199 " +
				"hits=57-64 blood=200-211 super_cancel=000-015+017-036 ground_dp=1000,-800 air_dp=0,-800");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"BIGBANG_SOURCE_PORT_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}

	private static FighterBoxFrame FindBox(NormalMoveData move, string tag)
	{
		if (move?.BoxTimeline == null) return null;
		foreach (FighterBoxFrame box in move.BoxTimeline)
		{
			if (box?.Tag == tag) return box;
		}
		return null;
	}
}

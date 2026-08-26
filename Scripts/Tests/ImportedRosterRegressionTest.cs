using System;
using System.IO;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

/// <summary>Loads every staged BBBR fighter and verifies its independent editor-ready resources.</summary>
public partial class ImportedRosterRegressionTest : Node
{
	private static readonly (string ScenePath, string FighterName, string CatalogPath, ArenaCharacterLoader.CharacterChoice Choice)[] Fighters =
	{
		Imported("Kinako", "Kinako", "Kinako", ArenaCharacterLoader.CharacterChoice.Kinako),
		Imported("Senna", "Senna", "Senna", ArenaCharacterLoader.CharacterChoice.Senna),
		Imported("MechaHeita", "Mecha Heita", "MechaHeita", ArenaCharacterLoader.CharacterChoice.MechaHeita),
		Imported("Kunagi", "Kunagi", "Kunagi", ArenaCharacterLoader.CharacterChoice.Kunagi),
		Imported("Daigo", "Daigo", "Daigo", ArenaCharacterLoader.CharacterChoice.Daigo),
		Imported("Rouga", "Rouga", "Rouga", ArenaCharacterLoader.CharacterChoice.Rouga),
		Imported("Kamui", "Kamui", "Kamui", ArenaCharacterLoader.CharacterChoice.Kamui),
		Imported("Heita", "Heita", "Heita", ArenaCharacterLoader.CharacterChoice.Heita),
		Imported("Agito", "Agito", "Agito", ArenaCharacterLoader.CharacterChoice.Agito)
	};

	private static (string, string, string, ArenaCharacterLoader.CharacterChoice) Imported(
		string sceneStem, string fighterName, string assetDirectory, ArenaCharacterLoader.CharacterChoice choice) =>
		($"res://Scenes/TestCharacters/{sceneStem}Test.tscn", fighterName,
			$"res://Assets/TestFighter/BigBangBeatRevolve/{assetDirectory}/animation_catalog.csv", choice);

	private static readonly StringName[] RequiredAnimations =
	{
		"anim_000", "idle", "walk", "walk_back", "neutral_jump", "fall",
		"crouch_start", "crouch_hold", "crouch_end", "light_punch", "heavy_punch",
		"crouching_light_punch", "air_light_punch", "hitstun_light", "knockdown",
		"get_up", "stand_block", "crouch_block", "air_block"
	};

	public override void _Ready()
	{
		int failures = 0;
		foreach ((string scenePath, string expectedName, string catalogPath, _) in Fighters)
		{
			PackedScene packed = GD.Load<PackedScene>(scenePath);
			if (packed == null)
			{
				GD.PushError($"Imported roster regression: could not load {scenePath}");
				failures++;
				continue;
			}

			Node instance = packed.Instantiate();
			if (instance is not FighterController fighter)
			{
				GD.PushError($"Imported roster regression: {scenePath} is not a FighterController");
				instance.Free();
				failures++;
				continue;
			}

			if (fighter.Definition == null || fighter.Definition.FighterName != expectedName)
			{
				GD.PushError($"Imported roster regression: {expectedName} has the wrong definition");
				failures++;
			}
			if (fighter.Definition?.NormalMoves == null || fighter.Definition?.SpecialMoves == null)
			{
				GD.PushError($"Imported roster regression: {expectedName} is missing independent move-set resources");
				failures++;
			}
			if (fighter.Definition?.SuperPortrait == null)
			{
				GD.PushError($"Imported roster regression: {expectedName} is missing its universal super portrait");
				failures++;
			}
			if (fighter.Definition?.StateBoxes?.Rules == null || fighter.Definition.StateBoxes.Rules.Length < 6)
			{
				GD.PushError($"Imported roster regression: {expectedName} is missing editable state-box resources");
				failures++;
			}

			AnimatedSprite2D sprite = instance.GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
			SpriteFrames frames = sprite?.SpriteFrames;
			if (frames == null || frames.GetAnimationNames().Length < 100)
			{
				GD.PushError($"Imported roster regression: {expectedName} did not load its raw action catalog");
				failures++;
			}
			else
			{
				foreach (StringName animation in RequiredAnimations)
					if (!frames.HasAnimation(animation) || frames.GetFrameCount(animation) <= 0)
					{
						GD.PushError($"Imported roster regression: {expectedName} is missing {animation}");
						failures++;
					}
				failures += ValidateCatalog(expectedName, catalogPath, frames);
				if (expectedName == "Mecha Heita")
					failures += ValidateMechaHeitaNeutral(fighter, frames);
			}
			instance.Free();
		}

		PackedScene arenaScene = GD.Load<PackedScene>("res://Arena.tscn");
		if (arenaScene == null)
		{
			GD.PushError("Imported roster regression: TestArena did not load");
			failures++;
		}
		else
		{
			foreach ((_, string expectedName, _, ArenaCharacterLoader.CharacterChoice choice) in Fighters)
			{
				ArenaCharacterLoader.SelectedCharacter = choice;
				ArenaCharacterLoader arena = arenaScene.Instantiate<ArenaCharacterLoader>();
				arena._EnterTree();
				FighterController selected = arena.GetNodeOrNull<FighterController>("Fighter");
				if (selected?.Definition?.FighterName != expectedName)
				{
					GD.PushError($"Imported roster regression: arena selection did not load {expectedName}");
					failures++;
				}
				arena.Free();
			}
			ArenaCharacterLoader.SelectedCharacter = ArenaCharacterLoader.CharacterChoice.KungFuMan;
		}

		if (failures == 0)
			GD.Print($"IMPORTED_ROSTER_REGRESSION_PASS fighters={Fighters.Length}");
		GetTree().Quit(failures == 0 ? 0 : 1);
	}

	private static int ValidateCatalog(string fighterName, string catalogPath, SpriteFrames frames)
	{
		string systemPath = ProjectSettings.GlobalizePath(catalogPath);
		if (!File.Exists(systemPath))
		{
			GD.PushError($"Imported roster regression: {fighterName} catalog is missing");
			return 1;
		}

		int failures = 0;
		foreach (string line in File.ReadLines(systemPath).Skip(1))
		{
			string[] columns = line.Split(',');
			if (columns.Length < 13 || !int.TryParse(columns[5], out int drawingCount))
			{
				GD.PushError($"Imported roster regression: malformed {fighterName} catalog row");
				failures++;
				continue;
			}

			StringName animation = columns[0].Trim().TrimStart('\ufeff');
			if (!frames.HasAnimation(animation))
			{
				GD.PushError($"Imported roster regression: {fighterName} is missing source animation {animation}");
				failures++;
				continue;
			}
			if (frames.GetFrameCount(animation) != drawingCount)
			{
				GD.PushError($"Imported roster regression: {fighterName} {animation} does not preserve every source drawing slot");
				failures++;
			}

			int catalogTicks = columns[9].Split(' ', StringSplitOptions.RemoveEmptyEntries).Sum(int.Parse);
			int animationTicks = Enumerable.Range(0, frames.GetFrameCount(animation))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(animation, index)));
			if (animationTicks != catalogTicks)
			{
				GD.PushError($"Imported roster regression: {fighterName} {animation} is {animationTicks} ticks, expected {catalogTicks}");
				failures++;
			}
			bool approvedMechaAssignment = fighterName == "Mecha Heita" &&
				(columns[4] == "FLY_UP" || columns[4] == "JUMPING_HEAVY_KICK" ||
				 columns[4] == "STANDING_LIGHT_PUNCH" || columns[4] == "STANDING_MEDIUM_PUNCH_BACK" ||
				 columns[4] == "STANDING_HEAVY_KICK" || columns[4] == "CROUCHING_LIGHT_PUNCH" ||
				 columns[4] == "CROUCHING_LIGHT_KICK_AND_DOWN_BACK_MEDIUM_KICK" ||
				 columns[4] == "CROUCHING_HEAVY_PUNCH" || columns[4] == "JUMPING_LIGHT_KICK" ||
				 columns[4] == "JUMPING_MEDIUM_PUNCH_BACK_LP" || columns[4] == "JUMPING_HEAVY_PUNCH" ||
					 columns[4] == "STANDING_LIGHT_KICK_FAST" || columns[4] == "THROW_STARTUP" ||
					 columns[4] == "FORWARD_THROW" || columns[4] == "BACK_THROW" ||
					 columns[4] == "BACK_THROW_STARTUP" ||
					 columns[4] == "THIRTEEN_HIT_HELICOPTER_DP" ||
					 columns[4] == "SPECIAL_MOVE_LANDING_RECOVERY" ||
					 columns[4] == "AIRBORNE_HELICOPTER_DP");
			if ((columns[3] is "attack" or "special" or "super") && columns[4] != "BENCHED" && !approvedMechaAssignment)
			{
				GD.PushError($"Imported roster regression: {fighterName} {animation} was assigned before design review");
				failures++;
			}
		}
		return failures;
	}

	private static int ValidateMechaHeitaNeutral(FighterController fighter, SpriteFrames frames)
	{
		int failures = 0;
		if (fighter.Definition?.AllowLegacyFallbackMoves != false)
		{
			GD.PushError("Imported roster regression: Mecha Heita can still invoke legacy Kung Fu Man fallback moves");
			failures++;
		}
		NormalMoveData airHeavyKick = fighter.Definition?.NormalMoves?.FindRule("HEAVY KICK", false, true);
		if (airHeavyKick?.AnimationName != "air_heavy_kick" || airHeavyKick.Stance != NormalMoveStance.Airborne ||
			airHeavyKick.StartupFrames != 3 || airHeavyKick.ActiveFrames != 18 || airHeavyKick.RecoveryFrames != 15 ||
			airHeavyKick.AnimationSourceTimeline?.Length != 36 ||
			airHeavyKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			airHeavyKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox && box.StartFrame == 3 && box.EndFrame == 20) != true ||
			!frames.HasAnimation("air_heavy_kick") || frames.GetFrameCount("air_heavy_kick") != 13 ||
			Enumerable.Range(0, frames.GetFrameCount("air_heavy_kick"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("air_heavy_kick", index))) != 1033 ||
			airHeavyKick.AnimationSourceTimeline[^6] != 1027 || airHeavyKick.AnimationSourceTimeline[^1] != 1032)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_128 is not the finite editor-ready jumping HK");
			failures++;
		}
		NormalMoveData standingLightPunch = fighter.Definition?.NormalMoves?.FindRule("LIGHT PUNCH", false, false);
		bool standingLightPunchAnimation = frames.HasAnimation("light_punch") &&
			frames.GetFrameCount("light_punch") == 6 && !frames.GetAnimationLoop("light_punch") &&
			Enumerable.Range(0, frames.GetFrameCount("light_punch"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("light_punch", index))) == 11;
		if (standingLightPunch?.AnimationName != "light_punch" ||
			standingLightPunch.Stance != NormalMoveStance.Standing ||
			standingLightPunch.StartupFrames != 3 || standingLightPunch.ActiveFrames != 4 ||
			standingLightPunch.RecoveryFrames != 4 || standingLightPunch.Damage != 35 ||
			standingLightPunch.AnimationSourceTimeline?.Length != 0 ||
			standingLightPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			standingLightPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 3 && box.EndFrame == 6) != true ||
			!standingLightPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_112 is not an editor-ready standing light punch");
			failures++;
		}
		NormalMoveData standingMediumPunch = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.BackLightPunchName, false, false);
		bool standingMediumPunchAnimation = frames.HasAnimation("medium_punch_back") &&
			frames.GetFrameCount("medium_punch_back") == 9 && !frames.GetAnimationLoop("medium_punch_back") &&
			Enumerable.Range(0, frames.GetFrameCount("medium_punch_back"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("medium_punch_back", index))) == 27;
		if (standingMediumPunch?.AnimationName != "medium_punch_back" ||
			standingMediumPunch.Stance != NormalMoveStance.Standing ||
			standingMediumPunch.StartupFrames != 9 || standingMediumPunch.ActiveFrames != 6 ||
			standingMediumPunch.RecoveryFrames != 12 || standingMediumPunch.Damage != 60 ||
			standingMediumPunch.AnimationSourceTimeline?.Length != 0 ||
			standingMediumPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			standingMediumPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 9 && box.EndFrame == 14 && box.AttackLevel == FighterAttackLevel.Mid) != true ||
			!standingMediumPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_113 is not an editor-ready back+LP medium punch");
			failures++;
		}
		NormalMoveData standingHeavyKick = fighter.Definition?.NormalMoves?.FindRule("HEAVY KICK", false, false);
		bool standingHeavyKickAnimation = frames.HasAnimation("heavy_kick") &&
			frames.GetFrameCount("heavy_kick") == 11 && !frames.GetAnimationLoop("heavy_kick") &&
			Enumerable.Range(0, frames.GetFrameCount("heavy_kick"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("heavy_kick", index))) == 30;
		if (standingHeavyKick?.AnimationName != "heavy_kick" ||
			standingHeavyKick.Stance != NormalMoveStance.Standing ||
			standingHeavyKick.StartupFrames != 8 || standingHeavyKick.ActiveFrames != 9 ||
			standingHeavyKick.RecoveryFrames != 13 || standingHeavyKick.Damage != 100 ||
			standingHeavyKick.AnimationSourceTimeline?.Length != 30 ||
			standingHeavyKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			standingHeavyKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 8 && box.EndFrame == 16 && box.AttackLevel == FighterAttackLevel.Mid) != true ||
			!standingHeavyKickAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_114 is not an editor-ready standing HK");
			failures++;
		}
		NormalMoveData crouchingLightPunch = fighter.Definition?.NormalMoves?.FindRule("LIGHT PUNCH", true, false);
		bool crouchingLightPunchAnimation = frames.HasAnimation("crouching_light_punch") &&
			frames.GetFrameCount("crouching_light_punch") == 6 && !frames.GetAnimationLoop("crouching_light_punch") &&
			Enumerable.Range(0, frames.GetFrameCount("crouching_light_punch"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("crouching_light_punch", index))) == 13;
		if (crouchingLightPunch?.AnimationName != "crouching_light_punch" ||
			crouchingLightPunch.Stance != NormalMoveStance.Crouching ||
			crouchingLightPunch.StartupFrames != 4 || crouchingLightPunch.ActiveFrames != 7 ||
			crouchingLightPunch.RecoveryFrames != 2 || crouchingLightPunch.Damage != 30 ||
			!crouchingLightPunch.CanChainToLight || crouchingLightPunch.AnimationSourceTimeline?.Length != 0 ||
			crouchingLightPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			crouchingLightPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 4 && box.EndFrame == 10 && box.AttackLevel == FighterAttackLevel.Low) != true ||
			!crouchingLightPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_115 is not an editor-ready crouching jab");
			failures++;
		}
		NormalMoveData crouchingLightKick = fighter.Definition?.NormalMoves?.FindRule("LIGHT KICK", true, false);
		NormalMoveData crouchingMediumKick = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.CrouchingMediumKickName, true, false);
		bool crouchingKickAnimations = frames.HasAnimation("crouching_light_kick") &&
			frames.HasAnimation("crouching_medium_kick") &&
			frames.GetFrameCount("crouching_light_kick") == 7 &&
			frames.GetFrameCount("crouching_medium_kick") == 7 &&
			Enumerable.Range(0, frames.GetFrameCount("crouching_medium_kick"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("crouching_medium_kick", index))) == 24;
		if (crouchingLightKick?.AnimationName != "crouching_light_kick" ||
			crouchingLightKick.Stance != NormalMoveStance.Crouching ||
			crouchingLightKick.StartupFrames != 3 || crouchingLightKick.ActiveFrames != 4 ||
			crouchingLightKick.RecoveryFrames != 5 || crouchingLightKick.AnimationSourceTimeline?.Length != 12 ||
			crouchingLightKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 3 && box.EndFrame == 6 && box.AttackLevel == FighterAttackLevel.Low) != true ||
			crouchingMediumKick?.AnimationName != "crouching_medium_kick" ||
			crouchingMediumKick.Stance != NormalMoveStance.Crouching ||
			crouchingMediumKick.StartupFrames != 4 || crouchingMediumKick.ActiveFrames != 8 ||
			crouchingMediumKick.RecoveryFrames != 12 || crouchingMediumKick.AnimationSourceTimeline?.Length != 0 ||
			crouchingMediumKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 4 && box.EndFrame == 11 && box.AttackLevel == FighterAttackLevel.Low) != true ||
			!crouchingKickAnimations)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_116 does not provide fast down+LK and full-speed down-back+LK variants");
			failures++;
		}
		NormalMoveData crouchingHeavyPunch = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.CrouchingHeavyPunchName, true, false);
		bool crouchingHeavyPunchAnimation = frames.HasAnimation("crouching_heavy_punch") &&
			frames.GetFrameCount("crouching_heavy_punch") == 10 && !frames.GetAnimationLoop("crouching_heavy_punch") &&
			Enumerable.Range(0, frames.GetFrameCount("crouching_heavy_punch"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("crouching_heavy_punch", index))) == 34;
		if (crouchingHeavyPunch?.AnimationName != "crouching_heavy_punch" ||
			crouchingHeavyPunch.Stance != NormalMoveStance.Crouching ||
			crouchingHeavyPunch.StartupFrames != 10 || crouchingHeavyPunch.ActiveFrames != 15 ||
			crouchingHeavyPunch.RecoveryFrames != 9 || crouchingHeavyPunch.Damage != 100 ||
			!crouchingHeavyPunch.Launches || crouchingHeavyPunch.LaunchSpeed != 1265f ||
			crouchingHeavyPunch.LaunchPushback != 180f || crouchingHeavyPunch.LaunchHitstunFrames != 30 ||
			crouchingHeavyPunch.JumpCancelWindowFrames != 30 || crouchingHeavyPunch.ChaseJumpSpeed != 1265f ||
			crouchingHeavyPunch.ChaseForwardSpeed != 360f ||
			crouchingHeavyPunch.AnimationSourceTimeline?.Length != 0 ||
			crouchingHeavyPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			crouchingHeavyPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 10 && box.EndFrame == 24 && box.AttackLevel == FighterAttackLevel.Mid) != true ||
			!crouchingHeavyPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_117 is not an editor-ready crouching HP launcher");
			failures++;
		}
		NormalMoveData jumpingLightKick = fighter.Definition?.NormalMoves?.FindRule("LIGHT KICK", false, true);
		bool jumpingLightKickAnimation = frames.HasAnimation("air_light_kick") &&
			frames.GetFrameCount("air_light_kick") == 6 && !frames.GetAnimationLoop("air_light_kick") &&
			Enumerable.Range(0, frames.GetFrameCount("air_light_kick"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("air_light_kick", index))) == 18;
		if (jumpingLightKick?.AnimationName != "air_light_kick" ||
			jumpingLightKick.Stance != NormalMoveStance.Airborne ||
			jumpingLightKick.StartupFrames != 3 || jumpingLightKick.ActiveFrames != 6 ||
			jumpingLightKick.RecoveryFrames != 9 || jumpingLightKick.Damage != 35 ||
			jumpingLightKick.AnimationSourceTimeline?.Length != 0 ||
			jumpingLightKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			jumpingLightKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 3 && box.EndFrame == 8) != true || !jumpingLightKickAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_118 is not an editor-ready jumping LK");
			failures++;
		}
		NormalMoveData jumpingMediumPunch = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.AirBackLightPunchName, false, true);
		bool jumpingMediumPunchAnimation = frames.HasAnimation("air_medium_punch_back") &&
			frames.GetFrameCount("air_medium_punch_back") == 10 && !frames.GetAnimationLoop("air_medium_punch_back") &&
			Enumerable.Range(0, frames.GetFrameCount("air_medium_punch_back"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("air_medium_punch_back", index))) == 33;
		if (jumpingMediumPunch?.AnimationName != "air_medium_punch_back" ||
			jumpingMediumPunch.Stance != NormalMoveStance.Airborne ||
			jumpingMediumPunch.StartupFrames != 8 || jumpingMediumPunch.ActiveFrames != 13 ||
			jumpingMediumPunch.RecoveryFrames != 12 || jumpingMediumPunch.Damage != 60 ||
			!jumpingMediumPunch.Launches || !Mathf.IsEqualApprox(jumpingMediumPunch.LaunchSpeed, 1150f) ||
			!Mathf.IsEqualApprox(jumpingMediumPunch.LaunchPushback, 70f) ||
			jumpingMediumPunch.LaunchHitstunFrames != 32 || jumpingMediumPunch.JumpCancelWindowFrames != 0 ||
			jumpingMediumPunch.AnimationSourceTimeline?.Length != 0 ||
			jumpingMediumPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 8 && box.EndFrame == 20) != true || !jumpingMediumPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_119 is not an upward-pop air back+LP medium punch");
			failures++;
		}
		NormalMoveData jumpingHeavyPunch = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.AirHeavyPunchName, false, true);
		bool jumpingHeavyPunchAnimation = frames.HasAnimation("air_heavy_punch") &&
			frames.GetFrameCount("air_heavy_punch") == 10 && !frames.GetAnimationLoop("air_heavy_punch") &&
			Enumerable.Range(0, frames.GetFrameCount("air_heavy_punch"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("air_heavy_punch", index))) == 33;
		if (jumpingHeavyPunch?.AnimationName != "air_heavy_punch" ||
			jumpingHeavyPunch.Stance != NormalMoveStance.Airborne ||
			jumpingHeavyPunch.StartupFrames != 9 || jumpingHeavyPunch.ActiveFrames != 12 ||
			jumpingHeavyPunch.RecoveryFrames != 12 || jumpingHeavyPunch.Damage != 100 ||
			jumpingHeavyPunch.Launches || jumpingHeavyPunch.KnocksDown ||
			jumpingHeavyPunch.AnimationSourceTimeline?.Length != 0 ||
			jumpingHeavyPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			jumpingHeavyPunch.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 9 && box.EndFrame == 20) != true || !jumpingHeavyPunchAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_120 is not an editor-ready jumping HP");
			failures++;
		}
		NormalMoveData standingLightKick = fighter.Definition?.NormalMoves?.FindRule("LIGHT KICK", false, false);
		bool standingLightKickAnimation = frames.HasAnimation("standing_light_kick") &&
			frames.GetFrameCount("standing_light_kick") == 12 && !frames.GetAnimationLoop("standing_light_kick") &&
			Enumerable.Range(0, frames.GetFrameCount("standing_light_kick"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("standing_light_kick", index))) == 32;
		if (standingLightKick?.AnimationName != "standing_light_kick" ||
			standingLightKick.Stance != NormalMoveStance.Standing ||
			standingLightKick.StartupFrames != 6 || standingLightKick.ActiveFrames != 6 ||
			standingLightKick.RecoveryFrames != 9 || standingLightKick.Damage != 35 ||
			standingLightKick.AnimationSourceTimeline?.Length != 21 ||
			standingLightKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			standingLightKick.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 6 && box.EndFrame == 11) != true || !standingLightKickAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_121 is not the accelerated standing LK");
			failures++;
		}
		NormalMoveData throwStartup = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.ThrowAttackName, false, false);
		bool throwStartupAnimation = frames.HasAnimation("throw") && frames.GetFrameCount("throw") == 6 &&
			!frames.GetAnimationLoop("throw") &&
			Enumerable.Range(0, frames.GetFrameCount("throw"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("throw", index))) == 31;
		if (throwStartup?.AnimationName != "throw" || throwStartup.Stance != NormalMoveStance.Standing ||
			throwStartup.StartupFrames != 5 || throwStartup.ActiveFrames != 4 ||
			throwStartup.RecoveryFrames != 22 || !throwStartup.KnocksDown ||
			throwStartup.KnockdownType != KnockdownType.HardKnockdown ||
			throwStartup.AnimationTailName != "forward_throw" || throwStartup.AnimationTailStartFrame != 5 ||
			throwStartup.ConnectedThrowRecoveryFrames != 78 ||
			throwStartup.AnimationSourceTimeline?.Length != 31 ||
			throwStartup.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hitbox &&
				box.StartFrame == 5 && box.EndFrame == 8 &&
				(box.Attributes & FighterBoxAttribute.Throw) != 0) != true || !throwStartupAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_123 is not an editor-ready throw startup");
			failures++;
		}
		bool forwardThrowAnimation = frames.HasAnimation("forward_throw") &&
			frames.GetFrameCount("forward_throw") == 18 && !frames.GetAnimationLoop("forward_throw") &&
			Enumerable.Range(0, frames.GetFrameCount("forward_throw"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("forward_throw", index))) == 78;
		FighterBoxFrame[] throwAnchors = throwStartup?.BoxTimeline?
			.Where(box => box?.Kind == FighterBoxKind.ThrowVictimAnchor).ToArray();
		if (!forwardThrowAnimation || throwAnchors?.Length != 3 ||
			throwAnchors[0].StartFrame != 5 || throwAnchors[0].EndFrame != 34 ||
			throwAnchors[1].StartFrame != 35 || throwAnchors[1].EndFrame != 55 ||
			throwAnchors[2].StartFrame != 56 || throwAnchors[2].EndFrame != 70)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_124 is not the connected forward throw with editable victim anchors");
			failures++;
		}
		NormalMoveData backThrow = fighter.Definition?.NormalMoves?
			.FindRule(FighterController.BackThrowAttackName, false, false);
		bool backThrowAnimation = frames.HasAnimation("back_throw") &&
			frames.GetFrameCount("back_throw") == 18 && !frames.GetAnimationLoop("back_throw") &&
			Enumerable.Range(0, frames.GetFrameCount("back_throw"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("back_throw", index))) == 78;
		bool backThrowStartupAnimation = frames.HasAnimation("back_throw_startup") &&
			frames.GetFrameCount("back_throw_startup") == 6 && !frames.GetAnimationLoop("back_throw_startup") &&
			Enumerable.Range(0, frames.GetFrameCount("back_throw_startup"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("back_throw_startup", index))) == 31;
		FighterBoxFrame[] backThrowAnchors = backThrow?.BoxTimeline?
			.Where(box => box?.Kind == FighterBoxKind.ThrowVictimAnchor).ToArray();
		if (backThrow?.AnimationName != "back_throw_startup" || backThrow.AnimationTailName != "back_throw" ||
			backThrow.StartupFrames != 5 || backThrow.ActiveFrames != 4 || backThrow.RecoveryFrames != 22 ||
			backThrow.ConnectedThrowRecoveryFrames != 78 || !backThrow.KnocksDown ||
			!backThrowAnimation || !backThrowStartupAnimation ||
			backThrowAnchors?.Length != 3 || backThrowAnchors[0].LocalRect.Position.X >= 0f ||
			backThrowAnchors[2].EndFrame != 70 || backThrowAnchors[2].LocalRect.Position.X >= 0f)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_125 is not the mirrored back throw");
			failures++;
		}
		NormalMoveData airAttackLanding = fighter.Definition?.StateBoxes?.FindStateRule("STATE AIR ATTACK LANDING");
		bool provisionalLandingAnimation = frames.HasAnimation("air_attack_landing") &&
			frames.GetFrameCount("air_attack_landing") == 5 && !frames.GetAnimationLoop("air_attack_landing") &&
			Enumerable.Range(0, frames.GetFrameCount("air_attack_landing"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("air_attack_landing", index))) == 10;
		if (airAttackLanding?.AnimationName != "air_attack_landing" || airAttackLanding.ActiveFrames != 10 ||
			airAttackLanding.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!provisionalLandingAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_090 is not available as the provisional air-attack landing");
			failures++;
		}
		NormalMoveData standardLanding = fighter.Definition?.StateBoxes?.FindStateRule("STATE LANDING");
		if (standardLanding?.AnimationName != "landing" || standardLanding.ActiveFrames != 11 ||
			standardLanding.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("landing") || frames.GetFrameCount("landing") != 6 ||
			frames.GetAnimationLoop("landing") ||
			Enumerable.Range(0, frames.GetFrameCount("landing"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("landing", index))) != 11)
		{
			GD.PushError("Imported roster regression: Mecha Heita anim_107 is not the standard jump landing");
			failures++;
		}
		if (!frames.HasAnimation("idle") || frames.GetFrameCount("idle") != 5 ||
			!frames.GetAnimationLoop("idle") ||
			Enumerable.Range(0, frames.GetFrameCount("idle"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("idle", index))) != 24)
		{
			GD.PushError("Imported roster regression: Mecha Heita neutral is not the five-drawing loop");
			failures++;
		}
		if (!frames.HasAnimation("idle_flourish") || frames.GetFrameCount("idle_flourish") != 7 ||
			frames.GetAnimationLoop("idle_flourish"))
		{
			GD.PushError("Imported roster regression: Mecha Heita arm-cross flourish was not preserved separately");
			failures++;
		}
		NormalMoveData idleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE IDLE");
		if (idleState?.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita neutral is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData crouchStartState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCH START");
		if (crouchStartState?.AnimationName != "crouch_start" || crouchStartState.ActiveFrames != 10 ||
			crouchStartState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita crouch entry is missing its ten-tick editable state");
			failures++;
		}
		NormalMoveData crouchState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCH");
		if (crouchState?.AnimationName != "crouch_hold" ||
			crouchState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouch_hold") || !frames.GetAnimationLoop("crouch_hold") ||
			frames.GetFrameCount("crouch_hold") != 9)
		{
			GD.PushError("Imported roster regression: Mecha Heita full crouch is not a nine-drawing editable loop");
			failures++;
		}
		NormalMoveData fullCrouch2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE FULL CROUCH 2");
		if (fullCrouch2State?.AnimationName != "full_crouch_2" ||
			fullCrouch2State.ActiveFrames != 1000 ||
			fullCrouch2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("full_crouch_2") || frames.GetFrameCount("full_crouch_2") != 1 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("full_crouch_2", 0)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's second full-crouch pose is incomplete");
			failures++;
		}
		NormalMoveData standingHitstunToIdleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING HITSTUN TO IDLE");
		if (standingHitstunToIdleState?.AnimationName != "standing_hitstun_to_idle" ||
			standingHitstunToIdleState.ActiveFrames != 24 ||
			standingHitstunToIdleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_hitstun_to_idle") ||
			frames.GetFrameCount("standing_hitstun_to_idle") != 8)
		{
			GD.PushError("Imported roster regression: Mecha Heita's standing hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingLightHitstunToIdleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING LIGHT HITSTUN TO IDLE");
		if (standingLightHitstunToIdleState?.AnimationName != "standing_light_hitstun_to_idle" ||
			standingLightHitstunToIdleState.ActiveFrames != 10 ||
			standingLightHitstunToIdleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_light_hitstun_to_idle") ||
			frames.GetFrameCount("standing_light_hitstun_to_idle") != 5)
		{
			GD.PushError("Imported roster regression: Mecha Heita's light hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingMediumHitstunToIdleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING MEDIUM HITSTUN TO IDLE");
		if (standingMediumHitstunToIdleState?.AnimationName != "standing_medium_hitstun_to_idle" ||
			standingMediumHitstunToIdleState.ActiveFrames != 17 ||
			standingMediumHitstunToIdleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_medium_hitstun_to_idle") ||
			frames.GetFrameCount("standing_medium_hitstun_to_idle") != 6)
		{
			GD.PushError("Imported roster regression: Mecha Heita's medium hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingBigHitstunToIdle2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING BIG HITSTUN TO IDLE 2");
		if (standingBigHitstunToIdle2State?.AnimationName != "standing_big_hitstun_to_idle_2" ||
			standingBigHitstunToIdle2State.ActiveFrames != 23 ||
			standingBigHitstunToIdle2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_big_hitstun_to_idle_2") ||
			frames.GetFrameCount("standing_big_hitstun_to_idle_2") != 8)
		{
			GD.PushError("Imported roster regression: Mecha Heita's second big hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingBigHitstunToIdle3State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING BIG HITSTUN TO IDLE 3");
		if (standingBigHitstunToIdle3State?.AnimationName != "standing_big_hitstun_to_idle_3" ||
			standingBigHitstunToIdle3State.ActiveFrames != 23 ||
			standingBigHitstunToIdle3State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_big_hitstun_to_idle_3") ||
			frames.GetFrameCount("standing_big_hitstun_to_idle_3") != 8)
		{
			GD.PushError("Imported roster regression: Mecha Heita's third big hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingMidHitstunToIdleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING MID HITSTUN TO IDLE");
		if (standingMidHitstunToIdleState?.AnimationName != "standing_mid_hitstun_to_idle" ||
			standingMidHitstunToIdleState.ActiveFrames != 24 ||
			standingMidHitstunToIdleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_mid_hitstun_to_idle") ||
			frames.GetFrameCount("standing_mid_hitstun_to_idle") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's midsection hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingLightMidHitstunToIdleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING LIGHT MID HITSTUN TO IDLE");
		if (standingLightMidHitstunToIdleState?.AnimationName != "standing_light_mid_hitstun_to_idle" ||
			standingLightMidHitstunToIdleState.ActiveFrames != 10 ||
			standingLightMidHitstunToIdleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_light_mid_hitstun_to_idle") ||
			frames.GetFrameCount("standing_light_mid_hitstun_to_idle") != 5)
		{
			GD.PushError("Imported roster regression: Mecha Heita's light midsection hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingLightMidHitstunToIdle2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING LIGHT MID HITSTUN TO IDLE 2");
		if (standingLightMidHitstunToIdle2State?.AnimationName != "standing_light_mid_hitstun_to_idle_2" ||
			standingLightMidHitstunToIdle2State.ActiveFrames != 17 ||
			standingLightMidHitstunToIdle2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_light_mid_hitstun_to_idle_2") ||
			frames.GetFrameCount("standing_light_mid_hitstun_to_idle_2") != 6)
		{
			GD.PushError("Imported roster regression: Mecha Heita's second light-mid hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingMidHitstunToIdle2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING MID HITSTUN TO IDLE 2");
		if (standingMidHitstunToIdle2State?.AnimationName != "standing_mid_hitstun_to_idle_2" ||
			standingMidHitstunToIdle2State.ActiveFrames != 21 ||
			standingMidHitstunToIdle2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_mid_hitstun_to_idle_2") ||
			frames.GetFrameCount("standing_mid_hitstun_to_idle_2") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's second midsection hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData standingMidHitstunToIdle3State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE STANDING MID HITSTUN TO IDLE 3");
		if (standingMidHitstunToIdle3State?.AnimationName != "standing_mid_hitstun_to_idle_3" ||
			standingMidHitstunToIdle3State.ActiveFrames != 23 ||
			standingMidHitstunToIdle3State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("standing_mid_hitstun_to_idle_3") ||
			frames.GetFrameCount("standing_mid_hitstun_to_idle_3") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's third midsection hitstun-to-idle transition is incomplete");
			failures++;
		}
		NormalMoveData crouchEndState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCH END");
		if (crouchEndState?.AnimationName != "crouch_end" || crouchEndState.ActiveFrames != 10 ||
			crouchEndState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita crouch exit is missing its ten-tick editable state");
			failures++;
		}
		NormalMoveData crouchHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCH HITSTUN");
		if (crouchHitstunState?.AnimationName != "crouch_hit" || crouchHitstunState.ActiveFrames != 5 ||
			crouchHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouch_hit") || frames.GetFrameCount("crouch_hit") != 1)
		{
			GD.PushError("Imported roster regression: Mecha Heita crouch hitstun is missing its editable source pose");
			failures++;
		}
		NormalMoveData crouchHitstun2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCH HITSTUN 2");
		if (crouchHitstun2State?.AnimationName != "crouch_hit_2" || crouchHitstun2State.ActiveFrames != 5 ||
			crouchHitstun2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouch_hit_2") || frames.GetFrameCount("crouch_hit_2") != 1)
		{
			GD.PushError("Imported roster regression: Mecha Heita crouch hitstun 2 was not preserved separately");
			failures++;
		}
		NormalMoveData crouchingHeavyHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCHING HEAVY HITSTUN");
		if (crouchingHeavyHitstunState?.AnimationName != "crouching_heavy_hitstun" ||
			crouchingHeavyHitstunState.ActiveFrames != 24 ||
			crouchingHeavyHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouching_heavy_hitstun") ||
			frames.GetFrameCount("crouching_heavy_hitstun") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's crouching heavy hitstun is incomplete");
			failures++;
		}
		NormalMoveData crouchingLightHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCHING LIGHT HITSTUN");
		if (crouchingLightHitstunState?.AnimationName != "crouching_light_hitstun" ||
			crouchingLightHitstunState.ActiveFrames != 10 ||
			crouchingLightHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouching_light_hitstun") ||
			frames.GetFrameCount("crouching_light_hitstun") != 4)
		{
			GD.PushError("Imported roster regression: Mecha Heita's crouching light hitstun is incomplete");
			failures++;
		}
		NormalMoveData crouchingMidHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCHING MID HITSTUN");
		if (crouchingMidHitstunState?.AnimationName != "crouching_mid_hitstun" ||
			crouchingMidHitstunState.ActiveFrames != 17 ||
			crouchingMidHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouching_mid_hitstun") ||
			frames.GetFrameCount("crouching_mid_hitstun") != 6)
		{
			GD.PushError("Imported roster regression: Mecha Heita's crouching mid hitstun is incomplete");
			failures++;
		}
		NormalMoveData crouchingMidHitstun2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCHING MID HITSTUN 2");
		if (crouchingMidHitstun2State?.AnimationName != "crouching_mid_hitstun_2" ||
			crouchingMidHitstun2State.ActiveFrames != 21 ||
			crouchingMidHitstun2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouching_mid_hitstun_2") ||
			frames.GetFrameCount("crouching_mid_hitstun_2") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's second crouching mid hitstun is incomplete");
			failures++;
		}
		NormalMoveData crouchingMidHitstun3State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE CROUCHING MID HITSTUN 3");
		if (crouchingMidHitstun3State?.AnimationName != "crouching_mid_hitstun_3" ||
			crouchingMidHitstun3State.ActiveFrames != 23 ||
			crouchingMidHitstun3State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouching_mid_hitstun_3") ||
			frames.GetFrameCount("crouching_mid_hitstun_3") != 7)
		{
			GD.PushError("Imported roster regression: Mecha Heita's third crouching mid hitstun is incomplete");
			failures++;
		}
		NormalMoveData fullCrouchHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE FULL CROUCH HITSTUN");
		if (fullCrouchHitstunState?.AnimationName != "full_crouch_hitstun" ||
			fullCrouchHitstunState.ActiveFrames != 46 ||
			fullCrouchHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("full_crouch_hitstun") ||
			frames.GetFrameCount("full_crouch_hitstun") != 5 ||
			frames.GetFrameTexture("full_crouch_hitstun", 0)?.ResourcePath.EndsWith("frame_0058.png") != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita full crouch hitstun includes the stray standing drawing");
			failures++;
		}
		NormalMoveData fullCrouchHitstun2State = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE FULL CROUCH HITSTUN 2");
		if (fullCrouchHitstun2State?.AnimationName != "full_crouch_hitstun_2" ||
			fullCrouchHitstun2State.ActiveFrames != 46 ||
			fullCrouchHitstun2State.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("full_crouch_hitstun_2") ||
			frames.GetFrameCount("full_crouch_hitstun_2") != 5 ||
			frames.GetFrameTexture("full_crouch_hitstun_2", 0)?.ResourcePath.EndsWith("frame_0058.png") != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita second full crouch hitstun includes the stray standing drawing");
			failures++;
		}
		NormalMoveData walkState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE WALK FORWARD");
		if (walkState?.AnimationName != "walk" ||
			walkState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita forward walk is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData walkBackState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE WALK BACK");
		if (walkBackState?.AnimationName != "walk_back" ||
			walkBackState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita backward walk is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData jumpState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE JUMP RISE");
		if (jumpState?.AnimationName != "neutral_jump" ||
			jumpState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita neutral jump is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData forwardJumpState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE JUMP FORWARD");
		if (forwardJumpState?.AnimationName != "forward_jump_loop" ||
			forwardJumpState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita forward jump is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData backwardJumpState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE JUMP BACK");
		if (backwardJumpState?.AnimationName != "backward_jump" ||
			backwardJumpState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita jump back is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData fallState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE FALL");
		if (fallState?.AnimationName != "fall" ||
			fallState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true)
		{
			GD.PushError("Imported roster regression: Mecha Heita fall is missing its editable hurtbox");
			failures++;
		}
		NormalMoveData flyUpState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE FLY UP");
		if (flyUpState?.AnimationName != "fly_up" ||
			flyUpState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("fly_up") || frames.GetFrameCount("fly_up") != 8 ||
			!frames.GetAnimationLoop("fly_up") ||
			!frames.HasAnimation("fly_up_start") || !frames.HasAnimation("fly_up_end") ||
			!frames.HasAnimation("fly_up_jet_effect") || !frames.GetAnimationLoop("fly_up_jet_effect"))
		{
			GD.PushError("Imported roster regression: Mecha Heita FLY UP category or jet animation is incomplete");
			failures++;
		}
		var superJumpStates = new[]
		{
			(State: "STATE SUPER JUMP NEUTRAL", Animation: "super_jump_neutral", Drawings: 6, Ticks: 34),
			(State: "STATE SUPER JUMP FORWARD", Animation: "super_jump_forward", Drawings: 6, Ticks: 34),
			(State: "STATE SUPER JUMP BACKWARD", Animation: "super_jump_backward", Drawings: 7, Ticks: 40),
		};
		foreach (var expected in superJumpStates)
		{
			NormalMoveData state = fighter.Definition?.StateBoxes?.FindStateRule(expected.State);
			if (state?.AnimationName != expected.Animation || state.ActiveFrames != expected.Ticks ||
				state.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
				!frames.HasAnimation(expected.Animation) ||
				frames.GetFrameCount(expected.Animation) != expected.Drawings ||
				Enumerable.Range(0, frames.GetFrameCount(expected.Animation))
					.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(expected.Animation, index))) != expected.Ticks)
			{
				GD.PushError($"Imported roster regression: Mecha Heita {expected.State} ascent is incomplete");
				failures++;
			}
		}
		NormalMoveData boosterState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE BOOSTER");
		FlightAbility boosterAbility = fighter.Definition?.Abilities?
			.OfType<FlightAbility>()
			.FirstOrDefault(ability => ability.Id == "mecha_heita_booster");
		bool directionalBoostWorks = false;
		if (boosterAbility != null)
		{
			fighter.ResetPlaceholderGauges();
			AbilityRuntime runtime = fighter.GetRuntime(boosterAbility);
			fighter.SetExternalInput(new FighterInput(0f, -1f, false, false, false, false,
				special1Pressed: true, special1Held: true));
			directionalBoostWorks = boosterAbility.CanStart(fighter, runtime);
			boosterAbility.Start(fighter, runtime);
			for (int frame = 0; frame < 6 && directionalBoostWorks; frame++)
				directionalBoostWorks = boosterAbility.Tick(fighter, runtime, 1f / 60f);
			directionalBoostWorks &= Mathf.IsEqualApprox(fighter.PlaceholderSpecialMeter, 88f) &&
				boosterAbility.IsBoosting(fighter) && boosterAbility.AirBoostsUsed(fighter) == 1 &&
				Mathf.IsEqualApprox(fighter.Velocity.Y, -900f);
			fighter.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false));
			boosterAbility.Stop(fighter, runtime);
		}
		if (boosterState?.AnimationName != "booster_loop" ||
			boosterState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			fighter.Definition?.Tuning?.NonFlightLandingLagMultiplier != 0.5f ||
			boosterAbility is not { CanStartGrounded: true, MaxFrames: 0, UseSpecial1Input: true,
				GasCostPerFrame: 0.5f, UseDirectionalAnimations: true, DirectVelocityControl: true,
				BackwardFlightSpeedMultiplier: 0.3f,
				UseDirectionalBoosts: true, BoostSpeed: 750f, BoostFrames: 12, MaxAirBoosts: 3,
				BoostGasCost: 12f, BoostCancelExtraGasCost: 4f, BoostAttackDelayFrames: 7,
				BackwardBoostSpeedMultiplier: 0.5f, BackwardBoostAirUseCost: 2,
				CommitAfterBackwardAirBoost: true,
				AllowNormalHitFlightCancel: true, AllowWhiffRecoveryFlightCancelNormals: true,
				AllowWhiffRecoveryFlightCancelSpecials: true, FlightCancelGasCost: 10f,
				FlightCancelMinimumFrames: 15, RequireNeutralBeforeCancelledFlightMovement: false,
				RequireDirectionBeforeCancelledFlightAttack: true, LockAirNormalsDuringPostFlightFall: true } ||
			fighter.Definition?.Gauges is not { SpecialMeterName: "GAS", MaxSpecialMeter: 100,
				StartingSpecialMeter: 100, SpecialMeterRecoveryPerSecond: 15f,
				SpecialMeterRecoveryDelayFrames: 30 } || !directionalBoostWorks ||
			!frames.HasAnimation("booster_start") || frames.GetFrameCount("booster_start") != 3 ||
			!frames.HasAnimation("booster_loop") || frames.GetFrameCount("booster_loop") != 8 ||
			!frames.GetAnimationLoop("booster_loop") ||
			!frames.HasAnimation("booster_recovery") || frames.GetFrameCount("booster_recovery") != 6 ||
			!frames.HasAnimation("flight_landing") || frames.GetFrameCount("flight_landing") != 3 ||
			fighter.Definition?.StateBoxes?.FindStateRule("STATE FLIGHT LANDING") is not
				{ AnimationName: "flight_landing", ActiveFrames: 8 } ||
			!frames.HasAnimation("flight_fall") || frames.GetFrameCount("flight_fall") != 5 ||
			fighter.Definition?.StateBoxes?.FindStateRule("STATE FLIGHT FALL") is not
				{ AnimationName: "flight_fall", ActiveFrames: 60 } ||
			!frames.HasAnimation("booster_jet_fire") || !frames.GetAnimationLoop("booster_jet_fire"))
		{
			GD.PushError("Imported roster regression: Mecha Heita gas-powered Special-1 booster or jet fire is incomplete");
			failures++;
		}
		var directionalBoosterStates = new[]
		{
			(State: "STATE BOOSTER UP", Animation: "booster_up"),
			(State: "STATE BOOSTER UP FORWARD", Animation: "booster_up_forward"),
			(State: "STATE BOOSTER FORWARD", Animation: "booster_forward"),
			(State: "STATE BOOSTER DOWN FORWARD", Animation: "booster_down_forward"),
			(State: "STATE BOOSTER DOWN", Animation: "booster_down"),
			(State: "STATE BOOSTER DOWN BACK", Animation: "booster_down_back"),
			(State: "STATE BOOSTER BACK", Animation: "booster_back"),
			(State: "STATE BOOSTER UP BACK", Animation: "booster_up_back"),
		};
		foreach (var expected in directionalBoosterStates)
		{
			int expectedFrameCount = expected.Animation switch
			{
				"booster_forward" or "booster_down_forward" => 24,
				"booster_back" => 56,
				_ => 8,
			};
			int expectedTicks = expectedFrameCount * 2;
			NormalMoveData state = fighter.Definition?.StateBoxes?.FindStateRule(expected.State);
			if (state?.AnimationName != expected.Animation || state.ActiveFrames != 16 ||
				state.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
				!frames.HasAnimation(expected.Animation) || frames.GetFrameCount(expected.Animation) != expectedFrameCount ||
				!frames.GetAnimationLoop(expected.Animation) ||
				Enumerable.Range(0, frames.GetFrameCount(expected.Animation))
					.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(expected.Animation, index))) != expectedTicks)
			{
				GD.PushError($"Imported roster regression: Mecha Heita directional flight '{expected.State}' is incomplete");
				failures++;
			}
		}
		NormalMoveData knockedAwayState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE KNOCKED AWAY");
		NormalMoveData launchedState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE LAUNCHED");
		if (knockedAwayState?.AnimationName != "knocked_away" || knockedAwayState.ActiveFrames != 16 ||
			knockedAwayState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("knocked_away") || frames.GetFrameCount("knocked_away") != 4 ||
			launchedState?.AnimationName != "launched" || launchedState.ActiveFrames != 40 ||
			launchedState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("launched") || frames.GetFrameCount("launched") != 9)
		{
			GD.PushError("Imported roster regression: Mecha Heita's knocked-away/launch split is incomplete");
			failures++;
		}
		NormalMoveData launchedHitstunState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE LAUNCHED HITSTUN");
		if (launchedHitstunState?.AnimationName != "launched_hitstun" ||
			launchedHitstunState.ActiveFrames != 1032 ||
			launchedHitstunState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("launched_hitstun") || frames.GetFrameCount("launched_hitstun") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("launched_hitstun", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's launched hitstun is incomplete");
			failures++;
		}
		NormalMoveData launchedFarState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE LAUNCHED FAR");
		if (launchedFarState?.AnimationName != "launched_far" || launchedFarState.ActiveFrames != 1032 ||
			launchedFarState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("launched_far") || frames.GetFrameCount("launched_far") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("launched_far", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's far launch reaction is incomplete");
			failures++;
		}
		NormalMoveData shortLaunchState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE SHORT LAUNCH");
		if (shortLaunchState?.AnimationName != "short_launch" || shortLaunchState.ActiveFrames != 1032 ||
			shortLaunchState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("short_launch") || frames.GetFrameCount("short_launch") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("short_launch", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's short launch reaction is incomplete");
			failures++;
		}
		NormalMoveData lightLaunchState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE LIGHT LAUNCH");
		if (lightLaunchState?.AnimationName != "light_launch" || lightLaunchState.ActiveFrames != 1032 ||
			lightLaunchState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("light_launch") || frames.GetFrameCount("light_launch") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("light_launch", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's light launch reaction is incomplete");
			failures++;
		}
		var blowAwayStates = new[]
		{
			(StateName: "STATE [ヒット]吹っ飛び_真横", Animation: "blow_away_horizontal", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.Horizontal, Strength: BlowAwayStrength.Medium, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真上_弱", Animation: "blow_away_vertical_weak", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Vertical, Strength: BlowAwayStrength.Weak, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真上_中", Animation: "blow_away_vertical_medium", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Vertical, Strength: BlowAwayStrength.Medium, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真上_強", Animation: "blow_away_vertical_strong", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Vertical, Strength: BlowAwayStrength.Strong, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_斜め_弱", Animation: "blow_away_diagonal_weak", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Diagonal, Strength: BlowAwayStrength.Weak, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_斜め_中", Animation: "blow_away_diagonal_medium", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Diagonal, Strength: BlowAwayStrength.Medium, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_斜め_強", Animation: "blow_away_diagonal_strong", Frames: 9,
				Ticks: 26, Direction: BlowAwayDirection.Diagonal, Strength: BlowAwayStrength.Strong, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真下_弱", Animation: "blow_away_downward_weak", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.Downward, Strength: BlowAwayStrength.Weak, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真下_中", Animation: "blow_away_downward_medium", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.Downward, Strength: BlowAwayStrength.Medium, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真下_強", Animation: "blow_away_downward_strong", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.Downward, Strength: BlowAwayStrength.Strong, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_斜め下", Animation: "blow_away_diagonal_down", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.DiagonalDown, Strength: BlowAwayStrength.Medium, NoBounce: false),
			(StateName: "STATE [ヒット]吹っ飛び_真下_無バウンド", Animation: "blow_away_downward_no_bounce", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.Downward, Strength: BlowAwayStrength.Medium, NoBounce: true),
			(StateName: "STATE [ヒット]吹っ飛び_斜め下_無バウンド", Animation: "blow_away_diagonal_down_no_bounce", Frames: 1,
				Ticks: 1000, Direction: BlowAwayDirection.DiagonalDown, Strength: BlowAwayStrength.Medium, NoBounce: true),
		};
		foreach (var expected in blowAwayStates)
		{
			NormalMoveData state = fighter.Definition?.StateBoxes?.Rules?
				.FirstOrDefault(rule => rule?.AttackName == expected.StateName);
			if (state?.AnimationName != expected.Animation || state.ActiveFrames != expected.Ticks ||
				state.BlowAwayDirection != expected.Direction || state.BlowAwayStrength != expected.Strength ||
				state.BlowAwayNoBounce != expected.NoBounce ||
				state.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
				!frames.HasAnimation(expected.Animation) || frames.GetFrameCount(expected.Animation) != expected.Frames)
			{
				GD.PushError($"Imported roster regression: Mecha Heita blow-away channel '{expected.StateName}' is incomplete");
				failures++;
			}
		}
		Vector2 verticalBlowAway = FighterController.ResolveBlowAwayVelocity(
			BlowAwayDirection.Vertical, BlowAwayStrength.Strong, 1, 900f);
		Vector2 diagonalDownBlowAway = FighterController.ResolveBlowAwayVelocity(
			BlowAwayDirection.DiagonalDown, BlowAwayStrength.Medium, -1, 900f);
		if (verticalBlowAway.Y >= 0f || verticalBlowAway.X <= 0f ||
			diagonalDownBlowAway.X >= 0f || diagonalDownBlowAway.Y <= 0f ||
			FighterController.ResolveBlowAwayAnimationName(BlowAwayDirection.Downward,
				BlowAwayStrength.Strong) != "blow_away_downward_strong" ||
			FighterController.ResolveBlowAwayAnimationName(BlowAwayDirection.DiagonalDown,
				BlowAwayStrength.Medium, true) != "blow_away_diagonal_down_no_bounce")
		{
			GD.PushError("Imported roster regression: directional blow-away reaction behavior is incomplete");
			failures++;
		}
		NormalMoveData stumbleState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE [ヒット]躓き");
		if (stumbleState?.AnimationName != "stumble" || stumbleState.ActiveFrames != 1032 ||
			stumbleState.HitReaction != HitReactionKind.Stumble ||
			stumbleState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("stumble") || frames.GetFrameCount("stumble") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("stumble", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's [ヒット]躓き stumble reaction is incomplete");
			failures++;
		}
		NormalMoveData wallBounceStrongState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE [やられ]壁バウンド_強");
		if (wallBounceStrongState?.AnimationName != "wall_bounce_strong" ||
			wallBounceStrongState.ActiveFrames != 1032 ||
			wallBounceStrongState.HitReaction != HitReactionKind.WallBounce ||
			wallBounceStrongState.KnockdownType != KnockdownType.WallBounce || !wallBounceStrongState.KnocksDown ||
			wallBounceStrongState.WallBounceStrength != WallBounceReactionStrength.Strong ||
			wallBounceStrongState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("wall_bounce_strong") || frames.GetFrameCount("wall_bounce_strong") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("wall_bounce_strong", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's strong wall-bounce reaction is incomplete");
			failures++;
		}
		NormalMoveData wallBounceWeakState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE [やられ]壁バウンド_弱");
		if (wallBounceWeakState?.AnimationName != "wall_bounce_weak" ||
			wallBounceWeakState.ActiveFrames != 1032 ||
			wallBounceWeakState.HitReaction != HitReactionKind.WallBounce ||
			wallBounceWeakState.KnockdownType != KnockdownType.WallBounce || !wallBounceWeakState.KnocksDown ||
			wallBounceWeakState.WallBounceStrength != WallBounceReactionStrength.Weak ||
			wallBounceWeakState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("wall_bounce_weak") || frames.GetFrameCount("wall_bounce_weak") != 9 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("wall_bounce_weak", 8)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's weak wall-bounce reaction is incomplete");
			failures++;
		}
		NormalMoveData hitFallState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE [やられ]ヒット落下");
		if (hitFallState?.AnimationName != "hit_fall" || hitFallState.ActiveFrames != 1000 ||
			hitFallState.HitReaction != HitReactionKind.HitFall ||
			hitFallState.KnockdownType != KnockdownType.AirKnockdown || !hitFallState.KnocksDown ||
			hitFallState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("hit_fall") || frames.GetFrameCount("hit_fall") != 1 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("hit_fall", 0)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's [やられ]ヒット落下 hit-fall reaction is incomplete");
			failures++;
		}
		NormalMoveData downedState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [やられ]ダウン");
		if (downedState?.AnimationName != "knockdown" || downedState.ActiveFrames != 20 ||
			downedState.HitReaction != HitReactionKind.Knockdown || !downedState.KnocksDown ||
			downedState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("knockdown") || frames.GetFrameCount("knockdown") != 7 ||
			Enumerable.Range(0, frames.GetFrameCount("knockdown"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("knockdown", index))) != 20)
		{
			GD.PushError("Imported roster regression: Mecha Heita's [やられ]ダウン downed state is incomplete");
			failures++;
		}
		NormalMoveData wakeupState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [やられ]起き上がり");
		if (wakeupState?.AnimationName != "get_up" || wakeupState.ActiveFrames != 45 ||
			wakeupState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("get_up") || frames.GetFrameCount("get_up") != 13 ||
			Enumerable.Range(0, frames.GetFrameCount("get_up"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("get_up", index))) != 45)
		{
			GD.PushError("Imported roster regression: Mecha Heita's [やられ]起き上がり wakeup state is incomplete");
			failures++;
		}
		NormalMoveData groundBounceWeakState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [やられ]垂直バウンド弱");
		if (groundBounceWeakState?.AnimationName != "ground_bounce_weak" || groundBounceWeakState.ActiveFrames != 1038 ||
			groundBounceWeakState.HitReaction != HitReactionKind.GroundBounce ||
			groundBounceWeakState.KnockdownType != KnockdownType.GroundBounce || !groundBounceWeakState.KnocksDown ||
			groundBounceWeakState.GroundBounceStrength != GroundBounceReactionStrength.Weak ||
			groundBounceWeakState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("ground_bounce_weak") || frames.GetFrameCount("ground_bounce_weak") != 10 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("ground_bounce_weak", 9)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's weak ground-bounce reaction is incomplete");
			failures++;
		}
		NormalMoveData groundBounceMediumState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [やられ]垂直バウンド中");
		if (groundBounceMediumState?.AnimationName != "ground_bounce_medium" || groundBounceMediumState.ActiveFrames != 1038 ||
			groundBounceMediumState.HitReaction != HitReactionKind.GroundBounce ||
			groundBounceMediumState.KnockdownType != KnockdownType.GroundBounce || !groundBounceMediumState.KnocksDown ||
			groundBounceMediumState.GroundBounceStrength != GroundBounceReactionStrength.Medium ||
			groundBounceMediumState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("ground_bounce_medium") || frames.GetFrameCount("ground_bounce_medium") != 10 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("ground_bounce_medium", 9)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's medium ground-bounce reaction is incomplete");
			failures++;
		}
		NormalMoveData groundBounceStrongState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [やられ]垂直バウンド強");
		if (groundBounceStrongState?.AnimationName != "ground_bounce_strong" || groundBounceStrongState.ActiveFrames != 1038 ||
			groundBounceStrongState.HitReaction != HitReactionKind.GroundBounce ||
			groundBounceStrongState.KnockdownType != KnockdownType.GroundBounce || !groundBounceStrongState.KnocksDown ||
			groundBounceStrongState.GroundBounceStrength != GroundBounceReactionStrength.Strong ||
			groundBounceStrongState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("ground_bounce_strong") || frames.GetFrameCount("ground_bounce_strong") != 10 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("ground_bounce_strong", 9)) != 1000)
		{
			GD.PushError("Imported roster regression: Mecha Heita's strong ground-bounce reaction is incomplete");
			failures++;
		}
		NormalMoveData standBlockWeakState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [ガード]立ち_弱");
		if (standBlockWeakState?.AnimationName != "stand_block_weak" || standBlockWeakState.ActiveFrames != 10 ||
			standBlockWeakState.GuardReactionStrength != GuardReactionStrength.Weak ||
			standBlockWeakState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("stand_block_weak") || frames.GetFrameCount("stand_block_weak") != 1 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("stand_block_weak", 0)) != 10)
		{
			GD.PushError("Imported roster regression: Mecha Heita's weak standing guard reaction is incomplete");
			failures++;
		}
		NormalMoveData standBlockMediumState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [ガード]立ち_中");
		if (standBlockMediumState?.AnimationName != "stand_block_medium" || standBlockMediumState.ActiveFrames != 17 ||
			standBlockMediumState.GuardReactionStrength != GuardReactionStrength.Medium ||
			standBlockMediumState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("stand_block_medium") || frames.GetFrameCount("stand_block_medium") != 2 ||
			Enumerable.Range(0, frames.GetFrameCount("stand_block_medium"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("stand_block_medium", index))) != 17)
		{
			GD.PushError("Imported roster regression: Mecha Heita's medium standing guard reaction is incomplete");
			failures++;
		}
		NormalMoveData standBlockStrongState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [ガード]立ち_強");
		if (standBlockStrongState?.AnimationName != "stand_block_strong" || standBlockStrongState.ActiveFrames != 21 ||
			standBlockStrongState.GuardReactionStrength != GuardReactionStrength.Strong ||
			standBlockStrongState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("stand_block_strong") || frames.GetFrameCount("stand_block_strong") != 2 ||
			Enumerable.Range(0, frames.GetFrameCount("stand_block_strong"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("stand_block_strong", index))) != 21)
		{
			GD.PushError("Imported roster regression: Mecha Heita's strong standing guard reaction is incomplete");
			failures++;
		}
		NormalMoveData standBlockSpecialStrongState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [ガード]立ち_特強");
		if (standBlockSpecialStrongState?.AnimationName != "stand_block_special_strong" ||
			standBlockSpecialStrongState.ActiveFrames != 23 ||
			standBlockSpecialStrongState.GuardReactionStrength != GuardReactionStrength.SpecialStrong ||
			standBlockSpecialStrongState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("stand_block_special_strong") || frames.GetFrameCount("stand_block_special_strong") != 3 ||
			Enumerable.Range(0, frames.GetFrameCount("stand_block_special_strong"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("stand_block_special_strong", index))) != 23)
		{
			GD.PushError("Imported roster regression: Mecha Heita's special-strength standing guard reaction is incomplete");
			failures++;
		}
		NormalMoveData crouchBlockWeakState = fighter.Definition?.StateBoxes?.FindStateRule("STATE [ガード]屈_弱");
		if (crouchBlockWeakState?.AnimationName != "crouch_block_weak" || crouchBlockWeakState.ActiveFrames != 10 ||
			crouchBlockWeakState.Stance != NormalMoveStance.Crouching ||
			crouchBlockWeakState.GuardReactionStrength != GuardReactionStrength.Weak ||
			crouchBlockWeakState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("crouch_block_weak") || frames.GetFrameCount("crouch_block_weak") != 1 ||
			Mathf.RoundToInt((float)frames.GetFrameDuration("crouch_block_weak", 0)) != 10)
		{
			GD.PushError("Imported roster regression: Mecha Heita's weak crouching guard reaction is incomplete");
			failures++;
		}
		var remainingGuardStates = new[]
		{
			(State: "STATE [ガード]屈_中", Animation: "crouch_block_medium", Stance: NormalMoveStance.Crouching,
				Strength: GuardReactionStrength.Medium, Drawings: 2, Ticks: 17),
			(State: "STATE [ガード]屈_強", Animation: "crouch_block_strong", Stance: NormalMoveStance.Crouching,
				Strength: GuardReactionStrength.Strong, Drawings: 2, Ticks: 21),
			(State: "STATE [ガード]屈_特強", Animation: "crouch_block_special_strong", Stance: NormalMoveStance.Crouching,
				Strength: GuardReactionStrength.SpecialStrong, Drawings: 3, Ticks: 23),
			(State: "STATE [ガード]空中_弱", Animation: "air_block_weak", Stance: NormalMoveStance.Airborne,
				Strength: GuardReactionStrength.Weak, Drawings: 1, Ticks: 7),
			(State: "STATE [ガード]空中_中", Animation: "air_block_medium", Stance: NormalMoveStance.Airborne,
				Strength: GuardReactionStrength.Medium, Drawings: 1, Ticks: 13),
			(State: "STATE [ガード]空中_強", Animation: "air_block_strong", Stance: NormalMoveStance.Airborne,
				Strength: GuardReactionStrength.Strong, Drawings: 1, Ticks: 15),
			(State: "STATE [ガード]空中_特強", Animation: "air_block_special_strong", Stance: NormalMoveStance.Airborne,
				Strength: GuardReactionStrength.SpecialStrong, Drawings: 1, Ticks: 18),
		};
		foreach (var expected in remainingGuardStates)
		{
			NormalMoveData guardState = fighter.Definition?.StateBoxes?.FindStateRule(expected.State);
			if (guardState?.AnimationName != expected.Animation || guardState.ActiveFrames != expected.Ticks ||
				guardState.Stance != expected.Stance || guardState.GuardReactionStrength != expected.Strength ||
				guardState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
				!frames.HasAnimation(expected.Animation) || frames.GetFrameCount(expected.Animation) != expected.Drawings ||
				Enumerable.Range(0, frames.GetFrameCount(expected.Animation))
					.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(expected.Animation, index))) != expected.Ticks)
			{
				GD.PushError($"Imported roster regression: Mecha Heita guard reaction '{expected.State}' is incomplete");
				failures++;
			}
		}
		var specialReactionStates = new[]
		{
			(State: "STATE [特殊やられ]よろめき", Animation: "special_stagger",
				Reaction: SpecialReactionKind.Stagger, Drawings: 7, Ticks: 30),
			(State: "STATE [特殊やられ]スライドダウン_横", Animation: "slide_down_horizontal",
				Reaction: SpecialReactionKind.SlideDownHorizontal, Drawings: 9, Ticks: 1032),
			(State: "STATE [特殊やられ]スライドダウン_斜め下", Animation: "slide_down_diagonal",
				Reaction: SpecialReactionKind.SlideDownDiagonal, Drawings: 1, Ticks: 1000),
			(State: "STATE [特殊やられ]ダウン(スライド)", Animation: "slide_downed",
				Reaction: SpecialReactionKind.SlideDowned, Drawings: 34, Ticks: 70),
			(State: "STATE [やられ]斜めバウンド", Animation: "diagonal_bounce",
				Reaction: SpecialReactionKind.DiagonalBounce, Drawings: 10, Ticks: 1036),
			(State: "STATE [特殊やられ]引き戻し_弱", Animation: "pullback_weak",
				Reaction: SpecialReactionKind.PullbackWeak, Drawings: 8, Ticks: 17),
			(State: "STATE [特殊やられ]引き戻し_強", Animation: "pullback_strong",
				Reaction: SpecialReactionKind.PullbackStrong, Drawings: 8, Ticks: 30),
			(State: "STATE [特殊ガード]引き戻し_弱", Animation: "guard_pullback_weak",
				Reaction: SpecialReactionKind.GuardPullbackWeak, Drawings: 1, Ticks: 17),
			(State: "STATE [特殊ガード]引き戻し_強", Animation: "guard_pullback_strong",
				Reaction: SpecialReactionKind.GuardPullbackStrong, Drawings: 2, Ticks: 24),
			(State: "STATE [特殊やられ]引き戻し_空中", Animation: "pullback_air",
				Reaction: SpecialReactionKind.PullbackAir, Drawings: 9, Ticks: 1032),
			(State: "STATE [特殊ガード]引き戻し_空中", Animation: "guard_pullback_air",
				Reaction: SpecialReactionKind.GuardPullbackAir, Drawings: 1, Ticks: 18),
		};
		foreach (var expected in specialReactionStates)
		{
			NormalMoveData reactionState = fighter.Definition?.StateBoxes?.FindStateRule(expected.State);
			if (reactionState?.AnimationName != expected.Animation || reactionState.ActiveFrames != expected.Ticks ||
				reactionState.SpecialReaction != expected.Reaction ||
				reactionState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
				!frames.HasAnimation(expected.Animation) || frames.GetFrameCount(expected.Animation) != expected.Drawings ||
				Enumerable.Range(0, frames.GetFrameCount(expected.Animation))
					.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(expected.Animation, index))) != expected.Ticks)
			{
				GD.PushError($"Imported roster regression: Mecha Heita reaction '{expected.State}' is incomplete");
				failures++;
			}
		}
		SpecialMoveData alphaCounter = fighter.Definition?.SpecialMoves?.FindMove("ALPHA COUNTER", false, false);
		FighterBoxFrame alphaCounterHitbox = alphaCounter?.BoxTimeline?
			.FirstOrDefault(box => box?.Kind == FighterBoxKind.Hitbox);
		if (alphaCounter?.AnimationName != "alpha_counter" || alphaCounter.StartupFrames != 36 ||
			alphaCounter.ActiveFrames != 8 || alphaCounter.RecoveryFrames != 14 || !alphaCounter.GuardCancel ||
			!alphaCounter.CanStartDuringBlockstun || alphaCounter.CommandInput != null ||
			alphaCounter.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			alphaCounterHitbox?.StartFrame != 36 || alphaCounterHitbox.EndFrame != 43 ||
			!frames.HasAnimation("alpha_counter") || frames.GetFrameCount("alpha_counter") != 22 ||
			Enumerable.Range(0, frames.GetFrameCount("alpha_counter"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("alpha_counter", index))) != 58)
		{
			GD.PushError("Imported roster regression: Mecha Heita's Alpha Counter is not editor-ready");
			failures++;
		}
		JetEscapeAbility[] jetEscapes = fighter.Definition?.Abilities?.OfType<JetEscapeAbility>().ToArray() ??
			Array.Empty<JetEscapeAbility>();
		JetEscapeAbility jetEscapeLeft = jetEscapes.FirstOrDefault(ability =>
			ability.Direction == JetEscapeDirection.Backward);
		JetEscapeAbility jetEscapeRight = jetEscapes.FirstOrDefault(ability =>
			ability.Direction == JetEscapeDirection.Forward);
		NormalMoveData jetEscapeLeftState = fighter.Definition?.StateBoxes?.FindStateRule(
			"STATE ESCAPE LEFT / JET BACK DASH");
		NormalMoveData jetEscapeRightState = fighter.Definition?.StateBoxes?.FindStateRule(
			"STATE ESCAPE RIGHT / JET FORWARD DASH");
		bool jetEscapeAnimationsReady = new[] { "jet_escape_left", "jet_escape_right" }
			.All(animation => frames.HasAnimation(animation) && frames.GetFrameCount(animation) == 8 &&
				frames.GetAnimationLoop(animation) &&
				Enumerable.Range(0, frames.GetFrameCount(animation))
					.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration(animation, index))) == 16);
		bool hasForbiddenNormalDash = fighter.Definition?.Abilities?.Any(ability =>
			ability is DashAbility || ability is RunAbility) == true;
		if (jetEscapes.Length != 2 || hasForbiddenNormalDash ||
			jetEscapeLeft is not { AnimationName: "jet_escape_left", StateName: "STATE ESCAPE LEFT / JET BACK DASH",
				GasCost: 20f, ActiveFrames: 16 } ||
			jetEscapeRight is not { AnimationName: "jet_escape_right", StateName: "STATE ESCAPE RIGHT / JET FORWARD DASH",
				GasCost: 20f, ActiveFrames: 16 } ||
			jetEscapeLeftState?.AnimationName != "jet_escape_left" || jetEscapeLeftState.ActiveFrames != 16 ||
			jetEscapeLeftState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			jetEscapeRightState?.AnimationName != "jet_escape_right" || jetEscapeRightState.ActiveFrames != 16 ||
			jetEscapeRightState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!jetEscapeAnimationsReady)
		{
			GD.PushError("Imported roster regression: Mecha Heita's Special-1 gas escapes or no-normal-dash rule is incomplete");
			failures++;
		}
		NormalMoveData escapeLandingState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE ESCAPE LANDING");
		bool sharedEscapeLandingAnimation = frames.HasAnimation("escape_landing") &&
			frames.GetFrameCount("escape_landing") == 5 &&
			Enumerable.Range(0, frames.GetFrameCount("escape_landing"))
				.Sum(index => Mathf.RoundToInt((float)frames.GetFrameDuration("escape_landing", index))) == 10;
		if (escapeLandingState?.AnimationName != "escape_landing" || escapeLandingState.ActiveFrames != 10 ||
			escapeLandingState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!sharedEscapeLandingAnimation)
		{
			GD.PushError("Imported roster regression: Mecha Heita's escape/backdash landing recovery is incomplete");
			failures++;
		}
		NormalMoveData winTauntState = fighter.Definition?.StateBoxes?.Rules?
			.FirstOrDefault(rule => rule?.AttackName == "STATE WIN TAUNT");
		if (winTauntState?.AnimationName != "win" || winTauntState.ActiveFrames != 309 ||
			winTauntState.BoxTimeline?.Any(box => box?.Kind == FighterBoxKind.Hurtbox) != true ||
			!frames.HasAnimation("win") || frames.GetFrameCount("win") != 17 ||
			!frames.HasAnimation("win_loop") || !frames.GetAnimationLoop("win_loop") ||
			!frames.HasAnimation("taunt") || frames.GetFrameCount("taunt") != 17)
		{
			GD.PushError("Imported roster regression: Mecha Heita shared win/taunt animation is incomplete");
			failures++;
		}
		fighter.ApplyStumbleHitstun(20, 120f);
		if (fighter.HitState != FighterHitState.Stumble || fighter.CurrentKnockdownType != KnockdownType.SoftKnockdown ||
			fighter.HitstunFramesLeft != 20 || fighter.Velocity.X <= 0f || fighter.Velocity.Y >= 0f)
		{
			GD.PushError("Imported roster regression: the stumble reaction does not launch into its landing knockdown");
			failures++;
		}
		fighter.ApplyWallBounceHitstun(24, 1, WallBounceReactionStrength.Weak);
		float weakWallBounceSpeed = Mathf.Abs(fighter.Velocity.X);
		if (fighter.HitState != FighterHitState.WallBounce ||
			fighter.CurrentWallBounceStrength != WallBounceReactionStrength.Weak ||
			!Mathf.IsEqualApprox(weakWallBounceSpeed, fighter.WeakWallBounceHorizontalSpeed))
		{
			GD.PushError("Imported roster regression: weak wall bounce does not use its weaker authored launch");
			failures++;
		}
		fighter.ApplyWallBounceHitstun(24, 1, WallBounceReactionStrength.Strong);
		if (fighter.CurrentWallBounceStrength != WallBounceReactionStrength.Strong ||
			Mathf.Abs(fighter.Velocity.X) <= weakWallBounceSpeed)
		{
			GD.PushError("Imported roster regression: strong wall bounce is not stronger than the weak variant");
			failures++;
		}
		fighter.ApplyGroundBounceHitstun(24, 70f, 500f, strength: GroundBounceReactionStrength.Weak);
		if (fighter.HitState != FighterHitState.GroundBounce ||
			fighter.CurrentKnockdownType != KnockdownType.GroundBounce ||
			fighter.CurrentGroundBounceStrength != GroundBounceReactionStrength.Weak ||
			fighter.CurrentGroundBounceAnimationName != "ground_bounce_weak")
		{
			GD.PushError("Imported roster regression: weak ground bounce does not enter its authored reaction channel");
			failures++;
		}
		fighter.ApplyGroundBounceHitstun(24, 70f, 650f, strength: GroundBounceReactionStrength.Medium);
		if (fighter.CurrentGroundBounceStrength != GroundBounceReactionStrength.Medium ||
			fighter.CurrentGroundBounceAnimationName != "ground_bounce_medium")
		{
			GD.PushError("Imported roster regression: medium ground bounce does not enter its authored reaction channel");
			failures++;
		}
		fighter.ApplyGroundBounceHitstun(24, 70f, 800f, strength: GroundBounceReactionStrength.Strong);
		if (fighter.CurrentGroundBounceStrength != GroundBounceReactionStrength.Strong ||
			fighter.CurrentGroundBounceAnimationName != "ground_bounce_strong")
		{
			GD.PushError("Imported roster regression: strong ground bounce does not enter its authored reaction channel");
			failures++;
		}
		fighter.ApplyBlockstun(10, 30f, GuardReactionStrength.Weak);
		if (!fighter.IsInBlockstun || fighter.CurrentGuardReactionStrength != GuardReactionStrength.Weak ||
			fighter.CurrentStandingGuardAnimationName != "stand_block_weak" ||
			fighter.CurrentCrouchingGuardAnimationName != "crouch_block_weak" ||
			fighter.CurrentAirGuardAnimationName != "air_block_weak" || fighter.BlockReactionSerial == 0)
		{
			GD.PushError("Imported roster regression: weak hits do not enter the weak standing guard reaction channel");
			failures++;
		}
		fighter.ApplyBlockstun(17, 45f, GuardReactionStrength.Medium);
		if (fighter.CurrentGuardReactionStrength != GuardReactionStrength.Medium ||
			fighter.CurrentStandingGuardAnimationName != "stand_block_medium" ||
			fighter.CurrentCrouchingGuardAnimationName != "crouch_block_medium" ||
			fighter.CurrentAirGuardAnimationName != "air_block_medium")
		{
			GD.PushError("Imported roster regression: medium hits do not enter the medium standing guard reaction channel");
			failures++;
		}
		fighter.ApplyBlockstun(21, 60f, GuardReactionStrength.Strong);
		if (fighter.CurrentGuardReactionStrength != GuardReactionStrength.Strong ||
			fighter.CurrentStandingGuardAnimationName != "stand_block_strong" ||
			fighter.CurrentCrouchingGuardAnimationName != "crouch_block_strong" ||
			fighter.CurrentAirGuardAnimationName != "air_block_strong")
		{
			GD.PushError("Imported roster regression: strong hits do not enter the strong standing guard reaction channel");
			failures++;
		}
		fighter.ApplyBlockstun(23, 75f, GuardReactionStrength.SpecialStrong);
		if (fighter.CurrentGuardReactionStrength != GuardReactionStrength.SpecialStrong ||
			fighter.CurrentStandingGuardAnimationName != "stand_block_special_strong" ||
			fighter.CurrentCrouchingGuardAnimationName != "crouch_block_special_strong" ||
			fighter.CurrentAirGuardAnimationName != "air_block_special_strong")
		{
			GD.PushError("Imported roster regression: special moves do not enter the special-strength standing guard channel");
			failures++;
		}
		if (!FighterController.ShouldUseAutomaticSpecialStagger(true, false, false) ||
			!FighterController.ShouldUseAutomaticSpecialStagger(false, true, false) ||
			FighterController.ShouldUseAutomaticSpecialStagger(true, true, true))
		{
			GD.PushError("Imported roster regression: special moves and counter hits do not select stagger correctly");
			failures++;
		}
		var playableSpecialReactions = new[]
		{
			(Kind: SpecialReactionKind.Stagger, Animation: "special_stagger"),
			(Kind: SpecialReactionKind.SlideDownHorizontal, Animation: "slide_down_horizontal"),
			(Kind: SpecialReactionKind.SlideDownDiagonal, Animation: "slide_down_diagonal"),
			(Kind: SpecialReactionKind.SlideDowned, Animation: "slide_downed"),
			(Kind: SpecialReactionKind.DiagonalBounce, Animation: "diagonal_bounce"),
			(Kind: SpecialReactionKind.PullbackWeak, Animation: "pullback_weak"),
			(Kind: SpecialReactionKind.PullbackStrong, Animation: "pullback_strong"),
			(Kind: SpecialReactionKind.PullbackAir, Animation: "pullback_air"),
		};
		foreach (var expected in playableSpecialReactions)
		{
			fighter.ApplySpecialReactionHitstun(30, 80f, expected.Kind);
			if (fighter.CurrentSpecialReaction != expected.Kind ||
				fighter.CurrentSpecialReactionAnimationName != expected.Animation || !fighter.IsInHitstun)
			{
				GD.PushError($"Imported roster regression: special reaction '{expected.Kind}' does not enter gameplay");
				failures++;
			}
		}
		fighter.ApplyBlockstun(17, 60f, GuardReactionStrength.Weak, SpecialReactionKind.GuardPullbackWeak);
		if (fighter.CurrentSpecialReaction != SpecialReactionKind.GuardPullbackWeak ||
			fighter.CurrentSpecialReactionAnimationName != "guard_pullback_weak" || fighter.Velocity.X >= 0f)
		{
			GD.PushError("Imported roster regression: weak pullback guard reaction does not pull toward the attacker");
			failures++;
		}
		fighter.ApplyBlockstun(24, 75f, GuardReactionStrength.Strong, SpecialReactionKind.GuardPullbackStrong);
		if (fighter.CurrentSpecialReaction != SpecialReactionKind.GuardPullbackStrong ||
			fighter.CurrentSpecialReactionAnimationName != "guard_pullback_strong")
		{
			GD.PushError("Imported roster regression: strong pullback guard reaction does not enter gameplay");
			failures++;
		}
		fighter.ApplyBlockstun(18, 60f, GuardReactionStrength.SpecialStrong, SpecialReactionKind.GuardPullbackAir);
		if (fighter.CurrentSpecialReaction != SpecialReactionKind.GuardPullbackAir ||
			fighter.CurrentSpecialReactionAnimationName != "guard_pullback_air")
		{
			GD.PushError("Imported roster regression: airborne pullback guard reaction does not enter gameplay");
			failures++;
		}
		fighter.ApplyHitFallHitstun(18, -90f);
		if (fighter.HitState != FighterHitState.HitFall ||
			fighter.CurrentKnockdownType != KnockdownType.AirKnockdown ||
			fighter.HitstunFramesLeft != 18 || fighter.Velocity.X >= 0f || fighter.Velocity.Y < fighter.HitFallSpeed)
		{
			GD.PushError("Imported roster regression: hit-fall does not force a downward landing knockdown");
			failures++;
		}
		fighter.ApplyWallSplat(1);
		if (fighter.HitState != FighterHitState.WallSplat ||
			fighter.CurrentKnockdownType != KnockdownType.SoftKnockdown ||
			!Mathf.IsZeroApprox(fighter.Velocity.X) || fighter.Velocity.Y < fighter.WallSplatSlideSpeed)
		{
			GD.PushError("Imported roster regression: strong wall bounce does not transition into the wall slide");
			failures++;
		}
		fighter.EnterGroundedKnockdown();
		if (!fighter.IsGroundedKnockdown || fighter.HitstunFramesLeft < fighter.GroundedKnockdownHoldFrames ||
			fighter.CurrentWallBounceStrength != WallBounceReactionStrength.None)
		{
			GD.PushError("Imported roster regression: [やられ]ダウン does not own the grounded knockdown hold");
			failures++;
		}
		fighter.BeginWakeup();
		if (!fighter.IsWakingUp || fighter.WakeupFramesLeft != 45 || fighter.CurrentWakeupFrame != 0 ||
			fighter.IsInHitstun || fighter.CurrentKnockdownType != KnockdownType.None)
		{
			GD.PushError("Imported roster regression: [やられ]起き上がり does not own the 45-tick invulnerable wakeup");
			failures++;
		}
		SpecialMoveData groundedDp = fighter.Definition?.SpecialMoves?
			.FindMove("HELICOPTER DP", false, false);
		SpecialMoveData airborneDp = fighter.Definition?.SpecialMoves?
			.FindMove("LIGHT HELICOPTER DP", false, true);
		if (groundedDp?.AnimationName != "anim_130" || groundedDp.Stance != NormalMoveStance.Standing ||
			groundedDp.SelfLaunchStartFrame != 9 || !groundedDp.SelfLaunchUsesFacing ||
			!Mathf.IsEqualApprox(groundedDp.SelfHorizontalSpeed, 1000f) ||
			!Mathf.IsEqualApprox(groundedDp.SelfLaunchSpeed, 800f) ||
			!Mathf.IsEqualApprox(groundedDp.SelfHorizontalDeceleration, 2400f) ||
			groundedDp.HitSparkScene == null)
		{
			GD.PushError("Imported roster regression: Mecha Heita grounded DP does not match source action 130 movement/blood");
			failures++;
		}
		if (airborneDp?.AnimationName != "anim_132" || airborneDp.Stance != NormalMoveStance.Airborne ||
			airborneDp.CommandInput?.AirOnly != true || airborneDp.CommandInput.GroundOnly ||
			airborneDp.SelfLaunchStartFrame != 9 || !airborneDp.SelfLaunchUsesFacing ||
			!Mathf.IsZeroApprox(airborneDp.SelfHorizontalSpeed) ||
			!Mathf.IsEqualApprox(airborneDp.SelfLaunchSpeed, 800f))
		{
			GD.PushError("Imported roster regression: Mecha Heita airborne DP does not match source action 132 movement");
			failures++;
		}
		return failures;
	}
}

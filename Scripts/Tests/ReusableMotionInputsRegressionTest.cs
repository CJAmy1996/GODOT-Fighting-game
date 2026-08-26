using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Regression coverage for the reusable motion library and automatic move routing.</summary>
public partial class ReusableMotionInputsRegressionTest : Node2D
{
	private const string MotionRoot = "res://Data/Motions/";
	private SpriteTestFighter _fighter;
	private int _stage;
	private int _settleFrames = 6;
	private int _watchdog = 90;

	public override void _Ready()
	{
		try { ValidateMotionLibrary(); }
		catch (Exception exception)
		{
			GD.PushError($"REUSABLE MOTION INPUT TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
			return;
		}

		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(900f, 20f) } });
		AddChild(floor);
		_fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<SpriteTestFighter>();
		_fighter.ReadLocalInput = false;
		_fighter.FaceWithMovement = false;
		_fighter.Position = Vector2.Zero;
		_fighter.SetFacing(1);
		_fighter.SetExternalInput(default);
		AddChild(_fighter);

		var routedMove = new SpecialMoveData
		{
			AttackName = "SPECIAL REUSABLE QCB TEST",
			AnimationName = "standing_heavy_kick",
			Stance = NormalMoveStance.Standing,
			StartupFrames = 2,
			ActiveFrames = 1,
			RecoveryFrames = 3,
			SuppressFallbackHitbox = true,
			CommandInput = Binding(Load("quarter_circle_back"), MotionAttackButton.HeavyPunch)
		};
		_fighter.Definition.SpecialMoves.Moves =
			(_fighter.Definition.SpecialMoves.Moves ?? Array.Empty<SpecialMoveData>()).Append(routedMove).ToArray();
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0) throw new InvalidOperationException($"runtime routing timed out at stage {_stage}");
			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					Expect(_fighter.IsOnFloor(), "routing fighter did not settle on the floor");
					_fighter.SetExternalInput(DirectionInput(MotionDirection.Down, 1));
					_stage = 1;
					break;
				case 1:
					_fighter.SetExternalInput(DirectionInput(MotionDirection.DownBack, 1));
					_stage = 2;
					break;
				case 2:
					_fighter.SetExternalInput(DirectionInput(MotionDirection.Back, 1, MotionAttackButton.HeavyPunch));
					_stage = 3;
					break;
				case 3:
					_fighter.SetExternalInput(default);
					Expect(_fighter.CurrentAttackName == "SPECIAL REUSABLE QCB TEST",
						$"data-driven QCB+HP routed as '{_fighter.CurrentAttackName}'");
					GD.Print("REUSABLE MOTION INPUT TEST PASSED: all resources match and QCB+HP routes a real Sanzou special.");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"REUSABLE MOTION INPUT TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void ValidateMotionLibrary()
	{
		var sequenceCases = new Dictionary<string, MotionDirection[]>
		{
			["quarter_circle_forward"] = new[] { MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward },
			["shoryuken"] = new[] { MotionDirection.Forward, MotionDirection.Down, MotionDirection.DownForward },
			["quarter_circle_back"] = new[] { MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back },
			["spd_circle"] = new[] { MotionDirection.Forward, MotionDirection.DownForward, MotionDirection.Down, MotionDirection.DownBack,
				MotionDirection.Back, MotionDirection.UpBack, MotionDirection.Up, MotionDirection.UpForward },
			["double_circle_720"] = new[] { MotionDirection.Forward, MotionDirection.DownForward, MotionDirection.Down, MotionDirection.DownBack,
				MotionDirection.Back, MotionDirection.UpBack, MotionDirection.Up, MotionDirection.UpForward,
				MotionDirection.Forward, MotionDirection.DownForward, MotionDirection.Down, MotionDirection.DownBack,
				MotionDirection.Back, MotionDirection.UpBack, MotionDirection.Up, MotionDirection.UpForward },
			["half_circle_back"] = new[] { MotionDirection.Forward, MotionDirection.DownForward, MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back },
			["half_circle_forward"] = new[] { MotionDirection.Back, MotionDirection.DownBack, MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward },
			["backward_shoryuken"] = new[] { MotionDirection.Back, MotionDirection.Down, MotionDirection.DownBack },
			["potemkin_buster"] = new[] { MotionDirection.Forward, MotionDirection.DownForward, MotionDirection.Down, MotionDirection.DownBack,
				MotionDirection.Back, MotionDirection.Forward },
			["double_quarter_circle_forward"] = new[] { MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward,
				MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward },
			["double_quarter_circle_back"] = new[] { MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back,
				MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back },
			["iori_maiden_masher"] = new[] { MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back, MotionDirection.DownBack,
				MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward },
			["down_down"] = new[] { MotionDirection.Down, MotionDirection.Neutral, MotionDirection.Down },
			["tiger_knee"] = new[] { MotionDirection.Down, MotionDirection.DownForward, MotionDirection.Forward, MotionDirection.UpForward }
		};

		foreach ((string resourceName, MotionDirection[] sequence) in sequenceCases)
		{
			MotionInputDefinition definition = Load(resourceName);
			Expect(definition.Kind == MotionInputKind.DirectionSequence, $"{resourceName} is not a direction sequence");
			Expect(MatchesSequence(definition, sequence, 1), $"{resourceName} failed facing right");
		}
		Expect(MatchesSequence(Load("shoryuken"), sequenceCases["shoryuken"], -1),
			"facing-relative Shoryuken failed while facing left");

		ValidateMash();
		ValidateCharge("charge_back_forward", MotionDirection.Back,
			new[] { MotionDirection.Forward });
		ValidateCharge("charge_down_up", MotionDirection.Down,
			new[] { MotionDirection.Up });
		ValidateCharge("charge_back_forward_back_forward", MotionDirection.Back,
			new[] { MotionDirection.Forward, MotionDirection.Back, MotionDirection.Forward });
		ValidateCharge("charge_down_back_forward_back_up_forward", MotionDirection.DownBack,
			new[] { MotionDirection.Forward, MotionDirection.Back, MotionDirection.UpForward });
		ValidateConsumptionAndExpiry();
		ValidateCleanDirectionalBackdash();
	}

	private static void ValidateCleanDirectionalBackdash()
	{
		var clean = new MotionInputBuffer();
		clean.PressHorizontalTap(-1, 1, 4, 12, 12, 4, 18);
		clean.Tick();
		clean.PressHorizontalTap(-1, 1, 4, 12, 12, 4, 18);
		Expect(clean.HasDashCommand && clean.DashCommandDirection == -1,
			"clean double-back did not produce a backward dash command");

		var interrupted = new MotionInputBuffer();
		interrupted.PressHorizontalTap(-1, 1, 4, 12, 12, 4, 18);
		interrupted.Tick();
		interrupted.PressDown();
		interrupted.Tick();
		interrupted.PressHorizontalTap(-1, 1, 4, 12, 12, 4, 18);
		Expect(!interrupted.HasDashCommand,
			"down input did not invalidate the pending double-back sequence");

		var groundBackdash = GD.Load<ModularFighter.Movement.DashAbility>(
			"res://Data/Characters/BigBangBeatRevolve/Kamui/kamui_ground_backdash.tres");
		Expect(groundBackdash != null && groundBackdash.RequireDirectionalDoubleTap && groundBackdash.DisallowDownInput,
			"Kamui ground backdash is not configured for a clean double-back");

		var airBackdash = GD.Load<ModularFighter.Movement.TeleportDashAbility>(
			"res://Data/Characters/BigBangBeatRevolve/Kamui/kamui_teleport_backdash.tres");
		Expect(airBackdash != null && airBackdash.RequireDirectionalDoubleTap && airBackdash.DisallowDownInput,
			"Kamui air backdash is not configured for a clean double-back");
	}

	private static bool MatchesSequence(MotionInputDefinition definition, MotionDirection[] directions, int facing)
	{
		var buffer = new MotionInputBuffer();
		MotionInputBinding binding = Binding(definition, MotionAttackButton.HeavyPunch);
		FighterInput finalInput = default;
		for (int index = 0; index < directions.Length; index++)
		{
			finalInput = DirectionInput(directions[index], facing,
				index == directions.Length - 1 ? MotionAttackButton.HeavyPunch : MotionAttackButton.None);
			buffer.RecordReusableInput(finalInput, facing);
		}
		return buffer.TryMatchReusableMotion(binding, finalInput, out _);
	}

	private static void ValidateMash()
	{
		MotionInputDefinition mash = Load("mash_five");
		var buffer = new MotionInputBuffer();
		MotionInputBinding punches = Binding(mash, MotionAttackButton.AnyPunch);
		for (int press = 0; press < 4; press++)
		{
			FighterInput input = DirectionInput(MotionDirection.Neutral, 1, MotionAttackButton.LightPunch);
			buffer.RecordReusableInput(input, 1);
			Expect(!buffer.TryMatchReusableMotion(punches, input, out _), $"mash activated after only {press + 1} presses");
			buffer.RecordReusableInput(default, 1);
		}
		FighterInput fifth = DirectionInput(MotionDirection.Neutral, 1, MotionAttackButton.HeavyPunch);
		buffer.RecordReusableInput(fifth, 1);
		Expect(buffer.TryMatchReusableMotion(punches, fifth, out _), "five-punch mash did not activate");
		MotionInputBinding kicks = Binding(mash, MotionAttackButton.AnyKick);
		Expect(!buffer.TryMatchReusableMotion(kicks, fifth, out _), "punch mash incorrectly activated a kick binding");

		buffer = new MotionInputBuffer();
		MotionInputBinding shortWindow = Binding(mash, MotionAttackButton.AnyPunch);
		shortWindow.MashWindowFramesOverride = 8;
		for (int press = 0; press < 4; press++)
		{
			FighterInput input = DirectionInput(MotionDirection.Neutral, 1, MotionAttackButton.LightPunch);
			buffer.RecordReusableInput(input, 1);
			buffer.RecordReusableInput(default, 1);
		}
		buffer.RecordReusableInput(default, 1);
		FighterInput expiredFifth = DirectionInput(MotionDirection.Neutral, 1, MotionAttackButton.HeavyPunch);
		buffer.RecordReusableInput(expiredFifth, 1);
		Expect(!buffer.TryMatchReusableMotion(shortWindow, expiredFifth, out _),
			"mash retained a press outside its per-move eight-frame window");

		buffer = new MotionInputBuffer();
		FighterInput inWindowFifth = default;
		for (int press = 0; press < 5; press++)
		{
			// Alternating LP/HP permits distinct just-presses on consecutive polling frames.
			inWindowFifth = DirectionInput(MotionDirection.Neutral, 1,
				press % 2 == 0 ? MotionAttackButton.LightPunch : MotionAttackButton.HeavyPunch);
			buffer.RecordReusableInput(inWindowFifth, 1);
		}
		Expect(buffer.TryMatchReusableMotion(shortWindow, inWindowFifth, out _),
			"five presses inside the per-move eight-frame mash window did not activate");
	}

	private static void ValidateCharge(string resourceName, MotionDirection chargeDirection, MotionDirection[] finish)
	{
		MotionInputDefinition definition = Load(resourceName);
		var buffer = new MotionInputBuffer();
		MotionInputBinding binding = Binding(definition, MotionAttackButton.HeavyKick);
		for (int frame = 0; frame < definition.ChargeFrames; frame++)
			buffer.RecordReusableInput(DirectionInput(chargeDirection, 1), 1);
		FighterInput finalInput = default;
		for (int index = 0; index < finish.Length; index++)
		{
			finalInput = DirectionInput(finish[index], 1,
				index == finish.Length - 1 ? MotionAttackButton.HeavyKick : MotionAttackButton.None);
			buffer.RecordReusableInput(finalInput, 1);
		}
		Expect(buffer.TryMatchReusableMotion(binding, finalInput, out _), $"{resourceName} failed after a legal 45f charge");
	}

	private static void ValidateConsumptionAndExpiry()
	{
		MotionInputDefinition qcb = Load("quarter_circle_back");
		var buffer = new MotionInputBuffer();
		MotionInputBinding binding = Binding(qcb, MotionAttackButton.LightKick);
		foreach (MotionDirection direction in new[] { MotionDirection.Down, MotionDirection.DownBack })
			buffer.RecordReusableInput(DirectionInput(direction, 1), 1);
		FighterInput final = DirectionInput(MotionDirection.Back, 1, MotionAttackButton.LightKick);
		buffer.RecordReusableInput(final, 1);
		Expect(buffer.TryMatchReusableMotion(binding, final, out long completion), "QCB did not match before consumption");
		buffer.ConsumeReusableMotion(qcb, completion);
		Expect(!buffer.TryMatchReusableMotion(binding, final, out _), "consumed QCB activated twice");

		buffer = new MotionInputBuffer();
		foreach (MotionDirection direction in new[] { MotionDirection.Down, MotionDirection.DownBack, MotionDirection.Back })
			buffer.RecordReusableInput(DirectionInput(direction, 1), 1);
		for (int frame = 0; frame <= qcb.ButtonLeniencyFrames; frame++) buffer.RecordReusableInput(default, 1);
		FighterInput late = DirectionInput(MotionDirection.Neutral, 1, MotionAttackButton.LightKick);
		buffer.RecordReusableInput(late, 1);
		Expect(!buffer.TryMatchReusableMotion(binding, late, out _), "expired QCB remained stored past five frames");
	}

	private static MotionInputDefinition Load(string name)
	{
		MotionInputDefinition definition = ResourceLoader.Load<MotionInputDefinition>($"{MotionRoot}{name}.tres");
		if (definition == null) throw new InvalidOperationException($"could not load motion resource '{name}'");
		return definition;
	}

	private static MotionInputBinding Binding(MotionInputDefinition definition, MotionAttackButton buttons) => new()
	{
		Motion = definition,
		Buttons = buttons,
		ButtonMatchMode = MotionButtonMatchMode.AnySelectedButton,
		GroundOnly = true
	};

	private static FighterInput DirectionInput(MotionDirection direction, int facing,
		MotionAttackButton button = MotionAttackButton.None)
	{
		float relativeHorizontal = direction is MotionDirection.Forward or MotionDirection.DownForward or MotionDirection.UpForward ? 1f :
			direction is MotionDirection.Back or MotionDirection.DownBack or MotionDirection.UpBack ? -1f : 0f;
		float vertical = direction is MotionDirection.Down or MotionDirection.DownForward or MotionDirection.DownBack ? 1f :
			direction is MotionDirection.Up or MotionDirection.UpForward or MotionDirection.UpBack ? -1f : 0f;
		float horizontal = relativeHorizontal * (facing >= 0 ? 1f : -1f);
		return new FighterInput(horizontal, vertical, false, false, false, false,
			lightPunchPressed: (button & MotionAttackButton.LightPunch) != 0,
			lightKickPressed: (button & MotionAttackButton.LightKick) != 0,
			heavyPunchPressed: (button & MotionAttackButton.HeavyPunch) != 0,
			heavyKickPressed: (button & MotionAttackButton.HeavyKick) != 0);
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

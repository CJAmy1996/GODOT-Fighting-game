using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Headless regression coverage for Sanzou's editor/runtime jab timeline contract.</summary>
public partial class SanzouJabRegressionTest : Node2D
{
	private SpriteTestFighter _fighter;
	private int _stage;
	private int _settleTicks = 5;
	private int _watchdog = 120;
	private FighterController _defender;
	private int _crouchMashTicks;
	private int _crouchMashHits;
	private string _crouchMashTarget = "Kung Fu Man";

	public override void _Ready()
	{
		try { ValidateAllSanzouMoveTimelines(); }
		catch (Exception exception)
		{
			GD.PushError($"SANZOU TIMELINE AUDIT FAILED: {exception.Message}");
			GetTree().Quit(1);
			return;
		}
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1000f, 20f) } });
		AddChild(floor);
		var scene = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn");
		_fighter = scene.Instantiate<SpriteTestFighter>();
		_fighter.ReadLocalInput = false;
		_fighter.TeamId = 1;
		_fighter.Position = Vector2.Zero;
		AddChild(_fighter);
		_fighter.SetExternalInput(default);
		_defender = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/KungFuManTest.tscn")
			.Instantiate<FighterController>();
		_defender.Name = "JabTestDefender";
		_defender.TeamId = 2;
		_defender.Position = new Vector2(80f, 0f);
		_defender.ProcessMode = ProcessModeEnum.Disabled;
		AddChild(_defender);
	}

	private static void ValidateAllSanzouMoveTimelines()
	{
		var definition = ResourceLoader.Load<FighterDefinition>("res://Data/Characters/Sanzo/sanzo_kongoumaru.tres");
		var frames = ResourceLoader.Load<SpriteFrames>("res://Assets/TestFighter/Sanzo/sanzo_sprite_frames.tres");
		var moves = new List<NormalMoveData>();
		moves.AddRange(definition.NormalMoves?.Rules ?? Array.Empty<NormalMoveData>());
		moves.AddRange(definition.SpecialMoves?.Moves ?? Array.Empty<SpecialMoveData>());
		moves.AddRange(definition.StateBoxes?.Rules ?? Array.Empty<NormalMoveData>());
		var errors = new List<string>();
		foreach (NormalMoveData move in moves.Where(move => move != null))
		{
			if (string.IsNullOrEmpty(move.AnimationName) || !frames.HasAnimation(move.AnimationName))
			{
				errors.Add($"{move.AttackName}: missing animation '{move.AnimationName}'");
				continue;
			}
			int startup = Mathf.Max(0, move.StartupFrames);
			int active = Mathf.Max(0, move.ActiveFrames);
			int recovery = Mathf.Max(0, move.RecoveryFrames);
			int activeEnd = startup + active - 1;
			foreach (FighterBoxFrame box in move.BoxTimeline ?? Array.Empty<FighterBoxFrame>())
			{
				if (box == null || box.Kind != FighterBoxKind.Hitbox) continue;
				if (box.StartFrame < startup || box.EndFrame > activeEnd)
					errors.Add($"{move.AttackName}: hitbox {box.StartFrame}-{box.EndFrame} exceeds active {startup}-{activeEnd}");
			}
		}
		if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0) throw new InvalidOperationException($"timeout at stage {_stage}, attack '{_fighter.CurrentAttackName}', frame {_fighter.CurrentAttackFrame}");
			switch (_stage)
			{
				case 0:
					if (--_settleTicks > 0) return;
					Expect(_fighter.IsOnFloor(), "fighter did not settle on the test floor");
					Expect(_fighter.CurrentBoxStateName == "STATE IDLE", $"expected idle box state, got '{_fighter.CurrentBoxStateName}'");
					Expect(_fighter.GetActiveLocalBoxInstances(FighterBoxKind.Hurtbox).Any(box => box.Source?.Tag == "idle-hurtbox"),
						"runtime did not use the authored idle hurtbox");
					_fighter.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false));
					_stage = 10;
					break;
				case 10:
					Expect(_fighter.CurrentBoxStateName == "STATE CROUCH", $"expected crouch box state, got '{_fighter.CurrentBoxStateName}'");
					Expect(_fighter.GetActiveLocalBoxInstances(FighterBoxKind.Hurtbox).Any(box => box.Source?.Tag == "full-crouch-hurtbox"),
						"runtime did not use the authored full-crouch hurtbox");
					_fighter.SetExternalInput(default);
					_stage = 11;
					break;
				case 11:
					_fighter.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false, lightPunchPressed: true));
					_stage = 1;
					break;
				case 1:
					_fighter.SetExternalInput(default);
					Expect(_fighter.CurrentAttackName == "LIGHT PUNCH", $"LP resolved as '{_fighter.CurrentAttackName}'");
					Expect(_fighter.CurrentAttackFrame == 0, $"attack began on frame {_fighter.CurrentAttackFrame}, not frame 0");
					_stage = 2;
					break;
				case 2:
					if (_fighter.CurrentAttackFrame < 4)
					{
						Expect(!_fighter.GetActiveLocalBoxes(FighterBoxKind.Hitbox).Any(),
							$"jab hitbox activated early on frame {_fighter.CurrentAttackFrame}");
						return;
					}
					Expect(_fighter.CurrentAttackFrame == 4, $"jab skipped active frame 4 (at {_fighter.CurrentAttackFrame})");
					Expect(_fighter.GetActiveLocalBoxes(FighterBoxKind.Hitbox).Any(), "jab has no active hitbox on frame 4");
					Expect(_fighter.CharacterSprite.Frame == 2, $"active jab displayed drawing {_fighter.CharacterSprite.Frame}, not 2");
					Expect(_fighter.TryApplyBasicAttackHit(_defender, out int standingHitstop, out _, out _, out _, out _),
						"jab hitbox exists but failed the real defender collision path");
					int expectedStandingJabHitstun = _fighter.Definition.NormalMoves
						.FindRule("LIGHT PUNCH", false, false)?.HitstunFrames ?? -1;
					Expect(_defender.HitstunFramesLeft == expectedStandingJabHitstun,
						$"standing jab applied {_defender.HitstunFramesLeft} hitstun frames instead of {expectedStandingJabHitstun}");
					GD.Print("SANZOU JAB TEST PASSED: LP resolves, frame 0 exists, active frame 4 drawing/hitbox align, defender is hit.");
					_fighter.RequestHitstop(standingHitstop);
					_defender.RequestHitstop(standingHitstop);
					_defender.ProcessMode = ProcessModeEnum.Inherit;
					_fighter.SetExternalInput(default);
					_settleTicks = 35;
					_watchdog = 240;
					_stage = 20;
					break;
				case 20:
					if (--_settleTicks > 0) return;
					Expect(!_fighter.IsAttacking, "standing jab did not recover before crouch-mash test");
					Expect(_defender.HitstunFramesLeft == 0, "defender did not recover before crouch-mash test");
					_fighter.Position = Vector2.Zero;
					_fighter.Velocity = Vector2.Zero;
					_fighter.SetFacing(1);
					// With authored pushback restored, point-blank mash range must still
					// permit the second jab while maximum-range jabs intentionally space out.
					_defender.Position = new Vector2(60f, 0f);
					_defender.Velocity = Vector2.Zero;
					_defender.SetFacing(-1);
					_crouchMashTicks = 0;
					_crouchMashHits = 0;
					_fighter.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false, lightPunchPressed: true));
					_stage = 21;
					break;
				case 21:
				case 31:
					if (_fighter.IsAttackActive && _fighter.TryApplyBasicAttackHit(_defender,
						out int crouchHitstop, out _, out _, out _, out _))
					{
						_crouchMashHits++;
						if (_crouchMashHits == 1)
						{
							NormalMoveData crouchJab = _fighter.Definition.NormalMoves.FindRule("LIGHT PUNCH", true, false);
							int expectedCrouchJabHitstun = crouchJab.HitstunFrames;
							Expect(_defender.HitstunFramesLeft == expectedCrouchJabHitstun,
								$"crouching low jab applied {_defender.HitstunFramesLeft} hitstun frames instead of {expectedCrouchJabHitstun}");
						}
						_fighter.RequestHitstop(crouchHitstop);
						_defender.RequestHitstop(crouchHitstop);
						if (_crouchMashHits >= 2)
						{
							Expect(_defender.ComboCount >= 2,
								$"second crouch jab connected after hitstun ended (combo count {_defender.ComboCount})");
							GD.Print($"SANZOU CROUCH JAB MASH TEST PASSED against {_crouchMashTarget}: two-hit combo connected at distance {Mathf.Abs(_defender.Position.X - _fighter.Position.X):0.0}.");
							if (_stage == 21)
							{
								ReplaceDefenderWithSanzou();
								_fighter.SetExternalInput(default);
								_settleTicks = 35;
								_watchdog = 240;
								_stage = 30;
							}
							else GetTree().Quit();
							break;
						}
					}
					_crouchMashTicks++;
					bool pressMashJab = _crouchMashTicks % 2 == 0;
					_fighter.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false,
						lightPunchPressed: pressMashJab));
					break;
				case 30:
					if (--_settleTicks > 0) return;
					Expect(!_fighter.IsAttacking, "attacker did not recover before Sanzou hurtbox test");
					_fighter.Position = Vector2.Zero;
					_fighter.Velocity = Vector2.Zero;
					_fighter.SetFacing(1);
					// Match the same practical contact depth against Sanzou's wider
					// authored hurtbox while preserving visible pushback.
					_defender.Position = new Vector2(83f, 0f);
					_defender.Velocity = Vector2.Zero;
					_defender.SetFacing(-1);
					_crouchMashTarget = "Sanzou";
					_crouchMashTicks = 0;
					_crouchMashHits = 0;
					_fighter.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false, lightPunchPressed: true));
					_stage = 31;
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU JAB TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private void ReplaceDefenderWithSanzou()
	{
		RemoveChild(_defender);
		_defender.QueueFree();
		_defender = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn")
			.Instantiate<FighterController>();
		_defender.Name = "SanzouJabTestDefender";
		_defender.ReadLocalInput = false;
		_defender.TeamId = 2;
		_defender.Position = new Vector2(300f, 0f);
		_defender.SetExternalInput(default);
		AddChild(_defender);
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

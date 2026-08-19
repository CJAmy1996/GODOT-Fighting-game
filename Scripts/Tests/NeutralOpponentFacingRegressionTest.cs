using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

public partial class NeutralOpponentFacingRegressionTest : Node2D
{
	private FighterController _fighter;
	private FighterController _opponent;
	private RunAbility _run;
	private int _stage;
	private int _settleFrames = 6;
	private int _watchdog = 300;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1400f, 20f) } });
		AddChild(floor);
		_fighter = Spawn("FacingProbe", -100f, 1);
		_opponent = Spawn("Opponent", 100f, -1);
		_fighter.SetOpponent(_opponent);
		_opponent.SetOpponent(_fighter);
		_run = new RunAbility { Id = "facing_test_run", Priority = 50 };
		_fighter.Definition.Abilities = _fighter.Definition.Abilities.Append(_run).ToArray();
	}

	private FighterController Spawn(string name, float x, int facing)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<FighterController>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.FaceWithMovement = false;
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(facing);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0) throw new InvalidOperationException($"timeout at stage {_stage}");
			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					_opponent.Position = new Vector2(-200f, -250f);
					_stage = 1;
					break;
				case 1:
					if (_opponent.IsOnFloor()) return;
					_fighter.SetFacing(1);
					_stage = 7;
					break;
				case 7:
					Expect(_fighter.Facing == 1,
						"grounded neutral fighter turned toward an airborne cross-up");
					_fighter.Position = new Vector2(-300f, 0f);
					_fighter.SetFacing(1);
					_fighter.SetExternalInput(new FighterInput(1f, 0f, false, false, true, false));
					_stage = 14;
					break;
				case 14:
					Expect(_fighter.ActiveAbility is RunAbility && _fighter.Facing == 1,
						"run-under probe did not begin with committed facing");
					_fighter.Position = new Vector2(-100f, 0f);
					_stage = 15;
					break;
				case 15:
					Expect(!_opponent.IsOnFloor(), "run-under probe allowed the opponent to land");
					Expect(_fighter.ActiveAbility is RunAbility && _fighter.Facing == 1,
						"fighter turned during the active run-under");
					_fighter.SetExternalInput(default);
					_stage = 16;
					break;
				case 16:
					if (_fighter.ActiveAbility != null || _fighter.IsInRunStopSlide) return;
					_stage = 17;
					break;
				case 17:
					Expect(!_opponent.IsOnFloor(), "run-under turn waited for the opponent to land");
					Expect(_fighter.Facing == -1,
						"fighter did not consume the run-under side switch on return to neutral");
					_fighter.Position = new Vector2(-100f, 0f);
					_fighter.SetFacing(1);
					_fighter.SetExternalInput(new FighterInput(-1f, 0f, false, false, false, false));
					_stage = 9;
					break;
				case 9:
					Expect(!_opponent.IsOnFloor(), "airborne walk-turn test allowed the opponent to land");
					Expect(_fighter.Facing == -1,
						"grounded walk did not turn toward an airborne opponent after the cross-up");
					_fighter.SetExternalInput(default);
					_opponent.Position = new Vector2(-200f, 0f);
					_opponent.Velocity = Vector2.Zero;
					_stage = 2;
					break;
				case 2:
					if (!_opponent.IsOnFloor()) return;
					_stage = 8;
					break;
				case 8:
					Expect(_fighter.Facing == -1, "neutral cross-under did not turn toward the opponent");
					_fighter.Position = new Vector2(-100f, -250f);
					_fighter.Velocity = Vector2.Zero;
					_fighter.SetFacing(1);
					_fighter.SetExternalInput(default);
					_stage = 10;
					break;
				case 10:
					if (_fighter.IsOnFloor()) return;
					// Positioning a CharacterBody2D does not refresh its floor flag until MoveAndSlide.
					// Establish the takeoff-facing side only after Godot reports the body airborne.
					_fighter.SetFacing(1);
					_stage = 13;
					break;
				case 13:
					Expect(!_fighter.IsOnFloor(), "normal-jump crossover probe landed before facing was checked");
					Expect(_fighter.Facing == 1, "normal jump turned after crossing over the opponent");
					_fighter.RefreshAirJumpResourcesForSuperJump();
					_stage = 11;
					break;
				case 11:
					Expect(!_fighter.IsOnFloor(), "super-jump crossover probe landed before facing was checked");
					Expect(_fighter.Facing == -1, "super jump did not turn after crossing over the opponent");
					_fighter.Position = new Vector2(-100f, 0f);
					_fighter.Velocity = Vector2.Zero;
					_stage = 12;
					break;
				case 12:
					if (!_fighter.IsOnFloor()) return;
					_fighter.Position = new Vector2(-100f, 0f);
					_opponent.Position = new Vector2(-200f, 0f);
					_fighter.SetFacing(1);
					_fighter.SetExternalInput(new FighterInput(1f, 0f, false, false, true, false));
					_stage = 3;
					break;
				case 3:
					Expect(_fighter.ActiveAbility is RunAbility && _fighter.Facing == 1,
						"running cross-under changed facing before the run ended");
					_fighter.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false,
						true, true, false, false, false, false, false, false, false, false, false, false));
					_stage = 4;
					break;
				case 4:
					Expect(_fighter.IsAttacking && _fighter.CurrentAttackName == "LIGHT PUNCH" && _fighter.Facing == 1,
						"run-to-jab handoff changed facing without an explicit neutral frame");
					Expect(!_fighter.CanAdoptFacingFromNeutral(_fighter.CurrentInput),
						"the stage resolver considers an active run-to-jab sequence neutral");
					_fighter.SetExternalInput(default);
					_stage = 5;
					break;
				case 5:
					if (_fighter.IsAttacking) return;
					_stage = 6;
					break;
				case 6:
					Expect(_fighter.Facing == -1,
						$"fighter did not turn after running (active={_fighter.ActiveAbility?.GetType().Name ?? "none"}, " +
						$"floor={_fighter.IsOnFloor()}, slide={_fighter.IsInRunStopSlide}, fighterX={_fighter.GlobalPosition.X:0.##}, opponentX={_opponent.GlobalPosition.X:0.##})");
					GD.Print("NEUTRAL_OPPONENT_FACING_TEST_PASS cross_under=turn run_under_airborne=turn_on_neutral defender_air_crossup=locked walk_turn=turn normal_jump_over=locked super_jump_over=turn run_to_jab=locked post_action=turn");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"NEUTRAL_OPPONENT_FACING_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Verifies that fighter draw priority changes on contact, not attack startup.</summary>
public partial class ContactLayeringRegressionTest : Node2D
{
	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private int _stage;
	private int _settleTicks = 6;
	private int _watchdog = 420;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(1400f, 20f) }
		});
		AddChild(floor);

		_fighterOne = Spawn("FighterOne", -300f, 1);
		_fighterTwo = Spawn("FighterTwo", 300f, -1);
		AddChild(new VersusStageRules
		{
			Name = "VersusStageRules",
			FighterOnePath = new NodePath("../FighterOne"),
			FighterTwoPath = new NodePath("../FighterTwo")
		});
	}

	private FighterController Spawn(string name, float x, int facing)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<FighterController>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
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
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}");

			switch (_stage)
			{
				case 0:
					if (--_settleTicks > 0) return;
					Expect(_fighterOne.ZIndex == 0 && _fighterTwo.ZIndex == 0,
						"fighters did not begin at equal draw priority");
					_fighterOne.SetExternalInput(LightPunchInput());
					_stage = 1;
					break;
				case 1:
					_fighterOne.SetExternalInput(default);
					if (_fighterOne.IsAttacking) _stage = 2;
					break;
				case 2:
					Expect(_fighterOne.ZIndex == 0 && _fighterTwo.ZIndex == 0,
						"a whiffed attack incorrectly changed fighter draw priority");
					if (_fighterOne.IsAttacking) return;
					PlaceForContact();
					_fighterOne.SetExternalInput(LightPunchInput());
					_stage = 3;
					break;
				case 3:
					_fighterOne.SetExternalInput(default);
					if (_fighterOne.ZIndex > _fighterTwo.ZIndex)
					{
						Expect(_fighterTwo.IsInHitstun, "foreground priority changed without a landed hit");
						_stage = 4;
					}
					else if (!_fighterOne.IsAttacking)
						throw new InvalidOperationException("Player 1 jab ended without earning foreground priority on hit");
					break;
				case 4:
					if (_fighterOne.IsAttacking || _fighterTwo.HitstunFramesLeft > 0 ||
						_fighterOne.IsInHitstop || _fighterTwo.IsInHitstop) return;
					PlaceForContact();
					_fighterOne.TrainingAutoBlock = true;
					_fighterTwo.SetExternalInput(LightPunchInput());
					_stage = 5;
					break;
				case 5:
					_fighterTwo.SetExternalInput(default);
					if (_fighterTwo.ZIndex > _fighterOne.ZIndex)
					{
						Expect(_fighterOne.IsInBlockstun,
							"Player 2 received foreground priority without Player 1 blocking the contact");
						GD.Print("CONTACT LAYERING TEST PASSED: whiffs preserve order; landed and blocked attacks transfer foreground priority.");
						GetTree().Quit();
					}
					else if (!_fighterTwo.IsAttacking)
						throw new InvalidOperationException("Player 2 jab ended without earning foreground priority on block");
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"CONTACT LAYERING TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private void PlaceForContact()
	{
		_fighterOne.Position = Vector2.Zero;
		_fighterTwo.Position = new Vector2(65f, 0f);
		_fighterOne.Velocity = Vector2.Zero;
		_fighterTwo.Velocity = Vector2.Zero;
		_fighterOne.SetFacing(1);
		_fighterTwo.SetFacing(-1);
		_fighterOne.SetExternalInput(default);
		_fighterTwo.SetExternalInput(default);
	}

	private static FighterInput LightPunchInput() =>
		new(0f, 0f, false, false, false, false, lightPunchPressed: true);

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

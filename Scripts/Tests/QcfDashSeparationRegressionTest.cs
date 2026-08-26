using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

/// <summary>Proves a quarter circle cannot manufacture the two forward edges required by a dash.</summary>
public partial class QcfDashSeparationRegressionTest : Node2D
{
	private SpriteTestFighter _qcfProbe;
	private SpriteTestFighter _dashProbe;
	private int _stage;
	private int _settleTicks = 6;
	private int _watchdogTicks = 60;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1600f, 20f) } });
		AddChild(floor);
		_qcfProbe = Spawn("QuarterCircleProbe", -220f);
		_dashProbe = Spawn("DoubleTapProbe", 220f);
	}

	private SpriteTestFighter Spawn(string name, float x)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<SpriteTestFighter>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.FaceWithMovement = false;
		fighter.TeamId = name.GetHashCode();
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(1);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdogTicks <= 0)
				throw new InvalidOperationException("test timed out before both direction sequences resolved");

			if (_stage == 0)
			{
				if (--_settleTicks > 0) return;
				Expect(_qcfProbe.IsOnFloor() && _dashProbe.IsOnFloor(), "fighters did not settle on the test floor");
				_qcfProbe.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false)); // D
				_dashProbe.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false)); // F
				_stage = 1;
				return;
			}

			if (_stage == 1)
			{
				_qcfProbe.SetExternalInput(new FighterInput(1f, 1f, false, false, false, false)); // DF
				_dashProbe.SetExternalInput(default); // neutral is required between taps
				_stage = 2;
				return;
			}

			if (_stage == 2)
			{
				_qcfProbe.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false)); // F
				_dashProbe.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false)); // F
				_stage = 3;
				return;
			}

			_qcfProbe.SetExternalInput(default);
			_dashProbe.SetExternalInput(default);
			Expect(_qcfProbe.HasBufferedQuarterCircleForwardCommand,
				"D > DF > F did not preserve the quarter-circle-forward command");
			Expect(!_qcfProbe.HasBufferedDashCommand && _qcfProbe.ActiveAbility is not DashAbility,
				"D > DF > F incorrectly generated a double-tap dash command");
			Expect(_dashProbe.ActiveAbility is DashAbility,
				"F > neutral > F no longer starts the genuine double-tap dash");
			GD.Print("QCF/DASH SEPARATION TEST PASSED: D>DF>F stores QCF without dash; F>N>F still dashes.");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"QCF/DASH SEPARATION TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

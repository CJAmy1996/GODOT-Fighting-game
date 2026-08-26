using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

public partial class KamuiMoveVisualRegressionTest : Node2D
{
	private SpriteTestFighter _backPunch;
	private SpriteTestFighter _sweep;
	private SpriteTestFighter _crouchJab;
	private SpriteTestFighter _airWalk;
	private SpriteTestFighter _airDash;
	private float _airDashStartX;
	private bool _backAssetSeen;
	private bool _sweepAssetSeen;
	private int _ticks;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1600f, 20f) } });
		AddChild(floor);
		_backPunch = Spawn("BackPunch", -250f);
		_sweep = Spawn("Sweep", 250f);
		_crouchJab = Spawn("CrouchJab", 0f);
		_airWalk = Spawn("AirWalk", -500f);
		_airDash = Spawn("AirDash", 500f);
	}

	private SpriteTestFighter Spawn(string name, float x)
	{
		var fighter = GD.Load<PackedScene>("res://Scenes/Characters/Kamui.tscn").Instantiate<SpriteTestFighter>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.TeamId = name.GetHashCode();
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(1);
		AddChild(fighter);
		return fighter;
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			_ticks++;
			MoveVisualEffect[] liveEffects = GetChildren().OfType<MoveVisualEffect>().ToArray();
			_backAssetSeen |= liveEffects.Any(effect => effect.Name.ToString().Contains(FighterController.BackLightPunchName));
			_sweepAssetSeen |= liveEffects.Any(effect => effect.Name.ToString().Contains(FighterController.CrouchingHeavyKickName));
			if (_ticks == 5)
			{
				_backPunch.SetExternalInput(new FighterInput(-1f, 0f, false, false, false, false,
					lightPunchPressed: true));
				_sweep.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false,
					heavyKickPressed: true));
				_crouchJab.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false,
					lightPunchPressed: true));
				_airWalk.SetExternalInput(new FighterInput(0f, -1f, true, true, false, false));
				_airDash.SetExternalInput(new FighterInput(0f, -1f, true, true, false, false));
				return;
			}
			if (_ticks == 6)
			{
				_backPunch.SetExternalInput(default);
				_sweep.SetExternalInput(default);
				_crouchJab.SetExternalInput(default);
				_airWalk.SetExternalInput(default);
				_airDash.SetExternalInput(default);
			}
			if (_ticks == 12)
			{
				Expect(!_airWalk.WasGrounded && !_airDash.WasGrounded, "air movement probes never left the floor");
				Expect(_crouchJab.CurrentAttackName == "LIGHT PUNCH" &&
					_crouchJab.CurrentAttackAnimationName == "crouching_light_punch",
					"down+LP did not resolve to Kamui's authored crouching jab");
				Expect(_crouchJab.CharacterSprite.Animation == "crouching_light_punch",
					"Kamui did not display the authored crouching-jab sprite");
				Expect(_crouchJab.CharacterSprite.SpriteFrames.GetFrameCount("crouching_light_punch") == 11,
					"Kamui crouching jab does not use the authored eleven-step pose sequence");
				_airWalk.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
					special2Pressed: true));
				return;
			}
			if (_ticks == 13)
			{
				_airWalk.SetExternalInput(default);
				Expect(_crouchJab.GetActiveLocalBoxes(FighterBoxKind.Hitbox).Any(),
					"Kamui crouching jab has no active authored hitbox");
				Expect(_airWalk.ActiveAbility is AirWalkAbility, "airborne Trait 2 did not activate Kamui air walk");
			}
			if (_ticks == 20)
			{
				_airDashStartX = _airDash.GlobalPosition.X;
				_airDash.SetExternalInput(new FighterInput(1f, 0f, false, false, true, false));
				return;
			}
			if (_ticks == 21)
			{
				_airDash.SetExternalInput(default);
				Expect(_airDash.ActiveAbility is TeleportDashAbility, "forward air dash did not activate after jump peak");
			}
			if (_ticks == 25)
				Expect(Mathf.Abs(_airDash.GlobalPosition.X - _airDashStartX) < 0.1f,
					"forward air dash moved before its six-frame startup completed");
			if (_ticks < 27) return;
			Expect(_backPunch.CurrentAttackName == FighterController.BackLightPunchName,
				$"back+LP resolved as {_backPunch.CurrentAttackName}");
			Expect(_sweep.CurrentAttackName == FighterController.CrouchingHeavyKickName,
				$"down+HK resolved as {_sweep.CurrentAttackName}");
			Expect(_backAssetSeen,
				"back+LP did not spawn its moving medium-punch asset");
			Expect(_sweepAssetSeen,
				"down+HK did not spawn its sweep sword asset");
			Expect(Mathf.Abs((_airDash.GlobalPosition.X - _airDashStartX) - 240f) < 0.1f,
				"forward air dash did not travel its authored 240-pixel distance");
			Expect(_airWalk.ActiveAbility is AirWalkAbility && Mathf.Abs(_airWalk.Velocity.Y) < 0.1f,
				"Kamui air walk did not remain suspended at its activation height");
			GD.Print("KAMUI_MOVE_VISUAL_TEST_PASS back+LP=asset sweep=sword airwalk=trait2 airdash=6f/240px");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"KAMUI_MOVE_VISUAL_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

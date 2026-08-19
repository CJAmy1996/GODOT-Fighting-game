using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

/// <summary>Locks jump-in momentum and Sanzou's Special-1 SPD flight/slam.</summary>
public partial class SanzouSpdAndAirMomentumRegressionTest : Node2D
{
	private FighterController _attacker;
	private FighterController _victim;
	private FighterController _momentumProbe;
	private int _stage = -1;
	private int _momentumTicks = 4;
	private float _momentumStartX;
	private int _settleTicks = 6;
	private int _watchdog = 240;
	private float _highestY;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1400f, 20f) } });
		AddChild(floor);

		_attacker = Spawn("SpdSanzou", 0f, 1, 1);
		_victim = Spawn("SpdVictim", 65f, -1, 2);
		_attacker.SetOpponent(_victim);
		_victim.SetOpponent(_attacker);
		SetUpAirNormalMomentumProbe();
	}

	private FighterController Spawn(string name, float x, int facing, int team)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<FighterController>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.TeamId = team;
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(facing);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	private void SetUpAirNormalMomentumProbe()
	{
		_momentumProbe = Spawn("AirMomentumProbe", 320f, 1, 3);
		_momentumProbe.Position = new Vector2(320f, -300f);
		_momentumProbe.JumpInInitialFullFreezeFrames = 0;
		_momentumProbe.AirToGroundHitstopMomentumScale = 0.85f;
		_momentumProbe.Velocity = new Vector2(600f, 0f);
		_momentumStartX = _momentumProbe.GlobalPosition.X;
		_momentumProbe.RequestHitstop(3, continueVerticalPhysics: true);
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, attack '{_attacker.CurrentAttackName}', frame {_attacker.CurrentAttackFrame}");
			switch (_stage)
			{
				case -1:
					if (--_momentumTicks > 0) return;
					float traveled = _momentumProbe.GlobalPosition.X - _momentumStartX;
					Expect(traveled >= 24f, $"jump-in traveled only {traveled:0.0}px during contact freeze");
					Expect(Mathf.IsEqualApprox(_momentumProbe.Velocity.X, 600f),
						"jump-in horizontal velocity was erased by grounded-opponent hitstop");
					_momentumProbe.QueueFree();
					_stage = 0;
					break;
				case 0:
					if (--_settleTicks > 0) return;
					Expect(_attacker.IsOnFloor() && _victim.IsOnFloor(), "SPD fighters did not settle on the floor");
					_attacker.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						special1Pressed: true));
					_stage = 1;
					break;
				case 1:
					_attacker.SetExternalInput(default);
					Expect(_attacker.CurrentAttackName == FighterController.SanzoSpdName,
						$"Special-1 resolved as '{_attacker.CurrentAttackName}'");
					Expect(_attacker.CurrentAttackStartupFrames == 5, "SPD startup is not five frames");
					Expect(_attacker.CurrentAttackAnimationName == "spd_grab", "SPD did not select its authored composite animation");
					AnimatedSprite2D sprite = (AnimatedSprite2D)_attacker.GetNode("CharacterSprite");
					SpriteFrames art = sprite.SpriteFrames;
					Expect(art.GetFrameTexture("spd_grab", 0) == art.GetFrameTexture("heavy_punch", 0),
						"SPD startup drawing zero does not reuse standing heavy punch");
					Expect(Mathf.IsEqualApprox(sprite.Scale.X, 0.85f) && Mathf.IsEqualApprox(sprite.Scale.Y, 0.85f),
						"SPD sprite is not scaled to 85 percent");
					float spdFloorLine = sprite.Position.Y + 58f * sprite.Scale.Y;
					Expect(Mathf.Abs(spdFloorLine) < 0.1f, "scaled SPD startup no longer meets its authored floor line");
					_stage = 2;
					break;
				case 2:
					if (!_attacker.IsAttackActive) return;
					Expect(_attacker.TryApplyBasicAttackHit(_victim, out int hitstop, out _, out _, out _, out _),
						"active SPD grab did not capture its grounded victim");
					Expect(hitstop == 0 && _attacker.SpdGrabConnected, "SPD contact did not enter its capture state");
					Expect(_attacker.CurrentAttackAnimationName == "spd_air_grab", "SPD did not switch to airborne hold art");
					Expect(_attacker.Velocity.Y <= -1400f, "SPD did not begin its high ascent");
					_highestY = _attacker.GlobalPosition.Y;
					_stage = 3;
					break;
				case 3:
					_highestY = Mathf.Min(_highestY, _attacker.GlobalPosition.Y);
					if (!_attacker.TryConsumeSpdSlamImpact(out FighterController victim, out _, out int damage)) return;
					Expect(victim == _victim, "SPD slam event referenced the wrong victim");
					Expect(_highestY <= -300f, $"SPD rose only {-_highestY:0.0}px before descending");
					Expect(damage == 260, $"SPD slam damage event reported {damage} instead of 260");
					Expect(_victim.HitState == FighterHitState.GroundedKnockdown,
						$"SPD victim landed in {_victim.HitState} instead of grounded knockdown");
					GD.Print("SANZOU SPD/MOMENTUM TEST PASSED: jump-ins carry 85% travel through hitstop; O gives 5f SPD startup, high carry, and hard slam.");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU SPD/MOMENTUM TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

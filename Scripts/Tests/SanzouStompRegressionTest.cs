using System;
using Godot;
using ModularFighter.Characters;
using ModularFighter.Core;

namespace ModularFighter.Tests;

/// <summary>Runtime coverage for Sanzou's complete charge-stomp arc and bounce result.</summary>
public partial class SanzouStompRegressionTest : Node2D
{
	private SanzoKongoumaruFighter _sanzou;
	private FighterController _defender;
	private int _stage;
	private int _ticks;
	private int _watchdog = 260;
	private float _maximumHeight;
	private bool _sawRiseCape;
	private bool _sawDescentCycle;
	private bool _stompConnected;
	private int _landingTicks;

	public override void _Ready()
	{
		ProcessPhysicsPriority = 200;
		var floor = new StaticBody2D { Name = "Floor", Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1200f, 20f) } });
		AddChild(floor);

		_sanzou = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<SanzoKongoumaruFighter>();
		_sanzou.Name = "Sanzou";
		_sanzou.ReadLocalInput = false;
		_sanzou.TeamId = 1;
		_sanzou.Position = Vector2.Zero;
		_sanzou.SetFacing(1);
		_sanzou.SetExternalInput(default);
		AddChild(_sanzou);
		_sanzou.ResetPlaceholderGauges();

		_defender = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/KungFuMan.tscn")
			.Instantiate<FighterController>();
		_defender.Name = "Defender";
		_defender.ReadLocalInput = false;
		_defender.TeamId = 2;
		_defender.Position = Vector2.Zero;
		_defender.SetFacing(-1);
		_defender.SetExternalInput(default);
		AddChild(_defender);
		_defender.ResetPlaceholderGauges();
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, move '{_sanzou.CurrentAttackName}', frame {_sanzou.CurrentAttackFrame}");
			switch (_stage)
			{
				case 0:
					if (++_ticks < 6) return;
					Expect(_sanzou.IsOnFloor() && _defender.IsOnFloor(), "fighters did not settle on the floor");
					Expect(Mathf.IsEqualApprox(_sanzou.PlaceholderLife, 1200f), "Sanzou did not start at full 1200 life");
					_ticks = 0;
					_sanzou.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false));
					_stage = 1;
					break;
				case 1:
					if (++_ticks < 45) return;
					_sanzou.SetExternalInput(new FighterInput(0f, -1f, jumpPressed: true, jumpHeld: true,
						dashPressed: false, flightHeld: false, lightKickPressed: true));
					_stage = 2;
					break;
				case 2:
					_sanzou.SetExternalInput(default);
					Expect(_sanzou.CurrentAttackName == FighterController.StompSpecialName,
						$"charge down-up+LK resolved as '{_sanzou.CurrentAttackName}'");
					_stage = 3;
					break;
				case 3:
					_maximumHeight = Mathf.Max(_maximumHeight, _sanzou.AirHeightAboveGround);
					if (!_sanzou.IsOnFloor() && _sanzou.CurrentAttackFrame >= _sanzou.CurrentAttackStartupFrames &&
						_sanzou.CurrentAttackFrame < _sanzou.CurrentAttackForceDownwardStartFrame)
					{
						Expect(_sanzou.CharacterSprite.Frame is 2 or 3,
							$"rise displayed drawing {_sanzou.CharacterSprite.Frame}, expected cape flap 2/3");
						_sawRiseCape = true;
					}
					if (!_sanzou.IsOnFloor() && _sanzou.CurrentAttackFrame >= _sanzou.CurrentAttackForceDownwardStartFrame)
					{
						Expect(_sanzou.CharacterSprite.Frame is 7 or 8 or 9,
							$"descent displayed drawing {_sanzou.CharacterSprite.Frame}, expected stomp loop 7/8/9");
						_sawDescentCycle = true;
						if (!_stompConnected && _sanzou.TryApplyBasicAttackHit(_defender, out _, out _, out float pushback,
							out _, out _))
						{
							_stompConnected = true;
							Expect(_defender.HitState == FighterHitState.Juggle,
								$"stomp bounce entered {_defender.HitState}, not Juggle");
							Expect(_defender.Velocity.Y < 0f && Mathf.Abs(_defender.Velocity.Y) <= 340.1f,
								$"stomp bounce velocity {_defender.Velocity.Y:0.0} is not the small authored bounce");
							Expect(Mathf.Abs(pushback) <= 35.1f, $"stomp pushed {pushback:0.0}, too far for a close juggle");
						}
					}
					if (!_sanzou.JustLanded) return;
					Expect(_maximumHeight >= 225f, $"stomp rose only {_maximumHeight:0.0}px");
					Expect(_sawRiseCape && _sawDescentCycle, "stomp skipped a cape-rise or stomp-descent phase");
					Expect(_stompConnected, "descending stomp never connected with the grounded defender");
					Expect(_sanzou.IsCurrentSpecialLandingRecovery, "landing did not begin dedicated recovery");
					Expect(_sanzou.CharacterSprite.Frame == 10,
						$"first landing drawing was {_sanzou.CharacterSprite.Frame}, expected 10");
					_landingTicks = 0;
					_stage = 4;
					break;
				case 4:
					_landingTicks++;
					if (_landingTicks < 7) return;
					Expect(_sanzou.CharacterSprite.Frame == 11,
						$"second landing drawing was {_sanzou.CharacterSprite.Frame}, expected 11");
					GD.Print($"SANZOU STOMP TEST PASSED: rose {_maximumHeight:0.0}px with cape loop, stomped until landing, and produced a close 340-speed juggle bounce.");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU STOMP TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

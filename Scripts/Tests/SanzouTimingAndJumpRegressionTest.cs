using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

/// <summary>Locks Sanzou's grounded jump-in timing, held-Up repeat, and super-jump height.</summary>
public partial class SanzouTimingAndJumpRegressionTest : Node2D
{
	private FighterController _airLight;
	private FighterController _victim;
	private FighterController _airHeavy;
	private FighterController _heavyVictim;
	private FighterController _jumpRepeater;
	private int _stage;
	private int _settleFrames = 8;
	private int _watchdog = 360;
	private bool _firstJumpLeftGround;
	private bool _firstJumpLanded;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1600f, 20f) } });
		AddChild(floor);

		_airLight = Spawn("AirLight", 0f, 1, 1);
		_victim = Spawn("GroundedVictim", 60f, -1, 2);
		_airHeavy = Spawn("AirHeavy", -360f, 1, 4);
		_heavyVictim = Spawn("HeavyGroundedVictim", -300f, -1, 5);
		_jumpRepeater = Spawn("HeldUpRepeater", 420f, 1, 3);
		_airLight.SetOpponent(_victim);
		_victim.SetOpponent(_airLight);
		_airHeavy.SetOpponent(_heavyVictim);
		_heavyVictim.SetOpponent(_airHeavy);
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

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}");

			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					Expect(_victim.IsOnFloor() && _heavyVictim.IsOnFloor() && _jumpRepeater.IsOnFloor(),
						"fighters did not settle on the test floor");
					_airLight.Position = new Vector2(0f, -60f);
					_airLight.Velocity = Vector2.Zero;
					_stage = 1;
					return;
				case 1:
					if (_airLight.WasGrounded) return;
					_airLight.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						lightPunchPressed: true));
					_stage = 2;
					return;
				case 2:
					_airLight.SetExternalInput(default);
					if (!_airLight.IsAttackActive) return;
					Expect(_airLight.TryApplyBasicAttackHit(_victim, out int hitlag, out _, out float airLightPushback, out _, out _),
						"air light did not contact the grounded defender");
					Expect(hitlag == 8, $"jump-in light produced {hitlag} hitlag frames instead of the reduced 8");
					Expect(Mathf.IsEqualApprox(airLightPushback, 50f),
						$"jump-in light produced {airLightPushback:0.0} pushback instead of the fixed 50");
					int expectedAirJabHitstun = _airLight.LightAttackHitstunFrames;
					Expect(_victim.HitstunFramesLeft == expectedAirJabHitstun,
						$"grounded defender received {_victim.HitstunFramesLeft} air-jab hitstun frames instead of {expectedAirJabHitstun}");
					_airHeavy.Position = new Vector2(-360f, -60f);
					_airHeavy.Velocity = Vector2.Zero;
					_airHeavy.SetExternalInput(default);
					_stage = 3;
					return;
				case 3:
					if (_airHeavy.WasGrounded) return;
					_airHeavy.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						heavyKickPressed: true));
					_stage = 4;
					return;
				case 4:
					_airHeavy.SetExternalInput(default);
					if (!_airHeavy.IsAttackActive) return;
					Expect(_airHeavy.TryApplyBasicAttackHit(_heavyVictim, out int heavyHitlag, out _, out _, out _, out _),
						"jumping heavy did not contact the grounded defender");
					Expect(_heavyVictim.HitstunFramesLeft == 12,
						$"jumping heavy caused {_heavyVictim.HitstunFramesLeft} grounded hitstun frames instead of 12 " +
						$"(move '{_airHeavy.CurrentAttackName}', airborne start {_airHeavy.CurrentAttackStartedAirborne}, target grounded {_heavyVictim.WasGrounded})");
					Expect(heavyHitlag == 8, $"jumping heavy caused {heavyHitlag} attacker hitlag frames instead of 8");
					Expect(_airHeavy.LastContactDefenderHitstopFrames == 6,
						$"jumping heavy caused {_airHeavy.LastContactDefenderHitstopFrames} defender hitstop frames instead of 6");
					VersusStageRules.ApplyHitstopForHit(_airHeavy, _heavyVictim, heavyHitlag);
					Expect(_airHeavy.HitstopFramesLeft == 8 && _heavyVictim.HitstopFramesLeft == 6,
						$"contact freeze resolved as attacker {_airHeavy.HitstopFramesLeft}/defender {_heavyVictim.HitstopFramesLeft} instead of 8/6");
					_jumpRepeater.SetExternalInput(new FighterInput(0f, -1f, jumpPressed: true, jumpHeld: true,
						dashPressed: false, flightHeld: false));
					_stage = 5;
					return;
				case 5:
					_jumpRepeater.SetExternalInput(new FighterInput(0f, -1f, jumpPressed: false, jumpHeld: true,
						dashPressed: false, flightHeld: false));
					if (!_firstJumpLeftGround && !_jumpRepeater.IsOnFloor())
					{
						_firstJumpLeftGround = true;
						Expect(!_jumpRepeater.IsInSuperJumpRoute, "ordinary held-Up jump incorrectly became a super jump");
					}
					if (_firstJumpLeftGround && !_firstJumpLanded && _jumpRepeater.JustLanded)
						_firstJumpLanded = true;
					else if (_firstJumpLanded && !_jumpRepeater.IsOnFloor())
					{
						Expect(_jumpRepeater.Velocity.Y < 0f, "held Up did not launch the repeated normal jump");
						ValidateSuperJumpHeight();
						GD.Print("SANZOU TIMING/JUMP TEST PASSED: jump-in heavy uses 12f grounded hitstun, 8f attacker hitlag, and 6f defender hitstop; held jumps and super-jump height hold.");
						GetTree().Quit();
					}
					return;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU TIMING/JUMP TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void ValidateSuperJumpHeight()
	{
		var definition = ResourceLoader.Load<FighterDefinition>("res://Data/Characters/Sanzo/sanzo_kongoumaru.tres");
		var rawSuperJump = ResourceLoader.Load<SuperJumpAbility>("res://Data/Characters/Sanzo/sanzo_super_jump.tres");
		var mechaSuperJump = ResourceLoader.Load<SuperJumpAbility>(
			"res://Data/Characters/BigBangBeatRevolve/MechaHeita/m_heita_super_jump.tres");
		NormalMoveData downHeavy = definition.NormalMoves.FindRule(FighterController.CrouchingHeavyPunchName, true, false);
		Expect(downHeavy != null && Mathf.IsEqualApprox(rawSuperJump.InitialSpeed, downHeavy.ChaseJumpSpeed),
			"raw super-jump speed does not equal the down+HP chase jump speed");
		Expect(Mathf.IsEqualApprox(rawSuperJump.ForwardSpeed, 385f),
			$"Sanzou super-jump forward speed leaked to {rawSuperJump.ForwardSpeed:0.##}");
		Expect(mechaSuperJump != null && !ReferenceEquals(rawSuperJump, mechaSuperJump) &&
			Mathf.IsEqualApprox(mechaSuperJump.InitialSpeed, 1440f) &&
			Mathf.IsEqualApprox(rawSuperJump.InitialSpeed, 1265f),
			"Sanzou and Mecha Heita no longer have independent super-jump tuning");
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

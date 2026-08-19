using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;
using ModularFighter.Movement;

namespace ModularFighter.Tests;

/// <summary>Locks Mecha Heita's boost count, flight inertia, and hit/whiff cancel distinctions.</summary>
public partial class MechaFlightRegressionTest : Node2D
{
	private SpriteTestFighter _whiffProbe;
	private SpriteTestFighter _hitProbe;
	private SpriteTestFighter _dummy;
	private SpriteTestFighter _fallProbe;
	private SpriteTestFighter _superFallProbe;
	private FlightAbility _whiffFlight;
	private FlightAbility _hitFlight;
	private FlightAbility _fallFlight;
	private FlightAbility _superFallFlight;
	private int _stage;
	private int _fallStage;
	private int _superFallStage;
	private int _settleFrames = 8;
	private int _neutralLockFrames;
	private int _watchdog = 300;
	private bool _mainDone;
	private bool _fallDone;
	private bool _superFallDone;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1800f, 20f) } });
		AddChild(floor);
		ValidateStandaloneBoostAndFlight();
		_whiffProbe = Spawn("WhiffFlightProbe", -320f, 1, 1);
		_hitProbe = Spawn("HitBoostProbe", 120f, 1, 2);
		_dummy = Spawn("HitDummy", 170f, -1, 3);
		_hitProbe.SetOpponent(_dummy);
		_dummy.SetOpponent(_hitProbe);
		_fallProbe = Spawn("PostFlightFallProbe", -650f, 1, 4);
		_fallProbe.Position = new Vector2(-650f, -350f);
		_superFallProbe = Spawn("PostSuperFlightFallProbe", -800f, 1, 5);
		_superFallProbe.Position = new Vector2(-800f, -350f);
		_superFallProbe.RefreshAirJumpResourcesForSuperJump();
		_whiffFlight = ResolveFlight(_whiffProbe);
		_hitFlight = ResolveFlight(_hitProbe);
		_fallFlight = ResolveFlight(_fallProbe);
		_superFallFlight = ResolveFlight(_superFallProbe);
		_fallProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		_superFallProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
	}

	private void ValidateStandaloneBoostAndFlight()
	{
		var probe = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
			.Instantiate<SpriteTestFighter>();
		probe.ReadLocalInput = false;
		probe.ResetPlaceholderGauges();
		FlightAbility flight = ResolveFlight(probe);
		AbilityRuntime runtime = probe.GetRuntime(flight);

		for (int use = 0; use < 3; use++)
		{
			probe.Velocity = new Vector2(-300f, 500f);
			probe.SetExternalInput(FlightInput(1f, 0f, pressed: true));
			Expect(flight.CanStart(probe, runtime), $"air boost {use + 1} was rejected");
			flight.Start(probe, runtime);
			Expect(flight.IsBoosting(probe), "direction + flight button did not select boost mode");
			Expect(flight.AirBoostsUsed(probe) == use + 1, "air boost counter did not increment once");
			Expect(probe.Velocity == new Vector2(900f, 0f), "boost inherited old movement momentum");
			if (use == 0)
			{
				Expect(!flight.CanStartAttack(probe, runtime), "boost allowed an attack on frame zero");
				for (int frame = 0; frame < 2; frame++)
				{
					flight.Tick(probe, runtime, 1f / 60f);
					Expect(!flight.CanStartAttack(probe, runtime), $"boost allowed an attack before frame 3 ({frame + 1})");
				}
				flight.Tick(probe, runtime, 1f / 60f);
				Expect(flight.CanStartAttack(probe, runtime), "boost did not allow attacks on frame 3");
			}
			flight.Stop(probe, runtime);
		}

		probe.SetExternalInput(FlightInput(1f, 0f, pressed: true));
		Expect(!flight.CanStart(probe, runtime), "a fourth air boost was allowed before landing");
		probe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(flight.CanStart(probe, runtime), "neutral flight was incorrectly tied to the boost-use counter");
		probe.Velocity = new Vector2(700f, -900f);
		flight.Start(probe, runtime);
		Expect(probe.Velocity == Vector2.Zero, "flight entry did not erase prior momentum");
		probe.SetExternalInput(FlightInput(1f, -1f));
		flight.Tick(probe, runtime, 1f / 60f);
		Vector2 expected = new Vector2(1f, -1f).Normalized() * flight.FlightSpeed;
		Expect(probe.Velocity.IsEqualApprox(expected), "flight did not switch directly to its new 8-way velocity");
		flight.Stop(probe, runtime);
		probe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(flight.CanStart(probe, runtime), "neutral flight entry was rejected before backward-speed test");
		flight.Start(probe, runtime);
		probe.SetExternalInput(FlightInput(-1f, 0f));
		flight.Tick(probe, runtime, 1f / 60f);
		Expect(Mathf.IsEqualApprox(probe.Velocity.X, -126f) && Mathf.IsZeroApprox(probe.Velocity.Y),
			"sustained backward flight did not use the 30% speed limit");
		flight.Stop(probe, runtime);
		probe.Free();

		var backwardProbe = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
			.Instantiate<SpriteTestFighter>();
		backwardProbe.ReadLocalInput = false;
		backwardProbe.SetFacing(1);
		backwardProbe.ResetPlaceholderGauges();
		FlightAbility backwardFlight = ResolveFlight(backwardProbe);
		AbilityRuntime backwardRuntime = backwardProbe.GetRuntime(backwardFlight);
		Expect(backwardProbe.ResolveLandingLagFramesForCurrentAirTime(11) == 2,
			"Mecha non-flight landing lag was not capped at two frames");
		backwardProbe.SetExternalInput(FlightInput(-1f, 0f, pressed: true));
		Expect(backwardFlight.CanStart(backwardProbe, backwardRuntime), "air backward boost was rejected");
		backwardFlight.Start(backwardProbe, backwardRuntime);
		Expect(backwardFlight.AirBoostsUsed(backwardProbe) == 2,
			"air backward boost did not consume two boost uses");
		Expect(Mathf.IsEqualApprox(backwardProbe.Velocity.X, -450f),
			"air backward boost did not use half boost speed");
		Expect(backwardFlight.IsBackwardBoostCommittedThisAirTime(backwardProbe),
			"air backward boost did not commit the remaining airtime");
		Expect(backwardProbe.ResolveLandingLagFramesForCurrentAirTime(11) == 11,
			"flight/boost airtime did not preserve full landing lag");
		backwardFlight.Stop(backwardProbe, backwardRuntime);

		backwardProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(!backwardFlight.CanStart(backwardProbe, backwardRuntime),
			"neutral flight was allowed after an air backward boost");
		foreach (Vector2 forbiddenDirection in new[]
		{
			new Vector2(-1f, -1f), Vector2.Up, new Vector2(1f, -1f), Vector2.Left,
			new Vector2(-1f, 1f), Vector2.Down, new Vector2(1f, 1f),
		})
		{
			backwardProbe.SetExternalInput(FlightInput(forbiddenDirection.X, forbiddenDirection.Y, pressed: true));
			Expect(!backwardFlight.CanStart(backwardProbe, backwardRuntime),
				$"forbidden {forbiddenDirection} boost was allowed after an air backward boost");
		}
		backwardProbe.SetExternalInput(FlightInput(1f, 0f, pressed: true));
		Expect(backwardFlight.CanStart(backwardProbe, backwardRuntime),
			"forward boost was rejected after an air backward boost");
		backwardFlight.Start(backwardProbe, backwardRuntime);
		Expect(backwardFlight.AirBoostsUsed(backwardProbe) == 3 &&
			Mathf.IsEqualApprox(backwardProbe.Velocity.X, 900f),
			"post-backward forward boost did not consume the final use at full speed");
		backwardFlight.Stop(backwardProbe, backwardRuntime);
		backwardProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(!backwardFlight.CanStart(backwardProbe, backwardRuntime),
			"flight was restored before landing after a backward boost");
		backwardProbe.Free();

		var modeProbe = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
			.Instantiate<SpriteTestFighter>();
		modeProbe.ReadLocalInput = false;
		modeProbe.ResetPlaceholderGauges();
		FlightAbility modeFlight = ResolveFlight(modeProbe);
		AbilityRuntime modeRuntime = modeProbe.GetRuntime(modeFlight);

		modeProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(modeFlight.CanStart(modeProbe, modeRuntime), "button flight press was rejected");
		modeFlight.Start(modeProbe, modeRuntime);
		Expect(modeFlight.IsButtonActivatedFlight(modeProbe), "flight press did not activate toggle mode immediately");
		Expect(modeFlight.ShouldPersistThroughNormal(modeProbe, "LIGHT PUNCH") &&
			modeFlight.ShouldTickDuringAttack(modeProbe),
			"button flight does not persist as the fixed-position normal-attack platform");
		modeProbe.SetExternalInput(FlightInput(0f, 0f, held: false, released: true));
		Expect(modeFlight.Tick(modeProbe, modeRuntime, 1f / 60f) && modeFlight.IsButtonActivatedFlight(modeProbe),
			"quick release did not remain in button-toggle flight");
		modeProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(!modeFlight.Tick(modeProbe, modeRuntime, 1f / 60f), "second press did not toggle button flight off");
		modeFlight.Stop(modeProbe, modeRuntime);

		modeProbe.SetExternalInput(FlightInput(0f, 0f, pressed: true));
		Expect(modeFlight.CanStart(modeProbe, modeRuntime), "held flight press was rejected");
		modeFlight.Start(modeProbe, modeRuntime);
		modeProbe.SetExternalInput(FlightInput(0f, 0f));
		for (int frame = 1; frame < 20; frame++)
		{
			Expect(modeFlight.Tick(modeProbe, modeRuntime, 1f / 60f), $"held flight ended on frame {frame}");
			Expect(modeFlight.IsButtonActivatedFlight(modeProbe), $"negative edge armed before frame 20 ({frame})");
		}
		Expect(modeFlight.Tick(modeProbe, modeRuntime, 1f / 60f) && modeFlight.IsNegativeEdgeFlight(modeProbe),
			"20-frame hold did not arm negative-edge flight");
		Expect(!modeFlight.ShouldPersistThroughNormal(modeProbe, "LIGHT PUNCH") &&
			!modeFlight.ShouldTickDuringAttack(modeProbe),
			"negative-edge flight incorrectly persists through a normal attack");
		modeProbe.SetExternalInput(FlightInput(0f, 0f, held: false, released: true));
		Expect(!modeFlight.Tick(modeProbe, modeRuntime, 1f / 60f),
			"negative-edge flight did not end on release");
		modeFlight.Stop(modeProbe, modeRuntime);
		modeProbe.Free();
	}

	private SpriteTestFighter Spawn(string name, float x, int facing, int team)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
			.Instantiate<SpriteTestFighter>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.FaceWithMovement = false;
		fighter.TeamId = team;
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(facing);
		fighter.SetExternalInput(default);
		fighter.ResetPlaceholderGauges();
		AddChild(fighter);
		return fighter;
	}

	private static FlightAbility ResolveFlight(FighterController fighter) => fighter.Definition.Abilities
		.OfType<FlightAbility>().First(ability => ability.Id == "mecha_heita_booster");

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0) throw new InvalidOperationException($"timeout at stage {_stage}");
			TickPostFlightFallRule();
			TickPostSuperFlightFallRule();
			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					Expect(_whiffProbe.IsOnFloor() && _hitProbe.IsOnFloor() && _dummy.IsOnFloor(),
						"flight regression fighters did not settle");
					_whiffProbe.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						lightPunchPressed: true));
					_stage = 1;
					break;
				case 1:
					_whiffProbe.SetExternalInput(default);
					if (!_whiffProbe.IsAttackRecovering) return;
					_whiffProbe.SetExternalInput(FlightInput(1f, 0f, pressed: true));
					_stage = 2;
					break;
				case 2:
					_whiffProbe.SetExternalInput(FlightInput(1f, 0f));
					if (_whiffProbe.ActiveAbility != _whiffFlight) return;
					Expect(_whiffFlight.IsCancelledFlight(_whiffProbe),
						"whiff recovery + direction incorrectly became a boost");
					Expect(!_whiffFlight.IsBoosting(_whiffProbe), "whiff recovery bypassed the hit-confirm rule");
					Expect(Mathf.IsEqualApprox(_whiffProbe.Velocity.X, _whiffFlight.FlightSpeed),
						"cancelled flight did not accept immediate directional movement");
					_stage = 5;
					break;
				case 3:
					_whiffProbe.SetExternalInput(FlightInput(1f, 0f));
					Expect(Mathf.IsZeroApprox(_whiffProbe.Velocity.X), "cancelled flight escaped its neutral lock");
					if (--_neutralLockFrames > 0) return;
					_whiffProbe.SetExternalInput(FlightInput(0f, 0f));
					_stage = 4;
					break;
				case 4:
					Expect(Mathf.IsZeroApprox(_whiffProbe.Velocity.X), "neutral release introduced momentum");
					_whiffProbe.SetExternalInput(FlightInput(1f, 0f));
					_stage = 5;
					break;
				case 5:
					_whiffProbe.SetExternalInput(FlightInput(1f, 0f));
					Expect(Mathf.IsEqualApprox(_whiffProbe.Velocity.X, _whiffFlight.FlightSpeed),
						"cancelled flight did not move after neutral then direction");
					AbilityRuntime whiffRuntime = _whiffProbe.GetRuntime(_whiffFlight);
					if (_whiffFlight.ElapsedFrames(_whiffProbe) < 15)
					{
						Expect(!_whiffFlight.CanStartAttack(_whiffProbe, whiffRuntime),
							"flight cancel allowed an attack before 15 frames");
						return;
					}
					Expect(_whiffFlight.CanStartAttack(_whiffProbe, whiffRuntime),
						"flight cancel stayed attack-locked after 15 frames, neutral, and movement");
					_whiffProbe.SetExternalInput(default);
					_hitProbe.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						lightPunchPressed: true));
					_stage = 6;
					break;
				case 6:
					_hitProbe.SetExternalInput(default);
					if (!_hitProbe.GetActiveWorldBoxes(FighterBoxKind.Hitbox).Any()) return;
					_dummy.GlobalPosition = _hitProbe.GlobalPosition + new Vector2(50f, 0f);
					Expect(_hitProbe.TryApplyBasicAttackHit(_dummy, out _, out _, out _, out _, out _),
						"test normal did not register its unblocked hit");
					Expect(_hitProbe.CurrentAttackHasUnblockedHit, "unblocked contact was not tracked separately");
					_hitProbe.SetExternalInput(FlightInput(1f, 0f, pressed: true));
					_stage = 7;
					break;
				case 7:
					_hitProbe.SetExternalInput(FlightInput(1f, 0f));
					if (_hitProbe.ActiveAbility != _hitFlight || !_hitFlight.IsBoosting(_hitProbe)) return;
					Expect(_hitProbe.PlaceholderSpecialMeter <= 84.001f,
						"hit-confirm boost did not charge base gas plus its cancel surcharge");
					_mainDone = true;
					_stage = 8;
					TryFinish();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"MECHA FLIGHT REGRESSION FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private void TickPostSuperFlightFallRule()
	{
		if (_superFallDone) return;
		switch (_superFallStage)
		{
			case 0:
				if (_superFallProbe.ActiveAbility != _superFallFlight) return;
				Expect(_superFallProbe.IsInSuperJumpRoute,
					"super-jump flight probe lost its super-jump route");
				Expect(!_superFallProbe.IsPostFlightFallNormalLocked,
					"super-jump normal lock engaged while flight was still active");
				_superFallProbe.SetExternalInput(default);
				_superFallStage = 1;
				break;
			case 1:
				if (_superFallProbe.ActiveAbility != null) return;
				Expect(!_superFallProbe.WasGrounded && _superFallProbe.IsPostFlightFallNormalLocked,
					"super-jump flight exit did not enter the normal-locked fall state");
				_superFallProbe.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
					lightPunchPressed: true));
				_superFallStage = 2;
				break;
			case 2:
				Expect(!_superFallProbe.IsAttacking,
					"an air normal started during post-flight super-jump fall");
				_superFallProbe.SetExternalInput(default);
				_superFallStage = 3;
				break;
			case 3:
				if (!_superFallProbe.IsOnFloor() || _superFallProbe.IsPostFlightFallNormalLocked ||
					_superFallProbe.IsInFlightLanding) return;
				_superFallDone = true;
				TryFinish();
				break;
		}
	}

	private void TickPostFlightFallRule()
	{
		if (_fallDone) return;
		switch (_fallStage)
		{
			case 0:
				if (_fallProbe.ActiveAbility != _fallFlight) return;
				Expect(!_fallProbe.IsPostFlightFallNormalLocked,
					"normal lock engaged while flight was still active");
				_fallProbe.SetExternalInput(default);
				_fallStage = 1;
				break;
			case 1:
				if (_fallProbe.ActiveAbility != null) return;
				Expect(!_fallProbe.WasGrounded && _fallProbe.IsPostFlightFallNormalLocked,
					"airborne flight exit did not enter the normal-locked fall state");
				_fallProbe.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
					lightPunchPressed: true));
				_fallStage = 2;
				break;
			case 2:
				Expect(!_fallProbe.IsAttacking, "an air normal started during post-flight fall");
				_fallProbe.SetExternalInput(default);
				_fallStage = 3;
				break;
			case 3:
				if (!_fallProbe.IsOnFloor() || _fallProbe.IsPostFlightFallNormalLocked || _fallProbe.IsInFlightLanding) return;
				_fallProbe.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
					lightPunchPressed: true));
				_fallStage = 4;
				break;
			case 4:
				_fallProbe.SetExternalInput(default);
				if (!_fallProbe.IsAttacking) return;
				_fallDone = true;
				TryFinish();
				break;
		}
	}

	private void TryFinish()
	{
		if (!_mainDone || !_fallDone || !_superFallDone) return;
		GD.Print("MECHA_FLIGHT_REGRESSION_PASS press=immediate_toggle hold=20f_negative_edge release=off boosts=3 boost_attack=3f flight_cancel=15f neutral_gate=false hit_boost_cost=16 backward_flight_speed=126 backward_boost_speed=450 backward_boost_cost=2 post_backward_only_forward=true non_flight_landing=2f flight_landing=full normal_jump_fall_normals_locked=true super_jump_fall_normals_locked=true");
		GetTree().Quit();
	}

	private static FighterInput FlightInput(float horizontal, float vertical, bool pressed = false,
		bool held = true, bool released = false) =>
		new(horizontal, vertical, false, false, false, false,
			special1Pressed: pressed, special1Held: held, special1Released: released);

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

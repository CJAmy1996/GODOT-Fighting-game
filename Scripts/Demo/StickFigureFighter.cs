using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Demo;

/// <summary>Temporary visual/test character. Replace this with animation and hitboxes later.</summary>
public partial class StickFigureFighter : FighterController
{
	[Export] public Color BodyColor { get; set; } = new("61d8ff");

	public override void _Ready()
	{
		FaceWithMovement = false;
		Definition ??= CreateBaselineDefinition();
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 visualOffset = VisualCorrectionOffset;
		const float headRadius = 15f;
		Color ink = BodyColor;
		bool airborne = !IsOnFloor();
		bool crouching = !airborne && (CurrentInput.Vertical > 0.5f || IsCrouchAttackLocked);
		bool jumpSquat = IsJumpSquatting();
		bool dashing = ActiveAbility is DashAbility && Mathf.Abs(Velocity.X) > Definition.Tuning.WalkSpeed;
		bool running = ActiveAbility is RunAbility;
		bool walking = !running && !airborne && !crouching && !jumpSquat && !IsAttacking && !IsInHitstun && Mathf.Abs(Velocity.X) > 45f;
		float facing = Facing;
		float walkPhase = walking ? Mathf.Sin((float)Engine.GetPhysicsFrames() * 0.55f) : 0f;
		float runPhase = running ? Mathf.Sin((float)Engine.GetPhysicsFrames() * 0.9f) : 0f;
		bool heavyAttack = CurrentAttackName.StartsWith("HEAVY");
		bool specialAttack = CurrentAttackName.StartsWith("SPECIAL");
		bool electricAttack = CurrentAttackName == "ELECTRIC WIND GOD FIST";
		bool kickAttack = CurrentAttackName.Contains("KICK");
		bool airKickAttack = airborne && IsAttacking && kickAttack;
		float hitstunIntensity = IsInHitstun ? Mathf.Clamp(HitstunFramesLeft / 40f, 0.25f, 1f) : 0f;
		float lean = dashing || running
			? Mathf.Clamp(Velocity.X / 1500f, -0.42f, 0.42f)
			: Mathf.Clamp(Velocity.X / 850f, -0.22f, 0.22f);
		if (heavyAttack || electricAttack) lean += facing * 0.14f;
		if (IsInHitstun) lean += Mathf.Clamp(Velocity.X / 420f, -1f, 1f) * 0.42f * hitstunIntensity;
		float squash = jumpSquat ? 9f : crouching ? 18f : 0f;
		float headY = -42f + squash;
		Vector2 shoulder = new(lean * 24f, -22f + squash);
		Vector2 hip = new(lean * 48f, 12f + squash * 0.65f);
		if (IsGroundedKnockdown)
		{
			float slide = Mathf.Clamp(Velocity.X / 420f, -1f, 1f);
			headY = 38f;
			shoulder = new Vector2(-26 + slide * 12f, 41);
			hip = new Vector2(24 + slide * 18f, 44);
		}
		string stateName = GetAnimationStateName(airborne, crouching, jumpSquat, walking, dashing, running);

		DrawEllipse(visualOffset + new Vector2(0, 51), new Vector2(27, 5), new Color(0, 0, 0, 0.22f));
		DrawString(ThemeDB.FallbackFont, visualOffset + new Vector2(-34, -86), stateName, HorizontalAlignment.Left, -1, 12, new Color(1f, 1f, 1f, 0.72f));
		if (ComboCount >= 2 && (IsInHitstun || ComboDisplayFramesLeft > 0))
			DrawString(ThemeDB.FallbackFont, visualOffset + new Vector2(-50, -108), $"{ComboCount} HIT COMBO", HorizontalAlignment.Left, -1, 16, new Color(1f, 0.88f, 0.16f, 0.95f));
		DrawCircle(visualOffset + new Vector2(0, headY), headRadius, ink);
		DrawArc(visualOffset + new Vector2(0, headY), headRadius, 0, Mathf.Tau, 20, Colors.White, 2f);

		DrawLine(visualOffset + shoulder, visualOffset + hip, ink, 6f, true);

		Vector2 leftHand;
		Vector2 rightHand;
		Vector2 leftFoot;
		Vector2 rightFoot;

		if (IsGroundedKnockdown)
		{
			float slide = Mathf.Clamp(Velocity.X / 420f, -1f, 1f);
			leftHand = new Vector2(-46 + slide * 20f, 42);
			rightHand = new Vector2(-14 + slide * 20f, 44);
			leftFoot = new Vector2(28 + slide * 28f, 45);
			rightFoot = new Vector2(58 + slide * 28f, 47);
			shoulder = new Vector2(-26 + slide * 12f, 41);
			hip = new Vector2(24 + slide * 18f, 44);
			headY = 38f;
		}
		else if (IsInHitstun)
		{
			float recoil = Mathf.Clamp(Velocity.X / 420f, -1f, 1f);
			leftHand = new Vector2(-42 + recoil * 34f * hitstunIntensity, -18);
			rightHand = new Vector2(34 + recoil * 42f * hitstunIntensity, 24);
			leftFoot = new Vector2(-28 + recoil * 28f * hitstunIntensity, 47);
			rightFoot = new Vector2(31 + recoil * 28f * hitstunIntensity, 47);
		}
		else if (IsAttacking)
		{
			if (airKickAttack)
			{
				float kickReach = heavyAttack ? 82f : 66f;
				leftHand = new Vector2(-24 + lean * 24f, -4);
				rightHand = new Vector2(22 + lean * 24f, -14);
				leftFoot = new Vector2(-18 + lean * 32f, 38);
				rightFoot = new Vector2(facing * kickReach + lean * 36f, heavyAttack ? 8 : 16);
			}
			else if (IsCrouchAttackLocked)
			{
				if (CurrentAttackName == "HEAVY KICK")
				{
					leftHand = new Vector2(-30 + lean * 20f, 22);
					rightHand = new Vector2(18 + lean * 22f, 20);
					leftFoot = new Vector2(-32 + lean * 24f, 48);
					rightFoot = new Vector2(facing * 92f + lean * 30f, 40);
				}
				else
				{
					float reach = heavyAttack ? 72f : specialAttack ? 66f : 54f;
					leftHand = new Vector2(-24 + lean * 24f, 13);
					rightHand = new Vector2(facing * reach + lean * 24f, heavyAttack ? -3 : 6);
					leftFoot = new Vector2(-34 + lean * 28f, 48);
					rightFoot = new Vector2(26 + lean * 28f, 48);
				}
			}
			else
			{
				if (electricAttack)
				{
					leftHand = new Vector2(-28 + lean * 26f, 2);
					rightHand = new Vector2(facing * 82f + lean * 28f, -22);
					leftFoot = new Vector2(-32 + lean * 35f, 47);
					rightFoot = new Vector2(34 + lean * 42f, 46);
				}
				else
				{
					float reach = heavyAttack ? 78f : specialAttack ? 66f : 56f;
					leftHand = new Vector2(-20 + lean * 25f, 4);
					rightHand = new Vector2(facing * reach + lean * 25f, heavyAttack ? -14 : -10);
					leftFoot = new Vector2(-22 + lean * 35f, 47);
					rightFoot = new Vector2(24 + lean * 35f, 47);
				}
			}
		}
		else if (jumpSquat)
		{
			leftHand = new Vector2(-24 + lean * 28f, 6);
			rightHand = new Vector2(24 + lean * 28f, 6);
			leftFoot = new Vector2(-27 + lean * 42f, 47);
			rightFoot = new Vector2(27 + lean * 42f, 47);
		}
		else if (crouching)
		{
			leftHand = new Vector2(-28 + facing * 6f, 9);
			rightHand = new Vector2(28 + facing * 10f, 3);
			leftFoot = new Vector2(-34 + lean * 28f, 48);
			rightFoot = new Vector2(25 + lean * 28f, 48);
		}
		else if (airborne)
		{
			float riseTuck = Velocity.Y < 0 ? -1f : 1f;
			leftHand = new Vector2(-26 + lean * 35f, -2 + riseTuck * 5f);
			rightHand = new Vector2(28 + lean * 35f, -10 + riseTuck * 2f);
			leftFoot = new Vector2(-18 + lean * 48f, Velocity.Y < 0 ? 34 : 42);
			rightFoot = new Vector2(22 + lean * 48f, Velocity.Y < 0 ? 24 : 48);
		}
		else if (running)
		{
			leftHand = new Vector2(-32 + lean * 48f, -2 + runPhase * 10f);
			rightHand = new Vector2(34 + lean * 48f, -4 - runPhase * 10f);
			leftFoot = new Vector2(-24 + lean * 62f + runPhase * 24f, 47);
			rightFoot = new Vector2(26 + lean * 62f - runPhase * 24f, 47);
		}
		else if (walking)
		{
			leftHand = new Vector2(-24 + lean * 35f, 2 + walkPhase * 5f);
			rightHand = new Vector2(24 + lean * 35f, 2 - walkPhase * 5f);
			leftFoot = new Vector2(-18 + lean * 48f + walkPhase * 14f, 47);
			rightFoot = new Vector2(18 + lean * 48f - walkPhase * 14f, 47);
		}
		else
		{
			leftHand = new Vector2(-25 + lean * 35f, 2);
			rightHand = new Vector2(25 + lean * 35f, 2);
			leftFoot = new Vector2(-18 + lean * 48f, 47);
			rightFoot = new Vector2(18 + lean * 48f, 47);
		}

		DrawLine(visualOffset + shoulder, visualOffset + leftHand, ink, 5f, true);
		DrawLine(visualOffset + shoulder, visualOffset + rightHand, ink, 5f, true);
		DrawLine(visualOffset + hip, visualOffset + leftFoot, ink, 6f, true);
		DrawLine(visualOffset + hip, visualOffset + rightFoot, ink, 6f, true);
		if (running)
		{
			Color speedLine = new Color(ink.R, ink.G, ink.B, 0.35f);
			DrawLine(visualOffset + new Vector2(-facing * 42f, -18f), visualOffset + new Vector2(-facing * 78f, -18f), speedLine, 2f, true);
			DrawLine(visualOffset + new Vector2(-facing * 36f, 14f), visualOffset + new Vector2(-facing * 72f, 14f), speedLine, 2f, true);
		}
		if (DebugDrawCombatBoxes) DrawCombatDebugBoxes();
	}

	private static FighterDefinition CreateBaselineDefinition() => new()
	{
		FighterName = "Physics Test Fighter",
		Tuning = new MovementTuning
		{
			WalkSpeed = 360f, BackWalkSpeedMultiplier = 0.82f, GroundAcceleration = 4400f, GroundTurnAcceleration = 50000f, GroundDeceleration = 5200f, GroundFriction = 6000f,
			Gravity = 4100f, TerminalFallSpeed = 2700f, AirSpeed = 420f,
			AirAcceleration = 2600f, AirDeceleration = 1700f, AllowAirControl = false,
			CoyoteFrames = 3, InputBufferFrames = 3
		},
		CancelRules = new CancelRule[]
		{
			new()
			{
				FromMove = "ANY_NORMAL",
				Kind = CancelKind.Special,
				RequiresContact = true,
				StartFrame = 0
			}
		},
		NormalMoves = CreateBaselineNormalMoveSet(),
		Abilities = new MovementAbility[]
		{
			new SuperJumpAbility { Id = "super_jump", Priority = 40, InitialSpeed = 1920f, ForwardSpeed = 540f, CommandWindowFrames = 4 },
			new JumpAbility { Id = "neutral_jump", Priority = 20, Direction = JumpDirection.Neutral, InitialSpeed = 1340f, AirJumpInitialSpeed = 1120f, MaxAirJumps = 1, HeldFrames = 10, JumpSquatFrames = 1, HeldGravityMultiplier = 1f, ReleaseVelocityMultiplier = 0.48f },
			new JumpAbility { Id = "forward_jump", Priority = 20, Direction = JumpDirection.Forward, InitialSpeed = 1400f, AirJumpInitialSpeed = 1120f, ForwardSpeed = 600f, MaxAirJumps = 1, HeldFrames = 10, JumpSquatFrames = 1, HeldGravityMultiplier = 1f, ReleaseVelocityMultiplier = 0.48f },
			new JumpAbility { Id = "backward_jump", Priority = 20, Direction = JumpDirection.Backward, InitialSpeed = 1320f, AirJumpInitialSpeed = 1120f, ForwardSpeed = 440f, MaxAirJumps = 1, HeldFrames = 10, JumpSquatFrames = 1, HeldGravityMultiplier = 1f, ReleaseVelocityMultiplier = 0.48f },
			new RunAbility { Id = "forward_run", Priority = 10, Speed = 800f, Acceleration = 7000f, StopFriction = 5200f, CrouchCancelFriction = 2600f },
			new DashAbility { Id = "backdash", Priority = 12, GroundOnly = true, DirectionRequirement = DashDirectionRequirement.Backward, ActiveFrames = 12, RecoveryFrames = 18, Speed = 520f, VerticalSpeed = -360f, PreserveGravity = true, CommittedUntilComplete = true, SuspendsInputBufferWhileActive = true },
			new DashAbility { Id = "backward_air_dash", Priority = 16, AirOnly = true, DirectionRequirement = DashDirectionRequirement.Backward, MaxAirUses = 1, ActiveFrames = 9, RecoveryFrames = 8, Speed = 860f, PreserveGravity = true, LandingLagFrames = 8 },
			new DashAbility { Id = "air_dash", Priority = 15, AirOnly = true, MaxAirUses = 1, ActiveFrames = 11, RecoveryFrames = 6, Speed = 820f, AimWithStick = true },
		}
	};

	private static NormalMoveSet CreateBaselineNormalMoveSet() => new()
	{
		Rules = new NormalMoveData[]
		{
			new()
			{
				AttackName = "ANY",
				Stance = NormalMoveStance.Airborne,
				CanChainToLight = true,
				CanChainToHeavy = true,
				CanChainToSpecial = true,
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = 4
			},
			new()
			{
				AttackName = "ELECTRIC WIND GOD FIST",
				Stance = NormalMoveStance.Standing,
				Launches = true,
				LaunchSpeed = 1120f,
				LaunchPushback = 260f,
				LaunchHitstunFrames = 24,
				JumpCancelWindowFrames = 0,
				HitstopFrames = 12,
				Damage = 900,
				CanChainToLight = false,
				CanChainToHeavy = false,
				CanChainToSpecial = false,
				ChainRequiresContact = true
			},
			new()
			{
				AttackName = "HEAVY PUNCH",
				Stance = NormalMoveStance.Crouching,
				Launches = true,
				LaunchSpeed = 1910f,
				LaunchPushback = 180f,
				LaunchHitstunFrames = 30,
				JumpCancelWindowFrames = 30,
				ChaseJumpSpeed = 1830f,
				ChaseForwardSpeed = 360f
			},
			new()
			{
				AttackName = "HEAVY KICK",
				Stance = NormalMoveStance.Crouching,
				HitReaction = HitReactionKind.Knockdown,
				KnockdownType = KnockdownType.Sweep,
				KnocksDown = true,
				KnockdownFrames = 54,
				HitstunFrames = 20,
				Pushback = 760f,
				ChainRequiresContact = true
			},
			new()
			{
				AttackName = "LIGHT PUNCH",
				Stance = NormalMoveStance.Standing,
				AllowedChainTargets = new[] { "STANDING LIGHT KICK", "STANDING HEAVY", "CROUCHING LIGHT", "CROUCHING HEAVY" },
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = 3
			},
			new()
			{
				AttackName = "LIGHT KICK",
				Stance = NormalMoveStance.Standing,
				AllowedChainTargets = new[] { "STANDING LIGHT PUNCH", "STANDING HEAVY", "CROUCHING LIGHT", "CROUCHING HEAVY" },
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = 3
			},
			new()
			{
				AttackName = "LIGHT",
				Stance = NormalMoveStance.Crouching,
				AllowedChainTargets = new[] { "CROUCHING LIGHT", "CROUCHING HEAVY" },
				ChainRequiresContact = true,
				ChainEarliestActiveFramesLeft = 3
			},
			new()
			{
				AttackName = "HEAVY PUNCH",
				Stance = NormalMoveStance.Standing,
				AllowedChainTargets = new[] { "CROUCHING HEAVY PUNCH", "CROUCHING HEAVY KICK" },
				ChainRequiresContact = true
			},
			new()
			{
				AttackName = "HEAVY KICK",
				Stance = NormalMoveStance.Standing,
				AllowedChainTargets = new[] { "CROUCHING HEAVY PUNCH", "CROUCHING HEAVY KICK" },
				ChainRequiresContact = true
			},
			new()
			{
				AttackName = "HEAVY",
				Stance = NormalMoveStance.Crouching,
				ChainRequiresContact = true
			}
		}
	};

	private void DrawEllipse(Vector2 center, Vector2 radii, Color color)
	{
		DrawSetTransform(center, 0, radii);
		DrawCircle(Vector2.Zero, 1f, color);
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	private bool IsJumpSquatting()
	{
		if (ActiveAbility is not JumpAbility jump) return false;
		return Runtime.TryGetValue(jump.Id, out var runtime) && runtime.IntValue > 0;
	}

	private string GetAnimationStateName(bool airborne, bool crouching, bool jumpSquat, bool walking, bool dashing, bool running)
	{
		string knockdownType = CurrentKnockdownType == KnockdownType.None ? "" : $" {CurrentKnockdownType.ToString().ToUpperInvariant()}";
		if (IsGroundedKnockdown) return $"GROUNDED KNOCKDOWN{knockdownType} {HitstunFramesLeft}";
		if (IsInHitstun) return $"{HitState.ToString().ToUpperInvariant()}{knockdownType} {HitstunFramesLeft}";
		if (IsAttacking && CurrentAttackName == "ELECTRIC WIND GOD FIST") return "EWGF";
		if (IsAttacking && IsCrouchAttackLocked && CurrentAttackName == "HEAVY KICK") return "SWEEP";
		if (IsAttacking) return IsCrouchAttackLocked ? $"CROUCH {CurrentAttackName}" : CurrentAttackName;
		if (jumpSquat) return "JUMP SQUAT";
		if (airborne && dashing) return "AIR DASH";
		if (airborne) return Velocity.Y < 0 ? "JUMP RISE" : "JUMP FALL";
		if (crouching) return "CROUCH";
		if (dashing) return "DASH";
		if (running) return "RUN";
		if (walking) return CurrentInput.Horizontal * Facing < 0 ? "BACK WALK" : "WALK";
		return "IDLE";
	}

	private void DrawCombatDebugBoxes()
	{
		DrawRect(PushboxLocal, new Color(1f, 1f, 0f, 0.12f), true);
		DrawRect(PushboxLocal, new Color(1f, 1f, 0f, 0.85f), false, 2f);
		if (!IsOnFloor() && SuppressesGroundedPushWhileAirborne)
		{
			DrawRect(AirbornePushboxLocal, new Color(1f, 0.65f, 0f, 0.16f), true);
			DrawRect(AirbornePushboxLocal, new Color(1f, 0.65f, 0f, 0.95f), false, 2f);
		}
		DrawRect(HurtboxLocal, new Color(0f, 0.8f, 1f, 0.10f), true);
		DrawRect(HurtboxLocal, new Color(0f, 0.8f, 1f, 0.85f), false, 2f);

		Rect2 selectedHitbox = CurrentHitboxLocal;
		Rect2 localHitbox = Facing >= 0
			? selectedHitbox
			: new Rect2(new Vector2(-selectedHitbox.Position.X - selectedHitbox.Size.X, selectedHitbox.Position.Y), selectedHitbox.Size);
		DrawRect(localHitbox, new Color(1f, 0.1f, 0.1f, 0.10f), true);
		DrawRect(localHitbox, IsAttackActive ? new Color(1f, 0.95f, 0.1f, 0.95f) : new Color(1f, 0.1f, 0.1f, 0.85f), false, 2f);

		DrawRect(PositionBoxLocal, new Color(1f, 1f, 1f, 0.90f), false, 2f);
		DrawLine(new Vector2(-10f, 0f), new Vector2(10f, 0f), Colors.White, 2f);
		DrawLine(new Vector2(0f, -14f), new Vector2(0f, 14f), Colors.White, 2f);
	}
}

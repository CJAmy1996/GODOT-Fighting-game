using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

[Tool, GlobalClass]
public partial class FlightAbility : MovementAbility
{
	private const int BoostMode = 1 << 0;
	private const int CancelEntry = 1 << 1;
	private const int AwaitingNeutral = 1 << 2;
	private const int NeutralSeen = 1 << 3;
	private const int DirectionMoved = 1 << 4;
	private const int BoostCancel = 1 << 5;
	private const int ButtonActivation = 1 << 6;
	private const int BlueRecoveryCancel = 1 << 8;
	private const int GroundButtonLift = 1 << 9;

	[Export] public float FlightSpeed { get; set; } = 400f;
	[Export] public float Acceleration { get; set; } = 1800f;
	[Export] public bool CanStartGrounded { get; set; }
	[Export] public bool UseSpecial1Input { get; set; }
	[Export] public float GasCostPerFrame { get; set; }
	[Export] public float GroundButtonFlightLiftSpeed { get; set; } = 240f;
	[Export(PropertyHint.Range, "1,60,1")] public int NegativeEdgeHoldFrames { get; set; } = 20;
	[Export] public bool UseDirectionalAnimations { get; set; }
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float DirectionThreshold { get; set; } = 0.35f;
	[Export] public bool DirectVelocityControl { get; set; }
	/// <summary>Scales sustained flight whenever the input points behind the fighter; boosts use their own setting.</summary>
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float BackwardFlightSpeedMultiplier { get; set; } = 1f;
	/// <summary>Zero means the booster remains available until its input is released.</summary>
	[Export] public int MaxFrames { get; set; } = 180;

	[ExportGroup("Directional Boost")]
	[Export] public bool UseDirectionalBoosts { get; set; }
	[Export] public float BoostSpeed { get; set; } = 900f;
	[Export] public int BoostFrames { get; set; } = 12;
	[Export] public int MaxAirBoosts { get; set; } = 3;
	[Export] public float BoostGasCost { get; set; } = 12f;
	[Export] public float BoostCancelExtraGasCost { get; set; } = 4f;
	[Export] public int BoostAttackDelayFrames { get; set; } = 7;
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float BackwardBoostSpeedMultiplier { get; set; } = 1f;
	[Export(PropertyHint.Range, "1,10,1")] public int BackwardBoostAirUseCost { get; set; } = 1;
	/// <summary>After an airborne backward boost, allow only a pure forward boost until landing.</summary>
	[Export] public bool CommitAfterBackwardAirBoost { get; set; }

	[ExportGroup("Attack Cancels")]
	[Export] public bool AllowNormalHitFlightCancel { get; set; } = true;
	[Export] public bool AllowWhiffRecoveryFlightCancelNormals { get; set; } = true;
	[Export] public bool AllowWhiffRecoveryFlightCancelSpecials { get; set; } = true;
	[Export] public float FlightCancelGasCost { get; set; } = 10f;
	[Export] public int FlightCancelMinimumFrames { get; set; } = 15;
	[Export] public bool RequireNeutralBeforeCancelledFlightMovement { get; set; } = true;
	[Export] public bool RequireDirectionBeforeCancelledFlightAttack { get; set; } = true;
	/// <summary>After this flight ends, reject airborne normals until the fighter touches the ground.</summary>
	[Export] public bool LockAirNormalsDuringPostFlightFall { get; set; }
	public override bool OwnsHorizontalVelocity => true;
	public override bool OwnsGravity => true;

	private bool InputHeld(FighterController fighter) =>
		UseSpecial1Input ? fighter.CurrentInput.Special1Held : fighter.CurrentInput.FlightHeld;
	private bool InputPressed(FighterController fighter) =>
		UseSpecial1Input
			? fighter.CurrentInput.Special1Pressed
			: fighter.CurrentInput.FlightPressed;
	private bool InputReleased(FighterController fighter) =>
		UseSpecial1Input
			? fighter.CurrentInput.Special1Released
			: fighter.CurrentInput.FlightReleased;
	private bool BufferedAttackCancelPressed(FighterController fighter) =>
		InputPressed(fighter) || (UseSpecial1Input
			? fighter.ActionInput.Special1Pressed
			: fighter.ActionInput.FlightPressed);

	private Vector2 InputDirection(FighterController fighter)
	{
		Vector2 direction = new(fighter.CurrentInput.Horizontal, fighter.CurrentInput.Vertical);
		if (Mathf.Abs(direction.X) < DirectionThreshold) direction.X = 0f;
		if (Mathf.Abs(direction.Y) < DirectionThreshold) direction.Y = 0f;
		return direction.LengthSquared() > 1f ? direction.Normalized() : direction;
	}

	public bool IsBoosting(FighterController fighter) =>
		(GetRuntimeFlags(fighter) & BoostMode) != 0;
	public bool IsCancelledFlight(FighterController fighter) =>
		(GetRuntimeFlags(fighter) & CancelEntry) != 0 && !IsBoosting(fighter);
	public bool IsButtonActivatedFlight(FighterController fighter) =>
		(GetRuntimeFlags(fighter) & ButtonActivation) != 0 && !IsBoosting(fighter);
	public bool IsNegativeEdgeFlight(FighterController fighter) =>
		(GetRuntimeFlags(fighter) & (ButtonActivation | BoostMode)) == 0;
	public bool ShouldPersistThroughNormal(FighterController fighter, string attackName) =>
		IsButtonActivatedFlight(fighter) && FighterController.IsNormalAttackName(attackName);
	public bool ShouldTickDuringAttack(FighterController fighter) => IsButtonActivatedFlight(fighter);
	public bool WantsManualDeactivation(FighterController fighter) => IsButtonActivatedFlight(fighter)
		? InputPressed(fighter)
		: IsNegativeEdgeFlight(fighter) && InputReleased(fighter);
	public int ElapsedFrames(FighterController fighter) => fighter.GetRuntime(this).IntValue;
	public int AirBoostsUsed(FighterController fighter) => fighter.GetRuntime(this).UsesThisAirTime;
	public bool IsBackwardBoostCommittedThisAirTime(FighterController fighter) => fighter.GetRuntime(this).BoolValue;

	private int GetRuntimeFlags(FighterController fighter) => fighter.GetRuntime(this).IntValue2;
	private bool IsBackwardDirection(FighterController fighter, Vector2 direction) =>
		direction.X * fighter.Facing <= -DirectionThreshold;
	private bool IsPureForwardBoost(FighterController fighter, Vector2 direction) =>
		direction.X * fighter.Facing >= DirectionThreshold && Mathf.Abs(direction.Y) < DirectionThreshold;
	private int ResolveAirBoostUseCost(FighterController fighter, Vector2 direction) =>
		IsBackwardDirection(fighter, direction) ? Mathf.Max(1, BackwardBoostAirUseCost) : 1;
	private float ResolveBoostSpeed(FighterController fighter, Vector2 direction) =>
		BoostSpeed * (IsBackwardDirection(fighter, direction) ? Mathf.Clamp(BackwardBoostSpeedMultiplier, 0.1f, 1f) : 1f);
	private float ResolveFlightSpeed(FighterController fighter, Vector2 direction) =>
		FlightSpeed * (IsBackwardDirection(fighter, direction) ? Mathf.Clamp(BackwardFlightSpeedMultiplier, 0.1f, 1f) : 1f);
	private bool HasEnoughAirBoostUses(FighterController fighter, AbilityRuntime runtime, Vector2 direction) =>
		fighter.WasGrounded || runtime.UsesThisAirTime + ResolveAirBoostUseCost(fighter, direction) <= Mathf.Max(0, MaxAirBoosts);
	private bool IsPostBackwardCommitInputAllowed(FighterController fighter, AbilityRuntime runtime, Vector2 direction) =>
		fighter.WasGrounded || !CommitAfterBackwardAirBoost || !runtime.BoolValue || IsPureForwardBoost(fighter, direction);

	public string ResolveAnimationName(FighterController fighter)
	{
		string token = ResolveDirectionToken(fighter, fighter.GetRuntime(this));
		return !UseDirectionalAnimations || token == "" ? "booster_loop" : $"booster_{token.ToLowerInvariant()}";
	}

	public string ResolveStateName(FighterController fighter)
	{
		string token = ResolveDirectionToken(fighter, fighter.GetRuntime(this));
		return !UseDirectionalAnimations || token == "" ? "STATE BOOSTER" : $"STATE BOOSTER {token.Replace('_', ' ')}";
	}

	private string ResolveDirectionToken(FighterController fighter, AbilityRuntime runtime)
	{
		Vector2 direction = (runtime.IntValue2 & BoostMode) != 0
			? runtime.VectorValue
			: (runtime.IntValue2 & AwaitingNeutral) != 0 && (runtime.IntValue2 & NeutralSeen) == 0
				? Vector2.Zero
				: InputDirection(fighter);
		float relativeHorizontal = direction.X * fighter.Facing;
		int horizontal = relativeHorizontal >= DirectionThreshold ? 1 : relativeHorizontal <= -DirectionThreshold ? -1 : 0;
		int vertical = direction.Y >= DirectionThreshold ? 1 : direction.Y <= -DirectionThreshold ? -1 : 0;
		return (horizontal, vertical) switch
		{
			(0, -1) => "UP",
			(1, -1) => "UP_FORWARD",
			(1, 0) => "FORWARD",
			(1, 1) => "DOWN_FORWARD",
			(0, 1) => "DOWN",
			(-1, 1) => "DOWN_BACK",
			(-1, 0) => "BACK",
			(-1, -1) => "UP_BACK",
			_ => "",
		};
	}

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime)
	{
		// Once toggle flight is running, the flight button belongs exclusively to
		// toggle-off. Direction + button must never reinterpret it as a boost.
		if (IsButtonActivatedFlight(fighter)) return false;
		bool pressedActivation = InputPressed(fighter);
		if (!pressedActivation || (!CanStartGrounded && fighter.WasGrounded)) return false;
		Vector2 direction = InputDirection(fighter);
		if (!IsPostBackwardCommitInputAllowed(fighter, runtime, direction)) return false;
		if (UseDirectionalBoosts && !direction.IsZeroApprox())
		{
			if (!HasEnoughAirBoostUses(fighter, runtime, direction)) return false;
			if (!fighter.HasGasMeter(BoostGasCost)) return false;
			runtime.IntValue2 = BoostMode | ButtonActivation;
			runtime.VectorValue = direction;
			return true;
		}
		if (!fighter.HasGasMeter(GasCostPerFrame)) return false;
		// A press is immediately toggle flight. Holding it long enough converts
		// this same flight to negative-edge (hold/release) mode.
		runtime.IntValue2 = ButtonActivation | (fighter.WasGrounded ? GroundButtonLift : 0);
		runtime.VectorValue = Vector2.Zero;
		return true;
	}

	public override bool CanStartFromAttack(FighterController fighter, AbilityRuntime runtime)
	{
		if (!BufferedAttackCancelPressed(fighter) || fighter.IsPerformingSuperMove) return false;
		Vector2 direction = InputDirection(fighter);
		bool authoredSpecialCancel = fighter.CanCancelCurrentNormalIntoSpecial("SPECIAL 1");
		if (authoredSpecialCancel && (!UseDirectionalBoosts || direction.IsZeroApprox()))
		{
			if (!fighter.HasGasMeter(GasCostPerFrame)) return false;
			// This is a true special cancel, not a hit-only flight cancel.
			runtime.IntValue2 = ButtonActivation |
				(fighter.WasGrounded ? GroundButtonLift : 0) |
				(RequireNeutralBeforeCancelledFlightMovement ? AwaitingNeutral : 0);
			runtime.VectorValue = Vector2.Zero;
			return true;
		}
		bool postBackwardCommit = !fighter.WasGrounded && CommitAfterBackwardAirBoost && runtime.BoolValue;
		if (postBackwardCommit && (!IsPureForwardBoost(fighter, direction) || !fighter.CurrentAttackHasUnblockedHit))
			return false;
		if (UseDirectionalBoosts && !direction.IsZeroApprox() && fighter.CurrentAttackHasUnblockedHit)
		{
			if (!HasEnoughAirBoostUses(fighter, runtime, direction)) return false;
			float cost = BoostGasCost + Mathf.Max(0f, BoostCancelExtraGasCost);
			if (!fighter.HasGasMeter(cost)) return false;
			runtime.IntValue2 = BoostMode | CancelEntry | BoostCancel;
			runtime.VectorValue = direction;
			return true;
		}

		bool normalWhiffRecovery = fighter.IsAttackRecovering && !fighter.CurrentAttackHasContact &&
			fighter.CurrentAttackIsNormal && AllowWhiffRecoveryFlightCancelNormals;
		bool specialRecoveryCancel = fighter.IsAttackRecovering && fighter.CurrentAttackIsSpecial &&
			AllowWhiffRecoveryFlightCancelSpecials && fighter.IsWithinBlueRecoveryCancelWindow &&
			(!fighter.CurrentAttackHasContact || fighter.CurrentAttackHasUnblockedHit);
		bool recoveryCancel = normalWhiffRecovery || specialRecoveryCancel;
		bool normalHitFlightCancel = AllowNormalHitFlightCancel && fighter.CurrentAttackIsNormal &&
			fighter.CurrentAttackHasUnblockedHit;
		if (!recoveryCancel && !normalHitFlightCancel) return false;
		if (!fighter.HasGasMeter(FlightCancelGasCost)) return false;
		bool blueRecoveryCancel = specialRecoveryCancel;
		runtime.IntValue2 = CancelEntry | (RequireNeutralBeforeCancelledFlightMovement ? AwaitingNeutral : 0) |
			(blueRecoveryCancel ? BlueRecoveryCancel | ButtonActivation : 0) |
			(blueRecoveryCancel && fighter.WasGrounded ? GroundButtonLift : 0);
		runtime.VectorValue = Vector2.Zero;
		return true;
	}

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		if (LockAirNormalsDuringPostFlightFall) fighter.MarkFlightUsedThisAirTime();
		runtime.IntValue = 0;
		fighter.Velocity = Vector2.Zero;
		if ((runtime.IntValue2 & BoostMode) != 0)
		{
			CallAudio(fighter, "play_mecha_boost");
			float cost = BoostGasCost + ((runtime.IntValue2 & BoostCancel) != 0 ? Mathf.Max(0f, BoostCancelExtraGasCost) : 0f);
			fighter.TrySpendGasMeter(cost);
			runtime.FramesRemaining = Mathf.Max(1, BoostFrames);
			if (!fighter.WasGrounded)
			{
				runtime.UsesThisAirTime += ResolveAirBoostUseCost(fighter, runtime.VectorValue);
				if (CommitAfterBackwardAirBoost && IsBackwardDirection(fighter, runtime.VectorValue))
					runtime.BoolValue = true;
			}
			fighter.Velocity = runtime.VectorValue * ResolveBoostSpeed(fighter, runtime.VectorValue);
			return;
		}
		CallAudio(fighter, "play_mecha_boost");
		if ((runtime.IntValue2 & CancelEntry) != 0)
			fighter.TrySpendGasMeter(FlightCancelGasCost);
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		runtime.IntValue++;
		if ((runtime.IntValue2 & BoostMode) != 0)
		{
			fighter.Velocity = runtime.VectorValue * ResolveBoostSpeed(fighter, runtime.VectorValue);
			return runtime.IntValue < Mathf.Max(1, BoostFrames);
		}
		if (IsButtonActivatedFlight(fighter) && InputHeld(fighter) &&
			runtime.IntValue >= Mathf.Max(1, NegativeEdgeHoldFrames))
			runtime.IntValue2 &= ~ButtonActivation;
		if (IsButtonActivatedFlight(fighter))
		{
			// Ignore the original activation edge, then use the next press as a toggle-off.
			if (runtime.IntValue > 1 && InputPressed(fighter)) return false;
		}
		else if (InputReleased(fighter) || !InputHeld(fighter))
		{
			return false;
		}
		if ((MaxFrames > 0 && runtime.IntValue >= MaxFrames) ||
			!fighter.TrySpendGasMeter(GasCostPerFrame)) return false;
		if ((runtime.IntValue2 & GroundButtonLift) != 0)
		{
			// A grounded flight cancel is not complete until the body has actually
			// separated from the floor. Keep commanding lift across collision frames.
			if (!fighter.WasGrounded) runtime.IntValue2 &= ~GroundButtonLift;
			fighter.Velocity = new Vector2(fighter.Velocity.X, -Mathf.Max(1f, GroundButtonFlightLiftSpeed));
			return true;
		}
		if (fighter.IsAttacking && IsButtonActivatedFlight(fighter))
		{
			// Toggle flight is a fixed aerial platform during normals. Clear both axes
			// every tick so neither prior flight input nor attack momentum can slide it.
			fighter.Velocity = Vector2.Zero;
			return true;
		}

		Vector2 inputDirection = InputDirection(fighter);
		if (!inputDirection.IsZeroApprox()) runtime.IntValue2 |= DirectionMoved;
		if ((runtime.IntValue2 & AwaitingNeutral) != 0)
		{
			if ((runtime.IntValue2 & NeutralSeen) == 0)
			{
				fighter.Velocity = Vector2.Zero;
				if (inputDirection.IsZeroApprox()) runtime.IntValue2 |= NeutralSeen;
				return true;
			}
			if (!inputDirection.IsZeroApprox()) runtime.IntValue2 |= DirectionMoved;
		}

		Vector2 desired = inputDirection * ResolveFlightSpeed(fighter, inputDirection);
		fighter.Velocity = DirectVelocityControl
			? desired
			: fighter.Velocity.MoveToward(desired, Acceleration * delta);
		return true;
	}

	public override bool CanStartAttack(FighterController fighter, AbilityRuntime runtime)
	{
		if ((runtime.IntValue2 & BoostMode) != 0)
			return runtime.IntValue >= Mathf.Max(0, BoostAttackDelayFrames);
		// Toggle flight must leave the floor before normals are accepted so the
		// attack resolver always selects airborne move data. Ground boosts are exempt.
		if (IsButtonActivatedFlight(fighter) && fighter.WasGrounded) return false;
		// Neutral -> direction is the fast-fly cancel rule, not a permanent lock on
		// ordinary flight normals. Once regular flight startup resolves, attacks are free.
		if (IsButtonActivatedFlight(fighter) && (runtime.IntValue2 & AwaitingNeutral) != 0)
			return (runtime.IntValue2 & (NeutralSeen | DirectionMoved)) == (NeutralSeen | DirectionMoved);
		if ((runtime.IntValue2 & CancelEntry) == 0) return true;
		if (runtime.IntValue < Mathf.Max(0, FlightCancelMinimumFrames)) return false;
		if (RequireNeutralBeforeCancelledFlightMovement && (runtime.IntValue2 & NeutralSeen) == 0) return false;
		return !RequireDirectionBeforeCancelledFlightAttack || (runtime.IntValue2 & DirectionMoved) != 0;
	}

	public override void Stop(FighterController fighter, AbilityRuntime runtime)
	{
		base.Stop(fighter, runtime);
		runtime.FramesRemaining = 0;
		runtime.IntValue = 0;
		runtime.IntValue2 = 0;
		runtime.VectorValue = Vector2.Zero;
	}

	private static void CallAudio(FighterController fighter, string method)
	{
		if (fighter?.IsInsideTree() != true) return;
		fighter.GetNodeOrNull<Node>("/root/AudioController")?.Call(method);
	}
}

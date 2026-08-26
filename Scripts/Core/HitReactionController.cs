using Godot;

namespace ModularFighter.Core;

public enum HitstunTickResult
{
	Active,
	PersistedUntilLanding,
	BeginWakeup,
	Recovered
}

public readonly record struct GroundBounceLandingTransition(float BounceSpeed, bool IntoJuggle);

/// <summary>
/// Mutable runtime state shared by ordinary hitstun and blockstun reactions.
/// Kept separate from launch, juggle, bounce, and knockdown state so those
/// mechanics can be extracted independently without changing their behavior.
/// </summary>
public sealed class HitReactionState
{
	public FighterHitState HitState { get; internal set; } = FighterHitState.None;
	public int HitstunFramesLeft { get; internal set; }
	public bool IsCrouchBlocking { get; internal set; }
	public bool HitReactionStartedCrouching { get; internal set; }
	public ulong HitReactionSerial { get; internal set; }
	public ulong BlockReactionSerial { get; internal set; }
	public int JuggleHitCount { get; internal set; }
	public int GroundNormalJuggleHitCount { get; internal set; }
	public KnockdownType KnockdownType { get; internal set; } = KnockdownType.None;
	public WallBounceReactionStrength WallBounceStrength { get; internal set; } = WallBounceReactionStrength.None;
	public GroundBounceReactionStrength GroundBounceStrength { get; internal set; } = GroundBounceReactionStrength.None;
	public bool PendingWallSplatKnockdown { get; internal set; }
	public int WallSplatDirection { get; internal set; }
	public float PendingGroundBounceSpeed { get; internal set; } = -1f;
	public bool PendingGroundBounceIntoJuggle { get; internal set; }
	public int WakeupFramesLeft { get; internal set; }
	public int ActiveWakeupTotalFrames { get; internal set; }
	public BlowAwayDirection BlowAwayDirection { get; internal set; } = BlowAwayDirection.None;
	public BlowAwayStrength BlowAwayStrength { get; internal set; } = BlowAwayStrength.None;
	public bool BlowAwayNoBounce { get; internal set; }
	public GuardReactionStrength GuardReactionStrength { get; internal set; } = GuardReactionStrength.None;
	public SpecialReactionKind SpecialReaction { get; internal set; } = SpecialReactionKind.None;
}

/// <summary>
/// Owns the common state transitions for receiving an ordinary hit or block.
/// FighterController remains responsible for velocity, ability interruption,
/// attack cleanup, and the still-coupled launch/knockdown reaction families.
/// </summary>
public sealed class HitReactionController
{
	public HitReactionState State { get; } = new();

	public void BeginHitReaction(int frames, FighterHitState state, bool startedCrouching)
	{
		State.GuardReactionStrength = GuardReactionStrength.None;
		State.SpecialReaction = SpecialReactionKind.None;
		State.HitReactionSerial++;
		State.HitReactionStartedCrouching = startedCrouching;
		State.HitstunFramesLeft = frames;
		State.HitState = state;
	}

	public Vector2 BeginJuggleReaction(int frames, float horizontalPushback, float verticalVelocity,
		bool startedCrouching)
	{
		bool continuingJuggle = State.HitState == FighterHitState.Juggle;
		State.JuggleHitCount = continuingJuggle ? State.JuggleHitCount + 1 : 1;
		if (!continuingJuggle) State.GroundNormalJuggleHitCount = 0;
		BeginHitReaction(frames, FighterHitState.Juggle, startedCrouching);
		return new Vector2(horizontalPushback, verticalVelocity);
	}

	public void IncrementGroundNormalJuggleHitCount() => State.GroundNormalJuggleHitCount++;

	/// <summary>
	/// Starts a new juggle route without beginning a new hit reaction. Used when
	/// an existing bounce reaction changes category during its landing transition.
	/// </summary>
	public void EnterFreshJuggle()
	{
		State.HitState = FighterHitState.Juggle;
		State.JuggleHitCount = 1;
		State.GroundNormalJuggleHitCount = 0;
	}

	public void ClearJuggleCounters()
	{
		State.JuggleHitCount = 0;
		State.GroundNormalJuggleHitCount = 0;
	}

	public static FighterHitState ResolveInitialKnockdownState(KnockdownType knockdownType, bool grounded) =>
		knockdownType switch
		{
			KnockdownType.Sweep => FighterHitState.Knockdown,
			KnockdownType.WallBounce => FighterHitState.WallBounce,
			KnockdownType.GroundBounce => FighterHitState.GroundBounce,
			KnockdownType.Crumple => FighterHitState.Crumple,
			_ => grounded ? FighterHitState.GroundedKnockdown : FighterHitState.Knockdown
		};

	public void BeginWakeup(int durationFrames)
	{
		State.ActiveWakeupTotalFrames = durationFrames;
		State.WakeupFramesLeft = durationFrames;
		State.HitstunFramesLeft = 0;
		State.HitState = FighterHitState.None;
		State.KnockdownType = KnockdownType.None;
		State.GroundBounceStrength = GroundBounceReactionStrength.None;
		State.SpecialReaction = SpecialReactionKind.None;
	}

	public KnockdownType BeginKnockdown(KnockdownType requestedType)
	{
		State.KnockdownType = requestedType == KnockdownType.None
			? KnockdownType.AirKnockdown
			: requestedType;
		return State.KnockdownType;
	}

	public WallBounceReactionStrength BeginWallSplat(WallBounceReactionStrength currentStrength,
		int wallDirection)
	{
		State.WallBounceStrength = currentStrength == WallBounceReactionStrength.None
			? WallBounceReactionStrength.Strong
			: currentStrength;
		State.PendingWallSplatKnockdown = true;
		State.WallSplatDirection = wallDirection >= 0 ? 1 : -1;
		State.KnockdownType = KnockdownType.SoftKnockdown;
		return State.WallBounceStrength;
	}

	public WallBounceReactionStrength ConfigureWallBounce(WallBounceReactionStrength strength)
	{
		State.WallBounceStrength = strength == WallBounceReactionStrength.None
			? WallBounceReactionStrength.Strong
			: strength;
		return State.WallBounceStrength;
	}

	public GroundBounceReactionStrength ConfigureGroundBounce(GroundBounceReactionStrength strength,
		float bounceSpeed, bool intoJuggle)
	{
		State.GroundBounceStrength = strength == GroundBounceReactionStrength.None
			? GroundBounceReactionStrength.Medium
			: strength;
		State.PendingGroundBounceSpeed = bounceSpeed;
		State.PendingGroundBounceIntoJuggle = intoJuggle;
		return State.GroundBounceStrength;
	}

	public void ClearPendingGroundBounce()
	{
		State.PendingGroundBounceSpeed = -1f;
		State.PendingGroundBounceIntoJuggle = false;
	}

	public void EnterGroundedKnockdown(int minimumHoldFrames)
	{
		State.HitState = FighterHitState.GroundedKnockdown;
		State.HitReactionSerial++;
		State.HitstunFramesLeft = Mathf.Max(State.HitstunFramesLeft, minimumHoldFrames);
		if (State.PendingWallSplatKnockdown) State.KnockdownType = KnockdownType.SoftKnockdown;
		if (State.KnockdownType == KnockdownType.None) State.KnockdownType = KnockdownType.AirKnockdown;
		State.WallBounceStrength = WallBounceReactionStrength.None;
		State.GroundBounceStrength = GroundBounceReactionStrength.None;
		State.SpecialReaction = SpecialReactionKind.None;
		State.PendingWallSplatKnockdown = false;
	}

	public void TickWakeup()
	{
		if (State.WakeupFramesLeft <= 0) return;
		State.WakeupFramesLeft--;
		if (State.WakeupFramesLeft == 0) State.ActiveWakeupTotalFrames = 0;
	}

	public HitstunTickResult TickHitstun(bool airborneReactionMustPersist, bool groundedKnockdownHasWakeup)
	{
		State.HitstunFramesLeft--;
		if (State.HitstunFramesLeft > 0) return HitstunTickResult.Active;
		if (airborneReactionMustPersist)
		{
			State.HitstunFramesLeft = 1;
			return HitstunTickResult.PersistedUntilLanding;
		}
		if (State.HitState == FighterHitState.GroundedKnockdown && groundedKnockdownHasWakeup)
			return HitstunTickResult.BeginWakeup;
		ClearRecoveredReaction();
		return HitstunTickResult.Recovered;
	}

	public bool ShouldPersistAirReaction(bool grounded) =>
		!grounded && (State.BlowAwayDirection != BlowAwayDirection.None || State.PendingWallSplatKnockdown ||
			State.HitState == FighterHitState.Knockdown || State.HitState == FighterHitState.GroundBounce ||
			State.HitState == FighterHitState.WallSplat || State.HitState == FighterHitState.Stumble ||
			State.HitState == FighterHitState.HitFall ||
			(State.HitState == FighterHitState.Juggle && State.KnockdownType != KnockdownType.None));

	public void ClearRecoveredReaction()
	{
		State.HitstunFramesLeft = 0;
		State.HitState = FighterHitState.None;
		State.KnockdownType = KnockdownType.None;
		ClearBlowAway();
		State.WallBounceStrength = WallBounceReactionStrength.None;
		State.GroundBounceStrength = GroundBounceReactionStrength.None;
		State.GuardReactionStrength = GuardReactionStrength.None;
		State.SpecialReaction = SpecialReactionKind.None;
		State.PendingWallSplatKnockdown = false;
		State.WallSplatDirection = 0;
		ClearJuggleCounters();
		ClearPendingGroundBounce();
	}

	public void ClearBlowAway()
	{
		State.BlowAwayDirection = BlowAwayDirection.None;
		State.BlowAwayStrength = BlowAwayStrength.None;
		State.BlowAwayNoBounce = false;
	}

	public void PrepareForIncomingReaction(FighterHitState state)
	{
		ClearBlowAway();
		State.WallBounceStrength = WallBounceReactionStrength.None;
		State.GroundBounceStrength = GroundBounceReactionStrength.None;
		if (state != FighterHitState.GroundBounce) ClearPendingGroundBounce();
		if (state != FighterHitState.Knockdown && state != FighterHitState.GroundedKnockdown &&
			state != FighterHitState.WallBounce && state != FighterHitState.GroundBounce &&
			state != FighterHitState.Crumple && state != FighterHitState.Stumble &&
			state != FighterHitState.HitFall)
			State.KnockdownType = KnockdownType.None;
	}

	public void ClearKnockdownAndBounceStrengths()
	{
		State.KnockdownType = KnockdownType.None;
		State.WallBounceStrength = WallBounceReactionStrength.None;
		State.GroundBounceStrength = GroundBounceReactionStrength.None;
	}

	public void SetKnockdownType(KnockdownType knockdownType) => State.KnockdownType = knockdownType;
	public void SetSpecialReaction(SpecialReactionKind reaction) => State.SpecialReaction = reaction;

	public void ConfigureBlowAway(BlowAwayDirection direction, BlowAwayStrength strength, bool noBounce)
	{
		State.BlowAwayDirection = direction;
		State.BlowAwayStrength = strength;
		State.BlowAwayNoBounce = noBounce;
	}

	public void ClearIdleHitState() => State.HitState = FighterHitState.None;

	public GroundBounceLandingTransition ResolveGroundBounceLanding(float defaultBounceSpeed)
	{
		float bounceSpeed = State.PendingGroundBounceSpeed > 0f
			? State.PendingGroundBounceSpeed
			: defaultBounceSpeed;
		bool intoJuggle = State.PendingGroundBounceIntoJuggle;
		if (intoJuggle) EnterFreshJuggle();
		else State.HitState = FighterHitState.Tumble;
		State.KnockdownType = KnockdownType.None;
		ClearPendingGroundBounce();
		return new GroundBounceLandingTransition(bounceSpeed, intoJuggle);
	}

	public void ResolveBlowAwayBounceLanding(int minimumHitstunFrames)
	{
		EnterFreshJuggle();
		State.KnockdownType = KnockdownType.AirKnockdown;
		State.BlowAwayDirection = BlowAwayDirection.Vertical;
		State.BlowAwayStrength = BlowAwayStrength.Weak;
		State.BlowAwayNoBounce = true;
		State.HitstunFramesLeft = Mathf.Max(State.HitstunFramesLeft, minimumHitstunFrames);
	}

	public void CancelBlockstunForReflector()
	{
		State.HitstunFramesLeft = 0;
		State.HitState = FighterHitState.None;
	}

	public void LockDefeatedKo()
	{
		State.HitState = FighterHitState.GroundedKnockdown;
		State.KnockdownType = KnockdownType.HardKnockdown;
		State.HitstunFramesLeft = int.MaxValue / 4;
	}

	public static Vector2 ResolveLaunchVelocity(float horizontalPushback, float verticalLaunchSpeed) =>
		new(horizontalPushback, -verticalLaunchSpeed);

	public static Vector2 ResolveAirPopVelocity(Vector2 currentVelocity, float horizontalPushback, float popUpSpeed) =>
		new(horizontalPushback, Mathf.Min(currentVelocity.Y, -popUpSpeed));

	public static Vector2 ResolveAirSpikeVelocity(Vector2 currentVelocity, float horizontalPushback, float spikeSpeed) =>
		new(horizontalPushback, Mathf.Max(currentVelocity.Y, spikeSpeed));

	public void ApplyHitstun(FighterController fighter, int frames, float horizontalPushback, bool counterHit = false)
	{
		fighter.ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Hitstun);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, fighter.Velocity.Y));
	}

	public void ApplyBlockstun(FighterController fighter, int frames, float horizontalPushback,
		GuardReactionStrength strength, SpecialReactionKind specialReaction, bool? crouchBlock)
	{
		float resolvedPushback = BeginBlockstun(frames, horizontalPushback, strength, specialReaction,
			fighter.WasGrounded && (crouchBlock ?? fighter.CurrentInput.Vertical > 0.5f));
		ClearKnockdownAndBounceStrengths();
		ClearBlowAway();
		fighter.Velocity = new Vector2(resolvedPushback, fighter.Velocity.Y);
		fighter.StopActiveAbility();
		fighter.ClearAttackState();
	}

	public void ApplyLaunchHitstun(FighterController fighter, int frames, float horizontalPushback,
		float verticalLaunchSpeed, bool counterHit = false)
	{
		fighter.ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			ResolveLaunchVelocity(horizontalPushback, verticalLaunchSpeed));
	}

	public void ApplyJuggleHitstun(FighterController fighter, int frames, float horizontalPushback,
		float verticalVelocity, bool knockdownOnLanding)
	{
		bool startedCrouching = fighter.PrepareHitReaction(FighterHitState.Juggle);
		Vector2 requestedVelocity = BeginJuggleReaction(frames, horizontalPushback, verticalVelocity, startedCrouching);
		if (knockdownOnLanding) SetKnockdownType(KnockdownType.AirKnockdown);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(requestedVelocity);
	}

	public void ApplyWallSplat(FighterController fighter, int wallDirection)
	{
		WallBounceReactionStrength reactionStrength = fighter.CurrentWallBounceStrength;
		fighter.ApplyHitReaction(Mathf.Max(1, fighter.WallSplatHitstunFrames), FighterHitState.WallSplat);
		BeginWallSplat(reactionStrength, wallDirection);
		fighter.Velocity = new Vector2(0f, Mathf.Max(0f, fighter.WallSplatSlideSpeed));
		fighter.QueueStateImpact(FighterHitState.WallSplat, State.WallSplatDirection);
	}

	public void ApplyAirPopHitstun(FighterController fighter, int frames, float horizontalPushback,
		float popUpSpeed, bool tumble = false)
	{
		fighter.ApplyHitReaction(frames, tumble ? FighterHitState.Tumble : FighterHitState.Hitstun);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			ResolveAirPopVelocity(fighter.Velocity, horizontalPushback, popUpSpeed));
	}

	public void ApplyAirSpikeHitstun(FighterController fighter, int frames, float horizontalPushback,
		float spikeSpeed, bool counterHit = false)
	{
		fighter.ApplyHitReaction(frames, counterHit ? FighterHitState.CounterHit : FighterHitState.Tumble);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			ResolveAirSpikeVelocity(fighter.Velocity, horizontalPushback, spikeSpeed));
	}

	public void ApplyStumbleHitstun(FighterController fighter, int frames, float horizontalPushback)
	{
		fighter.ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.Stumble);
		SetKnockdownType(KnockdownType.SoftKnockdown);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			new Vector2(horizontalPushback, -Mathf.Abs(fighter.StumblePopUpSpeed)));
	}

	public void ApplyHitFallHitstun(FighterController fighter, int frames, float horizontalPushback)
	{
		fighter.ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.HitFall);
		SetKnockdownType(KnockdownType.AirKnockdown);
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			new Vector2(horizontalPushback, Mathf.Max(fighter.Velocity.Y, Mathf.Abs(fighter.HitFallSpeed))));
	}

	public void ApplyBlowAwayHitstun(FighterController fighter, int frames, int horizontalDirection,
		BlowAwayDirection direction, BlowAwayStrength strength, bool noBounce, float authoredSpeed)
	{
		BlowAwayStrength resolvedStrength = strength == BlowAwayStrength.None ? BlowAwayStrength.Medium : strength;
		fighter.ApplyHitReaction(Mathf.Max(1, frames), FighterHitState.Tumble);
		ConfigureBlowAway(direction, resolvedStrength, noBounce);
		float speed = authoredSpeed > 0f ? authoredSpeed : resolvedStrength switch
		{
			BlowAwayStrength.Weak => fighter.WeakBlowAwaySpeed,
			BlowAwayStrength.Strong => fighter.StrongBlowAwaySpeed,
			_ => fighter.MediumBlowAwaySpeed
		};
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
			ResolveBlowAwayVelocity(direction, horizontalDirection, speed));
	}

	public static Vector2 ResolveBlowAwayVelocity(BlowAwayDirection direction, int horizontalDirection, float speed)
	{
		float resolvedSpeed = Mathf.Max(1f, speed);
		float facing = horizontalDirection >= 0 ? 1f : -1f;
		const float diagonal = 0.70710678f;
		return direction switch
		{
			BlowAwayDirection.Horizontal => new Vector2(facing * resolvedSpeed, -resolvedSpeed * 0.08f),
			BlowAwayDirection.Vertical => new Vector2(facing * resolvedSpeed * 0.12f, -resolvedSpeed),
			BlowAwayDirection.Diagonal => new Vector2(facing * resolvedSpeed * diagonal, -resolvedSpeed * diagonal),
			BlowAwayDirection.Downward => new Vector2(0f, resolvedSpeed),
			BlowAwayDirection.DiagonalDown => new Vector2(facing * resolvedSpeed * diagonal, resolvedSpeed * diagonal),
			_ => Vector2.Zero
		};
	}

	public static string ResolveBlowAwayAnimationName(BlowAwayDirection direction, BlowAwayStrength strength,
		bool noBounce = false)
	{
		string suffix = strength switch
		{
			BlowAwayStrength.Weak => "weak",
			BlowAwayStrength.Strong => "strong",
			_ => "medium"
		};
		return direction switch
		{
			BlowAwayDirection.Horizontal => "blow_away_horizontal",
			BlowAwayDirection.Vertical => $"blow_away_vertical_{suffix}",
			BlowAwayDirection.Diagonal => $"blow_away_diagonal_{suffix}",
			BlowAwayDirection.Downward when noBounce => "blow_away_downward_no_bounce",
			BlowAwayDirection.Downward => $"blow_away_downward_{suffix}",
			BlowAwayDirection.DiagonalDown when noBounce => "blow_away_diagonal_down_no_bounce",
			BlowAwayDirection.DiagonalDown => "blow_away_diagonal_down",
			_ => ""
		};
	}

	public static string ResolveBlowAwayStateName(BlowAwayDirection direction, BlowAwayStrength strength,
		bool noBounce = false)
	{
		string suffix = strength switch
		{
			BlowAwayStrength.Weak => "弱",
			BlowAwayStrength.Strong => "強",
			_ => "中"
		};
		return direction switch
		{
			BlowAwayDirection.Horizontal => "STATE [ヒット]吹っ飛び_真横",
			BlowAwayDirection.Vertical => $"STATE [ヒット]吹っ飛び_真上_{suffix}",
			BlowAwayDirection.Diagonal => $"STATE [ヒット]吹っ飛び_斜め_{suffix}",
			BlowAwayDirection.Downward when noBounce => "STATE [ヒット]吹っ飛び_真下_無バウンド",
			BlowAwayDirection.Downward => $"STATE [ヒット]吹っ飛び_真下_{suffix}",
			BlowAwayDirection.DiagonalDown when noBounce => "STATE [ヒット]吹っ飛び_斜め下_無バウンド",
			BlowAwayDirection.DiagonalDown => "STATE [ヒット]吹っ飛び_斜め下",
			_ => ""
		};
	}

	public void ApplySpecialReactionHitstun(FighterController fighter, int frames, float horizontalPushback,
		SpecialReactionKind reaction)
	{
		int resolvedFrames = Mathf.Max(1, frames);
		switch (reaction)
		{
			case SpecialReactionKind.SlideDownHorizontal:
				fighter.ApplyHitReaction(resolvedFrames, FighterHitState.Knockdown);
				SetKnockdownType(KnockdownType.SoftKnockdown);
				fighter.Velocity = new Vector2(horizontalPushback * 1.35f, Mathf.Max(0f, fighter.Velocity.Y));
				break;
			case SpecialReactionKind.SlideDownDiagonal:
				fighter.ApplyHitReaction(resolvedFrames, FighterHitState.Knockdown);
				SetKnockdownType(KnockdownType.SoftKnockdown);
				fighter.Velocity = new Vector2(horizontalPushback,
					Mathf.Max(fighter.Velocity.Y, fighter.HitFallSpeed * 0.75f));
				break;
			case SpecialReactionKind.SlideDowned:
				fighter.ApplyHitReaction(resolvedFrames, FighterHitState.GroundedKnockdown);
				SetKnockdownType(KnockdownType.SoftKnockdown);
				fighter.Velocity = new Vector2(horizontalPushback, 0f);
				break;
			case SpecialReactionKind.DiagonalBounce:
				ApplyKnockdown(fighter, resolvedFrames, horizontalPushback, 0f, KnockdownType.GroundBounce,
					groundBounceIntoJuggle: true, groundBounceStrength: GroundBounceReactionStrength.Strong);
				break;
			case SpecialReactionKind.PullbackWeak:
			case SpecialReactionKind.PullbackStrong:
				fighter.ApplyHitReaction(resolvedFrames, FighterHitState.Hitstun);
				float pullScale = reaction == SpecialReactionKind.PullbackStrong ? 1.25f : 0.75f;
				fighter.Velocity = new Vector2(-horizontalPushback * pullScale, fighter.Velocity.Y);
				break;
			case SpecialReactionKind.PullbackAir:
				ApplyJuggleHitstun(fighter, resolvedFrames, -horizontalPushback,
					Mathf.Min(fighter.Velocity.Y, -fighter.AirHitPopUpSpeed * 0.35f), false);
				break;
			default:
				fighter.ApplyHitReaction(resolvedFrames, FighterHitState.Hitstun);
				fighter.Velocity = new Vector2(horizontalPushback * 0.35f, fighter.Velocity.Y);
				break;
		}
		SetSpecialReaction(reaction);
	}

	public void ApplyKnockdown(FighterController fighter, int frames, float horizontalPushback,
		float downwardSpeed, KnockdownType knockdownType, float groundBounceSpeed = -1f,
		bool groundBounceIntoJuggle = false,
		GroundBounceReactionStrength groundBounceStrength = GroundBounceReactionStrength.None,
		WallBounceReactionStrength wallBounceStrength = WallBounceReactionStrength.None)
	{
		BeginKnockdown(knockdownType);
		FighterHitState state = ResolveInitialKnockdownState(State.KnockdownType, fighter.IsOnFloor());
		fighter.ApplyHitReaction(frames, state);
		if (State.KnockdownType == KnockdownType.Sweep && fighter.IsOnFloor())
		{
			fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
				new Vector2(horizontalPushback, -Mathf.Abs(fighter.SweepPopUpSpeed)));
			return;
		}
		if (State.KnockdownType == KnockdownType.WallBounce)
		{
			ConfigureWallBounce(wallBounceStrength);
			float direction = Mathf.Abs(horizontalPushback) > 1f ? Mathf.Sign(horizontalPushback) : fighter.Facing;
			float horizontalSpeed = State.WallBounceStrength == WallBounceReactionStrength.Weak
				? fighter.WeakWallBounceHorizontalSpeed
				: fighter.WallBounceHorizontalSpeed;
			fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(new Vector2(direction * horizontalSpeed,
				Mathf.Min(fighter.Velocity.Y, -fighter.GroundBounceSpeed * 0.35f)));
			return;
		}
		if (State.KnockdownType == KnockdownType.GroundBounce)
		{
			float resolvedBounceSpeed = groundBounceSpeed > 0f ? groundBounceSpeed : fighter.GroundBounceSpeed;
			ConfigureGroundBounce(groundBounceStrength, resolvedBounceSpeed, groundBounceIntoJuggle);
			if (fighter.IsOnFloor() && groundBounceIntoJuggle)
			{
				EnterFreshJuggle();
				SetKnockdownType(KnockdownType.None);
				fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(
					new Vector2(horizontalPushback, -resolvedBounceSpeed));
				ClearPendingGroundBounce();
				fighter.QueueStateImpact(FighterHitState.GroundBounce);
				return;
			}
			float verticalBounce = fighter.IsOnFloor()
				? -resolvedBounceSpeed
				: Mathf.Max(fighter.Velocity.Y, downwardSpeed);
			fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, verticalBounce));
			return;
		}
		float vertical = downwardSpeed > 0f ? Mathf.Max(fighter.Velocity.Y, downwardSpeed) : fighter.Velocity.Y;
		fighter.Velocity = fighter.ResolveWallSplatFollowupVelocity(new Vector2(horizontalPushback, vertical));
	}

	public float BeginBlockstun(int frames, float horizontalPushback, GuardReactionStrength strength,
		SpecialReactionKind specialReaction, bool crouchBlocking)
	{
		State.BlockReactionSerial++;
		State.GuardReactionStrength = strength == GuardReactionStrength.None
			? GuardReactionStrength.Medium
			: strength;
		State.HitstunFramesLeft = Mathf.Max(1, frames);
		State.HitState = FighterHitState.Blockstun;
		State.IsCrouchBlocking = crouchBlocking;
		State.SpecialReaction = specialReaction;

		return specialReaction is SpecialReactionKind.GuardPullbackWeak or
			SpecialReactionKind.GuardPullbackStrong or SpecialReactionKind.GuardPullbackAir
			? -horizontalPushback
			: horizontalPushback;
	}
}

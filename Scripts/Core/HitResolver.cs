using Godot;

namespace ModularFighter.Core;

/// <summary>Grounded normal strength tiers. None preserves authored hitstun.</summary>
public enum GroundedNormalStrength { None, Light, Medium, Heavy }

/// <summary>The mutually-exclusive defender reaction selected after a strike is confirmed.</summary>
public enum ResolvedHitReaction
{
	BlowAway,
	Launcher,
	Special,
	Stumble,
	HitFall,
	Knockdown,
	FinalSuperRush,
	FinalSuperKnockdown,
	AirHeavyJuggle,
	ContinuingJuggle,
	AirPop,
	GroundHitstun
}

public readonly record struct HitstunResolutionRequest(
	int AuthoredBaseHitstun,
	bool JumpingHeavyHitGroundedDefender,
	int JumpingHeavyGroundedHitstun,
	GroundedNormalStrength GroundedNormalStrength,
	int GroundedLightHitstun,
	int GroundedMediumHitstun,
	int GroundedHeavyHitstun,
	bool CounterHit,
	int CounterHitBonus,
	bool AttackerStartedAirborne,
	bool DefenderWasGrounded,
	bool CurrentAttackIsNormal,
	int AirToAirBonus,
	int AirToAirNormalAdjustment);

public readonly record struct HitstunResolution(int AuthoredBaseHitstun, int BaseHitstun, int AppliedHitstun);

public readonly record struct PushbackResolutionRequest(
	bool IsLauncher,
	float LauncherPushback,
	bool IsAirborneLightNormal,
	float AirLightPushback,
	float BasePushback,
	bool AttackerStartedAirborne,
	float AirAttackMultiplier,
	bool DefenderWasGrounded,
	bool DefenderIsAirborneJuggle,
	int DefenderJuggleHitCount,
	float JuggleDistanceScalePerHit,
	float MaximumJuggleDistanceScale,
	float GroundToAirMultiplier,
	bool GroundedNormalContinuingJuggle,
	float GroundNormalJuggleMultiplier);

public readonly record struct PushbackResolution(float AppliedPushback, bool GroundedNormalContinuesJuggle);

public readonly record struct HitReactionSelectionRequest(
	bool HasBlowAway,
	bool IsLauncher,
	bool HasUnguardedSpecialReaction,
	HitReactionKind AuthoredHitReaction,
	bool RequestsKnockdown,
	bool FinalSuperRush,
	bool FinalSuperKnockdown,
	bool DefenderWasGrounded,
	bool AttackerStartedAirborne,
	bool IsHeavyAttack,
	bool DefenderAlreadyInJuggle);

/// <summary>
/// Pure combat-number and reaction policy. This class never mutates fighters, consumes hits,
/// performs collision checks, or spawns presentation. FighterController applies its result in
/// the original order after contact, throw, parry, and guard validation.
/// </summary>
public static class HitResolver
{
	/// <summary>
	/// Resolution order is authored base -> grounded overrides -> counter-hit bonus -> air bonuses.
	/// Launcher hitstun deliberately bypasses grounded-normal tier replacement and calls
	/// ResolveModifiedHitstun directly with the authored launch value.
	/// </summary>
	public static HitstunResolution ResolveHitstun(in HitstunResolutionRequest request)
	{
		int baseHitstun = request.AuthoredBaseHitstun;
		if (request.JumpingHeavyHitGroundedDefender)
			baseHitstun = Mathf.Max(1, request.JumpingHeavyGroundedHitstun);
		else
		{
			baseHitstun = request.GroundedNormalStrength switch
			{
				GroundedNormalStrength.Light => Mathf.Max(1, request.GroundedLightHitstun),
				GroundedNormalStrength.Medium => Mathf.Max(1, request.GroundedMediumHitstun),
				GroundedNormalStrength.Heavy => Mathf.Max(1, request.GroundedHeavyHitstun),
				_ => baseHitstun
			};
		}
		int applied = ResolveModifiedHitstun(baseHitstun, request.CounterHit,
			request.CounterHitBonus, request.AttackerStartedAirborne, request.DefenderWasGrounded,
			request.CurrentAttackIsNormal, request.AirToAirBonus, request.AirToAirNormalAdjustment);
		return new HitstunResolution(request.AuthoredBaseHitstun, baseHitstun, applied);
	}

	public static int ResolveModifiedHitstun(int baseHitstun, bool counterHit, int counterHitBonus,
		bool attackerStartedAirborne, bool defenderWasGrounded, bool currentAttackIsNormal,
		int airToAirBonus, int airToAirNormalAdjustment)
	{
		int applied = baseHitstun + (counterHit ? counterHitBonus : 0);
		if (attackerStartedAirborne && !defenderWasGrounded) applied += airToAirBonus;
		if (attackerStartedAirborne && !defenderWasGrounded && currentAttackIsNormal)
			applied = Mathf.Max(1, applied + airToAirNormalAdjustment);
		return applied;
	}

	/// <summary>
	/// Pushback order matches gameplay: launcher/air-light base selection, juggle distance scaling,
	/// ground-to-air scaling, then the extra grounded-normal juggle reduction.
	/// </summary>
	public static PushbackResolution ResolvePushback(in PushbackResolutionRequest request)
	{
		float applied = request.IsLauncher
			? request.LauncherPushback
			: request.IsAirborneLightNormal
				? Mathf.Max(0f, request.AirLightPushback)
				: request.BasePushback * (request.AttackerStartedAirborne ? request.AirAttackMultiplier : 1f);
		if (!request.IsAirborneLightNormal && request.DefenderIsAirborneJuggle)
			applied *= Mathf.Min(request.MaximumJuggleDistanceScale,
				1f + Mathf.Max(0, request.DefenderJuggleHitCount) * request.JuggleDistanceScalePerHit);
		if (!request.AttackerStartedAirborne && !request.DefenderWasGrounded)
			applied *= request.GroundToAirMultiplier;
		if (request.GroundedNormalContinuingJuggle)
			applied *= request.GroundNormalJuggleMultiplier;
		return new PushbackResolution(applied, request.GroundedNormalContinuingJuggle);
	}

	public static int ResolveBlockstun(int authoredBlockstun, int authoredBaseHitstun,
		bool instantBlock)
	{
		int applied = authoredBlockstun > 0 ? authoredBlockstun : Mathf.Max(1, authoredBaseHitstun - 4);
		return instantBlock ? Mathf.Max(1, Mathf.CeilToInt(applied * 0.5f)) : applied;
	}

	public static float ResolveJuggleBounceSpeed(float initialBounceSpeed, int juggleHitCount,
		float decayPerHit, float minimumBounceSpeed) =>
		Mathf.Max(minimumBounceSpeed,
			initialBounceSpeed - Mathf.Max(0, juggleHitCount - 1) * decayPerHit);

	public static float ResolveJuggleGravityScale(int juggleHitCount, int scalingDelayHits,
		float scalePerHit, float maximumScale) =>
		Mathf.Min(Mathf.Max(1f, maximumScale),
			1f + Mathf.Max(0, juggleHitCount - Mathf.Max(0, scalingDelayHits)) * Mathf.Max(0f, scalePerHit));

	/// <summary>
	/// Universal corner wall splats from an existing juggle are reserved for grounded heavy normals.
	/// Authored wall-bounce hitboxes use their separate reaction path and are unaffected.
	/// </summary>
	public static bool CanApplyJuggleWallSplat(bool defenderWasJuggled, bool groundedHeavyNormal, bool blocked) =>
		defenderWasJuggled && groundedHeavyNormal && !blocked;

	/// <summary>
	/// Reaction precedence is intentionally explicit. Changing this order changes gameplay;
	/// add a regression before inserting or moving a branch.
	/// </summary>
	public static ResolvedHitReaction SelectReaction(in HitReactionSelectionRequest request)
	{
		if (request.HasBlowAway) return ResolvedHitReaction.BlowAway;
		if (request.IsLauncher) return ResolvedHitReaction.Launcher;
		if (request.HasUnguardedSpecialReaction) return ResolvedHitReaction.Special;
		if (request.AuthoredHitReaction == HitReactionKind.Stumble) return ResolvedHitReaction.Stumble;
		if (request.AuthoredHitReaction == HitReactionKind.HitFall) return ResolvedHitReaction.HitFall;
		if (request.RequestsKnockdown) return ResolvedHitReaction.Knockdown;
		if (request.FinalSuperRush) return ResolvedHitReaction.FinalSuperRush;
		if (request.FinalSuperKnockdown) return ResolvedHitReaction.FinalSuperKnockdown;
		if (!request.DefenderWasGrounded && request.AttackerStartedAirborne && request.IsHeavyAttack)
			return ResolvedHitReaction.AirHeavyJuggle;
		if (!request.DefenderWasGrounded && request.DefenderAlreadyInJuggle)
			return ResolvedHitReaction.ContinuingJuggle;
		if (!request.DefenderWasGrounded) return ResolvedHitReaction.AirPop;
		return ResolvedHitReaction.GroundHitstun;
	}
}

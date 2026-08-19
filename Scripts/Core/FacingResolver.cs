using Godot;

namespace ModularFighter.Core;

/// <summary>Snapshot of fighter state relevant to facing decisions.</summary>
public readonly record struct FighterFacingState(
	bool FaceOpponentWhenNeutral,
	bool IsGrounded,
	bool IsSuperJumpRoute,
	bool IsAttacking,
	bool HasActiveAbility,
	bool HasHitState,
	bool IsWakingUp,
	bool IsAirAttackLanding,
	bool IsFlightLanding,
	bool IsRunStopSlide,
	bool IsRunCrouchSlide,
	bool HasThrowInteraction,
	bool HasPendingGroundCrossUnderTurn);

/// <summary>
/// Stateless policy for adopting and resolving fighter facing. FighterController remains the
/// owner of the resulting direction; this class only decides whether and where it may turn.
/// </summary>
public static class FacingResolver
{
	private const float SideSwitchDeadZone = 0.5f;

	public static int Normalize(int direction) => direction >= 0 ? 1 : -1;

	public static bool CanAdoptFromNeutral(in FighterFacingState state, FighterInput input)
	{
		return state.FaceOpponentWhenNeutral && state.IsGrounded && !HasCommittedActionInput(input) &&
			!state.IsAttacking && !state.HasActiveAbility && !state.HasHitState && !state.IsWakingUp &&
			!state.IsAirAttackLanding && !state.IsFlightLanding && !state.IsRunStopSlide &&
			!state.IsRunCrouchSlide && !state.HasThrowInteraction;
	}

	public static bool CanAdoptTowardOpponent(in FighterFacingState state, FighterInput input,
		bool opponentIsGrounded)
	{
		if (CanAdoptFromNeutral(state, input))
			return opponentIsGrounded || Mathf.Abs(input.Horizontal) > 0.5f ||
				state.HasPendingGroundCrossUnderTurn;

		// Normal jumps retain takeoff facing. A free super jump may correct after crossing,
		// but an attack, ability, hit reaction, or throw still owns the committed side.
		return state.FaceOpponentWhenNeutral && !state.IsGrounded && state.IsSuperJumpRoute &&
			!HasCommittedActionInput(input) && !state.IsAttacking && !state.HasActiveAbility &&
			!state.HasHitState && !state.HasThrowInteraction;
	}

	public static bool TryResolveOpponentFacing(in FighterFacingState state, FighterInput input,
		bool opponentIsGrounded, float fighterCenterX, float opponentCenterX, int currentFacing,
		out int resolvedFacing)
	{
		resolvedFacing = Normalize(currentFacing);
		if (!CanAdoptTowardOpponent(state, input, opponentIsGrounded)) return false;

		float separation = opponentCenterX - fighterCenterX;
		if (Mathf.Abs(separation) > SideSwitchDeadZone)
			resolvedFacing = separation > 0f ? 1 : -1;
		return true;
	}

	public static bool TryResolveMovementFacing(in FighterFacingState state, FighterInput input,
		bool faceWithMovement, out int resolvedFacing)
	{
		resolvedFacing = 1;
		if (!faceWithMovement || Mathf.IsZeroApprox(input.Horizontal) ||
			!CanAdoptFromNeutral(state, input)) return false;
		resolvedFacing = input.Horizontal > 0f ? 1 : -1;
		return true;
	}

	private static bool HasCommittedActionInput(FighterInput input) =>
		input.DashPressed || input.JumpPressed || input.FlightPressed ||
		input.LightPunchPressed || input.LightKickPressed ||
		input.HeavyPunchPressed || input.HeavyKickPressed ||
		input.Special1Pressed || input.Special2Pressed;
}

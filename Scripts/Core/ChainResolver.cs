namespace ModularFighter.Core;

/// <summary>All already-resolved facts needed to decide one attack-chain transition.</summary>
public readonly record struct ChainResolutionContext(
	bool IsShortHopNormalChain,
	int NextMoveMaximumUses,
	int NextMoveUseCount,
	bool IsRekkaFollowup,
	bool RekkaStartupComplete,
	bool IsCommandRunFollowup,
	bool NextMoveIsSpecial,
	bool CurrentMoveCanChainToSpecial,
	bool ChainRequiresContact,
	bool CurrentMoveHasHit,
	bool IsInsideCurrentMoveWindow,
	bool GlobalSpecialCancelAllowed,
	bool AuthoredNormalTargetAllowed);

/// <summary>
/// Stateless attack-chain policy. Move lookup, counters, and fighter state remain with the
/// controller; this resolver owns the order and outcome of the chain rules.
/// </summary>
public static class ChainResolver
{
	public static bool CanChain(in ChainResolutionContext context)
	{
		if (context.IsShortHopNormalChain) return false;
		if (context.NextMoveMaximumUses > 0 && context.NextMoveUseCount >= context.NextMoveMaximumUses)
			return false;
		if (context.IsRekkaFollowup) return context.RekkaStartupComplete;
		if (context.IsCommandRunFollowup) return true;

		if (context.NextMoveIsSpecial)
		{
			if (CanUseAuthoredSpecialChain(context.CurrentMoveCanChainToSpecial,
				context.ChainRequiresContact, context.CurrentMoveHasHit,
				context.IsInsideCurrentMoveWindow)) return true;
			return context.GlobalSpecialCancelAllowed;
		}

		if (context.ChainRequiresContact && !context.CurrentMoveHasHit) return false;
		if (!context.IsInsideCurrentMoveWindow) return false;
		return context.AuthoredNormalTargetAllowed;
	}

	public static bool CanUseAuthoredSpecialChain(bool canChainToSpecial, bool requiresContact,
		bool currentMoveHasHit, bool isInsideCurrentMoveWindow) =>
		canChainToSpecial && (!requiresContact || currentMoveHasHit) && isInsideCurrentMoveWindow;

	public static bool IsWithinCancelWindow(int windowStartFrame, int windowEndFrame,
		int earliestActiveFramesLeft, int startupFrames, int activeFrames, int recoveryFrames,
		int startupFramesLeft, int activeFramesLeft, int recoveryFramesLeft)
	{
		if (windowStartFrame >= 0 || windowEndFrame >= 0)
		{
			int totalFrames = startupFrames + activeFrames + recoveryFrames;
			int remainingFrames = startupFramesLeft + activeFramesLeft + recoveryFramesLeft;
			int elapsedFrames = totalFrames - remainingFrames;
			if (windowStartFrame >= 0 && elapsedFrames < windowStartFrame) return false;
			if (windowEndFrame >= 0 && elapsedFrames > windowEndFrame) return false;
			return true;
		}

		if (startupFramesLeft > 0) return false;
		return activeFramesLeft <= earliestActiveFramesLeft;
	}
}

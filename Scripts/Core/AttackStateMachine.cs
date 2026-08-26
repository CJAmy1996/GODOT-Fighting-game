namespace ModularFighter.Core;

/// <summary>
/// Owns the deterministic frame timeline for one attack. Combat rules remain in
/// FighterController; this class only advances and redirects attack phases.
/// </summary>
public sealed class AttackStateMachine
{
	public int StartupFrames { get; private set; }
	public int ActiveFrames { get; private set; }
	public int RecoveryFrames { get; private set; }
	public int StartupFramesLeft { get; private set; }
	public int ActiveFramesLeft { get; private set; }
	public int RecoveryFramesLeft { get; private set; }
	public int Frame { get; private set; }

	public bool IsAttacking => StartupFramesLeft > 0 || ActiveFramesLeft > 0 || RecoveryFramesLeft > 0;
	public bool IsActive => IsAttacking && StartupFramesLeft <= 0 && ActiveFramesLeft > 0;
	public bool IsRecovering => IsAttacking && RecoveryFramesLeft > 0;

	public void Begin(int startupFrames, int activeFrames, int recoveryFrames)
	{
		StartupFrames = startupFrames;
		ActiveFrames = activeFrames;
		RecoveryFrames = recoveryFrames;
		Frame = -1;
		StartupFramesLeft = startupFrames;
		ActiveFramesLeft = startupFrames <= 0 ? activeFrames : 0;
		RecoveryFramesLeft = 0;
	}

	public AttackTimelineTickResult Tick(bool holdStartup, bool holdActive, bool holdRecovery)
	{
		bool enteredActive = false;
		if (StartupFramesLeft > 0)
		{
			if (holdStartup) return new(false, false, true);
			StartupFramesLeft--;
			if (StartupFramesLeft == 0)
			{
				ActiveFramesLeft = ActiveFrames;
				enteredActive = true;
			}
		}
		else if (ActiveFramesLeft > 0)
		{
			if (!holdActive)
			{
				ActiveFramesLeft--;
				// The extra step preserves the final authored recovery frame for a full tick.
				if (ActiveFramesLeft == 0) RecoveryFramesLeft = RecoveryFrames + 1;
			}
		}
		else if (RecoveryFramesLeft > 0)
		{
			if (!holdRecovery) RecoveryFramesLeft--;
			if (RecoveryFramesLeft == 0) return new(false, true, false);
		}

		return new(enteredActive, false, false);
	}

	public void AdvanceFrame()
	{
		if (IsAttacking) Frame++;
	}

	public void EndActiveIntoRecovery()
	{
		ActiveFramesLeft = 0;
		RecoveryFramesLeft = RecoveryFrames + 1;
	}

	public void BeginRecovery(int recoveryFrames)
	{
		StartupFramesLeft = 0;
		ActiveFramesLeft = 0;
		RecoveryFrames = recoveryFrames;
		RecoveryFramesLeft = recoveryFrames;
	}

	public void BeginLandingRecovery(int activeFrames, int recoveryFrames)
	{
		ActiveFrames = activeFrames;
		BeginRecovery(recoveryFrames);
	}

	public void ExtendActiveAtLeast(int activeFrames) => ActiveFramesLeft = System.Math.Max(ActiveFramesLeft, activeFrames);

	public void EndActiveWithMinimumRecovery()
	{
		ActiveFramesLeft = 0;
		RecoveryFramesLeft = System.Math.Max(RecoveryFramesLeft, RecoveryFrames + 1);
	}

	public void Clear()
	{
		StartupFrames = 0;
		ActiveFrames = 0;
		RecoveryFrames = 0;
		StartupFramesLeft = 0;
		ActiveFramesLeft = 0;
		RecoveryFramesLeft = 0;
		Frame = 0;
	}
}

public readonly record struct AttackTimelineTickResult(bool EnteredActive, bool Completed, bool HeldStartup);

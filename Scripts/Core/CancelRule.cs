using System;
using Godot;

namespace ModularFighter.Core;

public enum CancelKind
{
	Chain,
	Special,
	Jump,
	AirDash,
	Crouch
}

/// <summary>Reusable cancel permission. Add these to a fighter definition instead of hard-coding cancel behavior per move.</summary>
[GlobalClass]
public partial class CancelRule : Resource
{
	[Export] public string FromMove { get; set; } = "ANY_NORMAL";
	[Export] public CancelKind Kind { get; set; } = CancelKind.Special;
	[Export] public bool RequiresContact { get; set; } = true;
	[Export] public int StartFrame { get; set; }
	[Export] public int EndFrame { get; set; } = -1;
	[Export] public int EarliestActiveFramesLeft { get; set; }
	[Export] public string[] AllowedTargets { get; set; } = Array.Empty<string>();

	public bool Allows(string currentMove, string targetMove, CancelKind kind, bool currentMoveIsNormal, bool hasContact,
		int elapsedFrames, int startupFramesLeft, int activeFramesLeft)
	{
		if (Kind != kind) return false;
		if (RequiresContact && !hasContact) return false;
		if (!MatchesFrom(currentMove, currentMoveIsNormal)) return false;
		if (!AllowsTarget(targetMove)) return false;
		if (StartFrame >= 0 && elapsedFrames < StartFrame) return false;
		if (EndFrame >= 0 && elapsedFrames > EndFrame) return false;
		if (StartFrame < 0 && EndFrame < 0)
		{
			if (startupFramesLeft > 0) return false;
			if (activeFramesLeft > EarliestActiveFramesLeft) return false;
		}
		return true;
	}

	private bool MatchesFrom(string currentMove, bool currentMoveIsNormal)
	{
		string token = FromMove?.Trim().ToUpperInvariant() ?? "ANY_NORMAL";
		if (token == "" || token == "ANY") return true;
		if (token == "ANY_NORMAL" || token == "NORMAL") return currentMoveIsNormal;
		return NormalMoveData.MatchesAttackToken(token, currentMove);
	}

	private bool AllowsTarget(string targetMove)
	{
		if (AllowedTargets == null || AllowedTargets.Length == 0) return true;
		foreach (string target in AllowedTargets)
			if (NormalMoveData.MatchesAttackToken(target, targetMove)) return true;
		return false;
	}
}

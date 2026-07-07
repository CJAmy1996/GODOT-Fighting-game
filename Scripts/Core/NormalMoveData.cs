using System;
using Godot;

namespace ModularFighter.Core;

public enum NormalMoveStance
{
	Any,
	Standing,
	Crouching,
	Airborne
}

public enum HitReactionKind
{
	Normal,
	Tumble,
	Knockdown,
	WallBounce,
	GroundBounce,
	Crumple
}

public enum KnockdownType
{
	None,
	Sweep,
	AirKnockdown,
	HardKnockdown,
	SoftKnockdown,
	WallBounce,
	GroundBounce,
	Crumple
}

/// <summary>
/// Per-normal combo/cancel/launcher rule data. Designers should make one of these
/// for any normal that differs from the character's default behavior.
/// </summary>
[GlobalClass]
public partial class NormalMoveData : Resource
{
	/// <summary>
	/// Use exact names like "LIGHT PUNCH" or broad names like "LIGHT", "HEAVY", "SPECIAL", or "ANY".
	/// </summary>
	[Export] public string AttackName { get; set; } = "ANY";
	[Export] public NormalMoveStance Stance { get; set; } = NormalMoveStance.Any;

	[ExportGroup("Chains")]
	[Export] public bool CanChainToLight { get; set; }
	[Export] public bool CanChainToHeavy { get; set; }
	[Export] public bool CanChainToSpecial { get; set; }
	[Export] public string[] AllowedChainTargets { get; set; } = Array.Empty<string>();
	[Export] public int MaxUsesPerCombo { get; set; }
	[Export] public bool ChainRequiresContact { get; set; } = true;
	[Export] public int ChainEarliestActiveFramesLeft { get; set; }
	[Export] public int CancelWindowStartFrame { get; set; } = -1;
	[Export] public int CancelWindowEndFrame { get; set; } = -1;

	[ExportGroup("Hit Feel / Combat Data")]
	[Export] public int Damage { get; set; }
	[Export] public int HitstunFrames { get; set; } = -1;
	[Export] public int BlockstunFrames { get; set; } = -1;
	[Export] public int HitstopFrames { get; set; } = -1;
	[Export] public float Pushback { get; set; } = -1f;
	[Export] public HitReactionKind HitReaction { get; set; } = HitReactionKind.Normal;
	[Export] public KnockdownType KnockdownType { get; set; } = KnockdownType.None;
	[Export] public bool KnocksDown { get; set; }
	[Export] public int KnockdownFrames { get; set; }
	[Export] public bool CanHitGroundedKnockdown { get; set; }

	[ExportGroup("Box Timeline")]
	[Export] public FighterBoxFrame[] BoxTimeline { get; set; } = Array.Empty<FighterBoxFrame>();

	[ExportGroup("Launcher / Jump Cancel")]
	[Export] public bool Launches { get; set; }
	[Export] public float LaunchSpeed { get; set; } = 1820f;
	[Export] public float LaunchPushback { get; set; } = 180f;
	[Export] public int LaunchHitstunFrames { get; set; } = 72;
	[Export] public int JumpCancelWindowFrames { get; set; } = 30;
	[Export] public float ChaseJumpSpeed { get; set; } = 1820f;
	[Export] public float ChaseForwardSpeed { get; set; } = 360f;

	public bool Matches(string currentAttackName, bool startedCrouching, bool startedAirborne)
	{
		if (Stance == NormalMoveStance.Airborne && !startedAirborne) return false;
		if (Stance == NormalMoveStance.Crouching && (startedAirborne || !startedCrouching)) return false;
		if (Stance == NormalMoveStance.Standing && (startedAirborne || startedCrouching)) return false;

		string ruleName = AttackName?.Trim().ToUpperInvariant() ?? "ANY";
		string moveName = currentAttackName.ToUpperInvariant();
		if (ruleName == "" || ruleName == "ANY") return true;
		if (ruleName == "LIGHT" || ruleName == "HEAVY" || ruleName == "SPECIAL") return moveName.StartsWith(ruleName);
		return moveName == ruleName;
	}

	public bool AllowsChainTo(string nextAttackName, bool nextStartedCrouching, bool nextStartedAirborne)
	{
		if (AllowedChainTargets != null && AllowedChainTargets.Length > 0)
		{
			foreach (string target in AllowedChainTargets)
				if (MatchesAttackToken(target, nextAttackName, nextStartedCrouching, nextStartedAirborne)) return true;
			return false;
		}

		string next = nextAttackName.ToUpperInvariant();
		return (next.StartsWith("LIGHT") && CanChainToLight) ||
			(next.StartsWith("HEAVY") && CanChainToHeavy) ||
			(next.StartsWith("SPECIAL") && CanChainToSpecial);
	}

	internal static bool MatchesAttackToken(string token, string attackName) =>
		MatchesAttackToken(token, attackName, false, false);

	internal static bool MatchesAttackToken(string token, string attackName, bool startedCrouching, bool startedAirborne)
	{
		string ruleName = token?.Trim().ToUpperInvariant() ?? "";
		if (ruleName.StartsWith("STANDING "))
		{
			if (startedAirborne || startedCrouching) return false;
			ruleName = ruleName["STANDING ".Length..];
		}
		else if (ruleName.StartsWith("CROUCHING ") || ruleName.StartsWith("CROUCH "))
		{
			string prefix = ruleName.StartsWith("CROUCHING ") ? "CROUCHING " : "CROUCH ";
			if (startedAirborne || !startedCrouching) return false;
			ruleName = ruleName[prefix.Length..];
		}
		else if (ruleName.StartsWith("AIRBORNE ") || ruleName.StartsWith("AIR "))
		{
			string prefix = ruleName.StartsWith("AIRBORNE ") ? "AIRBORNE " : "AIR ";
			if (!startedAirborne) return false;
			ruleName = ruleName[prefix.Length..];
		}

		string moveName = attackName.ToUpperInvariant();
		if (ruleName == "" || ruleName == "ANY") return true;
		if (ruleName == "LIGHT" || ruleName == "HEAVY" || ruleName == "SPECIAL") return moveName.StartsWith(ruleName);
		return moveName == ruleName;
	}
}

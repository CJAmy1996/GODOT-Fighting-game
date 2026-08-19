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
	Crumple,
	/// <summary>A low airborne trip that holds the victim in hitstun until landing, then knocks down.</summary>
	Stumble,
	/// <summary>An airborne hurt reaction that forces the victim downward into landing knockdown.</summary>
	HitFall
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

public enum BlowAwayDirection
{
	None,
	Horizontal,
	Vertical,
	Diagonal,
	Downward,
	DiagonalDown
}

public enum BlowAwayStrength
{
	None,
	Weak,
	Medium,
	Strong
}

public enum WallBounceReactionStrength
{
	None,
	Weak,
	Strong
}

public enum GroundBounceReactionStrength
{
	None,
	Weak,
	Medium,
	Strong
}

public enum GuardReactionStrength
{
	None,
	Weak,
	Medium,
	Strong,
	SpecialStrong
}

public enum SpecialReactionKind
{
	None,
	Stagger,
	SlideDownHorizontal,
	SlideDownDiagonal,
	SlideDowned,
	DiagonalBounce,
	PullbackWeak,
	PullbackStrong,
	GuardPullbackWeak,
	GuardPullbackStrong,
	PullbackAir,
	GuardPullbackAir
}

/// <summary>
/// Per-normal combo/cancel/launcher rule data. Designers should make one of these
/// for any normal that differs from the character's default behavior.
/// </summary>
[Tool, GlobalClass]
public partial class NormalMoveData : Resource
{
	/// <summary>
	/// Use exact names like "LIGHT PUNCH" or broad names like "LIGHT", "HEAVY", "SPECIAL", or "ANY".
	/// </summary>
	[Export] public string AttackName { get; set; } = "ANY";
	[Export] public string AnimationName { get; set; } = "";
	[Export] public NormalMoveStance Stance { get; set; } = NormalMoveStance.Any;

	[ExportGroup("60 Hz Timeline")]
	[Export] public int StartupFrames { get; set; } = -1;
	[Export] public int ActiveFrames { get; set; } = -1;
	[Export] public int RecoveryFrames { get; set; } = -1;
	[Export] public bool SuppressFallbackHitbox { get; set; }
	/// <summary>
	/// Optional per-game-frame list of authored animation ticks to display. This
	/// lets a move hold, repeat, or reverse specific poses without changing its
	/// 60 Hz combat timing. Empty keeps the SpriteFrames-authored timing.
	/// </summary>
	[Export] public int[] AnimationSourceTimeline { get; set; } = Array.Empty<int>();
	/// <summary>Optional naturally-playing animation used after this game frame.</summary>
	[Export] public string AnimationTailName { get; set; } = "";
	[Export] public int AnimationTailStartFrame { get; set; } = -1;
	/// <summary>Optional naturally-looping animation shown only while the combat active counter is running.</summary>
	[Export] public string ActiveLoopAnimationName { get; set; } = "";
	/// <summary>Optional recovery lock used only after a regular throw connects.</summary>
	[Export] public int ConnectedThrowRecoveryFrames { get; set; }
	[Export] public string AirAttackLandingAnimationName { get; set; } = "";
	[Export] public int AirAttackLandingFrames { get; set; }
	[ExportGroup("Character Presentation")]
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float CharacterVisualScale { get; set; } = 1f;
	[Export] public Vector2 CharacterVisualOffset { get; set; } = Vector2.Zero;
	/// <summary>
	/// Original per-drawing bitmap-origin shifts, normalized against drawing zero.
	/// These move only presentation art; the fighter and collision origin stay fixed.
	/// </summary>
	[Export] public Vector2[] AnimationDrawingOffsets { get; set; } = Array.Empty<Vector2>();

	[ExportGroup("Visual Effect")]
	[Export] public SpriteFrames EffectSpriteFrames { get; set; }
	[Export] public string EffectAnimationName { get; set; } = "";
	[Export] public int EffectSpawnFrame { get; set; } = -1;
	[Export] public Vector2 EffectSpawnOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 EffectVisualOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 EffectScale { get; set; } = Vector2.One;
	[Export] public bool EffectRequiresFullCharge { get; set; }
	[Export] public bool EffectAdditiveBlend { get; set; }
	/// <summary>Discard the opaque near-black backing used by legacy additive effect sheets.</summary>
	[Export] public bool EffectBlackKey { get; set; }
	/// <summary>Spawn the effect from the exact hitbox/hurtbox intersection instead of the move timeline.</summary>
	[Export] public bool EffectSpawnOnHitContact { get; set; }
	/// <summary>Temporarily render the struck fighter as a solid black silhouette on confirmed hit.</summary>
	[Export] public bool EffectBlackensDefender { get; set; }
	[Export(PropertyHint.Range, "1,60,1")] public int EffectBlackSilhouetteFrames { get; set; } = 8;
	[Export] public SpriteFrames EffectDefenderFireSpriteFrames { get; set; }
	[Export] public string EffectDefenderFireAnimationName { get; set; } = "";
	[Export] public PackedScene HitSparkScene { get; set; }

	[ExportGroup("Charge")]
	[Export] public bool Chargeable { get; set; }
	[Export(PropertyHint.Range, "1,300,1")] public int MaxChargeFrames { get; set; } = 45;

	[ExportGroup("Chains")]
	[Export] public bool CanChainToLight { get; set; }
	[Export] public bool CanChainToHeavy { get; set; }
	[Export] public bool CanChainToSpecial { get; set; }
	[Export] public string[] AllowedChainTargets { get; set; } = Array.Empty<string>();
	/// <summary>Optional MVC-style second LP result, used to expose a medium without a direction chord.</summary>
	[Export] public string RepeatLightPunchChainTarget { get; set; } = "";
	[Export] public string RepeatLightKickChainTarget { get; set; } = "";
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
	/// <summary>Air hits retain the victim's vertical momentum instead of applying the universal pop-up.</summary>
	[Export] public bool PreserveAirborneTargetVelocity { get; set; }
	[Export] public float ShakeStrength { get; set; } = -1f;
	[Export] public HitReactionKind HitReaction { get; set; } = HitReactionKind.Normal;
	[Export] public KnockdownType KnockdownType { get; set; } = KnockdownType.None;
	[Export] public bool KnocksDown { get; set; }
	[Export] public int KnockdownFrames { get; set; }
	[Export] public bool CanHitGroundedKnockdown { get; set; }
	[Export] public GuardReactionStrength GuardReactionStrength { get; set; } = GuardReactionStrength.None;
	[Export] public SpecialReactionKind SpecialReaction { get; set; } = SpecialReactionKind.None;

	[ExportGroup("Blow Away Reaction")]
	[Export] public BlowAwayDirection BlowAwayDirection { get; set; } = BlowAwayDirection.None;
	[Export] public BlowAwayStrength BlowAwayStrength { get; set; } = BlowAwayStrength.None;
	/// <summary>Optional exact launch speed; a negative value uses the selected weak/medium/strong preset.</summary>
	[Export] public float BlowAwaySpeed { get; set; } = -1f;
	[Export] public bool BlowAwayNoBounce { get; set; }

	[ExportGroup("Wall Bounce Reaction")]
	[Export] public WallBounceReactionStrength WallBounceStrength { get; set; } = WallBounceReactionStrength.None;

	[ExportGroup("Ground Bounce Reaction")]
	[Export] public GroundBounceReactionStrength GroundBounceStrength { get; set; } = GroundBounceReactionStrength.None;

	[ExportGroup("Box Timeline")]
	[Export] public FighterBoxFrame[] BoxTimeline { get; set; } = Array.Empty<FighterBoxFrame>();

	/// <summary>Adds an initialized hitbox, hurtbox, or other combat box to this attack.</summary>
	public FighterBoxFrame AddBox(FighterBoxFrame box)
	{
		if (box == null) throw new ArgumentNullException(nameof(box));
		var boxes = new FighterBoxFrame[(BoxTimeline?.Length ?? 0) + 1];
		if (BoxTimeline != null) System.Array.Copy(BoxTimeline, boxes, BoxTimeline.Length);
		boxes[^1] = box;
		BoxTimeline = boxes;
		return box;
	}

	/// <summary>
	/// Creates timeline data from a CollisionShape2D node and adds it to this attack.
	/// This is useful for shapes positioned visually in a character or move scene.
	/// </summary>
	public FighterBoxFrame AddBox(CollisionShape2D shapeNode, FighterBoxKind kind,
		int startFrame = 0, int endFrame = -1, bool mirrorWithFacing = true, string tag = "") =>
		AddBox(FighterBoxFrame.FromCollisionShape(shapeNode, kind, startFrame, endFrame, mirrorWithFacing, tag));

	[ExportGroup("Launcher / Jump Cancel")]
	[Export] public bool Launches { get; set; }
	/// <summary>Applies the authored launch only when the defender was grounded on contact.</summary>
	[Export] public bool LaunchGroundedOnly { get; set; }
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
		string moveName = attackName.ToUpperInvariant();
		// Authored move names may themselves begin with a stance word (for example
		// CROUCHING MEDIUM KICK). Preserve an exact canonical-name match before
		// interpreting STANDING/CROUCHING/AIR as shorthand target qualifiers.
		if (ruleName == moveName) return true;
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

		if (ruleName == "" || ruleName == "ANY") return true;
		if (ruleName == "LIGHT" || ruleName == "HEAVY" || ruleName == "SPECIAL") return moveName.StartsWith(ruleName);
		return moveName == ruleName;
	}
}

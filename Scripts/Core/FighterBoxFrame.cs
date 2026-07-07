using Godot;

namespace ModularFighter.Core;

public enum FighterBoxKind
{
	Hurtbox,
	Hitbox,
	Pushbox,
	Throwbox,
	ThrowHurtbox,
	Clashbox,
	ProximityBlockbox
}

[GlobalClass]
public partial class FighterBoxFrame : Resource
{
	private const int UseMoveDefaultInt = -1;
	private const float UseMoveDefaultFloat = -1f;

	[Export] public FighterBoxKind Kind { get; set; } = FighterBoxKind.Hurtbox;
	[Export] public int StartFrame { get; set; }
	[Export] public int EndFrame { get; set; } = -1;
	[Export] public Rect2 LocalRect { get; set; } = new(-24f, -72f, 48f, 96f);
	[Export] public bool MirrorWithFacing { get; set; } = true;
	[Export] public string Tag { get; set; } = "";

	[ExportGroup("Hit Overrides")]
	[Export] public int Damage { get; set; } = UseMoveDefaultInt;
	[Export] public int HitstunFrames { get; set; } = UseMoveDefaultInt;
	[Export] public int BlockstunFrames { get; set; } = UseMoveDefaultInt;
	[Export] public int HitstopFrames { get; set; } = UseMoveDefaultInt;
	[Export] public float Pushback { get; set; } = UseMoveDefaultFloat;
	[Export] public float ShakeStrength { get; set; } = UseMoveDefaultFloat;
	[Export] public HitReactionKind HitReaction { get; set; } = HitReactionKind.Normal;
	[Export] public KnockdownType KnockdownType { get; set; } = KnockdownType.None;
	[Export] public bool KnocksDown { get; set; }
	[Export] public int KnockdownFrames { get; set; } = UseMoveDefaultInt;
	[Export] public bool CanHitGroundedKnockdown { get; set; }

	[ExportGroup("Launcher Overrides")]
	[Export] public bool Launches { get; set; }
	[Export] public float LaunchSpeed { get; set; } = UseMoveDefaultFloat;
	[Export] public float LaunchPushback { get; set; } = UseMoveDefaultFloat;
	[Export] public int LaunchHitstunFrames { get; set; } = UseMoveDefaultInt;
	[Export] public int JumpCancelWindowFrames { get; set; } = UseMoveDefaultInt;

	public bool IsActiveOnFrame(int frame)
	{
		if (frame < StartFrame) return false;
		return EndFrame < 0 || frame <= EndFrame;
	}
}

public readonly struct ActiveFighterBox
{
	public ActiveFighterBox(Rect2 rect, FighterBoxFrame source = null)
	{
		Rect = rect;
		Source = source;
	}

	public Rect2 Rect { get; }
	public FighterBoxFrame Source { get; }
}

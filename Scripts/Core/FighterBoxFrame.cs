using Godot;
using System;

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

[Flags]
public enum FighterBoxAttribute
{
	None = 0,
	Strike = 1 << 0,
	Projectile = 1 << 1,
	Throw = 1 << 2,
	Body = 1 << 3,
	Proximity = 1 << 4
}

public enum FighterAttackLevel
{
	Any,
	High,
	Mid,
	Low,
	Overhead,
	Air
}

[Tool, GlobalClass]
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
	[Export] public bool ReplacesSameKindWhileActive { get; set; }

	[ExportGroup("Interaction")]
	[Export] public FighterBoxAttribute Attributes { get; set; } = FighterBoxAttribute.Strike;
	[Export] public FighterBoxAttribute InteractsWith { get; set; } = FighterBoxAttribute.Strike;
	[Export] public FighterAttackLevel AttackLevel { get; set; } = FighterAttackLevel.Mid;
	[Export] public bool ReceivesHits { get; set; } = true;
	[Export] public bool CanClash { get; set; }
	[Export] public int Priority { get; set; }

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

	/// <summary>
	/// Initializes this timeline entry from a CollisionShape2D authored in the Godot editor.
	/// The node's transformed local bounds are stored so the deterministic combat resolver can
	/// continue to use Rect2 data at runtime.
	/// </summary>
	public FighterBoxFrame InitializeFrom(CollisionShape2D shapeNode, FighterBoxKind kind,
		int startFrame = 0, int endFrame = -1, bool mirrorWithFacing = true, string tag = "")
	{
		if (shapeNode?.Shape == null)
			throw new ArgumentException("A CollisionShape2D with a Shape resource is required.", nameof(shapeNode));

		Kind = kind;
		StartFrame = startFrame;
		EndFrame = endFrame;
		LocalRect = GetTransformedBounds(shapeNode.Shape.GetRect(), shapeNode.Transform);
		MirrorWithFacing = mirrorWithFacing;
		Tag = tag ?? "";
		return this;
	}

	public static FighterBoxFrame FromCollisionShape(CollisionShape2D shapeNode, FighterBoxKind kind,
		int startFrame = 0, int endFrame = -1, bool mirrorWithFacing = true, string tag = "") =>
		new FighterBoxFrame().InitializeFrom(shapeNode, kind, startFrame, endFrame, mirrorWithFacing, tag);

	public bool IsActiveOnFrame(int frame)
	{
		if (frame < StartFrame) return false;
		return EndFrame < 0 || frame <= EndFrame;
	}

	public bool CanInteractWith(FighterBoxFrame other)
	{
		if (other == null) return true;
		if (!ReceivesHits || !other.ReceivesHits) return false;
		return (Attributes & other.InteractsWith) != FighterBoxAttribute.None &&
			(other.Attributes & InteractsWith) != FighterBoxAttribute.None;
	}

	private static Rect2 GetTransformedBounds(Rect2 bounds, Transform2D transform)
	{
		Vector2 a = transform * bounds.Position;
		Vector2 b = transform * new Vector2(bounds.End.X, bounds.Position.Y);
		Vector2 c = transform * bounds.End;
		Vector2 d = transform * new Vector2(bounds.Position.X, bounds.End.Y);
		float left = Mathf.Min(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X));
		float top = Mathf.Min(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y));
		float right = Mathf.Max(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X));
		float bottom = Mathf.Max(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y));
		return new Rect2(left, top, right - left, bottom - top);
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

	public bool CanInteractWith(ActiveFighterBox other)
	{
		if (Source == null || other.Source == null) return true;
		return Source.CanInteractWith(other.Source);
	}
}

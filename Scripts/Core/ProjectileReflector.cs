using Godot;

namespace ModularFighter.Core;

/// <summary>
/// Sanzou's animated Aegis-style reflector. The block special is one active
/// animation cycle and one knockdown hit; the super floats for ten seconds,
/// can strike eight times, and reflects enemy projectiles.
/// </summary>
public partial class ProjectileReflector : BasicProjectile
{
	[Export] public Rect2 ReflectBox { get; set; } = new(-22f, -74f, 44f, 148f);
	[Export] public AnimatedSprite2D Visual { get; set; }
	[ExportGroup("Block Special")]
	[Export] public int OneCycleLifetimeFrames { get; set; } = 8;
	[Export] public int BlockHitstunFrames { get; set; } = 24;
	[Export] public float BlockPushback { get; set; } = 980f;
	[Export] public int BlockHitstopFrames { get; set; } = 10;
	[Export] public int BlockKnockdownFrames { get; set; } = 52;
	[ExportGroup("Super Reflector")]
	[Export] public int SuperLifetimeFrames { get; set; } = 600;
	[Export] public int SuperHitCount { get; set; } = 8;
	[Export] public int SuperHitCooldownFrames { get; set; } = 5;
	[Export] public float SuperSlideSpeed { get; set; } = 18f;
	[Export] public int SuperHitstunFrames { get; set; } = 12;
	[Export] public float SuperPushback { get; set; } = 170f;
	[Export] public int SuperHitstopFrames { get; set; } = 1;
	[Export] public float HoverAmplitude { get; set; } = 0f;
	[Export] public int HoverPeriodFrames { get; set; } = 120;

	public bool IsSuperReflector { get; private set; }
	public int ReflectedProjectileCount { get; private set; }
	private int _ageFrames;

	public override void _Ready()
	{
		base._Ready();
		Visual ??= GetNodeOrNull<AnimatedSprite2D>("Visual");
		if (Visual != null)
		{
			Visual.FlipH = Direction < 0;
			Visual.Play("reflector");
			AlignVisualToGround();
		}
	}

	public void Initialize(FighterController owner) => Initialize(owner, owner?.Facing ?? 1, false);

	public void Initialize(FighterController owner, int direction, bool super)
	{
		IsSuperReflector = super;
		HitboxLocal = ReflectBox;
		LifetimeFrames = super ? SuperLifetimeFrames : OneCycleLifetimeFrames;
		base.Initialize(owner, direction, super ? SuperSlideSpeed : 0f,
			super ? SuperHitstunFrames : BlockHitstunFrames,
			super ? SuperPushback : BlockPushback,
			super ? SuperHitstopFrames : BlockHitstopFrames,
			super ? 4.5f : 7f,
			heavy: true,
			hits: super ? SuperHitCount : 1,
			hitCooldownFrames: super ? SuperHitCooldownFrames : 1,
			super: super,
			finalHitKnocksDown: !super,
			finalKnockdownType: KnockdownType.SoftKnockdown,
			finalKnockdownFrames: !super ? BlockKnockdownFrames : 0,
			latchOnMultiHit: false);
		if (Visual != null) Visual.FlipH = Direction < 0;
	}

	public override void _PhysicsProcess(double delta)
	{
		_ageFrames++;
		base._PhysicsProcess(delta);
		if (Visual != null)
		{
			float period = Mathf.Max(1, HoverPeriodFrames);
			AlignVisualToGround(Mathf.Sin(_ageFrames * Mathf.Tau / period) * HoverAmplitude);
		}
		if (!IsSuperReflector) return;

		foreach (Node node in GetTree().GetNodesInGroup(ProjectileGroup))
		{
			if (node == this || node is not BasicProjectile projectile || projectile is ProjectileReflector ||
				projectile.OwnerFighter == OwnerFighter || projectile.IsQueuedForDeletion()) continue;
			Rect2 worldReflectBox = new(GlobalPosition + ReflectBox.Position, ReflectBox.Size);
			if (worldReflectBox.Intersects(projectile.WorldHitbox))
			{
				projectile.Reflect(OwnerFighter, OwnerFighter?.Facing ?? 1);
				ReflectedProjectileCount++;
			}
		}
	}

	private void AlignVisualToGround(float hoverOffset = 0f)
	{
		Texture2D texture = Visual?.SpriteFrames?.GetFrameTexture(Visual.Animation, Visual.Frame);
		if (texture == null) return;
		float scaledHeight = texture.GetHeight() * Mathf.Abs(Visual.Scale.Y);
		Visual.Position = new Vector2(0f, -scaledHeight * 0.5f + hoverOffset);
	}

	public override void _Draw() { }
}

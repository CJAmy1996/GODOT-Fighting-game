using Godot;

namespace ModularFighter.Core;

public partial class BasicProjectile : Node2D
{
	public const string ProjectileGroup = "fighter_projectiles";

	[Export] public Rect2 HitboxLocal { get; set; } = new(-18f, -18f, 36f, 36f);
	[Export] public int LifetimeFrames { get; set; } = 90;

	public FighterController OwnerFighter { get; private set; }
	public int Direction { get; private set; } = 1;
	public float Speed { get; private set; } = 760f;
	public int HitstunFrames { get; private set; } = 18;
	public float Pushback { get; private set; } = 520f;
	public int HitstopFrames { get; private set; } = 6;
	public float ShakeStrength { get; private set; } = 3f;
	public bool Heavy { get; private set; }
	public bool HasHit { get; private set; }
	public Rect2 WorldHitbox => new(GlobalPosition + HitboxLocal.Position, HitboxLocal.Size);

	public override void _Ready()
	{
		AddToGroup(ProjectileGroup);
	}

	public void Initialize(FighterController owner, int direction, float speed, int hitstunFrames, float pushback, int hitstopFrames, float shakeStrength, bool heavy)
	{
		OwnerFighter = owner;
		Direction = direction >= 0 ? 1 : -1;
		Speed = speed;
		HitstunFrames = hitstunFrames;
		Pushback = pushback;
		HitstopFrames = hitstopFrames;
		ShakeStrength = shakeStrength;
		Heavy = heavy;
	}

	public override void _PhysicsProcess(double delta)
	{
		GlobalPosition += Vector2.Right * Direction * Speed * (float)delta;
		LifetimeFrames--;
		if (LifetimeFrames <= 0) QueueFree();
		QueueRedraw();
	}

	public void MarkHit()
	{
		HasHit = true;
		QueueFree();
	}

	public override void _Draw()
	{
		Color core = Heavy ? new Color(0.35f, 0.72f, 1f, 0.95f) : new Color(1f, 0.82f, 0.2f, 0.95f);
		Color edge = Heavy ? new Color(0.82f, 0.95f, 1f, 0.75f) : new Color(1f, 0.35f, 0.08f, 0.75f);
		float radius = Heavy ? 17f : 13f;
		DrawCircle(Vector2.Zero, radius, core);
		DrawArc(Vector2.Zero, radius + 5f, 0f, Mathf.Tau, 24, edge, Heavy ? 4f : 3f, true);
		DrawLine(new Vector2(-Direction * (radius + 16f), 0f), new Vector2(-Direction * radius * 0.35f, 0f), edge, Heavy ? 5f : 3f, true);
	}
}

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
	public bool HasHit => HitsRemaining <= 0;
	public bool Super { get; private set; }
	public int HitsRemaining { get; private set; } = 1;
	public int HitCooldownFrames { get; private set; } = 4;
	public bool CanHit => HitsRemaining > 0 && _hitCooldownFramesLeft <= 0;
	public bool NextHitIsFinal => HitsRemaining == 1;
	public bool FinalHitKnocksDown { get; private set; }
	public KnockdownType FinalKnockdownType { get; private set; } = KnockdownType.SoftKnockdown;
	public int FinalKnockdownFrames { get; private set; }
	public Rect2 WorldHitbox => new(GlobalPosition + HitboxLocal.Position, HitboxLocal.Size);
	private int _hitCooldownFramesLeft;
	private FighterController _latchedDefender;
	private Vector2 _latchedDefenderOffset;

	public override void _Ready()
	{
		AddToGroup(ProjectileGroup);
	}

	public void Initialize(FighterController owner, int direction, float speed, int hitstunFrames, float pushback, int hitstopFrames, float shakeStrength, bool heavy,
		int hits = 1, int hitCooldownFrames = 4, bool super = false, bool finalHitKnocksDown = false,
		KnockdownType finalKnockdownType = KnockdownType.SoftKnockdown, int finalKnockdownFrames = 0)
	{
		OwnerFighter = owner;
		Direction = direction >= 0 ? 1 : -1;
		Speed = speed;
		HitstunFrames = hitstunFrames;
		Pushback = pushback;
		HitstopFrames = hitstopFrames;
		ShakeStrength = shakeStrength;
		Heavy = heavy;
		Super = super;
		HitsRemaining = Mathf.Max(1, hits);
		HitCooldownFrames = Mathf.Max(1, hitCooldownFrames);
		FinalHitKnocksDown = finalHitKnocksDown;
		FinalKnockdownType = finalKnockdownType;
		FinalKnockdownFrames = finalKnockdownFrames;
		if (Super) HitboxLocal = new Rect2(-36f, -34f, 72f, 68f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Super && HitsRemaining > 0 && GodotObject.IsInstanceValid(_latchedDefender))
			GlobalPosition = _latchedDefender.GlobalPosition + _latchedDefenderOffset;
		else
			GlobalPosition += Vector2.Right * Direction * Speed * (float)delta;
		if (_hitCooldownFramesLeft > 0) _hitCooldownFramesLeft--;
		LifetimeFrames--;
		if (LifetimeFrames <= 0) QueueFree();
		QueueRedraw();
	}

	public void MarkHit(FighterController defender = null)
	{
		HitsRemaining--;
		if (HitsRemaining <= 0)
		{
			QueueFree();
			return;
		}
		if (Super && defender != null)
		{
			_latchedDefender = defender;
			_latchedDefenderOffset = GlobalPosition - defender.GlobalPosition;
		}
		_hitCooldownFramesLeft = HitCooldownFrames;
	}

	public override void _Draw()
	{
		Color core = Super ? new Color(0.68f, 0.92f, 1f, 0.96f) : Heavy ? new Color(0.35f, 0.72f, 1f, 0.95f) : new Color(1f, 0.82f, 0.2f, 0.95f);
		Color edge = Super ? new Color(1f, 1f, 1f, 0.82f) : Heavy ? new Color(0.82f, 0.95f, 1f, 0.75f) : new Color(1f, 0.35f, 0.08f, 0.75f);
		float radius = Super ? 36f : Heavy ? 17f : 13f;
		DrawCircle(Vector2.Zero, radius, core);
		DrawArc(Vector2.Zero, radius + (Super ? 10f : 5f), 0f, Mathf.Tau, Super ? 36 : 24, edge, Super ? 7f : Heavy ? 4f : 3f, true);
		DrawLine(new Vector2(-Direction * (radius + (Super ? 42f : 16f)), 0f), new Vector2(-Direction * radius * 0.35f, 0f), edge, Super ? 10f : Heavy ? 5f : 3f, true);
		if (Super)
			DrawArc(Vector2.Zero, radius * 0.62f, 0f, Mathf.Tau, 28, new Color(0.25f, 0.55f, 1f, 0.9f), 5f, true);
	}
}

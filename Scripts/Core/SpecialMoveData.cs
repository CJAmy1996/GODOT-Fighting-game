using Godot;

namespace ModularFighter.Core;

/// <summary>
/// A data-driven special move. It inherits the shared 60 Hz timeline, combat boxes,
/// hit feel, and cancel data from NormalMoveData while adding projectile settings.
/// </summary>
[Tool, GlobalClass]
public partial class SpecialMoveData : NormalMoveData
{
	[ExportGroup("Parry")]
	[Export] public bool Parry { get; set; }
	[Export] public int ParrySuccessPresentationFrames { get; set; } = 18;

	[ExportGroup("Projectile")]
	[Export] public bool Projectile { get; set; }
	[Export] public bool HeavyProjectile { get; set; }
	[Export] public float ProjectileSpeed { get; set; } = 760f;
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(70f, -42f);
	[Export] public Rect2 ProjectileHitboxLocal { get; set; } = new(-18f, -18f, 36f, 36f);

	[ExportGroup("Reflector")]
	[Export] public PackedScene ReflectorScene { get; set; }
	[Export] public Vector2 ReflectorSpawnOffset { get; set; } = new(72f, -58f);

	[ExportGroup("Self Launch")]
	[Export] public bool SelfLaunch { get; set; }
	[Export] public float SelfLaunchSpeed { get; set; } = 1100f;
	[Export] public float SelfHorizontalSpeed { get; set; } = 140f;

	[ExportGroup("Self Drive")]
	[Export] public bool SelfDrive { get; set; }
	[Export] public float SelfDriveSpeed { get; set; }

	[ExportGroup("Forced Descent")]
	[Export] public int ForceDownwardStartFrame { get; set; } = -1;
	[Export] public float ForceDownwardSpeed { get; set; } = 1450f;
	[Export] public bool HoldUntilLanding { get; set; }
}

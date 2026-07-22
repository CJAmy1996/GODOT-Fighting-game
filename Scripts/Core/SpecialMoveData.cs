using Godot;

namespace ModularFighter.Core;

/// <summary>
/// A data-driven special move. It inherits the shared 60 Hz timeline, combat boxes,
/// hit feel, and cancel data from NormalMoveData while adding projectile settings.
/// </summary>
[Tool, GlobalClass]
public partial class SpecialMoveData : NormalMoveData
{
	[ExportGroup("Projectile")]
	[Export] public bool Projectile { get; set; }
	[Export] public bool HeavyProjectile { get; set; }
	[Export] public float ProjectileSpeed { get; set; } = 760f;
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(70f, -42f);
	[Export] public Rect2 ProjectileHitboxLocal { get; set; } = new(-18f, -18f, 36f, 36f);
}

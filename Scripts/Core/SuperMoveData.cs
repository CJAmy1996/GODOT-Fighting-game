using Godot;

namespace ModularFighter.Core;

/// <summary>Reusable super-move feel and hit data for cinematic freezes, rush supers, and projectile supers.</summary>
[Tool, GlobalClass]
public partial class SuperMoveData : Resource
{
	/// <summary>
	/// Optional authored combat timeline for supers that need the full per-frame box,
	/// movement, and landing feature set instead of the generic super hitbox.
	/// The move remains registered and classified exclusively as a SuperMoveData.
	/// </summary>
	[Export] public SpecialMoveData AuthoredMoveData { get; set; }
	[Export] public string AttackName { get; set; } = "";
	[Export] public string AnimationName { get; set; } = "";
	[Export(PropertyHint.Range, "1,3,1")] public int Level { get; set; } = 1;
	[ExportGroup("Command Input")]
	[Export] public MotionInputBinding CommandInput { get; set; }
	[ExportGroup("Timeline / Hit Data")]
	[Export] public int StartupFrames { get; set; } = 8;
	[Export] public int ActiveFrames { get; set; } = 24;
	[Export] public int RecoveryFrames { get; set; } = 24;
	[Export] public int ActivationFreezeFrames { get; set; } = 45;
	[Export] public int BackdropFrames { get; set; } = 90;
	[Export] public int HitCount { get; set; } = 1;
	[Export] public int HitIntervalFrames { get; set; } = 4;
	[Export] public int HitstunFrames { get; set; } = 12;
	[Export] public int HitstopFrames { get; set; } = 4;
	[Export] public bool AddsGlobalHitstopBonus { get; set; } = true;
	[Export] public float Pushback { get; set; } = 120f;
	[Export] public int FinalHitstunFrames { get; set; } = 24;
	[Export] public int FinalHitstopFrames { get; set; } = 8;
	[Export] public float FinalPushback { get; set; } = 900f;
	[Export] public float ShakeStrength { get; set; } = 5f;
	[Export] public float FinalShakeStrength { get; set; } = 9f;
	[Export] public Rect2 HitboxLocal { get; set; } = new(18f, -70f, 88f, 66f);
	[Export] public bool FinalHitKnocksDown { get; set; } = true;
	[Export] public KnockdownType FinalKnockdownType { get; set; } = KnockdownType.SoftKnockdown;
	[Export] public int FinalKnockdownFrames { get; set; } = 42;
	[Export] public bool Projectile { get; set; }
	[Export] public PackedScene ProjectileScene { get; set; }
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(78f, -62f);
	[Export] public float ProjectileSpeed { get; set; } = 620f;
	[Export] public bool ProjectileTargetsOpponent { get; set; }
	[Export] public bool ProjectileAnchoredToOwner { get; set; }
	[Export] public int ProjectileHitCooldownFrames { get; set; } = 5;
	/// <summary>Source effect frame where the damaging FA box first becomes active.</summary>
	[Export(PropertyHint.Range, "0,600,1")] public int ProjectileHitStartFrame { get; set; }
	[Export] public bool ProjectilePersistsVisuallyAfterFinalHit { get; set; }
	[Export] public SpriteFrames ProjectileSpriteFrames { get; set; }
	[Export] public string ProjectileAnimationName { get; set; } = "";
	[Export] public Vector2 ProjectileVisualOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 ProjectileVisualScale { get; set; } = Vector2.One;
	[Export] public bool ProjectileVisualAdditiveBlend { get; set; }
	[Export] public bool ProjectileVisualBlackKey { get; set; }
	[Export] public int[] ProjectileVisualOpacityFrames { get; set; } = System.Array.Empty<int>();
	[Export] public float[] ProjectileVisualOpacityValues { get; set; } = System.Array.Empty<float>();
	[Export] public float[] ProjectileVisualOpacityLossPerFrame { get; set; } = System.Array.Empty<float>();
	[Export] public Rect2 ProjectileHitboxLocal { get; set; } = new(-24f, -18f, 72f, 36f);
	[Export] public SpriteFrames ProjectileImpactSpriteFrames { get; set; }
	[Export] public string ProjectileImpactAnimationName { get; set; } = "";
	[Export] public Vector2 ProjectileImpactVisualOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 ProjectileImpactScale { get; set; } = Vector2.One;
	[Export] public bool ProjectileImpactAdditiveBlend { get; set; }
	[Export] public bool ProjectileImpactBlackKey { get; set; }
	[Export] public bool ProjectileImpactBlackensDefender { get; set; }
	[Export(PropertyHint.Range, "1,60,1")] public int ProjectileImpactBlackSilhouetteFrames { get; set; } = 8;
	[Export] public SpriteFrames ProjectileImpactDefenderFireSpriteFrames { get; set; }
	[Export] public string ProjectileImpactDefenderFireAnimationName { get; set; } = "";
	[Export(PropertyHint.Range, "1,32,1")] public int ProjectileCount { get; set; } = 1;
	[Export(PropertyHint.Range, "1,60,1")] public int ProjectileSpawnIntervalFrames { get; set; } = 4;
	[Export(PropertyHint.Range, "1,600,1")] public int ProjectileLifetimeFrames { get; set; } = 90;
	[Export] public float ProjectileVolleyHorizontalSpacing { get; set; }
	[Export] public float ProjectileVolleyVerticalSpacing { get; set; }
	[ExportGroup("Projectile Screen Carry")]
	[Export] public bool ProjectileVolleyScreenCarry { get; set; }
	[Export] public bool ProjectileVolleyFinalOnlyKnockdown { get; set; }
	[Export] public float ProjectileVolleyCarrySpeed { get; set; } = 1800f;
	[Export] public float ProjectileVolleyAttackerDashSpeed { get; set; } = 1250f;
	[Export(PropertyHint.Range, "1,60,1")] public int ProjectileVolleyCarryFrames { get; set; } = 9;
	[Export] public bool ProjectilePlaysElectricitySound { get; set; }
	[Export] public bool ProjectileElectrocutesDefender { get; set; }
	[ExportGroup("Editable Projectile Paths")]
	[Export] public PackedScene ProjectilePathLayoutScene { get; set; }
	[Export(PropertyHint.Range, "1,600,1")] public int ProjectilePathTravelFrames { get; set; } = 60;
	[Export] public bool ProjectileAlignToPath { get; set; } = true;
	[ExportGroup("Parry")]
	[Export] public bool Parry { get; set; }
	[Export] public int ParrySuccessPresentationFrames { get; set; } = 18;
	[Export] public bool RushesForward { get; set; }
	[Export] public float RushSpeed { get; set; } = 1200f;
	[Export] public bool StopRushOnFirstHit { get; set; } = true;
	[Export] public bool RequiresHitConfirmForMultiHit { get; set; }
	[Export] public int ConfirmedActiveFrames { get; set; } = 36;
	[Export] public bool LockPositionsDuringConfirmedHits { get; set; }
	[Export] public Vector2 ConfirmedAttackerOffsetFromDefender { get; set; } = new(-72f, 0f);
}

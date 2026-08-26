using Godot;

namespace ModularFighter.Core;

/// <summary>
/// A data-driven special move. It inherits the shared 60 Hz timeline, combat boxes,
/// hit feel, and cancel data from NormalMoveData while adding projectile settings.
/// </summary>
[Tool, GlobalClass]
public partial class SpecialMoveData : NormalMoveData
{
	[ExportGroup("Command Input")]
	[Export] public MotionInputBinding CommandInput { get; set; }
	[Export] public bool CanCancelIntoFromNormals { get; set; }
	[Export] public bool RequiresSwordInHand { get; set; }

	[ExportGroup("Guard Cancel")]
	[Export] public bool GuardCancel { get; set; }
	[Export] public bool CanStartDuringBlockstun { get; set; }

	[ExportGroup("Mash Sustain")]
	[Export] public bool SustainWithMash { get; set; }
	[Export(PropertyHint.Flags, "Light Punch,Heavy Punch,Light Kick,Heavy Kick")]
	public MotionAttackButton SustainMashButtons { get; set; } = MotionAttackButton.AnyAttack;
	[Export(PropertyHint.Range, "1,120,1")] public int SustainMashGraceFrames { get; set; } = 18;
	[Export(PropertyHint.Range, "1,60,1")] public int SustainMashHitIntervalFrames { get; set; } = 8;

	[ExportGroup("Parry")]
	[Export] public bool Parry { get; set; }
	[Export] public int ParrySuccessPresentationFrames { get; set; } = 18;

	[ExportGroup("Super Presentation")]
	[Export] public bool TriggersSuperPresentation { get; set; }
	[Export(PropertyHint.Range, "1,240,1")] public int SuperActivationFreezeFrames { get; set; } = 45;
	[Export(PropertyHint.Range, "1,600,1")] public int SuperBackdropFrames { get; set; } = 90;
	[Export] public bool AddsGlobalHitstopBonus { get; set; } = true;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float ContactHitstopMultiplier { get; set; } = 1f;

	[ExportGroup("Projectile")]
	[Export] public bool Projectile { get; set; }
	[Export] public bool HeavyProjectile { get; set; }
	[Export] public float ProjectileSpeed { get; set; } = 760f;
	[Export] public Vector2 ProjectileSpawnOffset { get; set; } = new(70f, -42f);
	[Export] public Rect2 ProjectileHitboxLocal { get; set; } = new(-18f, -18f, 36f, 36f);
	[Export] public SpriteFrames ProjectileSpriteFrames { get; set; }
	[Export] public string ProjectileAnimationName { get; set; } = "";
	[Export] public Vector2 ProjectileVisualOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 ProjectileVisualScale { get; set; } = Vector2.One;
	[Export] public bool ProjectileVisualAdditiveBlend { get; set; }
	/// <summary>Removes opaque near-black pixels before additive projectile rendering.</summary>
	[Export] public bool ProjectileVisualBlackKey { get; set; }
	[Export] public string ProjectileTrailAnimationName { get; set; } = "";
	[Export(PropertyHint.Range, "0,12,1")] public int ProjectileTrailCount { get; set; }
	[Export(PropertyHint.Range, "1,30,1")] public int ProjectileTrailFrameSpacing { get; set; } = 4;
	[Export(PropertyHint.Range, "0.0,1.0,0.05")] public float ProjectileTrailOpacity { get; set; } = 0.65f;
	[Export(PropertyHint.Range, "0.0,0.5,0.01")] public float ProjectileTrailScaleStep { get; set; } = 0.1f;
	[Export(PropertyHint.Range, "1,120,1")] public int ProjectileTrailLifetimeFrames { get; set; } = 30;
	[Export(PropertyHint.Range, "0.0,255.0,1.0")] public float ProjectileTrailOpacityLossPerFrame { get; set; }
	[Export(PropertyHint.Range, "1,30,1")] public int ProjectileHitCount { get; set; } = 1;
	[Export(PropertyHint.Range, "1,30,1")] public int ProjectileHitCooldownFrames { get; set; } = 4;
	/// <summary>Source effect frame where the damaging FA box first becomes active.</summary>
	[Export(PropertyHint.Range, "0,600,1")] public int ProjectileHitStartFrame { get; set; }
	[Export] public bool ProjectilePersistsVisuallyAfterFinalHit { get; set; }
	/// <summary>Keeps beam-style projectiles attached to their firing point instead of travelling.</summary>
	[Export] public bool ProjectileAnchoredToOwner { get; set; }
	/// <summary>Mirrors an asymmetric local hitbox so it always extends in the owner's facing direction.</summary>
	[Export] public bool ProjectileDirectionalHitbox { get; set; }
	[Export(PropertyHint.Range, "1,600,1")] public int ProjectileLifetimeFrames { get; set; } = 90;
	/// <summary>Optional source-authored horizontal speed change. A negative frame disables it.</summary>
	[Export] public float ProjectileSecondarySpeed { get; set; } = -1f;
	[Export] public int ProjectileSecondarySpeedFrame { get; set; } = -1;
	/// <summary>Source M-command velocity delta applied after each authored 60 Hz movement tick.</summary>
	[Export] public float ProjectileSpeedDeltaPerFrame { get; set; }
	/// <summary>Source-authored visual scale interpolation. ProjectileVisualScale is the target scale.</summary>
	[Export] public Vector2 ProjectileVisualStartScale { get; set; } = Vector2.One;
	[Export] public int ProjectileVisualScaleStartFrame { get; set; }
	[Export] public int ProjectileVisualScaleEndFrame { get; set; }
	/// <summary>Keeps cropped drawings of different dimensions on one authored bottom edge.</summary>
	[Export] public bool ProjectileVisualBottomAnchored { get; set; }
	/// <summary>Source color-command segments: frame, starting alpha (0-255), and alpha loss per 60 Hz tick.</summary>
	[Export] public int[] ProjectileVisualOpacityFrames { get; set; } = System.Array.Empty<int>();
	[Export] public float[] ProjectileVisualOpacityValues { get; set; } = System.Array.Empty<float>();
	[Export] public float[] ProjectileVisualOpacityLossPerFrame { get; set; } = System.Array.Empty<float>();
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
	[Export] public Curve2D ProjectilePath { get; set; }
	[Export(PropertyHint.Range, "1,600,1")] public int ProjectilePathTravelFrames { get; set; } = 60;

	[ExportGroup("Projectile Assist Emission")]
	/// <summary>Lets a summoned entity emit a second, independently damaging projectile.</summary>
	[Export] public bool EmitsAssistProjectile { get; set; }
	[Export(PropertyHint.Range, "0,600,1")] public int AssistProjectileSpawnFrame { get; set; }
	[Export] public Vector2 AssistProjectileSpawnOffset { get; set; } = Vector2.Zero;
	[Export] public float AssistProjectileSpeed { get; set; } = 420f;
	[Export] public float AssistProjectileVerticalSpeed { get; set; } = -260f;
	[Export] public float AssistProjectileGravity { get; set; } = 900f;
	[Export] public Rect2 AssistProjectileHitboxLocal { get; set; } = new(-24f, -24f, 48f, 48f);
	[Export] public SpriteFrames AssistProjectileSpriteFrames { get; set; }
	[Export] public string AssistProjectileAnimationName { get; set; } = "";
	[Export] public Vector2 AssistProjectileVisualOffset { get; set; } = Vector2.Zero;
	[Export] public Vector2 AssistProjectileVisualScale { get; set; } = Vector2.One;
	[Export] public bool AssistProjectileDirectionalHitbox { get; set; }
	[Export] public string AssistProjectileGroundAnimationName { get; set; } = "";
	/// <summary>Visible pixels below the texture origin; used for exact floor contact on uncropped source art.</summary>
	[Export] public float AssistProjectileGroundContactOffset { get; set; }
	[Export(PropertyHint.Range, "1,600,1")] public int AssistProjectileLifetimeFrames { get; set; } = 120;
	[Export(PropertyHint.Range, "1,600,1")] public int AssistProjectileGroundLifetimeFrames { get; set; } = 69;

	[ExportGroup("Reflector")]
	[Export] public PackedScene ReflectorScene { get; set; }
	[Export] public Vector2 ReflectorSpawnOffset { get; set; } = new(72f, -58f);

	[ExportGroup("Self Launch")]
	[Export] public bool SelfLaunch { get; set; }
	/// <summary>60 Hz gameplay frame on which the launch M command is applied.</summary>
	[Export] public int SelfLaunchStartFrame { get; set; }
	[Export] public float SelfLaunchSpeed { get; set; } = 1100f;
	[Export] public float SelfHorizontalSpeed { get; set; } = 140f;
	/// <summary>When true, horizontal launch always follows character facing instead of held input.</summary>
	[Export] public bool SelfLaunchUsesFacing { get; set; }
	/// <summary>Horizontal speed removed per second after launch. Zero preserves momentum.</summary>
	[Export] public float SelfHorizontalDeceleration { get; set; }
	[Export] public bool SelfRiseDuringAttack { get; set; }
	[Export] public float SelfRiseSpeed { get; set; } = 280f;
	[Export] public int SelfRiseStartFrame { get; set; }
	[Export] public int SelfRiseEndFrame { get; set; } = -1;

	[ExportGroup("Self Drive")]
	[Export] public bool SelfDrive { get; set; }
	[Export] public float SelfDriveSpeed { get; set; }

	[ExportGroup("Forced Descent")]
	[Export] public int ForceDownwardStartFrame { get; set; } = -1;
	[Export] public float ForceDownwardSpeed { get; set; } = 1450f;
	[Export] public float ForceDownwardTerminalSpeed { get; set; } = -1f;
	[Export] public bool HoldUntilLanding { get; set; }
	[Export] public int LandingRecoveryFrames { get; set; }
	[Export] public string LandingAnimationName { get; set; } = "";

	[ExportGroup("Air Phase Animation")]
	[Export] public int[] RiseAnimationSourceCycle { get; set; } = System.Array.Empty<int>();
	[Export] public int RiseAnimationTicksPerSource { get; set; } = 1;
	[Export] public int[] DescentAnimationSourceCycle { get; set; } = System.Array.Empty<int>();
	[Export] public int DescentAnimationTicksPerSource { get; set; } = 1;
	[Export] public int[] LandingAnimationSourceTimeline { get; set; } = System.Array.Empty<int>();
}

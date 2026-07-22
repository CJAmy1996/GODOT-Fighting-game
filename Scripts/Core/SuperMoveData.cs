using Godot;

namespace ModularFighter.Core;

/// <summary>Reusable super-move feel and hit data for cinematic freezes, rush supers, and projectile supers.</summary>
[GlobalClass]
public partial class SuperMoveData : Resource
{
	[Export] public string AttackName { get; set; } = "";
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
	[Export] public float ProjectileSpeed { get; set; } = 620f;
	[Export] public int ProjectileHitCooldownFrames { get; set; } = 5;
	[Export] public bool RushesForward { get; set; }
	[Export] public float RushSpeed { get; set; } = 1200f;
	[Export] public bool StopRushOnFirstHit { get; set; } = true;
	[Export] public bool RequiresHitConfirmForMultiHit { get; set; }
	[Export] public int ConfirmedActiveFrames { get; set; } = 36;
	[Export] public bool LockPositionsDuringConfirmedHits { get; set; }
	[Export] public Vector2 ConfirmedAttackerOffsetFromDefender { get; set; } = new(-72f, 0f);
}

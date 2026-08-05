using Godot;

namespace ModularFighter.Core;

/// <summary>
/// Reusable presentation settings for entering a combat state. Match rules can
/// apply the same profile to any status without embedding VFX tuning in moves.
/// </summary>
[GlobalClass]
public partial class StateImpactEffectProfile : Resource
{
	[Export] public FighterHitState TriggerState { get; set; } = FighterHitState.GroundedKnockdown;
	[Export] public bool SpawnDust { get; set; } = true;
	[Export] public bool SpawnWallBurst { get; set; }
	[Export] public float WallBurstScale { get; set; } = 1f;
	[Export] public int DustParticles { get; set; } = 7;
	[Export] public float DustSpread { get; set; } = 42f;
	[Export] public float ShakeStrength { get; set; } = 2.25f;
	[Export] public int ShakeFrames { get; set; } = 6;
	[Export] public int FreezeFrames { get; set; }

	public bool Matches(FighterHitState state) => state == TriggerState;
}

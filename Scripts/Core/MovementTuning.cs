using Godot;

namespace ModularFighter.Core;

[Tool, GlobalClass]
public partial class MovementTuning : Resource
{
	[ExportGroup("Ground")]
	[Export] public float WalkSpeed { get; set; } = 300f;
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float BackWalkSpeedMultiplier { get; set; } = 0.82f;
	[Export] public float GroundAcceleration { get; set; } = 3000f;
	[Export] public float GroundTurnAcceleration { get; set; } = 50000f;
	[Export] public float GroundDeceleration { get; set; } = 3800f;
	[Export] public float GroundFriction { get; set; } = 4200f;

	[ExportGroup("Air")]
	[Export] public float Gravity { get; set; } = 1500f;
	[Export] public float TerminalFallSpeed { get; set; } = 950f;
	[Export] public float AirSpeed { get; set; } = 280f;
	[Export] public float AirAcceleration { get; set; } = 1250f;
	[Export] public float AirDeceleration { get; set; } = 500f;
	[Export] public bool AllowAirControl { get; set; } = true;
	[Export] public int MaxAirActions { get; set; } = 1;
	[Export] public bool NormalJumpAirActionsRequirePeak { get; set; } = true;
	[Export] public float NormalJumpAirActionPeakVelocity { get; set; } = 0f;
	[Export] public bool AllowAirShortHops { get; set; } = false;

	[ExportGroup("Timing (frames at 60 fps)")]
	[Export] public int CoyoteFrames { get; set; } = 3;
	[Export] public int InputBufferFrames { get; set; } = 3;
}

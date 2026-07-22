using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

/// <summary>A controlled hover that retains normal walk-style horizontal handling.</summary>
[Tool, GlobalClass]
public partial class AirWalkAbility : MovementAbility
{
	[Export] public float VerticalCorrectionSpeed { get; set; } = 2200f;
	[Export] public int MaxFrames { get; set; } = 120;
	public override bool OwnsGravity => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime) =>
		!fighter.WasGrounded && fighter.CurrentInput.FlightHeld && runtime.IntValue < MaxFrames;

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		if (!fighter.CurrentInput.FlightHeld || runtime.IntValue++ >= MaxFrames) return false;
		fighter.Velocity = new Vector2(fighter.Velocity.X, Mathf.MoveToward(fighter.Velocity.Y, 0, VerticalCorrectionSpeed * delta));
		return true;
	}
}

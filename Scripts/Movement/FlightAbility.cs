using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

[GlobalClass]
public partial class FlightAbility : MovementAbility
{
	[Export] public float FlightSpeed { get; set; } = 400f;
	[Export] public float Acceleration { get; set; } = 1800f;
	[Export] public int MaxFrames { get; set; } = 180;
	public override bool OwnsHorizontalVelocity => true;
	public override bool OwnsGravity => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime) =>
		fighter.CurrentInput.FlightHeld && (!fighter.WasGrounded || runtime.Active) && runtime.IntValue < MaxFrames;

	public override void Start(FighterController fighter, AbilityRuntime runtime) => base.Start(fighter, runtime);

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		if (!fighter.CurrentInput.FlightHeld || runtime.IntValue++ >= MaxFrames) return false;
		Vector2 desired = new(fighter.CurrentInput.Horizontal * FlightSpeed, fighter.CurrentInput.Vertical * FlightSpeed);
		fighter.Velocity = fighter.Velocity.MoveToward(desired, Acceleration * delta);
		return true;
	}

	public override void Stop(FighterController fighter, AbilityRuntime runtime)
	{
		base.Stop(fighter, runtime);
		if (fighter.WasGrounded) runtime.IntValue = 0;
	}
}

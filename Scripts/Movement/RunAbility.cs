using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

[Tool, GlobalClass]
public partial class RunAbility : MovementAbility
{
	[Export] public float Speed { get; set; } = 650f;
	[Export] public float Acceleration { get; set; } = 5200f;
	[Export] public float StopFriction { get; set; } = 4200f;
	[Export] public float CrouchCancelFriction { get; set; } = 2600f;
	public override bool OwnsHorizontalVelocity => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime)
	{
		if (!fighter.WasGrounded) return false;
		if (!fighter.ActionInput.DashPressed) return false;
		if (fighter.CurrentInput.Vertical > 0.5f) return false;
		return fighter.DashInputDirection * fighter.Facing > 0;
	}

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		runtime.IntValue = fighter.DashInputDirection;
		fighter.ConsumeDashCommand();
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		int direction = runtime.IntValue;
		bool holdingRunDirection = fighter.CurrentInput.Horizontal * direction > 0;
		bool crouching = fighter.CurrentInput.Vertical > 0.5f;
		if (!fighter.WasGrounded || crouching || !holdingRunDirection)
		{
			if (crouching)
			{
				fighter.BeginRunCrouchSlide();
				return false;
			}
			if (fighter.WasGrounded && !holdingRunDirection) fighter.BeginRunStopSlide();
			return false;
		}

		float target = direction * Speed;
		fighter.Velocity = new Vector2(Mathf.MoveToward(fighter.Velocity.X, target, Acceleration * delta), fighter.Velocity.Y);
		return true;
	}
}

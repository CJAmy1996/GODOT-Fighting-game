using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

/// <summary>A controlled hover that retains normal walk-style horizontal handling.</summary>
[Tool, GlobalClass]
public partial class AirWalkAbility : MovementAbility
{
	[Export] public float WalkSpeed { get; set; } = 340f;
	[Export] public float RunSpeed { get; set; } = 650f;
	[Export] public float HorizontalAcceleration { get; set; } = 2200f;
	[Export] public int ExitInputGraceFrames { get; set; } = 2;
	[Export] public bool InfiniteDuration { get; set; }
	[Export] public int MaxFrames { get; set; } = 120;
	[Export] public bool StartsWithAirJumpInput { get; set; }
	[Export] public bool StartsWithTrait2Input { get; set; }
	public override bool OwnsGravity => true;
	public override bool OwnsHorizontalVelocity => true;
	public override bool PreventsBlocking => true;
	public override bool TicksDuringAttack => true;
	public override bool PersistsThroughNormalAttack => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime) =>
		!fighter.WasGrounded && (StartsWithTrait2Input
			? fighter.CurrentInput.Special2Pressed || fighter.ActionInput.Special2Pressed
			: StartsWithAirJumpInput
			? fighter.CurrentInput.JumpPressed || fighter.ActionInput.JumpPressed
			: fighter.CurrentInput.FlightPressed);

	public override bool CanStartFromAttack(FighterController fighter, AbilityRuntime runtime) =>
		fighter.CurrentAttackIsNormal && fighter.CurrentAttackStartedAirborne && CanStart(fighter, runtime);

	public override bool CanStartAttack(FighterController fighter, AbilityRuntime runtime) => true;

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		runtime.IntValue = 0;
		runtime.BoolValue = false;
		fighter.Velocity = Vector2.Zero;
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		runtime.IntValue++;
		if (!InfiniteDuration && runtime.IntValue >= MaxFrames) return false;
		if (runtime.IntValue > ExitInputGraceFrames && Mathf.Abs(fighter.CurrentInput.Vertical) > 0.5f) return false;
		if (fighter.CurrentInput.DashPressed && Mathf.Abs(fighter.CurrentInput.Horizontal) > 0.5f)
			runtime.BoolValue = true;
		if (Mathf.Abs(fighter.CurrentInput.Horizontal) <= 0.1f) runtime.BoolValue = false;
		float speed = runtime.BoolValue ? RunSpeed : WalkSpeed;
		float targetX = fighter.CurrentInput.Horizontal * speed;
		fighter.Velocity = new Vector2(Mathf.MoveToward(fighter.Velocity.X, targetX,
			HorizontalAcceleration * delta), 0f);
		return true;
	}
}

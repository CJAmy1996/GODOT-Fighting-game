using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

public enum DashDirectionRequirement { Any, Forward, Backward }

[GlobalClass]
public partial class DashAbility : MovementAbility
{
	[Export] public bool AirOnly { get; set; }
	[Export] public bool GroundOnly { get; set; }
	[Export] public DashDirectionRequirement DirectionRequirement { get; set; } = DashDirectionRequirement.Any;
	[Export] public int MaxAirUses { get; set; } = 1;
	[Export] public int ActiveFrames { get; set; } = 12;
	[Export] public int RecoveryFrames { get; set; } = 0;
	[Export] public float Speed { get; set; } = 700f;
	[Export] public float VerticalSpeed { get; set; }
	[Export] public bool PreserveGravity { get; set; }
	[Export] public bool AimWithStick { get; set; }
	[Export] public bool CrouchCancels { get; set; }
	[Export] public float CrouchCancelFriction { get; set; } = 7000f;
	[Export] public bool CommittedUntilComplete { get; set; }
	[Export] public bool ConsumesAirAction { get; set; } = true;
	[Export] public int LandingLagFrames { get; set; }
	public override bool OwnsHorizontalVelocity => true;
	public override bool OwnsGravity => !PreserveGravity;
	public override bool CanBeInterruptedBy(MovementAbility incoming) => !CommittedUntilComplete && base.CanBeInterruptedBy(incoming);

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime)
	{
		if (runtime.IntValue > 0)
		{
			runtime.IntValue--;
			return false;
		}
		if (!fighter.ActionInput.DashPressed) return false;
		if (AirOnly && fighter.WasGrounded) return false;
		if (GroundOnly && !fighter.WasGrounded) return false;
		if (!fighter.WasGrounded && ConsumesAirAction && !fighter.CanUseAirDashAction()) return false;
		if (!MatchesDirection(fighter)) return false;
		return fighter.WasGrounded || runtime.UsesThisAirTime < MaxAirUses;
	}

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		if (!fighter.WasGrounded)
		{
			runtime.UsesThisAirTime++;
			if (ConsumesAirAction) fighter.ConsumeAirDashAction();
			fighter.QueueLandingLag(LandingLagFrames);
		}
		runtime.FramesRemaining = ActiveFrames;
		Vector2 direction = AimWithStick ? new Vector2(fighter.CurrentInput.Horizontal, fighter.CurrentInput.Vertical) : Vector2.Right * fighter.DashInputDirection;
		if (direction == Vector2.Zero) direction = Vector2.Right * fighter.Facing;
		direction = direction.Normalized();
		fighter.ConsumeDashCommand();
		fighter.Velocity = direction * Speed + Vector2.Down * VerticalSpeed;
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		if (CrouchCancels && fighter.WasGrounded && fighter.CurrentInput.Vertical > 0.5f)
		{
			fighter.Velocity = new Vector2(Mathf.MoveToward(fighter.Velocity.X, 0f, CrouchCancelFriction * delta), fighter.Velocity.Y);
			runtime.IntValue = RecoveryFrames;
			return false;
		}
		if (runtime.FramesRemaining-- > 0) return true;
		runtime.IntValue = RecoveryFrames;
		return false;
	}

	private bool MatchesDirection(FighterController fighter)
	{
		int relativeDirection = fighter.DashInputDirection * fighter.Facing;
		return DirectionRequirement switch
		{
			DashDirectionRequirement.Forward => relativeDirection > 0,
			DashDirectionRequirement.Backward => relativeDirection < 0,
			_ => true
		};
	}
}

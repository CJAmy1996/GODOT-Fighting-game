using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

/// <summary>Marvel-style down-to-up super jump: fixed high launch with air steering.</summary>
[GlobalClass]
public partial class SuperJumpAbility : MovementAbility
{
	[Export] public int CommandWindowFrames { get; set; } = 4;
	[Export] public float InitialSpeed { get; set; } = 1250f;
	[Export] public float ForwardSpeed { get; set; } = 430f;
	public override bool SuppressesGroundedPushWhileAirborne => true;
	public override bool EnablesAirControlWhileAirborne => true;
	// Super jump releases retain momentum instead of stopping like a normal air-control state.
	public override float AirDecelerationMultiplierWhileAirborne => 0.08f;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime) =>
		(fighter.WasGrounded || fighter.CoyoteFramesLeft > 0) &&
		fighter.IsDownThenUpCommand(CommandWindowFrames);

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		fighter.RefreshAirJumpResourcesForSuperJump();
		fighter.ConsumeDownThenUpCommand();
		fighter.ConsumeJumpBuffer();
		fighter.Velocity = new Vector2(fighter.CurrentInput.Horizontal * ForwardSpeed, -InitialSpeed);
	}
}

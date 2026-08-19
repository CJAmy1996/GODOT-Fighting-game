using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

public enum JumpDirection { Neutral, Forward, Backward }

[Tool, GlobalClass]
public partial class JumpAbility : MovementAbility
{
	public override bool SuppressesGroundedPushWhileAirborne => true;
	public override bool OwnsHorizontalVelocity => true;
	[Export] public JumpDirection Direction { get; set; } = JumpDirection.Neutral;
	[Export] public string AirJumpResourceId { get; set; } = "standard_jump";
	[Export] public float InitialSpeed { get; set; } = 620f;
	[Export] public float AirJumpInitialSpeed { get; set; } = 0f;
	[Export] public float ForwardSpeed { get; set; } = 310f;
	[Export] public int MaxAirJumps { get; set; } = 1;
	[Export] public float HeldGravityMultiplier { get; set; } = 0.55f;
	[Export] public int HeldFrames { get; set; } = 10;
	[Export] public int JumpSquatFrames { get; set; } = 0;
	[Export] public float JumpSquatFriction { get; set; } = 10000f;
	[Export] public bool GroundJumpAirActionsRequirePeak { get; set; } = true;
	[Export] public bool AllowAirShortHop { get; set; } = false;
	[Export] public bool ShortHopAllowsAirJumps { get; set; } = false;
	[Export] public bool ShortHopInteractsWithGroundedPushbox { get; set; } = false;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float GroundedPushStrength { get; set; } = 0f;
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float ReleaseVelocityMultiplier { get; set; } = 0.5f;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime)
	{
		if (fighter.LandingLagFramesLeft > 0) return false;
		if (!fighter.ActionInput.JumpPressed) return false;
		if (!MatchesDirection(fighter)) return false;
		bool groundJump = fighter.WasGrounded || fighter.CoyoteFramesLeft > 0;
		return groundJump || fighter.CanUseAirJump(AirJumpResourceId, MaxAirJumps);
	}

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		bool groundJump = fighter.WasGrounded || fighter.CoyoteFramesLeft > 0;
		if (!groundJump) fighter.ConsumeAirJump(AirJumpResourceId);
		if (groundJump)
			fighter.SetAirActionsRequirePeakThisJump(
				GroundJumpAirActionsRequirePeak && fighter.Definition.Tuning.NormalJumpAirActionsRequirePeak);
		if (groundJump)
			fighter.SetJumpGroundedPushboxRules(GroundedPushStrength > 0f, GroundedPushStrength);
		runtime.BoolValue = groundJump;
		runtime.FramesRemaining = HeldFrames;
		fighter.ConsumeJumpBuffer();
		// Direction is sampled only at takeoff. It cannot be altered until landing.
		runtime.FloatValue = Direction == JumpDirection.Neutral
			? 0f
			: fighter.JumpInputHorizontal * ForwardSpeed;
		runtime.IntValue = groundJump ? JumpSquatFrames : 0;
		if (runtime.IntValue <= 0) Launch(fighter, runtime);
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		if (runtime.IntValue > 0)
		{
			runtime.IntValue--;
			fighter.Velocity = new Vector2(Mathf.MoveToward(fighter.Velocity.X, 0f, JumpSquatFriction * delta), fighter.Velocity.Y);
			if (runtime.IntValue == 0) Launch(fighter, runtime);
			return true;
		}
		if (runtime.FramesRemaining-- <= 0) return false;
		if (!fighter.CurrentInput.JumpHeld)
		{
			// Down-to-up is a committed super-jump command, never a variable-height
			// normal jump. A one-frame Up+Jump press must retain the full launch.
			if (fighter.IsInSuperJumpRoute) return false;
			bool shortHopAllowed = runtime.BoolValue ||
				(AllowAirShortHop && fighter.Definition.Tuning.AllowAirShortHops);
			// Releasing jump early cuts upward momentum: a tap produces a true short hop.
			// By default this is only legal from a grounded jump, not from an air jump.
			if (shortHopAllowed && fighter.Velocity.Y < 0)
			{
				fighter.MarkShortHopRoute();
				fighter.Velocity = new Vector2(fighter.Velocity.X, fighter.Velocity.Y * ReleaseVelocityMultiplier);
				if (runtime.BoolValue && !ShortHopAllowsAirJumps) fighter.DisableAirJumpsThisJump();
				if (runtime.BoolValue)
					fighter.SetShortHopPushboxRules(ShortHopInteractsWithGroundedPushbox, GroundedPushStrength > 0f);
			}
			return false;
		}
		// Counteract part of gravity for a variable-height jump.
		float gravityCancelled = fighter.Definition.Tuning.Gravity * (1f - HeldGravityMultiplier) * delta;
		fighter.Velocity = new Vector2(fighter.Velocity.X, fighter.Velocity.Y - gravityCancelled);
		return true;
	}

	private void Launch(FighterController fighter, AbilityRuntime runtime)
	{
		runtime.IntValue = -1;
		if (runtime.BoolValue) fighter.QueueGroundJumpStartEffect();
		float launchSpeed = runtime.BoolValue || AirJumpInitialSpeed <= 0f
			? InitialSpeed
			: AirJumpInitialSpeed;
		fighter.Velocity = new Vector2(runtime.FloatValue, -launchSpeed);
	}

	private bool MatchesDirection(FighterController fighter)
	{
		float horizontal = fighter.JumpInputHorizontal;
		return Direction switch
		{
			JumpDirection.Neutral => horizontal == 0,
			JumpDirection.Forward => horizontal * fighter.JumpInputFacing > 0,
			JumpDirection.Backward => horizontal * fighter.JumpInputFacing < 0,
			_ => false
		};
	}
}

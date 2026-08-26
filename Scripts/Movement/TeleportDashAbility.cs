using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

/// <summary>An airborne dash that moves one exact horizontal distance on activation.</summary>
[Tool, GlobalClass]
public partial class TeleportDashAbility : MovementAbility
{
	[Export] public float Distance { get; set; } = 360f;
	[Export] public DashDirectionRequirement DirectionRequirement { get; set; } = DashDirectionRequirement.Forward;
	[Export] public int StartupFrames { get; set; } = 2;
	[Export] public int ActiveFrames { get; set; } = 18;
	[Export] public int MaxAirUses { get; set; } = 1;
	[Export] public int InvulnerabilityFrames { get; set; } = 8;
	[Export] public bool RequireDirectionalDoubleTap { get; set; }
	[Export] public bool DisallowDownInput { get; set; }
	public override bool OwnsHorizontalVelocity => true;
	public override bool OwnsGravity => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime) =>
		!fighter.WasGrounded && fighter.ActionInput.DashPressed &&
		(!RequireDirectionalDoubleTap || fighter.HasBufferedDashCommand) &&
		(!DisallowDownInput || fighter.CurrentInput.Vertical <= 0.5f) &&
		MatchesDirection(fighter) &&
		runtime.UsesThisAirTime < MaxAirUses && fighter.CanUseAirDashAction();

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		base.Start(fighter, runtime);
		runtime.UsesThisAirTime++;
		runtime.FramesRemaining = ActiveFrames;
		runtime.IntValue = StartupFrames;
		runtime.BoolValue = false;
		fighter.ConsumeAirDashAction();
		fighter.ConsumeDashCommand();
		fighter.BeginMovementInvulnerability(InvulnerabilityFrames);
		fighter.Velocity = Vector2.Zero;
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta)
	{
		fighter.Velocity = Vector2.Zero;
		if (!runtime.BoolValue && runtime.IntValue-- <= 0)
		{
			int direction = DirectionRequirement == DashDirectionRequirement.Backward ? -fighter.Facing : fighter.Facing;
			fighter.MoveAndCollide(Vector2.Right * direction * Distance);
			runtime.BoolValue = true;
		}
		return runtime.FramesRemaining-- > 0;
	}

	private bool MatchesDirection(FighterController fighter)
	{
		int relative = fighter.DashInputDirection * fighter.Facing;
		return DirectionRequirement == DashDirectionRequirement.Backward ? relative < 0 : relative > 0;
	}
}

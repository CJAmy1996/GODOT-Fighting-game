using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

public enum JetEscapeDirection { Forward, Backward }

/// <summary>A gas-powered directional escape bound to Special 1, not the normal dash command.</summary>
[Tool, GlobalClass]
public partial class JetEscapeAbility : MovementAbility
{
	[Export] public JetEscapeDirection Direction { get; set; }
	[Export] public int ActiveFrames { get; set; } = 16;
	[Export] public float Speed { get; set; } = 650f;
	[Export] public float VerticalSpeed { get; set; } = 280f;
	[Export] public float GasCost { get; set; } = 20f;
	[Export] public int InvulnerabilityFrames { get; set; } = 4;
	[Export] public string AnimationName { get; set; } = "jet_escape_right";
	[Export] public string StateName { get; set; } = "STATE ESCAPE RIGHT / JET FORWARD DASH";
	[Export(PropertyHint.Range, "0.1,1.0,0.05")] public float DirectionThreshold { get; set; } = 0.35f;

	public override bool OwnsHorizontalVelocity => true;
	public override bool SuppressesGroundedPushWhileAirborne => true;

	public override bool CanStart(FighterController fighter, AbilityRuntime runtime)
	{
		if (!fighter.WasGrounded || !fighter.ActionInput.Special1Pressed ||
			!fighter.HasGasMeter(GasCost)) return false;
		float relativeHorizontal = fighter.CurrentInput.Horizontal * fighter.Facing;
		return Direction == JetEscapeDirection.Forward
			? relativeHorizontal >= DirectionThreshold
			: relativeHorizontal <= -DirectionThreshold;
	}

	public override void Start(FighterController fighter, AbilityRuntime runtime)
	{
		if (!fighter.TrySpendGasMeter(GasCost)) return;
		base.Start(fighter, runtime);
		if (fighter.IsInsideTree())
			fighter.GetNodeOrNull<Node>("/root/AudioController")?.Call("play_mecha_boost");
		runtime.FramesRemaining = Mathf.Max(1, ActiveFrames);
		float direction = Direction == JetEscapeDirection.Forward ? fighter.Facing : -fighter.Facing;
		fighter.Velocity = new Vector2(direction * Speed, -Mathf.Abs(VerticalSpeed));
		fighter.BeginMovementInvulnerability(InvulnerabilityFrames);
	}

	public override bool Tick(FighterController fighter, AbilityRuntime runtime, float delta) =>
		runtime.FramesRemaining-- > 0;
}

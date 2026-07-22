using Godot;
using ModularFighter.Core;

namespace ModularFighter.Movement;

/// <summary>
/// A stateless, shareable ability definition. Make a new subclass for a new archetype;
/// do not add archetype checks to FighterController.
/// </summary>
[Tool, GlobalClass]
public abstract partial class MovementAbility : Resource
{
	[Export] public string Id { get; set; } = "ability_id";
	[Export] public int Priority { get; set; }
	public virtual bool OwnsHorizontalVelocity => false;
	public virtual bool OwnsGravity => false;
	/// <summary>Prevents this airborne movement from pushing a grounded opponent.</summary>
	public virtual bool SuppressesGroundedPushWhileAirborne => false;
	/// <summary>Lets this movement retain horizontal steering for its full airborne lifetime.</summary>
	public virtual bool EnablesAirControlWhileAirborne => false;
	/// <summary>Multiplier used for air deceleration after directional input is released.</summary>
	public virtual float AirDecelerationMultiplierWhileAirborne => 1f;
	/// <summary>Discard queued actions while this move is active. Use for deliberately strict techniques.</summary>
	[Export] public bool SuspendsInputBufferWhileActive { get; set; }

	public abstract bool CanStart(FighterController fighter, AbilityRuntime runtime);
	public virtual void Start(FighterController fighter, AbilityRuntime runtime) => runtime.Active = true;
	/// <returns>True while the ability remains active.</returns>
	public virtual bool Tick(FighterController fighter, AbilityRuntime runtime, float delta) => false;
	public virtual void Stop(FighterController fighter, AbilityRuntime runtime) => runtime.Active = false;
	public virtual bool CanBeInterruptedBy(MovementAbility incoming) => incoming.Priority >= Priority;
}

using Godot;
using ModularFighter.Core;

namespace ModularFighter.Characters;

/// <summary>
/// Minimal command relay for a clone. A production clone spawner creates a FighterController
/// scene, assigns an owner, then uses this relay to choose whether it mirrors or follows orders.
/// </summary>
public partial class CloneCommandRelay : Node
{
	[Export] public FighterController Clone { get; set; }
	[Export] public FighterController OwnerFighter { get; set; }
	[Export] public bool MirrorOwnerMovement { get; set; } = true;

	public override void _Ready()
	{
		if (Clone != null) Clone.ReadLocalInput = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Clone == null || OwnerFighter == null || !MirrorOwnerMovement) return;
		var ownerInput = OwnerFighter.CurrentInput;
		// Clones may receive the same command, a delayed command history, or scripted inputs.
		Clone.SetExternalInput(new FighterInput(ownerInput.Horizontal, ownerInput.Vertical,
			ownerInput.JumpPressed, ownerInput.JumpHeld, ownerInput.DashPressed, ownerInput.FlightHeld,
			ownerInput.LightPunchPressed, ownerInput.LightPunchHeld,
			ownerInput.LightKickPressed, ownerInput.LightKickHeld,
			ownerInput.HeavyPunchPressed, ownerInput.HeavyPunchHeld,
			ownerInput.HeavyKickPressed, ownerInput.HeavyKickHeld,
			ownerInput.Special1Pressed, ownerInput.Special1Held,
			ownerInput.Special2Pressed, ownerInput.Special2Held));
	}
}

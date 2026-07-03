using Godot;
using ModularFighter.Core;

namespace ModularFighter.Characters;

/// <summary>
/// Gives one player direct control over either member of a pair. Both bodies still run the
/// same FighterController simulation, so collisions and hit reactions remain independent.
/// Attach this to a parent Node2D and assign two child fighters in the Inspector.
/// </summary>
public partial class PuppetCoordinator : Node
{
	[Export] public FighterController Primary { get; set; }
	[Export] public FighterController Puppet { get; set; }
	[Export] public StringName SwapAction { get; set; } = "swap_control";
	[Export] public bool PuppetFollowsPrimary { get; set; } = true;
	[Export] public float FollowDeadZone { get; set; } = 90f;
	[Export] public int ActiveIndex { get; private set; }

	public override void _Ready()
	{
		if (Primary != null) Primary.ReadLocalInput = false;
		if (Puppet != null) Puppet.ReadLocalInput = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Primary == null || Puppet == null) return;
		if (Input.IsActionJustPressed(SwapAction)) ActiveIndex = 1 - ActiveIndex;

		FighterInput playerInput = FighterInput.ReadLocal();
		FighterController active = ActiveIndex == 0 ? Primary : Puppet;
		FighterController inactive = ActiveIndex == 0 ? Puppet : Primary;
		active.SetExternalInput(playerInput);
		inactive.SetExternalInput(BuildPuppetInput(inactive, active));
	}

	private FighterInput BuildPuppetInput(FighterController puppet, FighterController owner)
	{
		if (!PuppetFollowsPrimary) return new FighterInput();
		float distance = owner.GlobalPosition.X - puppet.GlobalPosition.X;
		float horizontal = Mathf.Abs(distance) > FollowDeadZone ? Mathf.Sign(distance) : 0f;
		return new FighterInput(horizontal, 0, false, false, false, false);
	}
}

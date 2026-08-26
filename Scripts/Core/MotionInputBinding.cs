using Godot;

namespace ModularFighter.Core;

/// <summary>Per-move binding between a reusable motion and the buttons that activate it.</summary>
[Tool, GlobalClass]
public partial class MotionInputBinding : Resource
{
	[Export] public MotionInputDefinition Motion { get; set; }
	[Export(PropertyHint.Flags, "Light Punch,Heavy Punch,Light Kick,Heavy Kick")]
	public MotionAttackButton Buttons { get; set; } = MotionAttackButton.LightPunch;
	[Export] public MotionButtonMatchMode ButtonMatchMode { get; set; } = MotionButtonMatchMode.AnySelectedButton;
	[Export] public int Priority { get; set; }
	[Export] public bool GroundOnly { get; set; } = true;
	[Export] public bool AirOnly { get; set; }
	[Export] public bool ConsumeOnUse { get; set; } = true;
	[ExportGroup("Mash Override")]
	[Export(PropertyHint.Range, "0,240,1")]
	public int MashWindowFramesOverride { get; set; }
}

using Godot;

namespace ModularFighter.Core;

/// <summary>Reusable HUD and capacity data for a fighter's life and special-resource gauges.</summary>
[Tool, GlobalClass]
public partial class FighterGaugeData : Resource
{
	[Export] public int MaxLife { get; set; } = 1000;
	[Export] public int StartingLife { get; set; } = 1000;
	[Export] public string SpecialMeterName { get; set; } = "CHAKRA";
	[Export] public int MaxSpecialMeter { get; set; } = 300;
	[Export] public int StartingSpecialMeter { get; set; }
	[Export] public float SpecialMeterRecoveryPerSecond { get; set; }
	[Export] public int SpecialMeterRecoveryDelayFrames { get; set; }
	[Export] public Color LifeColor { get; set; } = new(0.25f, 0.9f, 0.3f);
	[Export] public Color SpecialMeterColor { get; set; } = new(0.18f, 0.62f, 1f);
}

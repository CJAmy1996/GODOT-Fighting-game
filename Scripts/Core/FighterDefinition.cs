using Godot;
using ModularFighter.Movement;
using System;

namespace ModularFighter.Core;

/// <summary>All character movement differences live in this asset and its ability assets.</summary>
[Tool, GlobalClass]
public partial class FighterDefinition : Resource
{
	[Export] public string FighterName { get; set; } = "New Fighter";
	[Export] public bool AllowLegacyFallbackMoves { get; set; } = true;
	[ExportGroup("Universal Super Presentation")]
	[Export] public Texture2D SuperPortrait { get; set; }
	[ExportGroup("Fighter Data")]
	[Export] public FighterGaugeData Gauges { get; set; }
	[Export] public MovementTuning Tuning { get; set; }
	[Export] public MovementAbility[] Abilities { get; set; } = Array.Empty<MovementAbility>();
	[Export] public NormalMoveSet NormalMoves { get; set; }
	[Export] public NormalMoveSet StateBoxes { get; set; }
	[Export] public SpecialMoveSet SpecialMoves { get; set; }
	[Export] public SuperMoveData[] SuperMoves { get; set; } = Array.Empty<SuperMoveData>();
	[Export] public CancelRule[] CancelRules { get; set; } = Array.Empty<CancelRule>();
}

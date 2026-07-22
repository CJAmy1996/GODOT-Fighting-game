using Godot;
using ModularFighter.Movement;
using System;

namespace ModularFighter.Core;

/// <summary>All character movement differences live in this asset and its ability assets.</summary>
[Tool, GlobalClass]
public partial class FighterDefinition : Resource
{
	[Export] public string FighterName { get; set; } = "New Fighter";
	[Export] public MovementTuning Tuning { get; set; }
	[Export] public MovementAbility[] Abilities { get; set; } = Array.Empty<MovementAbility>();
	[Export] public NormalMoveSet NormalMoves { get; set; }
	[Export] public SpecialMoveSet SpecialMoves { get; set; }
	[Export] public SuperMoveData[] SuperMoves { get; set; } = Array.Empty<SuperMoveData>();
	[Export] public CancelRule[] CancelRules { get; set; } = Array.Empty<CancelRule>();
}

using Godot;
using ModularFighter.Movement;
using System;

namespace ModularFighter.Core;

/// <summary>All character movement differences live in this asset and its ability assets.</summary>
[GlobalClass]
public partial class FighterDefinition : Resource
{
	[Export] public string FighterName { get; set; } = "New Fighter";
	[Export] public MovementTuning Tuning { get; set; }
	[Export] public MovementAbility[] Abilities { get; set; } = Array.Empty<MovementAbility>();
	[Export] public NormalMoveSet NormalMoves { get; set; }
	[Export] public CancelRule[] CancelRules { get; set; } = Array.Empty<CancelRule>();
}

using System;
using Godot;

namespace ModularFighter.Core;

[Tool, GlobalClass]
public partial class SpecialMoveSet : Resource
{
	[Export] public SpecialMoveData[] Moves { get; set; } = Array.Empty<SpecialMoveData>();

	public SpecialMoveData FindMove(string attackName, bool startedCrouching, bool startedAirborne)
	{
		foreach (SpecialMoveData move in Moves)
			if (move != null && move.Matches(attackName, startedCrouching, startedAirborne))
				return move;
		return null;
	}
}

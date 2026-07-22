using System;
using Godot;

namespace ModularFighter.Core;

/// <summary>
/// Character-level normal cancel rules. Rule order matters: put specific rules
/// before broad rules such as LIGHT or ANY.
/// </summary>
[Tool, GlobalClass]
public partial class NormalMoveSet : Resource
{
	[Export] public NormalMoveData[] Rules { get; set; } = Array.Empty<NormalMoveData>();

	public NormalMoveData FindRule(string attackName, bool startedCrouching, bool startedAirborne)
	{
		foreach (var rule in Rules)
			if (rule != null && rule.Matches(attackName, startedCrouching, startedAirborne))
				return rule;
		return null;
	}
}

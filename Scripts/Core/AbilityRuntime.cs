using Godot;

namespace ModularFighter.Core;

/// <summary>Per-fighter mutable state. Never put timers or counters on shared Resource assets.</summary>
public sealed class AbilityRuntime
{
	public int FramesRemaining;
	public int UsesThisAirTime;
	public bool Active;
	public int IntValue;
	public int IntValue2;
	public float FloatValue;
	public bool BoolValue;
	public Vector2 VectorValue;
}

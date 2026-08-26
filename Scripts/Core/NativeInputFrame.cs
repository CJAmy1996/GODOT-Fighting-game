using System;

namespace ModularFighter.Core;

/// <summary>
/// Stable bit layout sent over the network or stored for rollback/replay. Never reorder existing bits.
/// </summary>
[Flags]
public enum NativeInputButtons : uint
{
	None = 0,
	Left = 1u << 0,
	Right = 1u << 1,
	Up = 1u << 2,
	Down = 1u << 3,
	Jump = 1u << 4,
	Dash = 1u << 5,
	Flight = 1u << 6,
	LightPunch = 1u << 7,
	LightKick = 1u << 8,
	HeavyPunch = 1u << 9,
	HeavyKick = 1u << 10,
	Special1 = 1u << 11,
	Special2 = 1u << 12,
	MenuAccept = 1u << 13,
	Pause = 1u << 14,
	Swap = 1u << 15
}

/// <summary>
/// Immutable 60 Hz input packet. Held is sufficient for network transport; Pressed and Released are
/// included so a rollback host can replay the exact edge decisions without consulting live hardware.
/// </summary>
public readonly struct NativeInputFrame
{
	public NativeInputFrame(long simulationFrame, int playerIndex, NativeInputButtons held,
		NativeInputButtons pressed, NativeInputButtons released)
	{
		SimulationFrame = simulationFrame;
		PlayerIndex = playerIndex;
		Held = held;
		Pressed = pressed;
		Released = released;
	}

	public long SimulationFrame { get; }
	public int PlayerIndex { get; }
	public NativeInputButtons Held { get; }
	public NativeInputButtons Pressed { get; }
	public NativeInputButtons Released { get; }
	public uint NetworkWord => (uint)Held;

	public bool IsHeld(NativeInputButtons button) => (Held & button) != 0;
	public bool WasPressed(NativeInputButtons button) => (Pressed & button) != 0;
	public bool WasReleased(NativeInputButtons button) => (Released & button) != 0;

	public FighterInput ToFighterInput()
	{
		float horizontal = (IsHeld(NativeInputButtons.Right) ? 1f : 0f) -
			(IsHeld(NativeInputButtons.Left) ? 1f : 0f);
		float vertical = (IsHeld(NativeInputButtons.Down) ? 1f : 0f) -
			(IsHeld(NativeInputButtons.Up) ? 1f : 0f);
		return new FighterInput(horizontal, vertical,
			WasPressed(NativeInputButtons.Jump), IsHeld(NativeInputButtons.Jump),
			WasPressed(NativeInputButtons.Dash), IsHeld(NativeInputButtons.Flight),
			WasPressed(NativeInputButtons.LightPunch), IsHeld(NativeInputButtons.LightPunch),
			WasPressed(NativeInputButtons.LightKick), IsHeld(NativeInputButtons.LightKick),
			WasPressed(NativeInputButtons.HeavyPunch), IsHeld(NativeInputButtons.HeavyPunch),
			WasPressed(NativeInputButtons.HeavyKick), IsHeld(NativeInputButtons.HeavyKick),
			WasPressed(NativeInputButtons.Special1), IsHeld(NativeInputButtons.Special1),
			WasPressed(NativeInputButtons.Special2), IsHeld(NativeInputButtons.Special2),
			WasPressed(NativeInputButtons.Flight), WasReleased(NativeInputButtons.Flight),
			WasReleased(NativeInputButtons.Special1));
	}

	public static NativeInputFrame FromNetworkWord(long simulationFrame, int playerIndex, uint heldWord,
		uint previousHeldWord) => new(simulationFrame, playerIndex, (NativeInputButtons)heldWord,
		(NativeInputButtons)(heldWord & ~previousHeldWord),
		(NativeInputButtons)(previousHeldWord & ~heldWord));
}

namespace ModularFighter.Core;

/// <summary>Input sampled once per simulation frame. Replace this with network input for rollback.</summary>
public readonly struct FighterInput
{
	public readonly float Horizontal;
	public readonly float Vertical;
	public readonly bool JumpPressed;
	public readonly bool JumpHeld;
	public readonly bool DashPressed;
	public readonly bool FlightHeld;
	public readonly bool LightPunchPressed;
	public readonly bool LightPunchHeld;
	public readonly bool LightKickPressed;
	public readonly bool LightKickHeld;
	public readonly bool HeavyPunchPressed;
	public readonly bool HeavyPunchHeld;
	public readonly bool HeavyKickPressed;
	public readonly bool HeavyKickHeld;
	public readonly bool Special1Pressed;
	public readonly bool Special1Held;
	public readonly bool Special2Pressed;
	public readonly bool Special2Held;
	public readonly bool FlightPressed;
	public readonly bool FlightReleased;
	public readonly bool Special1Released;

	public FighterInput(float horizontal, float vertical, bool jumpPressed, bool jumpHeld, bool dashPressed, bool flightHeld,
		bool lightPunchPressed = false, bool lightPunchHeld = false,
		bool lightKickPressed = false, bool lightKickHeld = false,
		bool heavyPunchPressed = false, bool heavyPunchHeld = false,
		bool heavyKickPressed = false, bool heavyKickHeld = false,
		bool special1Pressed = false, bool special1Held = false,
		bool special2Pressed = false, bool special2Held = false,
		bool flightPressed = false, bool flightReleased = false, bool special1Released = false)
	{
		Horizontal = horizontal;
		Vertical = vertical;
		JumpPressed = jumpPressed;
		JumpHeld = jumpHeld;
		DashPressed = dashPressed;
		FlightHeld = flightHeld;
		LightPunchPressed = lightPunchPressed;
		LightPunchHeld = lightPunchHeld;
		LightKickPressed = lightKickPressed;
		LightKickHeld = lightKickHeld;
		HeavyPunchPressed = heavyPunchPressed;
		HeavyPunchHeld = heavyPunchHeld;
		HeavyKickPressed = heavyKickPressed;
		HeavyKickHeld = heavyKickHeld;
		Special1Pressed = special1Pressed;
		Special1Held = special1Held;
		Special2Pressed = special2Pressed;
		Special2Held = special2Held;
		FlightPressed = flightPressed;
		FlightReleased = flightReleased;
		Special1Released = special1Released;
	}

	/// <summary>Compatibility entry point backed by the deterministic native frame router.</summary>
	public static FighterInput ReadLocal(int playerIndex = 0) =>
		NativeInputRouter.GetCurrentGameplayInput(playerIndex);
}

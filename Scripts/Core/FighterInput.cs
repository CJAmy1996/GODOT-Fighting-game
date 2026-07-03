using Godot;

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

	public FighterInput(float horizontal, float vertical, bool jumpPressed, bool jumpHeld, bool dashPressed, bool flightHeld,
		bool lightPunchPressed = false, bool lightPunchHeld = false,
		bool lightKickPressed = false, bool lightKickHeld = false,
		bool heavyPunchPressed = false, bool heavyPunchHeld = false,
		bool heavyKickPressed = false, bool heavyKickHeld = false,
		bool special1Pressed = false, bool special1Held = false,
		bool special2Pressed = false, bool special2Held = false)
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
	}

	public static FighterInput ReadLocal() => new(
		Input.GetAxis("move_left", "move_right"),
		Input.GetAxis("move_up", "move_down"),
		Input.IsActionJustPressed("jump"), Input.IsActionPressed("jump"),
		Input.IsActionJustPressed("dash"), Input.IsActionPressed("flight"),
		Input.IsActionJustPressed("light_punch"), Input.IsActionPressed("light_punch"),
		Input.IsActionJustPressed("light_kick"), Input.IsActionPressed("light_kick"),
		Input.IsActionJustPressed("heavy_punch"), Input.IsActionPressed("heavy_punch"),
		Input.IsActionJustPressed("heavy_kick"), Input.IsActionPressed("heavy_kick"),
		Input.IsActionJustPressed("special_1"), Input.IsActionPressed("special_1"),
		Input.IsActionJustPressed("special_2"), Input.IsActionPressed("special_2"));
}

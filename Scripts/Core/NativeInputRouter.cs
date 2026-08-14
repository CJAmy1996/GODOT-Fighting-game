using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace ModularFighter.Core;

/// <summary>Raw device state returned by a platform backend before frame edges are calculated.</summary>
public readonly struct NativeDeviceSample
{
	public NativeDeviceSample(NativeInputButtons held) => Held = held;
	public NativeInputButtons Held { get; }
}

/// <summary>Implement this interface for another native platform or a deterministic test device.</summary>
public interface INativeInputBackend
{
	NativeDeviceSample Poll(int playerIndex);
}

/// <summary>
/// Samples native hardware exactly once per 60 Hz simulation frame and retains immutable input packets
/// for rollback, replay, and remote-input replacement. Motions consume FighterInput derived from these
/// packets, never live device state.
/// </summary>
public static class NativeInputRouter
{
	public const int RollbackHistoryFrames = 720;
	private const int MaxPlayers = 4;
	private static readonly object Sync = new();
	private static readonly FrameHistory[] GameplayHistory = CreateHistories();
	private static readonly UiChannel[] UiHistory = CreateUiChannels();
	private static INativeInputBackend _backend = CreateDefaultBackend();

	private sealed class FrameHistory
	{
		public readonly NativeInputFrame[] Frames = new NativeInputFrame[RollbackHistoryFrames];
		public readonly bool[] Valid = new bool[RollbackHistoryFrames];
		public NativeInputButtons PreviousHeld;
		public long LatestFrame = -1;
	}

	private sealed class UiChannel
	{
		public ulong ProcessFrame = ulong.MaxValue;
		public NativeInputButtons PreviousHeld;
		public NativeInputFrame Current;
	}

	public static NativeInputFrame GetGameplayFrame(long simulationFrame, int playerIndex = 0)
	{
		playerIndex = NormalizePlayer(playerIndex);
		lock (Sync)
		{
			FrameHistory history = GameplayHistory[playerIndex];
			if (TryRead(history, simulationFrame, out NativeInputFrame cached)) return cached;

			NativeInputButtons held = _backend.Poll(playerIndex).Held;
			NativeInputFrame frame = new(simulationFrame, playerIndex, held,
				held & ~history.PreviousHeld, history.PreviousHeld & ~held);
			Store(history, frame);
			history.PreviousHeld = held;
			history.LatestFrame = Math.Max(history.LatestFrame, simulationFrame);
			return frame;
		}
	}

	public static FighterInput GetGameplayInput(long simulationFrame, int playerIndex = 0) =>
		GetGameplayFrame(simulationFrame, playerIndex).ToFighterInput();

	public static FighterInput GetCurrentGameplayInput(int playerIndex = 0) =>
		GetGameplayInput((long)Engine.GetPhysicsFrames(), playerIndex);

	/// <summary>Non-rollback UI channel. It remains native but can continue polling while the tree is paused.</summary>
	public static NativeInputFrame GetUiFrame(int playerIndex = 0)
	{
		playerIndex = NormalizePlayer(playerIndex);
		ulong processFrame = Engine.GetProcessFrames();
		lock (Sync)
		{
			UiChannel channel = UiHistory[playerIndex];
			if (channel.ProcessFrame == processFrame) return channel.Current;
			NativeInputButtons held = _backend.Poll(playerIndex).Held;
			channel.Current = new NativeInputFrame((long)processFrame, playerIndex, held,
				held & ~channel.PreviousHeld, channel.PreviousHeld & ~held);
			channel.PreviousHeld = held;
			channel.ProcessFrame = processFrame;
			return channel.Current;
		}
	}

	/// <summary>Returns a stored local/remote packet without polling hardware.</summary>
	public static bool TryGetStoredFrame(long simulationFrame, int playerIndex, out NativeInputFrame frame)
	{
		playerIndex = NormalizePlayer(playerIndex);
		lock (Sync) return TryRead(GameplayHistory[playerIndex], simulationFrame, out frame);
	}

	/// <summary>
	/// Installs a packet received from the network or replay. Call InvalidateAfter before resimulation when
	/// replacing an already-predicted frame range.
	/// </summary>
	public static void SubmitFrame(NativeInputFrame frame)
	{
		int playerIndex = NormalizePlayer(frame.PlayerIndex);
		NativeInputFrame normalized = new(frame.SimulationFrame, playerIndex, frame.Held, frame.Pressed, frame.Released);
		lock (Sync)
		{
			FrameHistory history = GameplayHistory[playerIndex];
			Store(history, normalized);
			if (normalized.SimulationFrame >= history.LatestFrame)
			{
				history.LatestFrame = normalized.SimulationFrame;
				history.PreviousHeld = normalized.Held;
			}
		}
	}

	public static void SubmitNetworkWord(long simulationFrame, int playerIndex, uint heldWord, uint previousHeldWord) =>
		SubmitFrame(NativeInputFrame.FromNetworkWord(simulationFrame, NormalizePlayer(playerIndex), heldWord, previousHeldWord));

	/// <summary>Discards predicted packets after a corrected rollback frame.</summary>
	public static void InvalidateAfter(long simulationFrame, int playerIndex)
	{
		playerIndex = NormalizePlayer(playerIndex);
		lock (Sync)
		{
			FrameHistory history = GameplayHistory[playerIndex];
			for (int index = 0; index < history.Frames.Length; index++)
				if (history.Valid[index] && history.Frames[index].SimulationFrame > simulationFrame)
					history.Valid[index] = false;
			history.LatestFrame = simulationFrame;
			history.PreviousHeld = TryRead(history, simulationFrame, out NativeInputFrame frame)
				? frame.Held : NativeInputButtons.None;
		}
	}

	public static void Reset()
	{
		lock (Sync)
		{
			for (int player = 0; player < MaxPlayers; player++)
			{
				Array.Clear(GameplayHistory[player].Valid);
				GameplayHistory[player].PreviousHeld = NativeInputButtons.None;
				GameplayHistory[player].LatestFrame = -1;
				UiHistory[player].ProcessFrame = ulong.MaxValue;
				UiHistory[player].PreviousHeld = NativeInputButtons.None;
				UiHistory[player].Current = default;
			}
		}
	}

	public static void SetBackendForTesting(INativeInputBackend backend)
	{
		lock (Sync)
		{
			_backend = backend ?? throw new ArgumentNullException(nameof(backend));
			Reset();
		}
	}

	public static void RestorePlatformBackend()
	{
		lock (Sync)
		{
			_backend = CreateDefaultBackend();
			Reset();
		}
	}

	private static void Store(FrameHistory history, NativeInputFrame frame)
	{
		int index = HistoryIndex(frame.SimulationFrame);
		history.Frames[index] = frame;
		history.Valid[index] = true;
	}

	private static bool TryRead(FrameHistory history, long frameNumber, out NativeInputFrame frame)
	{
		int index = HistoryIndex(frameNumber);
		frame = history.Frames[index];
		return history.Valid[index] && frame.SimulationFrame == frameNumber;
	}

	private static int HistoryIndex(long frame) => (int)(((frame % RollbackHistoryFrames) + RollbackHistoryFrames) % RollbackHistoryFrames);
	private static int NormalizePlayer(int playerIndex) => Math.Clamp(playerIndex, 0, MaxPlayers - 1);
	private static FrameHistory[] CreateHistories() =>
		new[] { new FrameHistory(), new FrameHistory(), new FrameHistory(), new FrameHistory() };
	private static UiChannel[] CreateUiChannels() =>
		new[] { new UiChannel(), new UiChannel(), new UiChannel(), new UiChannel() };
	private static INativeInputBackend CreateDefaultBackend() =>
		OperatingSystem.IsWindows() ? new WindowsNativeInputBackend() : new GodotInputFallbackBackend();
}

/// <summary>Direct Win32 keyboard and XInput polling. No Godot InputMap state is consulted on Windows.</summary>
internal sealed class WindowsNativeInputBackend : INativeInputBackend
{
	private const int VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28;
	private const int VkReturn = 0x0D, VkEscape = 0x1B, VkShift = 0x10, VkTab = 0x09;
	private const ushort DpadUp = 0x0001, DpadDown = 0x0002, DpadLeft = 0x0004, DpadRight = 0x0008;
	private const ushort Start = 0x0010, Back = 0x0020, LeftShoulder = 0x0100, RightShoulder = 0x0200;
	private const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
	private const short StickDeadZone = 12000;
	private bool _xinputAvailable = true;

	public NativeDeviceSample Poll(int playerIndex)
	{
		if (!IsCurrentProcessForeground()) return default;
		NativeInputButtons held = playerIndex == 0 ? PollKeyboard() : NativeInputButtons.None;
		if (_xinputAvailable) held |= PollGamepad(playerIndex);
		return new NativeDeviceSample(held);
	}

	private static NativeInputButtons PollKeyboard()
	{
		NativeInputButtons held = NativeInputButtons.None;
		if (Key(0x41) || Key(VkLeft)) held |= NativeInputButtons.Left;
		if (Key(0x44) || Key(VkRight)) held |= NativeInputButtons.Right;
		if (Key(0x57) || Key(VkUp)) held |= NativeInputButtons.Up | NativeInputButtons.Jump;
		if (Key(0x53) || Key(VkDown)) held |= NativeInputButtons.Down;
		if (Key(VkShift)) held |= NativeInputButtons.Dash;
		if (Key(0x46)) held |= NativeInputButtons.Flight;
		if (Key(0x55)) held |= NativeInputButtons.LightPunch;
		if (Key(0x4A)) held |= NativeInputButtons.LightKick;
		if (Key(0x49)) held |= NativeInputButtons.HeavyPunch;
		if (Key(0x4B)) held |= NativeInputButtons.HeavyKick;
		if (Key(0x4F)) held |= NativeInputButtons.Special1;
		if (Key(0x4C)) held |= NativeInputButtons.Special2;
		if (Key(VkReturn)) held |= NativeInputButtons.MenuAccept;
		if (Key(VkEscape)) held |= NativeInputButtons.Pause;
		if (Key(VkTab)) held |= NativeInputButtons.Swap;
		return held;
	}

	private NativeInputButtons PollGamepad(int playerIndex)
	{
		try
		{
			if (XInputGetState((uint)playerIndex, out XInputState state) != 0) return NativeInputButtons.None;
			XInputGamepad pad = state.Gamepad;
			NativeInputButtons held = NativeInputButtons.None;
			if ((pad.Buttons & DpadLeft) != 0 || pad.ThumbLX < -StickDeadZone) held |= NativeInputButtons.Left;
			if ((pad.Buttons & DpadRight) != 0 || pad.ThumbLX > StickDeadZone) held |= NativeInputButtons.Right;
			if ((pad.Buttons & DpadUp) != 0 || pad.ThumbLY > StickDeadZone) held |= NativeInputButtons.Up | NativeInputButtons.Jump;
			if ((pad.Buttons & DpadDown) != 0 || pad.ThumbLY < -StickDeadZone) held |= NativeInputButtons.Down;
			if (pad.LeftTrigger > 30) held |= NativeInputButtons.Dash;
			if (pad.RightTrigger > 30) held |= NativeInputButtons.Flight;
			if ((pad.Buttons & X) != 0) held |= NativeInputButtons.LightPunch;
			if ((pad.Buttons & A) != 0) held |= NativeInputButtons.LightKick | NativeInputButtons.MenuAccept;
			if ((pad.Buttons & Y) != 0) held |= NativeInputButtons.HeavyPunch;
			if ((pad.Buttons & B) != 0) held |= NativeInputButtons.HeavyKick;
			if ((pad.Buttons & LeftShoulder) != 0) held |= NativeInputButtons.Special1;
			if ((pad.Buttons & RightShoulder) != 0) held |= NativeInputButtons.Special2;
			if ((pad.Buttons & Start) != 0) held |= NativeInputButtons.MenuAccept | NativeInputButtons.Pause;
			if ((pad.Buttons & Back) != 0) held |= NativeInputButtons.Swap;
			return held;
		}
		catch (DllNotFoundException) { _xinputAvailable = false; }
		catch (EntryPointNotFoundException) { _xinputAvailable = false; }
		catch (BadImageFormatException) { _xinputAvailable = false; }
		return NativeInputButtons.None;
	}

	private static bool Key(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

	private static bool IsCurrentProcessForeground()
	{
		IntPtr window = GetForegroundWindow();
		if (window == IntPtr.Zero) return true;
		GetWindowThreadProcessId(window, out uint processId);
		return processId == (uint)System.Environment.ProcessId;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct XInputState { public uint PacketNumber; public XInputGamepad Gamepad; }

	[StructLayout(LayoutKind.Sequential)]
	private struct XInputGamepad
	{
		public ushort Buttons;
		public byte LeftTrigger;
		public byte RightTrigger;
		public short ThumbLX;
		public short ThumbLY;
		public short ThumbRX;
		public short ThumbRY;
	}

	[DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
	[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
	[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
	[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
	private static extern uint XInputGetState(uint userIndex, out XInputState state);
}

/// <summary>Unsupported-platform fallback. It is still sampled once per deterministic frame.</summary>
internal sealed class GodotInputFallbackBackend : INativeInputBackend
{
	public NativeDeviceSample Poll(int playerIndex)
	{
		if (playerIndex != 0) return default;
		NativeInputButtons held = NativeInputButtons.None;
		if (Input.IsActionPressed("move_left")) held |= NativeInputButtons.Left;
		if (Input.IsActionPressed("move_right")) held |= NativeInputButtons.Right;
		if (Input.IsActionPressed("move_up")) held |= NativeInputButtons.Up | NativeInputButtons.Jump;
		if (Input.IsActionPressed("move_down")) held |= NativeInputButtons.Down;
		if (Input.IsActionPressed("dash")) held |= NativeInputButtons.Dash;
		if (Input.IsActionPressed("flight")) held |= NativeInputButtons.Flight;
		if (Input.IsActionPressed("light_punch")) held |= NativeInputButtons.LightPunch;
		if (Input.IsActionPressed("light_kick")) held |= NativeInputButtons.LightKick;
		if (Input.IsActionPressed("heavy_punch")) held |= NativeInputButtons.HeavyPunch;
		if (Input.IsActionPressed("heavy_kick")) held |= NativeInputButtons.HeavyKick;
		if (Input.IsActionPressed("special_1")) held |= NativeInputButtons.Special1;
		if (Input.IsActionPressed("special_2")) held |= NativeInputButtons.Special2;
		if (Input.IsActionPressed("ui_accept")) held |= NativeInputButtons.MenuAccept;
		if (Input.IsKeyPressed(Key.Escape)) held |= NativeInputButtons.Pause;
		return new NativeDeviceSample(held);
	}
}

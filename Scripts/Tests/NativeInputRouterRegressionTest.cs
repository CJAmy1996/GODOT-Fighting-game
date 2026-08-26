using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

/// <summary>Verifies native sampling, frame caching, edge derivation, and rollback packet APIs.</summary>
public partial class NativeInputRouterRegressionTest : Node
{
	private sealed class FakeBackend : INativeInputBackend
	{
		private readonly NativeInputButtons[] _samples;
		public FakeBackend(params NativeInputButtons[] samples) => _samples = samples;
		public int PollCount { get; private set; }

		public NativeDeviceSample Poll(int playerIndex)
		{
			int index = Math.Min(PollCount, _samples.Length - 1);
			PollCount++;
			return new NativeDeviceSample(_samples[index]);
		}
	}

	public override void _Ready()
	{
		try
		{
			var backend = new FakeBackend(
				NativeInputButtons.Left | NativeInputButtons.LightPunch,
				NativeInputButtons.Left | NativeInputButtons.LightPunch,
				NativeInputButtons.Right | NativeInputButtons.HeavyPunch);
			NativeInputRouter.SetBackendForTesting(backend);

			NativeInputFrame frame100 = NativeInputRouter.GetGameplayFrame(100, 0);
			NativeInputFrame repeated100 = NativeInputRouter.GetGameplayFrame(100, 0);
			Expect(backend.PollCount == 1, "hardware was polled more than once for one simulation frame");
			Expect(frame100.Held == repeated100.Held && frame100.Pressed == repeated100.Pressed,
				"same-frame reads returned different immutable packets");
			Expect(frame100.WasPressed(NativeInputButtons.Left | NativeInputButtons.LightPunch),
				"first native frame did not create direction/button press edges");

			FighterInput fighter100 = frame100.ToFighterInput();
			Expect(fighter100.Horizontal == -1f && fighter100.LightPunchPressed && fighter100.LightPunchHeld,
				"native packet did not convert to FighterInput correctly");

			NativeInputFrame frame101 = NativeInputRouter.GetGameplayFrame(101, 0);
			Expect(frame101.Pressed == NativeInputButtons.None,
				"held hardware state incorrectly repeated pressed edges on the next tick");
			NativeInputFrame frame102 = NativeInputRouter.GetGameplayFrame(102, 0);
			Expect(frame102.WasPressed(NativeInputButtons.Right | NativeInputButtons.HeavyPunch) &&
				frame102.WasReleased(NativeInputButtons.Left | NativeInputButtons.LightPunch),
				"native edge transition was not deterministic");

			Expect(NativeInputRouter.TryGetStoredFrame(100, 0, out NativeInputFrame historical) &&
				historical.NetworkWord == frame100.NetworkWord,
				"rollback history did not retain the original local packet");

			uint remoteHeld = (uint)(NativeInputButtons.Down | NativeInputButtons.LightKick);
			NativeInputRouter.SubmitNetworkWord(77, 1, remoteHeld, 0u);
			Expect(NativeInputRouter.TryGetStoredFrame(77, 1, out NativeInputFrame remote) &&
				remote.ToFighterInput().Vertical == 1f && remote.ToFighterInput().LightKickPressed,
				"network word did not enter the same FighterInput/motion pipeline");

			NativeInputRouter.InvalidateAfter(101, 0);
			Expect(!NativeInputRouter.TryGetStoredFrame(102, 0, out _),
				"rollback invalidation retained a predicted future frame");
			GD.Print("NATIVE INPUT ROUTER TEST PASSED: one native poll per tick, stable packets, rollback history/invalidation, and network input conversion work.");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"NATIVE INPUT ROUTER TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
		finally
		{
			NativeInputRouter.RestorePlatformBackend();
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class MotionInputBufferTests : Node
{
	[Export] public bool RunOnReady { get; set; } = true;

	private const int InputBufferFrames = 3;
	private const int DoubleTapWindowFrames = 12;
	private const int QuarterCircleForwardWindowFrames = 20;
	private const int QuarterCircleForwardLatchFrames = 18;
	private const int BackDashInputLockoutWindowFrames = 18;

	public override void _Ready()
	{
		if (!RunOnReady) return;
		try
		{
			GD.Print(RunAll());
		}
		catch (Exception exception)
		{
			GD.PushError(exception.Message);
		}
	}

	public static string RunAll()
	{
		TestQuarterCircleForward();
		TestQuarterCircleForwardMirrorsWithFacing();
		TestLenientQuarterCircleForwardWindow();
		TestQuarterCircleForwardExpires();
		TestDoubleTapDash();
		TestDoubleTapOutsideWindowDoesNotDash();
		TestDownThenUpCommandWindow();
		return "Motion input tests passed: QCF, mirrored QCF, expiry, double-tap dash, dash timeout, down-up.";
	}

	private static void TestQuarterCircleForward()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressDown();
		Tick(buffer, 6);
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Expect(buffer.HasQuarterCircleForwardCommand, "QCF should be stored after down, forward.");
	}

	private static void TestQuarterCircleForwardMirrorsWithFacing()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressDown();
		Tick(buffer, 4);
		buffer.PressHorizontalTap(-1, -1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Expect(buffer.HasQuarterCircleForwardCommand, "QCF should use fighter-facing forward.");
	}

	private static void TestLenientQuarterCircleForwardWindow()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressDown();
		Tick(buffer, 19);
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Expect(buffer.HasQuarterCircleForwardCommand, "QCF should accept a 19-frame down-to-forward gap.");
	}

	private static void TestQuarterCircleForwardExpires()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressDown();
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Tick(buffer, QuarterCircleForwardLatchFrames);
		Expect(!buffer.HasQuarterCircleForwardCommand, "QCF should expire after its motion window.");
	}

	private static void TestDoubleTapDash()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Tick(buffer, DoubleTapWindowFrames);
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Expect(buffer.HasDashCommand && buffer.DashCommandDirection == 1, "Double-tap forward should store a forward dash.");
	}

	private static void TestDoubleTapOutsideWindowDoesNotDash()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Tick(buffer, DoubleTapWindowFrames + 1);
		buffer.PressHorizontalTap(1, 1, InputBufferFrames, DoubleTapWindowFrames,
			QuarterCircleForwardWindowFrames, QuarterCircleForwardLatchFrames, BackDashInputLockoutWindowFrames);
		Expect(!buffer.HasDashCommand, "Double-tap should fail outside the dash window.");
	}

	private static void TestDownThenUpCommandWindow()
	{
		var buffer = new MotionInputBuffer();
		buffer.PressDown();
		Tick(buffer, 4);
		buffer.PressJump(InputBufferFrames);
		Expect(buffer.IsDownThenUpCommand(4), "Down-up should pass inside the requested window.");
		Expect(!buffer.IsDownThenUpCommand(3), "Down-up should fail outside the requested window.");
	}

	private static void Tick(MotionInputBuffer buffer, int frames)
	{
		for (int i = 0; i < frames; i++)
			buffer.Tick();
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException($"Motion input test failed: {message}");
	}
}

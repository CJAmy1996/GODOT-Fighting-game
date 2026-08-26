using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class AttackStateMachineRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			AttackStateMachine timeline = new();
			timeline.Begin(2, 3, 4);
			Expect(timeline.Frame == -1 && timeline.StartupFramesLeft == 2, "attack did not begin before frame zero");
			Expect(!Tick(timeline).EnteredActive && timeline.Frame == 0 && timeline.StartupFramesLeft == 1,
				"first startup tick changed timing");
			AttackTimelineTickResult activation = Tick(timeline);
			Expect(activation.EnteredActive && timeline.Frame == 1 && timeline.ActiveFramesLeft == 3,
				"startup-to-active transition changed timing");
			Tick(timeline);
			Tick(timeline);
			Tick(timeline);
			Expect(timeline.RecoveryFramesLeft == 5, "recovery did not retain its compatibility counter step");
			for (int i = 0; i < 4; i++) Expect(!Tick(timeline).Completed, "recovery ended early");
			Expect(Tick(timeline).Completed && !timeline.IsAttacking, "recovery did not end on schedule");

			timeline.Begin(1, 2, 1);
			Expect(Tick(timeline, holdStartup: true).HeldStartup && timeline.Frame == -1,
				"charged startup advanced while held");
			Tick(timeline);
			Tick(timeline, holdActive: true);
			Expect(timeline.ActiveFramesLeft == 2 && timeline.Frame == 1, "held active frame did not preserve active time");

			timeline.BeginRecovery(3);
			Expect(timeline.RecoveryFrames == 3 && timeline.RecoveryFramesLeft == 3, "forced recovery was not authoritative");
			timeline.Clear();
			Expect(timeline.Frame == 0 && !timeline.IsAttacking, "clear left attack timeline state behind");
			GD.Print("ATTACK_STATE_MACHINE_TEST_PASS startup=preserved active=preserved recovery=preserved holds=preserved");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"ATTACK_STATE_MACHINE_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}

	private static AttackTimelineTickResult Tick(AttackStateMachine timeline,
		bool holdStartup = false, bool holdActive = false, bool holdRecovery = false)
	{
		AttackTimelineTickResult result = timeline.Tick(holdStartup, holdActive, holdRecovery);
		if (!result.HeldStartup && !result.Completed) timeline.AdvanceFrame();
		return result;
	}
}

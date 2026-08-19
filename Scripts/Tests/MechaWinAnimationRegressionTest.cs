using System;
using Godot;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

public partial class MechaWinAnimationRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			SpriteTestFighter fighter = GD.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
				.Instantiate<SpriteTestFighter>();
			AddChild(fighter);
			fighter.BeginWinAnimation();
			fighter.SetPhysicsProcess(false);
			Expect(fighter.IsPlayingWinAnimation && fighter.CharacterSprite.Animation == "win" &&
				fighter.CharacterSprite.IsPlaying() && Mathf.IsEqualApprox(fighter.CharacterSprite.SpeedScale, 1f),
				"Mecha win animation did not start before KO physics was disabled");

			SpriteTestFighter defeated = GD.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
				.Instantiate<SpriteTestFighter>();
			AddChild(defeated);
			defeated.BeginDefeatedKoState();
			defeated.SetPhysicsProcess(false);
			Expect(defeated.IsGroundedKnockdown && defeated.CharacterSprite.Animation == "knockdown" &&
				defeated.CharacterSprite.Frame == defeated.CharacterSprite.SpriteFrames.GetFrameCount("knockdown") - 1,
				"defeated fighter did not remain on the final knockdown pose through KO");
			GD.Print("MECHA_WIN_ANIMATION_TEST_PASS immediate-win-before-ko-freeze defeated=held-knockdown");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"MECHA_WIN_ANIMATION_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

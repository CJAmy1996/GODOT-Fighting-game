using System;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Locks Mecha Heita's QCF+LK+HK Shinryuken to the universal super presentation.</summary>
public partial class MechaShinryukenSuperEffectRegressionTest : Node2D
{
	private const string ShinryukenName = "MECHA SHINRYUKEN";
	private SpriteTestFighter _fighter;
	private int _stage;
	private int _settleFrames = 5;
	private int _watchdog = 90;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1200f, 20f) } });
		AddChild(floor);

		_fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/MechaHeita.tscn")
			.Instantiate<SpriteTestFighter>();
		_fighter.ReadLocalInput = false;
		_fighter.FaceWithMovement = false;
		_fighter.TeamId = 1;
		_fighter.Position = Vector2.Zero;
		_fighter.SetFacing(1);
		_fighter.SetExternalInput(default);
		_fighter.ResetPlaceholderGauges();
		AddChild(_fighter);
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, attack '{_fighter.CurrentAttackName}'");
			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					Expect(_fighter.IsOnFloor(), "Mecha Heita did not settle on the test floor");
					SpecialMoveData shinryuken = _fighter.Definition.SpecialMoves.FindMove(ShinryukenName, false, false);
					Expect(shinryuken?.CommandInput is
					{
						Buttons: MotionAttackButton.AnyKick,
						ButtonMatchMode: MotionButtonMatchMode.AllSelectedButtons
					}, "Shinryuken is not bound to the LK+HK chord");
					Expect(shinryuken.CommandInput.Motion?.MotionName == "Quarter Circle Forward",
						"Shinryuken is not bound to quarter-circle forward");
					Expect(shinryuken.TriggersSuperPresentation &&
						shinryuken.SuperActivationFreezeFrames == 45 && shinryuken.SuperBackdropFrames == 95,
						"Shinryuken is missing its configured super presentation");
					Expect(shinryuken.Launches && Mathf.IsEqualApprox(shinryuken.LaunchSpeed, 520f) &&
						Mathf.IsEqualApprox(shinryuken.LaunchPushback, 25f) &&
						!shinryuken.AddsGlobalHitstopBonus &&
						Mathf.IsEqualApprox(shinryuken.ContactHitstopMultiplier, 0.4f),
						"Shinryuken does not compensate its repeated lift for combo gravity with reduced hitstop");
					FighterBoxFrame finalHit = shinryuken.BoxTimeline[^1];
					Expect(finalHit.BlowAwayDirection != BlowAwayDirection.None && finalHit.KnocksDown,
						"Shinryuken's final hit does not knock the opponent away");
					Expect(_fighter.Definition.SuperPortrait != null,
						"Mecha Heita is missing his universal super portrait");
					_fighter.InjectMotionAction("move_down");
					_fighter.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false));
					_stage = 1;
					break;
				case 1:
					_fighter.InjectMotionAction("move_right");
					_fighter.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false,
						lightKickPressed: true, heavyKickPressed: true));
					_stage = 2;
					break;
				case 2:
					_fighter.SetExternalInput(default);
					Expect(_fighter.CurrentAttackName == ShinryukenName,
						$"QCF+LK+HK resolved as '{_fighter.CurrentAttackName}'");
					Expect(_fighter.CurrentAttackIsSpecial && _fighter.CurrentAttackAnimationName == "anim_145",
						"QCF+LK+HK did not preserve Mecha Shinryuken's authored move behavior");
					Expect(_fighter.CurrentAttackTriggersHyperComboFinish,
						"Shinryuken is not classified for Hyper Combo Finish on KO");
					Expect(_fighter.ConsumeSuperActivationData(out int freezeFrames, out int backdropFrames),
						"Shinryuken did not request the universal super activation");
					Expect(freezeFrames == 45 && backdropFrames == 95,
						$"Shinryuken requested {freezeFrames} freeze/{backdropFrames} backdrop frames instead of 45/95");
					GD.Print("MECHA_SHINRYUKEN_SUPER_EFFECT_TEST_PASS QCF+LK+HK freeze=45 backdrop=95");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"MECHA_SHINRYUKEN_SUPER_EFFECT_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

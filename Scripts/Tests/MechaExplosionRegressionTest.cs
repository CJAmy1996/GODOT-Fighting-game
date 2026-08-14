using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Exercises the charged-HP explosion through the real fighter collision path.</summary>
public partial class MechaExplosionRegressionTest : Node2D
{
	private SpriteTestFighter _mecha;
	private SpriteTestFighter _defender;
	private int _stage;
	private int _settleFrames = 5;
	private int _watchdog = 180;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1000f, 20f) } });
		AddChild(floor);

		_mecha = GD.Load<PackedScene>("res://Scenes/TestCharacters/MechaHeitaTest.tscn")
			.Instantiate<SpriteTestFighter>();
		_mecha.ReadLocalInput = false;
		_mecha.TeamId = 1;
		_mecha.Position = Vector2.Zero;
		_mecha.SetFacing(1);
		AddChild(_mecha);
		_mecha.SetExternalInput(default);

		_defender = GD.Load<PackedScene>("res://Scenes/TestCharacters/KungFuManTest.tscn")
			.Instantiate<SpriteTestFighter>();
		_defender.ReadLocalInput = false;
		_defender.TeamId = 2;
		_defender.Position = new Vector2(84f, 0f);
		_defender.SetFacing(-1);
		_defender.ProcessMode = ProcessModeEnum.Disabled;
		AddChild(_defender);
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, move '{_mecha.CurrentAttackName}', frame {_mecha.CurrentAttackFrame}");
			switch (_stage)
			{
				case 0:
					if (--_settleFrames > 0) return;
					Expect(_mecha.IsOnFloor(), "Mecha did not settle on the test floor");
					_mecha.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						heavyPunchPressed: true, heavyPunchHeld: true));
					_stage = 1;
					break;
				case 1:
					_mecha.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						heavyPunchHeld: true));
					if (!_mecha.IsAttackActive) return;
					Expect(_mecha.CurrentAttackName == "HEAVY PUNCH",
						$"charged HP resolved as '{_mecha.CurrentAttackName}'");
					// The combat active counter opens one tick before this source animation's authored frame-14 box.
					if (!_mecha.GetActiveWorldBoxes(FighterBoxKind.Hitbox).Any()) return;
					if (!_mecha.TryApplyBasicAttackHit(_defender, out _, out _, out _, out Vector2 hitPoint, out _))
					{
						string hitboxes = string.Join("; ", _mecha.GetActiveWorldBoxes(FighterBoxKind.Hitbox));
						string hurtboxes = string.Join("; ", _defender.GetActiveWorldBoxes(FighterBoxKind.Hurtbox));
						throw new InvalidOperationException($"charged HP did not contact on frame {_mecha.CurrentAttackFrame}; " +
							$"hitboxes=[{hitboxes}] hurtboxes=[{hurtboxes}]");
					}

					MoveVisualEffect explosion = GetChildren().OfType<MoveVisualEffect>().LastOrDefault();
					Expect(explosion != null, "confirmed charged HP did not spawn its explosion");
					Expect(explosion.GlobalPosition.IsEqualApprox(hitPoint),
						$"explosion spawned at {explosion.GlobalPosition}, not shared hitspark point {hitPoint}");
					AnimatedSprite2D explosionSprite = explosion.GetChildCount() > 0
						? explosion.GetChild(0) as AnimatedSprite2D
						: null;
					Expect(explosionSprite?.Animation == "system_explosion",
						"contact effect is not using the system explosion animation");
					Expect(explosionSprite?.Material is ShaderMaterial,
						"legacy opaque explosion is missing its additive black-key material");
					Expect(_defender.CharacterSprite.SelfModulate == Colors.Black,
						"defender did not become a black silhouette on explosion hit");
					int fireLayers = _defender.CharacterSprite.GetChildren()
						.Count(child => child.Name.ToString().StartsWith("SystemBurnFlame_"));
					Expect(fireLayers == 6, $"defender received {fireLayers} fire layers instead of 6");

					GD.Print($"MECHA_EXPLOSION_TEST_PASS punch_contact={hitPoint} " +
						$"silhouette=black fire_layers={fireLayers}");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"MECHA_EXPLOSION_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

using System;
using Godot;
using ModularFighter.Characters;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Locks Sanzou's S2/L Trait 2 parry and both authored reflector variants.</summary>
public partial class SanzouParryReflectorRegressionTest : Node2D
{
	private SanzoKongoumaruFighter _parryFighter;
	private SanzoKongoumaruFighter _attacker;
	private SanzoKongoumaruFighter _superReflectorProbe;
	private int _stage;
	private int _settleTicks = 5;
	private int _watchdog = 180;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1200f, 20f) } });
		AddChild(floor);
		_parryFighter = Spawn("ParrySanzou", 80f, 1, 1);
		_attacker = Spawn("AttackSanzou", 0f, 1, 2);
		_superReflectorProbe = Spawn("SuperReflectorProbe", -300f, 1, 3);
	}

	private SanzoKongoumaruFighter Spawn(string name, float x, int facing, int team)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
			.Instantiate<SanzoKongoumaruFighter>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.TeamId = team;
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(facing);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, parry '{_parryFighter.CurrentAttackName}' frame {_parryFighter.CurrentAttackFrame}");
			switch (_stage)
			{
				case 0:
					if (--_settleTicks > 0) return;
					ValidateConfiguredMovesAndReflectors();
					_parryFighter.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false,
						special2Pressed: true));
					LatchMotionEvent(_superReflectorProbe, "move_down");
					_superReflectorProbe.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false));
					_stage = 1;
					break;
				case 1:
					_parryFighter.SetExternalInput(default);
					LatchMotionEvent(_superReflectorProbe, "move_right");
					_superReflectorProbe.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false,
						lightKickPressed: true, heavyKickPressed: true));
					Expect(_parryFighter.CurrentAttackName == FighterController.SanzoParryName,
						$"L resolved as '{_parryFighter.CurrentAttackName}'");
					Expect(_parryFighter.CurrentAttackAnimationName == "trait_2", "S2/L parry did not use the Trait 2 animation");
					Expect(_parryFighter.IsParryWindowActive, "L did not open its parry window");
					Expect(!_parryFighter.IsPerformingSuperMove && !_parryFighter.SuperActivationFreezeRequested,
						"L parry still triggers super classification or activation effects");
					Expect(_parryFighter.CharacterSprite.Material is ShaderMaterial parryFlash &&
						(float)parryFlash.GetShaderParameter("white_flash_amount") > 0.9f,
						"Sanzou did not flash pure white when the parry window opened");
					_attacker.SetExternalInput(new FighterInput(0f, 0f, false, false, false, false, lightPunchPressed: true));
					_stage = 2;
					break;
				case 2:
					_superReflectorProbe.SetExternalInput(default);
					_attacker.SetExternalInput(default);
					if (!_attacker.IsAttackActive) return;
					_attacker.Position = Vector2.Zero;
					_parryFighter.Position = new Vector2(70f, 0f);
					Expect(_attacker.TryApplyBasicAttackHit(_parryFighter, out _, out _, out _, out _, out _),
						"active jab did not reach Sanzou's parry collision path");
					Expect(_attacker.LastContactWasParried, "contact was not marked as parried");
					Expect(_parryFighter.HitstunFramesLeft == 0, "successful parry incorrectly applied hitstun");
					Expect(_parryFighter.ParrySuccessSerial == 1, "successful parry did not fire its presentation event");
					Expect(_parryFighter.IsParrySuccessPresentationActive, "320 overlay window did not start");
					Expect(_parryFighter.GetNodeOrNull<Sprite2D>("ParryGuardFlash320") != null,
						"320 parry guard overlay was not created");
					_stage = 3;
					break;
				case 3:
					Expect(_parryFighter.CharacterSprite.Material == null,
						"parry-window white flash remained after the parry succeeded");
					Expect(_superReflectorProbe.CurrentAttackName == FighterController.SanzoSuperReflectorName,
						$"QCF+LK+HK resolved as '{_superReflectorProbe.CurrentAttackName}'");
					if (_superReflectorProbe.CurrentAttackFrame < 22) return;
					Expect(_superReflectorProbe.IsPerformingSuperMove,
						"Super Reflector is not classified as a real super");
					Expect(GetNodeOrNull<Sprite2D>("SuperAfterimage1")?.Visible == true,
						"Super Reflector never displayed shared super afterimages");
					GD.Print("SANZOU PARRY/REFLECTOR TEST PASSED: S2/L Trait 2 is a non-super parry; Super Reflector uses universal activation/afterimages.");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU PARRY/REFLECTOR TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private void ValidateConfiguredMovesAndReflectors()
	{
		SpecialMoveData parry = _parryFighter.Definition.SpecialMoves.FindMove(
			FighterController.SanzoParryName, false, false);
		SpecialMoveData blockReflector = _parryFighter.Definition.SpecialMoves.FindMove(
			FighterController.BlockReflectorName, false, false);
		SuperMoveData superReflector = Array.Find(_parryFighter.Definition.SuperMoves,
			move => move?.AttackName == FighterController.SanzoSuperReflectorName);
		Expect(parry?.Parry == true && parry.StartupFrames == 0 && parry.ActiveFrames == 30,
			"L parry resource is missing its 30-frame window");
		Expect(parry.AnimationName == "trait_2",
			"S2/L parry resource is not assigned to the Trait 2 source animation");
		Expect(Array.Find(_parryFighter.Definition.SuperMoves,
			move => move?.AttackName == FighterController.SanzoParryName) == null,
			"L parry is still registered in Sanzou's super list");
		Expect(superReflector?.ProjectileScene != null && superReflector.HitCount == 8 &&
			superReflector.HitstopFrames == 1 && superReflector.ProjectileHitCooldownFrames == 5 &&
			superReflector.HitstunFrames > superReflector.ProjectileHitCooldownFrames,
			"super reflector resource is not configured for eight hits and its authored scene");
		Expect(blockReflector?.ReflectorSpawnOffset.IsEqualApprox(new Vector2(62f, 48f)) == true &&
			superReflector.ProjectileSpawnOffset.IsEqualApprox(new Vector2(62f, 48f)),
			"reflector is not anchored at Sanzou's forward hand at source-sized height");

		PackedScene scene = ResourceLoader.Load<PackedScene>("res://Assets/TestFighter/Sanzo/sanzo_reflector.tscn");
		var block = scene.Instantiate<ProjectileReflector>();
		block.ProcessMode = ProcessModeEnum.Disabled;
		block.Initialize(_parryFighter, 1, false);
		AddChild(block);
		Expect(!block.IsSuperReflector && block.LifetimeFrames == 8 && block.HitsRemaining == 1,
			"block reflector is not exactly one eight-frame animation cycle and one hit");
		Expect(block.FinalHitKnocksDown && block.FinalKnockdownFrames > 0 && block.Pushback >= 900f,
			"block reflector does not push back and knock down");
		Expect(block.Visual?.SpriteFrames?.GetFrameCount("reflector") == 8,
			"321-328 reflector animation does not contain eight frames");
		Expect(block.Visual != null && block.Visual.Scale.IsEqualApprox(new Vector2(1.15f, 1.15f)),
			"reflector visual is not larger than Sanzou like the source Aegis shield");
		Texture2D reflectorFrame = block.Visual?.SpriteFrames?.GetFrameTexture(block.Visual.Animation, block.Visual.Frame);
		Expect(reflectorFrame != null && Mathf.IsZeroApprox(block.Visual.Position.Y +
			reflectorFrame.GetHeight() * Mathf.Abs(block.Visual.Scale.Y) * 0.5f),
			"reflector frame is not bottom-aligned to ground level");
		Expect(block.Visual.Material is CanvasItemMaterial reflectorMaterial &&
			reflectorMaterial.BlendMode == CanvasItemMaterial.BlendModeEnum.Add,
			"reflector does not use additive rendering to remove its opaque black pixels");
		Expect(block.ReflectBox.IsEqualApprox(new Rect2(-46f, -286f, 92f, 286f)),
			"reflector collision does not cover the enlarged source-sized shield");

		var super = scene.Instantiate<ProjectileReflector>();
		super.ProcessMode = ProcessModeEnum.Disabled;
		super.Initialize(_parryFighter, 1, true);
		AddChild(super);
		Expect(super.IsSuperReflector && super.Super && super.LifetimeFrames == 600,
			"super reflector does not last exactly 600 frames (10 seconds at 60 Hz)");
		Expect(super.HitsRemaining == 8 && !super.LatchOnMultiHit && Mathf.IsEqualApprox(super.Speed, 18f) &&
			super.HitstopFrames == 1 && super.HitCooldownFrames == 5 &&
			super.HitstunFrames > super.HitCooldownFrames,
			"super reflector is not an eight-hit, slowly sliding projectile");

		var incoming = new BasicProjectile { ProcessMode = ProcessModeEnum.Disabled, GlobalPosition = super.GlobalPosition };
		incoming.Initialize(_attacker, -1, 0f, 10, 100f, 2, 1f, false);
		AddChild(incoming);
		super._PhysicsProcess(1.0 / 60.0);
		Expect(incoming.OwnerFighter == _parryFighter && super.ReflectedProjectileCount == 1,
			"super reflector did not reflect an overlapping enemy projectile");
		for (int hit = 0; hit < 7; hit++) super.MarkHit();
		Expect(super.HitsRemaining == 1 && !super.IsQueuedForDeletion(), "super reflector disappeared before hit eight");
		super.MarkHit();
		Expect(super.IsQueuedForDeletion(), "super reflector did not disappear on hit eight");
	}

	private static void LatchMotionEvent(FighterController fighter, StringName action)
	{
		fighter.InjectMotionAction(action);
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

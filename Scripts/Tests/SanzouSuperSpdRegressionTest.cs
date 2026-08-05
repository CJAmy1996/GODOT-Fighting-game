using System;
using Godot;
using ModularFighter.Characters;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Locks Sanzou's clone isolation and multi-screen QCF+LP+HP Super SPD.</summary>
public partial class SanzouSuperSpdRegressionTest : Node2D
{
	private FighterController _attacker;
	private FighterController _victim;
	private VersusStageRules _presentationRules;
	private int _stage;
	private int _settleTicks = 6;
	private int _watchdog = 360;
	private float _highestY;
	private bool _sawForcedDescent;
	private bool _sawSuperAfterimage;

	public override void _Ready()
	{
		ValidateSanzouArenaRemovesCloneController();
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1400f, 20f) } });
		AddChild(floor);
		_attacker = Spawn("SuperSpdSanzou", 0f, 1, 1);
		_victim = Spawn("SuperSpdVictim", 65f, -1, 2);
		_attacker.SetOpponent(_victim);
		_victim.SetOpponent(_attacker);
		var camera = new StageCamera
		{
			Name = "StageCamera",
			FighterOnePath = new NodePath("../SuperSpdSanzou"),
			FighterTwoPath = new NodePath("../SuperSpdVictim")
		};
		AddChild(camera);
		_presentationRules = new VersusStageRules
		{
			Name = "PresentationRules",
			FighterOnePath = new NodePath("../SuperSpdSanzou"),
			FighterTwoPath = new NodePath("../SuperSpdVictim"),
			CameraPath = new NodePath("../StageCamera")
		};
		AddChild(_presentationRules);
	}

	private FighterController Spawn(string name, float x, int facing, int team)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn")
			.Instantiate<FighterController>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.TeamId = team;
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(facing);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	private void ValidateSanzouArenaRemovesCloneController()
	{
		ArenaCharacterLoader.SelectedCharacter = ArenaCharacterLoader.CharacterChoice.Sanzou;
		var loader = new ArenaCharacterLoader
		{
			Name = "SanzouLoaderProbe",
			ProcessMode = ProcessModeEnum.Disabled,
			SanzouScene = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn")
		};
		loader.AddChild(new Node2D { Name = "Fighter" });
		loader.AddChild(new Node { Name = "NarutoCloneController" });
		AddChild(loader);
		Expect(loader.GetNodeOrNull("NarutoCloneController") == null,
			"Sanzou arena retained the O/L clone controller");
		Expect(loader.GetNodeOrNull<SanzoKongoumaruFighter>("Fighter") != null,
			"Sanzou arena loader did not install his dedicated fighter");
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (--_watchdog <= 0)
				throw new InvalidOperationException($"timeout at stage {_stage}, attack '{_attacker.CurrentAttackName}', frame {_attacker.CurrentAttackFrame}");
			switch (_stage)
			{
				case 0:
					if (--_settleTicks > 0) return;
					Expect(_attacker.IsOnFloor() && _victim.IsOnFloor(), "Super SPD fighters did not settle");
					Expect(!_attacker.TryBeginCloneCall(), "Sanzou still accepts clone calls through FighterController");
					Expect(Array.Exists(_attacker.Definition.SuperMoves,
						move => move?.AttackName == FighterController.SanzoSuperSpdName),
						"Sanzou Super 1 resource is not the Super SPD");
					LatchMotionEvent(_attacker, "move_down");
					_attacker.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false));
					_stage = 1;
					break;
				case 1:
					LatchMotionEvent(_attacker, "move_right");
					_attacker.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false,
						lightPunchPressed: true, heavyPunchPressed: true));
					_stage = 2;
					break;
				case 2:
					_attacker.SetExternalInput(default);
					Expect(_attacker.CurrentAttackName == FighterController.SanzoSuperSpdName,
						$"QCF+LP+HP resolved as '{_attacker.CurrentAttackName}'");
					Expect(_attacker.CurrentAttackAnimationName == "spd_grab",
						"Super SPD did not reuse the SPD grab animation");
					Expect(_attacker.CurrentAttackStartupFrames == 5, "Super SPD startup is not five frames");
					Expect(_attacker.IsPerformingSuperMove, "Super SPD is not classified as a real super");
					Expect(ResourceLoader.Exists("res://Assets/TestFighter/Sanzo/sanzou_kongoumaru/9999.png"),
						"Sanzou's 9999.png super portrait is missing");
					SuperPortraitOverlay portrait = GetNodeOrNull<SuperPortraitOverlay>("SanzouSuperPortrait9999");
					Expect(portrait?.Portrait == ResourceLoader.Load<Texture2D>(
						"res://Assets/TestFighter/Sanzo/sanzou_kongoumaru/9999.png"),
						"Sanzou Super 1 did not select 9999.png for its activation cut-in");
					Expect(portrait.GetNodeOrNull<SuperActivationRings>("ForegroundActivationRings") != null,
						"Sanzou's super did not receive the universal Kung Fu Man activation spark/rings");
					_presentationRules.QueueFree();
					_stage = 3;
					break;
				case 3:
					if (!_attacker.IsAttackActive) return;
					Expect(_attacker.TryApplyBasicAttackHit(_victim, out int hitstop, out _, out _, out _, out _),
						"Super SPD did not capture its grounded victim");
					Expect(hitstop == 0 && _attacker.SpdGrabConnected, "Super SPD did not enter capture flight");
					Expect(_attacker.CurrentAttackAnimationName == "spd_air_grab", "Super SPD did not use airborne SPD art");
					Expect(_attacker.Velocity.Y <= -3500f, "Super SPD did not begin its ultra-high ascent");
					_highestY = _attacker.GlobalPosition.Y;
					_stage = 4;
					break;
				case 4:
					_highestY = Mathf.Min(_highestY, _attacker.GlobalPosition.Y);
					_sawForcedDescent |= _attacker.Velocity.Y >= 4000f;
					_sawSuperAfterimage |= GetNodeOrNull<Sprite2D>("SuperAfterimage1")?.Visible == true;
					if (!_attacker.TryConsumeSpdSlamImpact(out FighterController victim, out _, out int damage, out bool wasSuper)) return;
					Expect(victim == _victim && wasSuper, "Super SPD slam was not identified as the super variant");
					Expect(_highestY <= -1800f, $"Super SPD rose only {-_highestY:0.0}px");
					Expect(_sawForcedDescent, "Super SPD never entered its multi-screen forced plunge");
					Expect(_sawSuperAfterimage, "Sanzou Super SPD never displayed its shared super afterimages");
					Expect(damage == 600, $"Super SPD dealt {damage} instead of 600 damage");
					Expect(_victim.HitState == FighterHitState.GroundedKnockdown,
						"Super SPD victim did not enter hard grounded knockdown");
					GD.Print("SANZOU SUPER SPD TEST PASSED: clone controller removed; QCF+LP+HP rises multiple screens and force-slams for 600.");
					GetTree().Quit();
					break;
			}
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU SUPER SPD TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void LatchMotionEvent(FighterController fighter, StringName action)
	{
		fighter.ReadLocalInput = true;
		fighter._Input(new InputEventAction { Action = action, Pressed = true });
		fighter.ReadLocalInput = false;
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

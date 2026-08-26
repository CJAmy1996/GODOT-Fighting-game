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
	private StageCamera _camera;
	private VersusStageRules _presentationRules;
	private int _stage;
	private int _settleTicks = 6;
	private int _watchdog = 360;
	private float _highestY;
	private bool _sawForcedDescent;
	private bool _sawSuperAfterimage;
	private int _backdropInitialFrame;
	private int _backdropAnimationTicks;

	public override void _Ready()
	{
		ValidateSanzouArenaRemovesCloneController();
		ValidateHyperComboBackdropAssets();
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1400f, 20f) } });
		AddChild(floor);
		_attacker = Spawn("SuperSpdSanzou", 0f, 1, 1);
		_victim = Spawn("SuperSpdVictim", 65f, -1, 2);
		_attacker.SetOpponent(_victim);
		_victim.SetOpponent(_attacker);
		_camera = new StageCamera
		{
			Name = "StageCamera",
			FighterOnePath = new NodePath("../SuperSpdSanzou"),
			FighterTwoPath = new NodePath("../SuperSpdVictim")
		};
		AddChild(_camera);
		_presentationRules = new VersusStageRules
		{
			Name = "PresentationRules",
			FighterOnePath = new NodePath("../SuperSpdSanzou"),
			FighterTwoPath = new NodePath("../SuperSpdVictim"),
			CameraPath = new NodePath("../StageCamera")
		};
		AddChild(_presentationRules);
	}

	private static void ValidateHyperComboBackdropAssets()
	{
		for (ulong index = 0; index < 2; index++)
		{
			string path = VersusStageRules.ChooseHyperComboBackdropPath(index);
			Expect(AnimatedGifFrameSource.TryOpen(path, out AnimatedGifFrameSource source, out string error),
				$"hyper-combo background '{path}' could not decode: {error}");
			using (source)
			{
				int expectedFrames = index == 0 ? 201 : 212;
				Expect(source.FrameCount == expectedFrames,
					$"hyper-combo background '{path}' decoded {source.FrameCount} frames, expected {expectedFrames}");
				source.Advance(0.1);
				Expect(source.CurrentFrame > 0, $"hyper-combo background '{path}' did not animate");
			}
		}
	}

	private FighterController Spawn(string name, float x, int facing, int team)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
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
			SanzouScene = ResourceLoader.Load<PackedScene>("res://Scenes/Characters/SanzoKongoumaru.tscn")
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
						move => move?.AttackName == SanzoKongoumaruFighter.SuperSpdName),
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
					Expect(_attacker.CurrentAttackName == SanzoKongoumaruFighter.SuperSpdName,
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
						"Sanzou's super did not retain its portrait-container border");
					BigBangSuperCancelEffect superCancel = GetNodeOrNull<BigBangSuperCancelEffect>(
						"UniversalBigBangSuperCancelEffect");
					Expect(superCancel != null && superCancel.CurrentInnerFrame is >= 0 and <= 7 &&
						superCancel.CurrentOuterFrame is >= 8 and <= 15 &&
						superCancel.CurrentCoreFrame is >= 17 and <= 36,
						"Sanzou's super did not spawn the universal layered BBB super-cancel effect");
					Expect(superCancel.GlobalPosition.DistanceTo(_attacker.WorldPositionBox.GetCenter()) < 0.5f,
						"BBB super-cancel effect was not centered on the activating fighter");
					SuperBackdrop backdrop = GetNodeOrNull<SuperBackdrop>("SuperBackdrop");
					string firstBackdropPath = VersusStageRules.ChooseHyperComboBackdropPath(0);
					string secondBackdropPath = VersusStageRules.ChooseHyperComboBackdropPath(1);
					Expect(firstBackdropPath != secondBackdropPath,
						"hyper-combo backdrop selector does not expose both background images");
					Expect(backdrop != null &&
						(backdrop.AnimatedBackgroundPath == firstBackdropPath || backdrop.AnimatedBackgroundPath == secondBackdropPath),
						"super activation did not randomly select one of the two hyper-combo backgrounds");
					int expectedFrameCount = backdrop.AnimatedBackgroundPath == firstBackdropPath ? 201 : 212;
					Expect(backdrop.AnimatedBackgroundReady && backdrop.AnimatedBackgroundFrameCount == expectedFrameCount,
						$"selected hyper-combo GIF decoded {backdrop.AnimatedBackgroundFrameCount} frames, expected {expectedFrameCount}");
					Expect(Mathf.IsEqualApprox(backdrop.AnimationSpeedMultiplier, 4f),
						$"hyper-combo background speed is {backdrop.AnimationSpeedMultiplier:0.0}x instead of 4x");
					Expect(Mathf.IsEqualApprox(backdrop.MaxAnimationTextureUpdatesPerSecond, 30f),
						"hyper-combo texture decoding is not capped at 30 updates per second");
					Vector2 expectedBackdropSize = GetViewportRect().Size / _camera.Zoom;
					Expect(Mathf.Abs(backdrop.Width - expectedBackdropSize.X) < 0.5f &&
						Mathf.Abs(backdrop.Height - expectedBackdropSize.Y) < 0.5f,
						$"hyper-combo background size {backdrop.Width:0.0}x{backdrop.Height:0.0} does not cover camera view {expectedBackdropSize.X:0.0}x{expectedBackdropSize.Y:0.0}");
					_backdropInitialFrame = backdrop.AnimatedBackgroundFrame;
					_stage = 3;
					break;
				case 3:
					if (++_backdropAnimationTicks < 5) return;
					SuperBackdrop animatedBackdrop = GetNodeOrNull<SuperBackdrop>("SuperBackdrop");
					Expect(animatedBackdrop != null && animatedBackdrop.AnimatedBackgroundFrame != _backdropInitialFrame,
						"hyper-combo background did not advance beyond its initial GIF frame");
					_presentationRules.QueueFree();
					_stage = 4;
					break;
				case 4:
					if (!_attacker.IsAttackActive) return;
					Expect(_attacker.TryApplyBasicAttackHit(_victim, out int hitstop, out _, out _, out _, out _),
						"Super SPD did not capture its grounded victim");
					Expect(hitstop == 0 && _attacker.CharacterGrabConnected, "Super SPD did not enter capture flight");
					Expect(_attacker.CurrentAttackAnimationName == "spd_air_grab", "Super SPD did not use airborne SPD art");
					Expect(_attacker.Velocity.Y <= -3500f, "Super SPD did not begin its ultra-high ascent");
					_highestY = _attacker.GlobalPosition.Y;
					_stage = 5;
					break;
				case 5:
					_highestY = Mathf.Min(_highestY, _attacker.GlobalPosition.Y);
					_sawForcedDescent |= _attacker.Velocity.Y >= 4000f;
					_sawSuperAfterimage |= GetNodeOrNull<Sprite2D>("SuperAfterimage1")?.Visible == true;
					if (!_attacker.TryConsumeCharacterGrabImpact(out FighterController victim, out _, out int damage, out bool wasSuper)) return;
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
		fighter.InjectMotionAction(action);
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

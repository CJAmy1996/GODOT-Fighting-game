using System;
using System.Linq;
using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

/// <summary>Locks Sanzou's crouch remaps and confirms forward+HP remains his standing heavy.</summary>
public partial class SanzouCrouchRemapRegressionTest : Node2D
{
	private SpriteTestFighter _lightKick;
	private SpriteTestFighter _medium;
	private SpriteTestFighter _downForwardHeavy;
	private SpriteTestFighter _sweep;
	private SpriteTestFighter _forwardHeavy;
	private SpriteTestFighter _backHeavy;
	private SpriteTestFighter _heavyBlocker;
	private int _stage;
	private int _settleTicks = 5;

	public override void _Ready()
	{
		var floor = new StaticBody2D { Position = new Vector2(0f, 10f) };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1600f, 20f) } });
		AddChild(floor);
		_lightKick = Spawn("CrouchLightKick", -300f);
		_medium = Spawn("CrouchMedium", 0f);
		_downForwardHeavy = Spawn("DownForwardHeavy", 300f);
		_sweep = Spawn("Sweep", 550f);
		_forwardHeavy = Spawn("RemovedForwardHeavy", -550f);
		_backHeavy = Spawn("RemovedBackHeavy", -700f);
		_heavyBlocker = Spawn("HeavyBlocker", 365f);
		_heavyBlocker.SetFacing(-1);
		_heavyBlocker.TrainingAutoBlock = true;
	}

	private SpriteTestFighter Spawn(string name, float x)
	{
		var fighter = ResourceLoader.Load<PackedScene>("res://Scenes/TestCharacters/SanzoKongoumaruTest.tscn")
			.Instantiate<SpriteTestFighter>();
		fighter.Name = name;
		fighter.ReadLocalInput = false;
		fighter.TeamId = name.GetHashCode();
		fighter.Position = new Vector2(x, 0f);
		fighter.SetFacing(1);
		fighter.SetExternalInput(default);
		AddChild(fighter);
		return fighter;
	}

	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (_stage == 0)
			{
				if (--_settleTicks > 0) return;
				Expect(_lightKick.IsOnFloor() && _medium.IsOnFloor() && _downForwardHeavy.IsOnFloor() &&
					_sweep.IsOnFloor() && _forwardHeavy.IsOnFloor() && _backHeavy.IsOnFloor() && _heavyBlocker.IsOnFloor(),
					"fighters did not settle on the test floor");
				_lightKick.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false,
					lightKickPressed: true));
				_medium.SetExternalInput(new FighterInput(1f, 1f, false, false, false, false,
					lightPunchPressed: true));
				_downForwardHeavy.SetExternalInput(new FighterInput(1f, 1f, false, false, false, false,
					heavyPunchPressed: true));
				_sweep.SetExternalInput(new FighterInput(0f, 1f, false, false, false, false,
					heavyKickPressed: true));
				_forwardHeavy.SetExternalInput(new FighterInput(1f, 0f, false, false, false, false,
					heavyPunchPressed: true));
				_backHeavy.SetExternalInput(new FighterInput(-1f, 0f, false, false, false, false,
					heavyPunchPressed: true));
				_stage = 1;
				return;
			}

			_lightKick.SetExternalInput(default);
			_medium.SetExternalInput(default);
			_downForwardHeavy.SetExternalInput(default);
			_sweep.SetExternalInput(default);
			_forwardHeavy.SetExternalInput(default);
			_backHeavy.SetExternalInput(default);
			_heavyBlocker.SetExternalInput(default);
			if (!_downForwardHeavy.IsAttackActive) return;
			Expect(_lightKick.CurrentAttackName == "LIGHT KICK", $"down+LK resolved as '{_lightKick.CurrentAttackName}'");
			Expect(_lightKick.CurrentAttackAnimationName == "crouching_light_kick", "down+LK did not select new crouching-light-kick art");
			Expect(_lightKick.CharacterSprite.SpriteFrames.GetFrameCount("crouching_light_kick") == 5,
				"new crouching light kick does not contain five drawings");

			Expect(_medium.CurrentAttackName == FighterController.CrouchingMediumJabName,
				$"down-forward+LP resolved as '{_medium.CurrentAttackName}'");
			Expect(_medium.CurrentAttackAnimationName == "crouching_medium_punch",
				"crouching medium did not receive the former crouching-LK placeholder art");
			Expect(_medium.CharacterSprite.SpriteFrames.GetFrameCount("crouching_medium_punch") == 5,
				"repurposed crouching medium does not retain Group 18's five drawings");

			Expect(_downForwardHeavy.CurrentAttackName == FighterController.DownForwardHeavyPunchName,
				$"down-forward+HP resolved as '{_downForwardHeavy.CurrentAttackName}'");
			Expect(_downForwardHeavy.CurrentAttackAnimationName == "down_forward_heavy_punch",
				"down-forward+HP did not receive the former crouching-medium art");
			Expect(_downForwardHeavy.CharacterSprite.SpriteFrames.GetFrameCount("down_forward_heavy_punch") == 9,
				"down-forward+HP does not retain Group 19's nine drawings");

			Expect(_sweep.CurrentAttackName == FighterController.CrouchingHeavyKickName,
				$"down+HK resolved as '{_sweep.CurrentAttackName}'");
			Expect(_forwardHeavy.CurrentAttackName == "HEAVY PUNCH",
				$"forward+HP resolved as removed move '{_forwardHeavy.CurrentAttackName}' instead of standing HP");
			Expect(_forwardHeavy.CurrentAttackAnimationName == "heavy_punch",
				"forward+HP did not use Sanzou's standing-heavy animation");
			Expect(_backHeavy.CurrentAttackName == "HEAVY PUNCH" &&
				_backHeavy.CurrentAttackAnimationName == "heavy_punch",
				$"back+HP resolved as '{_backHeavy.CurrentAttackName}' instead of standing HP");
			Expect(_forwardHeavy.Definition.NormalMoves.FindRule(FighterController.ForwardHeavyPunchName, true, false) == null,
				"Sanzou's removed forward-HP move remains in his authored move list");
			NormalMoveData standingHeavyRule = _forwardHeavy.Definition.NormalMoves.FindRule("HEAVY PUNCH", false, false);
			Expect(standingHeavyRule?.RecoveryFrames == 29,
				$"Sanzou standing HP recovery is {standingHeavyRule?.RecoveryFrames}, expected 29");
			Expect(Mathf.IsEqualApprox(_sweep.CharacterSprite.Scale.X, 0.85f) &&
				Mathf.IsEqualApprox(_sweep.CharacterSprite.Scale.Y, 0.85f),
				"Sanzou sweep sprite is not scaled to 85 percent");
			float sweepFloorLine = _sweep.CharacterSprite.Position.Y +
				_sweep.AuthoredSpriteFloorOffset * _sweep.CharacterSprite.Scale.Y;
			Expect(Mathf.Abs(sweepFloorLine) < 0.1f, "scaled sweep sprite no longer meets its authored floor line");

			NormalMoveData mediumRule = _medium.Definition.NormalMoves.FindRule(FighterController.CrouchingMediumJabName, true, false);
			NormalMoveData heavyRule = _downForwardHeavy.Definition.NormalMoves.FindRule(FighterController.DownForwardHeavyPunchName, true, false);
			NormalMoveData lowJabRule = _lightKick.Definition.NormalMoves.FindRule("LIGHT PUNCH", true, false);
			NormalMoveData crouchingHeavyRule = _lightKick.Definition.NormalMoves.FindRule(FighterController.CrouchingHeavyPunchName, true, false);
			NormalMoveData backMediumRule = _lightKick.Definition.NormalMoves.FindRule(FighterController.BackLightPunchName, false, false);
			Expect(mediumRule != null && mediumRule.BoxTimeline.Count(box => box?.Kind == FighterBoxKind.Hitbox) == 1,
				"repurposed crouching medium should be a single-hit normal");
			Expect(heavyRule != null && heavyRule.BoxTimeline.Count(box => box?.Kind == FighterBoxKind.Hitbox) == 2,
				"down-forward+HP did not inherit the former two-hit crouching-medium boxes");
			Expect(_lightKick.LightAttackHitstunFrames == 5 && _lightKick.HeavyAttackHitstunFrames == 9 &&
				_lightKick.BasicAttackHitstunFrames == 15,
				"Sanzou normal hitstun was not reduced by five frames");
			Expect(lowJabRule?.HitstunFrames == 9 && lowJabRule.BlockstunFrames == 4 &&
				Mathf.IsEqualApprox(lowJabRule.Pushback, 520f),
				"crouching jab did not restore pushback with five fewer hit/blockstun frames");
			Expect(crouchingHeavyRule?.LaunchHitstunFrames == 25 && backMediumRule?.HitstunFrames == 13,
				"authored launcher/medium hitstun did not lose five frames");
			Expect(Mathf.IsEqualApprox(_lightKick.HeavyNormalBlockPushbackScale, 0.6f),
				"heavy normals do not use Sanzou's reduced block-pushback scale");
			Expect(_downForwardHeavy.TryApplyBasicAttackHit(_heavyBlocker,
				out _, out _, out float blockedHeavyPushback, out _, out _),
				"active heavy normal did not reach the blocking regression target");
			float expectedBlockedHeavyPushback = _downForwardHeavy.HeavyAttackPushback *
				_downForwardHeavy.BlockPushbackMultiplier * _downForwardHeavy.HeavyNormalBlockPushbackScale;
			Expect(_downForwardHeavy.LastContactWasBlocked &&
				Mathf.IsEqualApprox(blockedHeavyPushback, expectedBlockedHeavyPushback),
				$"heavy block pushback was {blockedHeavyPushback:0.0}, expected {expectedBlockedHeavyPushback:0.0}");
			Expect(_downForwardHeavy.CurrentAttackHitstunFrames == 9 && _heavyBlocker.HitstunFramesLeft == 5,
				$"heavy normal hit/blockstun resolved as {_downForwardHeavy.CurrentAttackHitstunFrames}/{_heavyBlocker.HitstunFramesLeft}, expected 9/5");
			GD.Print("SANZOU CROUCH REMAP TEST PASSED: forward/back+HP use the 29f-recovery standing heavy; crouch remaps hold.");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU CROUCH REMAP TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

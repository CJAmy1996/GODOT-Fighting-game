using System.Collections.Generic;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Demo;

/// <summary>Minimal AnimatedSprite2D presentation layer for testing imported sprite animations.</summary>
public partial class SpriteTestFighter : FighterController
{
	[Export] public AnimatedSprite2D CharacterSprite { get; set; }
	[ExportGroup("Heavy Walk Presentation")]
	[Export] public bool HeavyWalkFootstepShake { get; set; }
	[Export] public float HeavyWalkShakeStrength { get; set; } = 1.1f;
	[Export] public int HeavyWalkShakeFrames { get; set; } = 3;
	[ExportGroup("Selected Move Presentation")]
	[Export(PropertyHint.Range, "0.1,1.0,0.01")]
	public float SweepAndSpdVisualScale { get; set; } = 1f;
	[Export] public float AuthoredSpriteFloorOffset { get; set; } = 58f;
	private bool _forwardJumpIntroStarted;
	private bool _crouchIntroStarted;
	private bool _crouchExitStarted;
	private bool _wasVisuallyAttacking;
	private string _lastVisualAttackName = "";
	private int _lastVisualAttackFrame = -1;
	private bool _superOneVisualActive;
	private ulong _lastVisualHitReactionSerial;
	private ulong _lastVisualBlockReactionSerial;
	private StringName _lastFootstepAnimation;
	private int _lastFootstepDrawing = -1;
	private Vector2 _baseCharacterSpritePosition;
	private Vector2 _baseCharacterSpriteScale = Vector2.One;
	private bool _capturedCharacterSpriteTransform;
	private static readonly int[] SuperAfterimageDelays = { 5, 10, 15, 20 };
	private readonly List<SuperShadowSample> _superShadowHistory = new();
	private readonly List<Sprite2D> _superAfterimages = new();
	private static readonly StringName[] SuperOneAttackCycle =
	{
		"heavy_punch", "standing_light_kick", "light_punch", "standing_heavy_kick",
		"crouching_light_punch", "forward_heavy_punch", "crouching_light_kick", "forward_light_kick"
	};

	public override void _Ready()
	{
		CharacterSprite ??= GetNodeOrNull<AnimatedSprite2D>("CharacterSprite");
		if (CharacterSprite != null)
		{
			_baseCharacterSpritePosition = CharacterSprite.Position;
			_baseCharacterSpriteScale = CharacterSprite.Scale;
			_capturedCharacterSpriteTransform = true;
		}
		UpdateAnimation();
		ApplySelectedMoveVisualScale();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateAnimation();
		ApplySelectedMoveVisualScale();
		UpdateHeavyWalkFootsteps();
		UpdateSuperShadows();
	}

	private void ApplySelectedMoveVisualScale()
	{
		if (CharacterSprite == null || !_capturedCharacterSpriteTransform) return;
		bool selectedMove = CurrentAttackName == CrouchingHeavyKickName ||
			CurrentAttackName == SanzoSpdName || CurrentAttackName == SanzoSuperSpdName;
		bool selectedAnimation = CharacterSprite.Animation == "crouching_heavy_kick" ||
			CharacterSprite.Animation == "spd_grab" || CharacterSprite.Animation == "spd_air_grab";
		float factor = selectedMove || selectedAnimation
			? Mathf.Clamp(SweepAndSpdVisualScale, 0.1f, 1f)
			: 1f;
		CharacterSprite.Scale = _baseCharacterSpriteScale * factor;
		float floorCompensation = AuthoredSpriteFloorOffset * (1f - factor) * Mathf.Abs(_baseCharacterSpriteScale.Y);
		CharacterSprite.Position = _baseCharacterSpritePosition + Vector2.Down * floorCompensation;
	}

	private void UpdateHeavyWalkFootsteps()
	{
		if (!HeavyWalkFootstepShake || CharacterSprite == null) return;
		StringName animation = CharacterSprite.Animation;
		int drawing = CharacterSprite.Frame;
		bool walking = animation == "walk" || animation == "walk_back";
		if (!walking)
		{
			_lastFootstepAnimation = animation;
			_lastFootstepDrawing = -1;
			return;
		}
		if (animation == _lastFootstepAnimation && drawing == _lastFootstepDrawing) return;
		_lastFootstepAnimation = animation;
		_lastFootstepDrawing = drawing;
		if (drawing != 3 && drawing != 9) return;
		if (GetViewport().GetCamera2D() is StageCamera camera)
			camera.Shake(HeavyWalkShakeStrength, HeavyWalkShakeFrames);
	}

	private void UpdateAnimation()
	{
		if (CharacterSprite?.SpriteFrames == null) return;
		// SpeedScale freezes the exact displayed sprite frame without resetting animation state.
		// Attack drawings are selected manually from the deterministic combat
		// frame below; AnimatedSprite must not independently advance between ticks.
		CharacterSprite.SpeedScale = IsInHitstop || IsAttacking ? 0f : 1f;
		CharacterSprite.FlipH = Facing < 0;
		bool restartAttackAnimation = IsAttacking &&
			(!_wasVisuallyAttacking || CurrentAttackName != _lastVisualAttackName || CurrentAttackFrame < _lastVisualAttackFrame);
		bool backdashing = ActiveAbility?.Id == "backdash";
		bool forwardShortHopping = ActiveAbility?.Id == "forward_short_hop";
		bool airDashing = ActiveAbility?.Id == "air_dash";
		bool holdingCrouch = WasGrounded && CurrentInput.Vertical > 0.5f;
		bool leavingCrouchForAnotherAction = !holdingCrouch &&
			(ActiveAbility != null || Mathf.Abs(CurrentInput.Horizontal) > 0.1f);
		if (restartAttackAnimation)
		{
			// An attack owns the presentation immediately. A standing attack must not
			// inherit a pending crouch-exit, while a crouching attack returns to crouch hold.
			_crouchIntroStarted = IsCrouchAttackLocked;
			_crouchExitStarted = false;
		}
		else if (backdashing || !WasGrounded || leavingCrouchForAnotherAction)
		{
			// Dash/jump states replace crouch outright; never resume crouch_end afterward.
			_crouchIntroStarted = false;
			_crouchExitStarted = false;
		}
		_wasVisuallyAttacking = IsAttacking;
		_lastVisualAttackName = CurrentAttackName;
		_lastVisualAttackFrame = CurrentAttackFrame;
		if (IsAttacking && CurrentAttackName == "SUPER RUSH")
		{
			if (!_superOneVisualActive)
			{
				_superOneVisualActive = true;
				CharacterSprite.Stop();
				CharacterSprite.Play("air_dash");
				CharacterSprite.SetFrameAndProgress(1, 0f);
			}
			UpdateSuperOneAnimation();
			return;
		}
		_superOneVisualActive = false;
		if (IsParrySuccessPresentationActive)
		{
			if (CharacterSprite.Animation != "stand_block") CharacterSprite.Play("stand_block");
			CharacterSprite.SetFrameAndProgress(0, 0f);
			return;
		}
		if (IsWakingUp)
		{
			if (CharacterSprite.Animation != "get_up") CharacterSprite.Play("get_up");
			return;
		}
		if (IsInBlockstun)
		{
			StringName blockAnimation = !WasGrounded ? "air_block" : IsCrouchBlocking ? "crouch_block" : "stand_block";
			StringName impactAnimation = !WasGrounded ? "air_block_impact" : IsCrouchBlocking ? "crouch_block_impact" : "stand_block_impact";
			if (_lastVisualBlockReactionSerial != BlockReactionSerial)
			{
				_lastVisualBlockReactionSerial = BlockReactionSerial;
				CharacterSprite.Play(impactAnimation);
				return;
			}
			if (CharacterSprite.Animation == impactAnimation && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != blockAnimation) CharacterSprite.Play(blockAnimation);
			return;
		}
		if (IsInHitstun)
		{
			StringName reactionAnimation;
			if (IsGroundedKnockdown)
				reactionAnimation = "knockdown";
			else if (HitState == FighterHitState.Juggle || HitState == FighterHitState.WallSplat || HitState == FighterHitState.Tumble || HitState == FighterHitState.Knockdown ||
				HitState == FighterHitState.WallBounce || HitState == FighterHitState.GroundBounce)
				reactionAnimation = "tumble";
			else if (!WasGrounded)
				reactionAnimation = "air_hitstun";
			else if (LastHitReactionLevel >= 2 && LastHitCameFromAir)
				reactionAnimation = "hitstun_heavy_air";
			else if (LastHitReactionLevel >= 2)
				reactionAnimation = "hitstun_heavy";
			else if (LastHitReactionLevel == 1)
				reactionAnimation = "hitstun_medium";
			else
				reactionAnimation = "hitstun_light";
			if (CharacterSprite.Animation != reactionAnimation || _lastVisualHitReactionSerial != HitReactionSerial)
			{
				CharacterSprite.Play(reactionAnimation);
				CharacterSprite.SetFrameAndProgress(0, 0f);
				_lastVisualHitReactionSerial = HitReactionSerial;
			}
			return;
		}
		if (IsAttackActive && !IsInSuperJumpRoute && !CurrentAttackHasHit && CurrentAttackStartedAirborne &&
			(CurrentAttackName == "LIGHT PUNCH" || CurrentAttackName == "LIGHT KICK"))
		{
			StringName heldAirLightAnimation = CurrentAttackName == "LIGHT PUNCH" ? "air_light_punch" : "air_light_kick";
			if (CharacterSprite.Animation != heldAirLightAnimation) CharacterSprite.Play(heldAirLightAnimation);
			int heldFrameCount = CharacterSprite.SpriteFrames.GetFrameCount(heldAirLightAnimation);
			if (heldFrameCount > 4) CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "LIGHT PUNCH" && CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "air_light_punch") CharacterSprite.Play("air_light_punch");
			int jabFrameCount = CharacterSprite.SpriteFrames.GetFrameCount("air_light_punch");
			if (jabFrameCount > 4) CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && ((CurrentAttackName == "LIGHT PUNCH" && !CurrentAttackStartedAirborne) ||
			CurrentAttackName == CrouchingMediumJabName || CurrentAttackName == AirHeavyPunchName) && !restartAttackAnimation)
		{
			StringName jabAnimation = CurrentAttackName == AirHeavyPunchName
				? "air_heavy_punch"
				: CurrentAttackName == CrouchingMediumJabName
				? "crouching_medium_punch"
				: IsCrouchAttackLocked ? "crouching_light_punch" : "light_punch";
			if (CharacterSprite.Animation != jabAnimation) CharacterSprite.Play(jabAnimation);
			// These Sanzou jab sequences reach their first actual punch drawing
			// at index 2. Index 4 is recovery (and does not exist for standing LP).
			int activePose = Mathf.Min(2, CharacterSprite.SpriteFrames.GetFrameCount(jabAnimation) - 1);
			CharacterSprite.Frame = Mathf.Max(0, activePose);
		}
		if (IsInHitstop && CurrentAttackName == "LIGHT KICK" && !IsCrouchAttackLocked &&
			!CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "standing_light_kick") CharacterSprite.Play("standing_light_kick");
			// Timeline frame 4 always displays source frame 18_01.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == ForwardLightKickName && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "forward_light_kick") CharacterSprite.Play("forward_light_kick");
			// Timeline frame 4 always displays source frame 19_03.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "HEAVY KICK" && !IsCrouchAttackLocked &&
			!CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "standing_heavy_kick") CharacterSprite.Play("standing_heavy_kick");
			// Timeline frame 4 always displays source frame 20_04.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == AirUpHeavyKickName && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "air_up_heavy_kick") CharacterSprite.Play("air_up_heavy_kick");
			// Timeline frame 4 always displays source frame 21_04.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "LIGHT KICK" && CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "air_light_kick") CharacterSprite.Play("air_light_kick");
			// Timeline frame 4 always displays source frame 23_01.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "HEAVY KICK" && CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "air_heavy_kick") CharacterSprite.Play("air_heavy_kick");
			// Timeline frame 4 always displays source frame 24_02.
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "LIGHT KICK" && IsCrouchAttackLocked && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "crouching_light_kick") CharacterSprite.Play("crouching_light_kick");
			// The first kick drawing begins on gameplay frame 4.
			CharacterSprite.Frame = 2;
		}
		if (IsInHitstop && CurrentAttackName == CrouchingHeavyKickName && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "crouching_heavy_kick") CharacterSprite.Play("crouching_heavy_kick");
			// Timeline frame 4 always displays source frame 26_03.
			CharacterSprite.Frame = 4;
		}
		bool forwardAirborne = !WasGrounded && Velocity.X * Facing > 25f;
		if (!forwardAirborne) _forwardJumpIntroStarted = false;
		bool crouching = holdingCrouch;

		if (!IsAttacking && !backdashing && !airDashing && !forwardShortHopping && forwardAirborne)
		{
			if (!_forwardJumpIntroStarted)
			{
				_forwardJumpIntroStarted = true;
				CharacterSprite.Play("forward_jump_start");
				return;
			}
			if (CharacterSprite.Animation == "forward_jump_start" && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != "forward_jump_loop") CharacterSprite.Play("forward_jump_loop");
			return;
		}

		if (!IsAttacking && !backdashing && crouching)
		{
			_crouchExitStarted = false;
			if (!_crouchIntroStarted)
			{
				_crouchIntroStarted = true;
				CharacterSprite.Play("crouch_start");
				return;
			}
			if (CharacterSprite.Animation == "crouch_start" && CharacterSprite.IsPlaying()) return;
			if (CharacterSprite.Animation != "crouch_hold") CharacterSprite.Play("crouch_hold");
			return;
		}

		if (!IsAttacking && !backdashing && !crouching && _crouchIntroStarted)
		{
			if (!_crouchExitStarted)
			{
				_crouchExitStarted = true;
				CharacterSprite.Play("crouch_end");
				return;
			}
			if (CharacterSprite.Animation == "crouch_end" && CharacterSprite.IsPlaying()) return;
			_crouchIntroStarted = false;
			_crouchExitStarted = false;
			return;
		}

		StringName nextAnimation;
		if (IsAttacking && CurrentAttackName == "SUPER FIREBALL")
			nextAnimation = "super_fireball";
		else if (IsAttacking && !string.IsNullOrEmpty(CurrentAttackAnimationName))
			nextAnimation = CurrentAttackAnimationName;
		else if (IsAttacking && (CurrentAttackName == "LIGHT PROJECTILE" || CurrentAttackName == "HEAVY PROJECTILE"))
			nextAnimation = "fireball";
		else if (IsAttacking && CurrentAttackName == ThrowAttackName) nextAnimation = "throw";
		else if (IsAttacking && CurrentAttackName == ForwardHeavyPunchName) nextAnimation = "forward_heavy_punch";
		else if (IsAttacking && CurrentAttackName == CrouchingMediumJabName) nextAnimation = "crouching_medium_punch";
		else if (IsAttacking && CurrentAttackName == CrouchingHeavyPunchName) nextAnimation = "crouching_heavy_punch";
		else if (IsAttacking && CurrentAttackName == AirHeavyPunchName) nextAnimation = "air_heavy_punch";
		else if (IsAttacking && CurrentAttackName == "HEAVY PUNCH") nextAnimation = "heavy_punch";
		else if (IsAttacking && IsCrouchAttackLocked && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "crouching_light_punch";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "air_light_punch";
		else if (IsAttacking && !CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT PUNCH") nextAnimation = "light_punch";
		else if (IsAttacking && CurrentAttackName == ForwardLightKickName) nextAnimation = "forward_light_kick";
		else if (IsAttacking && !IsCrouchAttackLocked && !CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT KICK") nextAnimation = "standing_light_kick";
		else if (IsAttacking && !IsCrouchAttackLocked && !CurrentAttackStartedAirborne && CurrentAttackName == "HEAVY KICK") nextAnimation = "standing_heavy_kick";
		else if (IsAttacking && CurrentAttackName == AirUpHeavyKickName) nextAnimation = "air_up_heavy_kick";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "LIGHT KICK") nextAnimation = "air_light_kick";
		else if (IsAttacking && CurrentAttackStartedAirborne && CurrentAttackName == "HEAVY KICK") nextAnimation = "air_heavy_kick";
		else if (IsAttacking && IsCrouchAttackLocked && CurrentAttackName == "LIGHT KICK") nextAnimation = "crouching_light_kick";
		else if (IsAttacking && CurrentAttackName == CrouchingHeavyKickName) nextAnimation = "crouching_heavy_kick";
		else if (IsAttacking) nextAnimation = "attack";
		else if (backdashing) nextAnimation = "back_dash";
		else if (forwardShortHopping) nextAnimation = "forward_dash";
		else if (airDashing) nextAnimation = "air_dash";
		else if (!WasGrounded) nextAnimation = Velocity.Y < 0f ? "neutral_jump" : "fall";
		else if (ActiveAbility is RunAbility) nextAnimation = "run";
		else if (Mathf.Abs(Velocity.X) > 25f)
			nextAnimation = Velocity.X * Facing < 0f ? "walk_back" : "walk";
		else nextAnimation = "idle";

		if (IsAttacking && CharacterSprite.SpriteFrames.HasAnimation(nextAnimation))
		{
			if (CharacterSprite.Animation != nextAnimation || restartAttackAnimation)
				CharacterSprite.Play(nextAnimation);
			SyncAttackDrawingToCombatFrame(nextAnimation);
			return;
		}
		if (CharacterSprite.Animation == nextAnimation && !restartAttackAnimation) return;
		CharacterSprite.Play(nextAnimation);
	}

	/// <summary>
	/// Attack art uses the same deterministic 60 Hz clock as combat boxes. Sprite
	/// frame durations are authored in gameplay ticks, so rendering can neither
	/// race ahead of nor lag behind startup/active/recovery state.
	/// </summary>
	private void SyncAttackDrawingToCombatFrame(StringName animation)
	{
		if ((CurrentAttackName == SanzoSpdName || CurrentAttackName == SanzoSuperSpdName) &&
			SpdGrabConnected && animation == "spd_air_grab")
		{
			int drawings = CharacterSprite.SpriteFrames.GetFrameCount(animation);
			int flightTick = Mathf.Max(0, CurrentAttackFrame - CurrentAttackStartupFrames);
			CharacterSprite.SetFrameAndProgress((flightTick / 4) % Mathf.Max(1, drawings), 0f);
			return;
		}
		int drawing = AttackDrawingTimeline.Resolve(CharacterSprite.SpriteFrames, animation, CurrentAttackFrame,
			CurrentAttackStartupFrames, CurrentAttackActiveFrames, CurrentAttackRecoveryFrames,
			ReverseAttackRecoveryToNeutral);
		CharacterSprite.SetFrameAndProgress(drawing, 0f);
	}

	private void UpdateSuperOneAnimation()
	{
		const int startupFrames = 7;
		const int finisherStartFrame = 63;
		const int finisherMotionStartFrame = 67;
		if (!IsCurrentSuperConfirmed)
		{
			if (CharacterSprite.Animation != "air_dash") CharacterSprite.Play("air_dash");
			CharacterSprite.Frame = 1; // Source frame 22_01 is the rush pose.
			return;
		}
		int confirmedAnimationFrame = startupFrames + (CurrentAttackFrame - CurrentSuperConfirmedFrame);

		if (confirmedAnimationFrame >= finisherStartFrame)
		{
			if (CharacterSprite.Animation != "super_one_finisher") CharacterSprite.Play("super_one_finisher");
			if (CurrentAttackHitsRemaining == 0)
			{
				if (IsInHitstop)
				{
					CharacterSprite.Frame = 6;
					return;
				}
				if (!IsAttackRecovering)
				{
					CharacterSprite.Frame = 4 + (confirmedAnimationFrame & 1);
					return;
				}
				// Finish on 27_07; intentionally never display the final 27_08 frame.
				CharacterSprite.Frame = confirmedAnimationFrame < 100 ? 6 : 7;
				return;
			}
			CharacterSprite.Frame = confirmedAnimationFrame < finisherMotionStartFrame
				? 0
				: Mathf.Clamp((confirmedAnimationFrame - finisherMotionStartFrame) / 2, 0, 8);
			return;
		}

		int montageFrame = confirmedAnimationFrame - startupFrames;
		StringName animation = SuperOneAttackCycle[(montageFrame / 3) % SuperOneAttackCycle.Length];
		if (CharacterSprite.Animation != animation) CharacterSprite.Play(animation);
		int frameCount = CharacterSprite.SpriteFrames.GetFrameCount(animation);
		CharacterSprite.Frame = Mathf.Clamp(montageFrame % 3, 0, frameCount - 1);
	}

	private void UpdateSuperShadows()
	{
		bool superAttackAfterimagesActive = IsPerformingSuperMove;
		bool superJumpAfterimagesActive = IsInSuperJumpRoute && !WasGrounded;
		bool superAfterimagesActive = superAttackAfterimagesActive || superJumpAfterimagesActive;
		if (!superAfterimagesActive)
		{
			ClearSuperAfterimages();
			return;
		}

		EnsureSuperAfterimages();
		Texture2D texture = CharacterSprite.SpriteFrames.GetFrameTexture(CharacterSprite.Animation, CharacterSprite.Frame);
		if (texture == null) return;
		_superShadowHistory.Add(new SuperShadowSample
		{
			Texture = texture,
			WorldPosition = CharacterSprite.GlobalPosition,
			WorldScale = CharacterSprite.GlobalScale,
			FlipH = CharacterSprite.FlipH,
			FlipV = CharacterSprite.FlipV
		});
		while (_superShadowHistory.Count > SuperAfterimageDelays[^1] + 1) _superShadowHistory.RemoveAt(0);

		for (int index = 0; index < _superAfterimages.Count; index++)
		{
			Sprite2D afterimage = _superAfterimages[index];
			afterimage.Modulate = superJumpAfterimagesActive && !superAttackAfterimagesActive
				? new Color(0.3f, 0.8f, 1.8f, 0.28f)
				: new Color(0.25f, 1.25f, 4f, 1f);
			int sampleIndex = _superShadowHistory.Count - 1 - SuperAfterimageDelays[index];
			afterimage.Visible = sampleIndex >= 0;
			if (sampleIndex < 0) continue;
			SuperShadowSample sample = _superShadowHistory[sampleIndex];
			afterimage.Texture = sample.Texture;
			afterimage.GlobalPosition = sample.WorldPosition;
			afterimage.GlobalScale = sample.WorldScale;
			afterimage.FlipH = sample.FlipH;
			afterimage.FlipV = sample.FlipV;
		}
	}

	private void EnsureSuperAfterimages()
	{
		if (_superAfterimages.Count == SuperAfterimageDelays.Length) return;
		Node shadowHost = GetParent() ?? this;
		for (int index = _superAfterimages.Count; index < SuperAfterimageDelays.Length; index++)
		{
			Sprite2D afterimage = new()
			{
				Name = $"SuperAfterimage{index + 1}",
				Centered = CharacterSprite.Centered,
				Offset = CharacterSprite.Offset,
				Visible = false,
				ZIndex = CharacterSprite.ZIndex - index - 1,
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
				Modulate = new Color(0.25f, 1.25f, 4f, 1f)
			};
			shadowHost.AddChild(afterimage);
			_superAfterimages.Add(afterimage);
		}
	}

	private void ClearSuperAfterimages()
	{
		_superShadowHistory.Clear();
		foreach (Sprite2D afterimage in _superAfterimages)
			if (GodotObject.IsInstanceValid(afterimage)) afterimage.QueueFree();
		_superAfterimages.Clear();
	}

	private sealed class SuperShadowSample
	{
		public Texture2D Texture;
		public Vector2 WorldPosition;
		public Vector2 WorldScale;
		public bool FlipH;
		public bool FlipV;
	}
}

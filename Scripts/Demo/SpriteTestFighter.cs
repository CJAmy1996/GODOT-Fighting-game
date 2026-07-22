using System.Collections.Generic;
using Godot;
using ModularFighter.Core;
using ModularFighter.Movement;

namespace ModularFighter.Demo;

/// <summary>Minimal AnimatedSprite2D presentation layer for testing imported sprite animations.</summary>
public partial class SpriteTestFighter : FighterController
{
	[Export] public AnimatedSprite2D CharacterSprite { get; set; }
	private bool _forwardJumpIntroStarted;
	private bool _crouchIntroStarted;
	private bool _crouchExitStarted;
	private bool _wasVisuallyAttacking;
	private string _lastVisualAttackName = "";
	private int _lastVisualAttackFrame = -1;
	private bool _superOneVisualActive;
	private static readonly int[] SuperAfterimageDelays = { 3, 6, 9, 12 };
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
		UpdateAnimation();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateAnimation();
		UpdateSuperShadows();
	}

	private void UpdateAnimation()
	{
		if (CharacterSprite?.SpriteFrames == null) return;
		// SpeedScale freezes the exact displayed sprite frame without resetting animation state.
		CharacterSprite.SpeedScale = IsInHitstop ? 0f : 1f;
		CharacterSprite.FlipH = Facing < 0;
		bool restartAttackAnimation = IsAttacking &&
			(!_wasVisuallyAttacking || CurrentAttackName != _lastVisualAttackName || CurrentAttackFrame < _lastVisualAttackFrame);
		bool backdashing = ActiveAbility?.Id == "backdash";
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
		if (IsAttackActive && !IsInSuperJumpRoute && !CurrentAttackHasHit && CurrentAttackStartedAirborne &&
			(CurrentAttackName == "LIGHT PUNCH" || CurrentAttackName == "LIGHT KICK"))
		{
			StringName heldAirLightAnimation = CurrentAttackName == "LIGHT PUNCH" ? "air_light_punch" : "air_light_kick";
			if (CharacterSprite.Animation != heldAirLightAnimation) CharacterSprite.Play(heldAirLightAnimation);
			CharacterSprite.Frame = 4;
		}
		if (IsInHitstop && CurrentAttackName == "LIGHT PUNCH" && CurrentAttackStartedAirborne && !restartAttackAnimation)
		{
			if (CharacterSprite.Animation != "air_light_punch") CharacterSprite.Play("air_light_punch");
			CharacterSprite.Frame = 4; // Source frame 13_04.
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
			// Timeline frame 4 is the active pose for these grounded jab stances.
			CharacterSprite.Frame = 4;
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
			// Timeline frame 4 always displays source frame 25_02.
			CharacterSprite.Frame = 4;
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

		if (!IsAttacking && !backdashing && !airDashing && forwardAirborne)
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
		else if (airDashing) nextAnimation = "air_dash";
		else if (!WasGrounded) nextAnimation = Velocity.Y < 0f ? "neutral_jump" : "fall";
		else if (ActiveAbility is RunAbility) nextAnimation = "run";
		else if (Mathf.Abs(Velocity.X) > 25f)
			nextAnimation = Velocity.X * Facing < 0f ? "walk_back" : "walk";
		else nextAnimation = "idle";

		if (CharacterSprite.Animation == nextAnimation && !restartAttackAnimation) return;
		CharacterSprite.Play(nextAnimation);
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
		bool superAttackAfterimagesActive = IsAttacking &&
			(CurrentAttackName == "SUPER RUSH" || CurrentAttackName == "SUPER FIREBALL");
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

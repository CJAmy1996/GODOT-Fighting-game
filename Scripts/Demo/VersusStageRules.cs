using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Shared versus rules: opponent-facing and Street Fighter-style pushboxes.</summary>
public partial class VersusStageRules : Node
{
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }
	[Export] public NodePath CameraPath { get; set; }
	[Export] public float StageWidth { get; set; } = 3360f;
	[Export] public float ViewportWidth { get; set; } = 1280f;
	[Export] public float CornerPushbackTransferStartDistance { get; set; } = 28f;
	[Export] public float CornerPushbackTransferMultiplier { get; set; } = 0.75f;
	[Export] public float GroundedLightCornerPushbackTransferMultiplier { get; set; } = 1.65f;
	[Export] public float FacingSideSwitchDeadZone { get; set; } = 18f;
	[Export] public float LandingCrossupDeadZone { get; set; } = 10f;
	[Export] public float CornerProtectionDistance { get; set; } = 8f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float PushboxInstantCorrectionShare { get; set; } = 0.55f;
	[Export] public float PushboxSmoothVelocityScale { get; set; } = 30f;

	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private Camera2D _stageCamera;
	private StageCamera _fightCamera;
	private HitSparkLayer _hitSparkLayer;

	public override void _Ready()
	{
		_fighterOne = GetNode<FighterController>(FighterOnePath);
		_fighterTwo = GetNode<FighterController>(FighterTwoPath);
		_stageCamera = CameraPath == null || CameraPath.IsEmpty ? GetParent().GetNodeOrNull<Camera2D>("StageCamera") : GetNode<Camera2D>(CameraPath);
		_fightCamera = _stageCamera as StageCamera;
		_fighterOne.FaceWithMovement = false;
		_fighterTwo.FaceWithMovement = false;
		_hitSparkLayer = new HitSparkLayer { Name = "HitSparkLayer" };
		GetParent().AddChild(_hitSparkLayer);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fighterOne == null || _fighterTwo == null) return;
		UpdateFacing();
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		if (_fighterOne.JustLanded) ResolveLandingOverlap(_fighterOne, _fighterTwo, leftEdge, rightEdge);
		if (_fighterTwo.JustLanded) ResolveLandingOverlap(_fighterTwo, _fighterOne, leftEdge, rightEdge);
		ResolvePushboxes();
		ResolveBasicAttackHits();
		ResolveProjectileHits();
		ClampFightersToCameraCorners(leftEdge, rightEdge);
	}

	private void UpdateFacing()
	{
		float delta = _fighterTwo.WorldPositionBox.GetCenter().X - _fighterOne.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(delta) <= FacingSideSwitchDeadZone) return;
		int direction = delta >= 0f ? 1 : -1;
		if (CanAutoFaceOpponent(_fighterOne)) _fighterOne.SetFacing(direction);
		if (CanAutoFaceOpponent(_fighterTwo)) _fighterTwo.SetFacing(-direction);
	}

	private bool CanAutoFaceOpponent(FighterController fighter) =>
		fighter.WasGrounded || fighter.JustLanded || !fighter.SuppressesGroundedPushWhileAirborne || fighter.EnablesAirControlWhileAirborne;

	private static bool IsGroundedForPushbox(FighterController fighter) =>
		fighter.IsOnFloor() || fighter.JustLanded;

	private static bool IsAirborneGroundedPushSuppressed(FighterController fighter) =>
		!IsGroundedForPushbox(fighter) && fighter.SuppressesGroundedPushWhileAirborne;

	private void ResolvePushboxes()
	{
		// A jump passes through a grounded opponent without body push, but two airborne
		// fighters still collide. Air dashes use the default false rule and can push ground.
		if (IsGroundedForPushbox(_fighterOne) && IsAirborneGroundedPushSuppressed(_fighterTwo))
		{
			if (_fighterTwo.JumpInteractsWithGroundedPushbox || _fighterTwo.ShortHopInteractsWithGroundedPushbox)
				ResolveAirborneVsGroundedPushbox(_fighterTwo, _fighterOne);
			return;
		}
		if (IsGroundedForPushbox(_fighterTwo) && IsAirborneGroundedPushSuppressed(_fighterOne))
		{
			if (_fighterOne.JumpInteractsWithGroundedPushbox || _fighterOne.ShortHopInteractsWithGroundedPushbox)
				ResolveAirborneVsGroundedPushbox(_fighterOne, _fighterTwo);
			return;
		}
		if (!_fighterOne.WorldPushbox.Intersects(_fighterTwo.WorldPushbox)) return;

		FighterController left = _fighterOne.WorldPushbox.GetCenter().X <= _fighterTwo.WorldPushbox.GetCenter().X ? _fighterOne : _fighterTwo;
		FighterController right = left == _fighterOne ? _fighterTwo : _fighterOne;
		float overlap = HorizontalOverlap(left.WorldPushbox, right.WorldPushbox);
		ApplyImmediateHorizontalPush(left, -overlap * 0.5f);
		ApplyImmediateHorizontalPush(right, overlap * 0.5f);

		float remaining = HorizontalOverlap(left.WorldPushbox, right.WorldPushbox);
		if (remaining > 0)
		{
			if (Mathf.IsEqualApprox(left.GlobalPosition.X, MinOriginX(left)))
				ApplyImmediateHorizontalPush(right, remaining);
			else
				ApplyImmediateHorizontalPush(left, -remaining);
		}
	}

	private void ResolveAirborneVsGroundedPushbox(FighterController airborne, FighterController grounded)
	{
		if (!airborne.WorldPushbox.Intersects(grounded.WorldPushbox)) return;

		float overlap = HorizontalOverlap(airborne.WorldPushbox, grounded.WorldPushbox);
		if (overlap <= 0f) return;
		if (TryGetCornerProtectionOpenSide(grounded, out int openSide) && IsOnProtectedCornerSide(airborne, grounded, openSide))
		{
			ApplySmoothHorizontalPush(airborne, openSide * overlap);
			return;
		}
		float currentDelta = airborne.WorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		float previousDelta = airborne.PreviousWorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		int side = !Mathf.IsZeroApprox(currentDelta)
			? Mathf.Sign(currentDelta) >= 0 ? 1 : -1
			: (!Mathf.IsZeroApprox(previousDelta) ? (Mathf.Sign(previousDelta) >= 0 ? 1 : -1) : (airborne.Velocity.X >= 0 ? 1 : -1));

		float groundedShare = Mathf.Clamp(airborne.JumpGroundedPushStrength, 0f, 1f);
		float airborneShare = 1f - groundedShare;
		ApplySmoothHorizontalPush(airborne, side * overlap * airborneShare);
		ApplySmoothHorizontalPush(grounded, -side * overlap * groundedShare);
	}

	private void ResolveLandingOverlap(FighterController lander, FighterController opponent, float leftEdge, float rightEdge)
	{
		if (!lander.WorldPushbox.Intersects(opponent.WorldPushbox)) return;

		// Basic fighting-game landing resolver:
		// the torso/axis position box chooses side; pushboxes only provide spacing.
		int side = TryGetCornerProtectionOpenSide(opponent, out int openSide)
			? openSide
			: ChooseLandingSide(lander, opponent);
		float overlap = HorizontalOverlap(lander.WorldPushbox, opponent.WorldPushbox);
		// Resolve by pressure, not perfect placement. This lets the corner deny
		// a crossup with a basic push instead of snapping the lander across the body.
		ApplySmoothHorizontalPush(lander, side * overlap, leftEdge, rightEdge);
	}

	private int ChooseLandingSide(FighterController lander, FighterController opponent)
	{
		float previousDelta = lander.PreviousWorldPositionBox.GetCenter().X - opponent.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(previousDelta) > LandingCrossupDeadZone) return previousDelta >= 0f ? 1 : -1;

		float currentDelta = lander.WorldPositionBox.GetCenter().X - opponent.WorldPositionBox.GetCenter().X;
		if (Mathf.Abs(currentDelta) > LandingCrossupDeadZone) return currentDelta >= 0f ? 1 : -1;

		return lander.Velocity.X >= 0f ? 1 : -1;
	}

	private bool TryGetCornerProtectionOpenSide(FighterController grounded, out int openSide)
	{
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		if (grounded.WorldPushbox.Position.X <= leftEdge + CornerProtectionDistance)
		{
			openSide = 1;
			return true;
		}
		if (grounded.WorldPushbox.End.X >= rightEdge - CornerProtectionDistance)
		{
			openSide = -1;
			return true;
		}
		openSide = 0;
		return false;
	}

	private bool IsOnProtectedCornerSide(FighterController airborne, FighterController grounded, int openSide)
	{
		float delta = airborne.WorldPositionBox.GetCenter().X - grounded.WorldPositionBox.GetCenter().X;
		return delta * openSide < 0f;
	}

	private float HorizontalOverlap(Rect2 first, Rect2 second) =>
		Mathf.Max(0, Mathf.Min(first.End.X, second.End.X) - Mathf.Max(first.Position.X, second.Position.X));

	private void ApplySmoothHorizontalPush(FighterController fighter, float pushDelta)
	{
		ApplySmoothHorizontalPush(fighter, pushDelta, float.NegativeInfinity, float.PositiveInfinity);
	}

	private void ApplySmoothHorizontalPush(FighterController fighter, float pushDelta, float leftEdge, float rightEdge)
	{
		if (Mathf.IsZeroApprox(pushDelta)) return;

		float oldX = fighter.GlobalPosition.X;
		float targetX = float.IsNegativeInfinity(leftEdge) || float.IsPositiveInfinity(rightEdge)
			? ClampOriginX(fighter, fighter.GlobalPosition.X + pushDelta)
			: ClampOriginX(fighter, fighter.GlobalPosition.X + pushDelta, leftEdge, rightEdge);
		fighter.GlobalPosition = new Vector2(targetX, fighter.GlobalPosition.Y);
		fighter.AddVisualCorrection(new Vector2(targetX - oldX, 0f));
	}

	private void ApplyImmediateHorizontalPush(FighterController fighter, float pushDelta)
	{
		if (Mathf.IsZeroApprox(pushDelta)) return;
		fighter.GlobalPosition = new Vector2(ClampOriginX(fighter, fighter.GlobalPosition.X + pushDelta), fighter.GlobalPosition.Y);
	}

	private void ResolveBasicAttackHits()
	{
		bool firstHit = _fighterOne.TryApplyBasicAttackHit(_fighterTwo, out int firstHitstop, out float firstShake, out float firstPushback, out Vector2 firstHitPoint, out bool firstHeavySpark);
		bool secondHit = _fighterTwo.TryApplyBasicAttackHit(_fighterOne, out int secondHitstop, out float secondShake, out float secondPushback, out Vector2 secondHitPoint, out bool secondHeavySpark);
		if (firstHit)
		{
			ApplyHitstopForHit(_fighterOne, _fighterTwo, firstHitstop);
			ApplyCornerPushbackTransfer(_fighterOne, _fighterTwo, firstPushback);
			_hitSparkLayer?.Spawn(firstHitPoint, firstHeavySpark);
		}
		if (secondHit)
		{
			ApplyHitstopForHit(_fighterTwo, _fighterOne, secondHitstop);
			ApplyCornerPushbackTransfer(_fighterTwo, _fighterOne, secondPushback);
			_hitSparkLayer?.Spawn(secondHitPoint, secondHeavySpark);
		}
		float shake = Mathf.Max(firstShake, secondShake);
		int shakeFrames = Mathf.Max(firstHitstop, secondHitstop);
		if (shake > 0f) _fightCamera?.Shake(shake, shakeFrames);
	}

	private void ResolveProjectileHits()
	{
		foreach (Node node in GetTree().GetNodesInGroup(BasicProjectile.ProjectileGroup))
		{
			if (node is not BasicProjectile projectile || projectile.HasHit || projectile.OwnerFighter == null) continue;
			FighterController defender = projectile.OwnerFighter == _fighterOne
				? _fighterTwo
				: projectile.OwnerFighter == _fighterTwo ? _fighterOne : null;
			if (defender == null) continue;

			if (!projectile.OwnerFighter.TryApplyProjectileHit(defender, projectile.WorldHitbox, projectile.HitstunFrames, projectile.Pushback,
				projectile.HitstopFrames, projectile.ShakeStrength, out int hitstop, out float shake, out _, out Vector2 hitPoint, out bool heavySpark)) continue;

			if (hitstop > 0) defender.RequestHitstop(hitstop);
			if (shake > 0f) _fightCamera?.Shake(shake, hitstop);
			_hitSparkLayer?.Spawn(hitPoint, heavySpark);
			projectile.MarkHit();
		}
	}

	private void ApplyCornerPushbackTransfer(FighterController attacker, FighterController defender, float hitPushback)
	{
		if (hitPushback <= 0f) return;
		GetFightBoxEdges(out float leftEdge, out float rightEdge);
		float pushDirection = Mathf.Sign(attacker.Facing);
		float availableSpace = pushDirection > 0f
			? rightEdge - defender.WorldPushbox.End.X
			: defender.WorldPushbox.Position.X - leftEdge;

		float blockedFraction = 1f - Mathf.Clamp(availableSpace / CornerPushbackTransferStartDistance, 0f, 1f);
		if (blockedFraction <= 0f) return;

		float moveMultiplier = !attacker.CurrentAttackStartedAirborne && attacker.CurrentAttackName.StartsWith("LIGHT")
			? GroundedLightCornerPushbackTransferMultiplier
			: 1f;
		float recoil = hitPushback * CornerPushbackTransferMultiplier * moveMultiplier * blockedFraction;
		float recoilVelocity = -pushDirection * recoil;
		if (Mathf.Abs(recoilVelocity) > Mathf.Abs(attacker.Velocity.X))
			attacker.Velocity = new Vector2(recoilVelocity, attacker.Velocity.Y);
	}

	private static void ApplyHitstopForHit(FighterController attacker, FighterController defender, int hitstop)
	{
		if (hitstop <= 0) return;
		bool airAttackHitGroundedDefender = attacker.CurrentAttackStartedAirborne && defender.WasGrounded;
		int attackerHitstop = airAttackHitGroundedDefender
			? System.Math.Min(hitstop, attacker.CurrentAirToGroundAttackerHitstopFrames)
			: hitstop;
		if (attackerHitstop > 0) attacker.RequestHitstop(attackerHitstop);
		defender.RequestHitstop(hitstop);
	}

	private void ClampFightersToCameraCorners(float leftEdge, float rightEdge)
	{
		ClampFighterToCameraRange(_fighterOne, leftEdge, rightEdge);
		ClampFighterToCameraRange(_fighterTwo, leftEdge, rightEdge);
	}

	private void GetFightBoxEdges(out float leftEdge, out float rightEdge)
	{
		float halfView = _stageCamera == null || Mathf.IsZeroApprox(_stageCamera.Zoom.X)
			? ViewportWidth * 0.5f
			: (ViewportWidth / _stageCamera.Zoom.X) * 0.5f;
		leftEdge = _fightCamera != null ? _fightCamera.FightBoxLeft : (_stageCamera == null ? 0f : _stageCamera.GlobalPosition.X - halfView);
		rightEdge = _fightCamera != null ? _fightCamera.FightBoxRight : (_stageCamera == null ? StageWidth : _stageCamera.GlobalPosition.X + halfView);
		leftEdge = Mathf.Clamp(leftEdge, 0f, StageWidth);
		rightEdge = Mathf.Clamp(rightEdge, 0f, StageWidth);
	}

	private void ClampFighterToCameraRange(FighterController fighter, float leftEdge, float rightEdge)
	{
		float min = Mathf.Max(MinOriginX(fighter), leftEdge - fighter.PushboxLocal.Position.X);
		float max = Mathf.Min(MaxOriginX(fighter), rightEdge - fighter.PushboxLocal.End.X);
		float clampedX = Mathf.Clamp(fighter.GlobalPosition.X, min, max);
		if (Mathf.IsEqualApprox(clampedX, fighter.GlobalPosition.X)) return;

		// Camera corners are game rules, not physical walls. Clamp position only so
		// movement intent/velocity remains alive and walk/run animations keep playing.
		fighter.GlobalPosition = new Vector2(clampedX, fighter.GlobalPosition.Y);
	}

	private float MinOriginX(FighterController fighter) => -fighter.PushboxLocal.Position.X;
	private float MaxOriginX(FighterController fighter) => StageWidth - fighter.PushboxLocal.End.X;
	private float ClampOriginX(FighterController fighter, float x) => Mathf.Clamp(x, MinOriginX(fighter), MaxOriginX(fighter));
	private float MinOriginX(FighterController fighter, float leftEdge) => Mathf.Max(MinOriginX(fighter), leftEdge - fighter.PushboxLocal.Position.X);
	private float MaxOriginX(FighterController fighter, float rightEdge) => Mathf.Min(MaxOriginX(fighter), rightEdge - fighter.PushboxLocal.End.X);
	private float ClampOriginX(FighterController fighter, float x, float leftEdge, float rightEdge) => Mathf.Clamp(x, MinOriginX(fighter, leftEdge), MaxOriginX(fighter, rightEdge));
}

using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>
/// Art of Fighting-style versus camera: a smaller fight box moves inside the full stage.
/// The camera shows exactly that fight box, while stage art can be much larger.
/// </summary>
public partial class StageCamera : Camera2D
{
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }
	[Export] public float StageWidth { get; set; } = 3360f;
	[Export] public float StageTopY { get; set; } = -650f;
	[Export] public float StageHeight { get; set; } = 1008f;
	[Export] public float ViewportWidth { get; set; } = 1280f;
	[Export] public float ViewportHeight { get; set; } = 720f;
	[Export] public float CameraHeight { get; set; } = 500f;
	[Export] public float FollowSpeed { get; set; } = 10f;
	[ExportGroup("Fight Box")]
	[Export] public float CloseFightBoxWidth { get; set; } = 720f;
	[Export] public float FarFightBoxWidth { get; set; } = 860f;
	[Export] public float FighterHorizontalPadding { get; set; } = 160f;
	[ExportGroup("Vertical Follow")]
	[Export] public float SuperJumpFollowHeight { get; set; } = 90f;
	[Export] public float SuperJumpFollowWeight { get; set; } = 0.85f;
	[Export] public float VerticalFighterPadding { get; set; } = 120f;
	[ExportGroup("Zoom")]
	[Export] public float ZoomSpeed { get; set; } = 5f;
	public Rect2 CurrentFightBox { get; private set; }

	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private readonly RandomNumberGenerator _shakeRandom = new();
	private int _shakeFramesLeft;
	private float _shakeStrength;
	private bool _cinematicShake;
	private FighterController _cinematicHorizontalFocus;
	private bool _shockwaveScrollActive;
	private int _shockwaveImpactFramesLeft;
	private float _shockwaveImpactStrength;

	public override void _Ready()
	{
		// Fighters update at the default priority, then the camera establishes the
		// deterministic fight box, then VersusStageRules resolves at priority 100.
		ProcessPhysicsPriority = 50;
		_fighterOne = GetNode<FighterController>(FighterOnePath);
		_fighterTwo = GetNode<FighterController>(FighterTwoPath);
		Position = new Vector2(ViewportWidth * 0.5f, CameraHeight);
		Zoom = Vector2.One * (ViewportWidth / FarFightBoxWidth);
		UpdateCurrentFightBox();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_fighterOne == null || _fighterTwo == null) return;

		float fighterOneX = _fighterOne.GlobalPosition.X;
		float fighterTwoX = _fighterTwo.GlobalPosition.X;
		float leftFighterX = Mathf.Min(fighterOneX, fighterTwoX);
		float rightFighterX = Mathf.Max(fighterOneX, fighterTwoX);
		float midpoint = (fighterOneX + fighterTwoX) * 0.5f;
		float horizontalFightBoxWidth = GodotObject.IsInstanceValid(_cinematicHorizontalFocus)
			? CloseFightBoxWidth
			: Mathf.Clamp(rightFighterX - leftFighterX + FighterHorizontalPadding * 2f,
				CloseFightBoxWidth, Mathf.Min(FarFightBoxWidth, StageWidth));
		float highestFighterY = Mathf.Min(_fighterOne.GlobalPosition.Y, _fighterTwo.GlobalPosition.Y);
		float lowestFighterY = Mathf.Max(_fighterOne.GlobalPosition.Y, _fighterTwo.GlobalPosition.Y);
		bool launcherChaseActive = IsLauncherChase(_fighterOne, _fighterTwo) ||
			IsLauncherChase(_fighterTwo, _fighterOne);
		float requiredVerticalViewHeight = lowestFighterY - highestFighterY + VerticalFighterPadding * 2f;
		float verticalFightBoxWidth = requiredVerticalViewHeight * ViewportWidth / ViewportHeight;
		float desiredFightBoxWidth = Mathf.Clamp(launcherChaseActive
			? Mathf.Max(horizontalFightBoxWidth, verticalFightBoxWidth)
			: horizontalFightBoxWidth,
			CloseFightBoxWidth, StageWidth);
		float targetZoom = ViewportWidth / desiredFightBoxWidth;
		targetZoom = Mathf.Max(targetZoom, MinimumStageZoom());
		float zoomWeight = 1f - Mathf.Exp(-ZoomSpeed * (float)delta);
		float smoothedZoom = Mathf.Lerp(Zoom.X, targetZoom, zoomWeight);
		smoothedZoom = Mathf.Max(smoothedZoom, MinimumStageZoom());
		Zoom = Vector2.One * smoothedZoom;

		float halfView = Mathf.Min((ViewportWidth / smoothedZoom) * 0.5f, StageWidth * 0.5f);
		float targetX = GodotObject.IsInstanceValid(_cinematicHorizontalFocus)
			? _cinematicHorizontalFocus.GlobalPosition.X
			: midpoint;
		targetX = Mathf.Clamp(targetX, halfView, StageWidth - halfView);
		float weight = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
		float resolvedX;
		if (GodotObject.IsInstanceValid(_cinematicHorizontalFocus))
		{
			resolvedX = Mathf.Lerp(GlobalPosition.X, targetX, weight);
		}
		else if (TryResolveSharedCameraCenterRange(halfView, out float minimumCenter, out float maximumCenter))
		{
			// A fighter already occupying the opposite screen edge creates an
			// unofficial corner. Clamp the camera itself before stage rules clamp
			// either character, so retreating can never drag the opponent.
			targetX = Mathf.Clamp(targetX, minimumCenter, maximumCenter);
			resolvedX = Mathf.Clamp(Mathf.Lerp(GlobalPosition.X, targetX, weight), minimumCenter, maximumCenter);
		}
		else
		{
			// The fighters no longer fit in a wider translated view. Hold the camera
			// still; VersusStageRules will stop whichever fighter crossed its edge.
			resolvedX = Mathf.Clamp(GlobalPosition.X, halfView, StageWidth - halfView);
		}
		float halfHeight = (ViewportHeight / smoothedZoom) * 0.5f;
		float highJumpAmount = Mathf.Max(0f, CameraHeight - highestFighterY - SuperJumpFollowHeight);
		float targetY = CameraHeight - highJumpAmount * SuperJumpFollowWeight;
		float usableVerticalHalfView = Mathf.Max(1f, halfHeight - VerticalFighterPadding);
		float minimumFramingCenter = lowestFighterY - usableVerticalHalfView;
		float maximumFramingCenter = highestFighterY + usableVerticalHalfView;
		if (launcherChaseActive && minimumFramingCenter <= maximumFramingCenter)
			targetY = Mathf.Clamp(targetY, minimumFramingCenter, maximumFramingCenter);
		bool superSpdFlight = _fighterOne.IsPerformingCharacterSuperGrab || _fighterTwo.IsPerformingCharacterSuperGrab;
		// Super SPD deliberately travels several screens above the normal stage.
		// Keep the bottom clamp, but release the ordinary top clamp for its flight.
		targetY = superSpdFlight
			? Mathf.Min(targetY, StageHeight - halfHeight)
			: Mathf.Clamp(targetY, StageTopY + halfHeight, StageHeight - halfHeight);
		GlobalPosition = new Vector2(resolvedX, Mathf.Lerp(GlobalPosition.Y, targetY, weight));
		UpdateShake();
		UpdateCurrentFightBox();
	}

	private static bool IsLauncherChase(FighterController chaser, FighterController launched)
	{
		if (!chaser.IsInSuperJumpRoute || chaser.WasGrounded || launched.WasGrounded ||
			launched.HitstunFramesLeft <= 0) return false;
		return launched.HitState is FighterHitState.Juggle or FighterHitState.Tumble or
			FighterHitState.CounterHit or FighterHitState.Hitstun;
	}

	private bool TryResolveSharedCameraCenterRange(float halfView, out float minimumCenter, out float maximumCenter)
	{
		Rect2 fighterOneBox = _fighterOne.WorldPushbox;
		Rect2 fighterTwoBox = _fighterTwo.WorldPushbox;
		minimumCenter = Mathf.Max(halfView,
			Mathf.Max(fighterOneBox.End.X - halfView, fighterTwoBox.End.X - halfView));
		maximumCenter = Mathf.Min(StageWidth - halfView,
			Mathf.Min(fighterOneBox.Position.X + halfView, fighterTwoBox.Position.X + halfView));
		return minimumCenter <= maximumCenter;
	}

	public float FightBoxLeft => CurrentFightBox.Position.X;
	public float FightBoxRight => CurrentFightBox.End.X;
	public void SetPrimaryFighter(FighterController fighter)
	{
		if (fighter != null) _fighterOne = fighter;
	}
	public void FocusHorizontalOn(FighterController fighter) => _cinematicHorizontalFocus = fighter;
	public void ClearHorizontalFocus() => _cinematicHorizontalFocus = null;
	public void BeginShockwaveScroll()
	{
		_shockwaveScrollActive = true;
		// The activation rumble must not fight the scrolling camera. MVC2's wave
		// presentation is driven by screen travel plus discrete ground impacts.
		_shakeFramesLeft = 0;
		_shakeStrength = 0f;
		_cinematicShake = false;
	}
	public void EndShockwaveScroll() => _shockwaveScrollActive = false;
	public void ShockwaveImpact(float strength)
	{
		_shockwaveImpactStrength = Mathf.Clamp(strength * 0.42f, 1.5f, 4.25f);
		_shockwaveImpactFramesLeft = 5;
	}
	public void Shake(float strength, int frames)
	{
		if (strength <= _shakeStrength && frames <= _shakeFramesLeft) return;
		_shakeStrength = Mathf.Max(_shakeStrength, strength);
		_shakeFramesLeft = Mathf.Max(_shakeFramesLeft, frames);
	}

	public void ShakeSuper(float strength, int frames)
	{
		_cinematicShake = true;
		_shakeStrength = Mathf.Max(_shakeStrength, strength);
		_shakeFramesLeft = Mathf.Max(_shakeFramesLeft, Mathf.Max(12, frames));
	}

	private float MinimumStageZoom()
	{
		float horizontalMinimum = ViewportWidth / StageWidth;
		float verticalMinimum = ViewportHeight / (StageHeight - StageTopY);
		return Mathf.Max(horizontalMinimum, verticalMinimum);
	}

	private void UpdateCurrentFightBox()
	{
		float width = ViewportWidth / Zoom.X;
		float height = ViewportHeight / Zoom.Y;
		CurrentFightBox = new Rect2(GlobalPosition - new Vector2(width, height) * 0.5f, new Vector2(width, height));
	}

	private void UpdateShake()
	{
		Vector2 randomShake = Vector2.Zero;
		if (_shakeFramesLeft > 0 && !_shockwaveScrollActive)
		{
			_shakeFramesLeft--;
			randomShake = new Vector2(
				_shakeRandom.RandfRange(-_shakeStrength, _shakeStrength),
				_shakeRandom.RandfRange(-_shakeStrength, _shakeStrength));
			_shakeStrength *= _cinematicShake ? 0.94f : 0.82f;
		}
		else if (_shakeFramesLeft <= 0)
		{
			_shakeStrength = 0f;
			_cinematicShake = false;
		}

		Vector2 impactKick = Vector2.Zero;
		if (_shockwaveImpactFramesLeft > 0)
		{
			int phase = 5 - _shockwaveImpactFramesLeft--;
			float vertical = phase switch
			{
				0 => 1f,
				1 => -0.52f,
				2 => 0.28f,
				3 => -0.12f,
				_ => 0f
			};
			impactKick = new Vector2(0f, vertical * _shockwaveImpactStrength);
		}
		Offset = randomShake + impactKick;
	}
}

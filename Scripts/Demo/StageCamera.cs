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
	[ExportGroup("Zoom")]
	[Export] public float ZoomSpeed { get; set; } = 5f;
	public Rect2 CurrentFightBox { get; private set; }

	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private readonly RandomNumberGenerator _shakeRandom = new();
	private int _shakeFramesLeft;
	private float _shakeStrength;
	private bool _cinematicShake;

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
		float desiredFightBoxWidth = Mathf.Clamp(
			rightFighterX - leftFighterX + FighterHorizontalPadding * 2f,
			CloseFightBoxWidth,
			Mathf.Min(FarFightBoxWidth, StageWidth));
		float targetZoom = ViewportWidth / desiredFightBoxWidth;
		targetZoom = Mathf.Max(targetZoom, MinimumStageZoom());
		float zoomWeight = 1f - Mathf.Exp(-ZoomSpeed * (float)delta);
		float smoothedZoom = Mathf.Lerp(Zoom.X, targetZoom, zoomWeight);
		smoothedZoom = Mathf.Max(smoothedZoom, MinimumStageZoom());
		Zoom = Vector2.One * smoothedZoom;

		float halfView = Mathf.Min((ViewportWidth / smoothedZoom) * 0.5f, StageWidth * 0.5f);
		float targetX = midpoint;
		targetX = Mathf.Clamp(targetX, halfView, StageWidth - halfView);
		float weight = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
		float halfHeight = (ViewportHeight / smoothedZoom) * 0.5f;
		float highestFighterY = Mathf.Min(_fighterOne.GlobalPosition.Y, _fighterTwo.GlobalPosition.Y);
		float highJumpAmount = Mathf.Max(0f, CameraHeight - highestFighterY - SuperJumpFollowHeight);
		float targetY = CameraHeight - highJumpAmount * SuperJumpFollowWeight;
		targetY = Mathf.Clamp(targetY, StageTopY + halfHeight, StageHeight - halfHeight);
		GlobalPosition = new Vector2(Mathf.Lerp(GlobalPosition.X, targetX, weight), Mathf.Lerp(GlobalPosition.Y, targetY, weight));
		UpdateShake();
		UpdateCurrentFightBox();
	}

	public float FightBoxLeft => CurrentFightBox.Position.X;
	public float FightBoxRight => CurrentFightBox.End.X;
	public void SetPrimaryFighter(FighterController fighter)
	{
		if (fighter != null) _fighterOne = fighter;
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
		if (_shakeFramesLeft <= 0)
		{
			Offset = Vector2.Zero;
			_shakeStrength = 0f;
			_cinematicShake = false;
			return;
		}
		_shakeFramesLeft--;
		float x = _shakeRandom.RandfRange(-_shakeStrength, _shakeStrength);
		float y = _shakeRandom.RandfRange(-_shakeStrength, _shakeStrength);
		Offset = new Vector2(x, y);
		_shakeStrength *= _cinematicShake ? 0.94f : 0.82f;
	}
}

using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Placeholder versus HUD backed by fighter resources; combat values can be wired in later.</summary>
public partial class CombatHud : Node2D
{
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath FighterTwoPath { get; set; }
	[Export] public NodePath StageCameraPath { get; set; }
	[Export] public NodePath StageRulesPath { get; set; }
	[Export] public float DamageTrailHoldSeconds { get; set; } = 0.3f;
	[Export] public float DamageTrailDrainPerSecond { get; set; } = 280f;
	[Export] public float LifeBarTopOffset { get; set; } = 136f;
	[Export] public float ComboCounterOffset { get; set; } = 48f;
	private FighterController _fighterOne;
	private FighterController _fighterTwo;
	private StageCamera _stageCamera;
	private VersusStageRules _stageRules;
	private float _fighterOneTrailLife;
	private float _fighterTwoTrailLife;
	private float _fighterOneTrailHold;
	private float _fighterTwoTrailHold;

	public override void _Ready()
	{
		_fighterOne = GetNodeOrNull<FighterController>(FighterOnePath);
		_fighterTwo = GetNodeOrNull<FighterController>(FighterTwoPath);
		_stageCamera = GetNodeOrNull<StageCamera>(StageCameraPath);
		_stageRules = GetNodeOrNull<VersusStageRules>(StageRulesPath);
		_fighterOneTrailLife = _fighterOne?.PlaceholderLife ?? 0f;
		_fighterTwoTrailLife = _fighterTwo?.PlaceholderLife ?? 0f;
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = -80;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		UpdateDamageTrail(_fighterOne, ref _fighterOneTrailLife, ref _fighterOneTrailHold, (float)delta);
		UpdateDamageTrail(_fighterTwo, ref _fighterTwoTrailLife, ref _fighterTwoTrailHold, (float)delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 cameraRect = _stageCamera?.CurrentFightBox ?? new Rect2(Vector2.Zero, GetViewportRect().Size);
		float zoom = Mathf.Max(0.01f, _stageCamera?.Zoom.X ?? 1f);
		float barHeight = 22f / zoom;
		float centerGap = 104f / zoom;
		float sideMargin = 56f / zoom;
		float barWidth = (cameraRect.Size.X - centerGap - sideMargin * 2f) * 0.5f;
		float y = cameraRect.Position.Y + LifeBarTopOffset / zoom;
		float centerX = cameraRect.GetCenter().X;
		DrawFighterGauge(_fighterOne, new Rect2(centerX - centerGap * 0.5f - barWidth, y, barWidth, barHeight), true, _fighterOneTrailLife, zoom);
		DrawFighterGauge(_fighterTwo, new Rect2(centerX + centerGap * 0.5f, y, barWidth, barHeight), false, _fighterTwoTrailLife, zoom);
		DrawTimer(centerX, y, zoom);
	}

	private void DrawFighterGauge(FighterController fighter, Rect2 lifeRect, bool reverse, float trailLife, float zoom)
	{
		FighterGaugeData data = fighter?.Definition?.Gauges;
		if (data == null) return;
		float textWidth = 170f / zoom;
		DrawString(ThemeDB.FallbackFont, lifeRect.Position + new Vector2(reverse ? lifeRect.Size.X - textWidth : 0f, -8f / zoom),
			fighter.Definition.FighterName, HorizontalAlignment.Left, textWidth, Mathf.RoundToInt(16f / zoom), Colors.White);
		float lifeRatio = data.MaxLife <= 0 ? 0f : fighter.PlaceholderLife / data.MaxLife;
		float trailRatio = data.MaxLife <= 0 ? 0f : trailLife / data.MaxLife;
		DrawBar(lifeRect, trailRatio, new Color(0.85f, 0.08f, 0.08f, 0.48f), reverse, zoom);
		DrawBarFill(lifeRect, lifeRatio, data.LifeColor, reverse);
		DrawComboCounter(fighter, lifeRect, reverse, zoom);
		Rect2 meterRect = new(lifeRect.Position + new Vector2(0f, 58f / zoom), new Vector2(lifeRect.Size.X, 12f / zoom));
		DrawBar(meterRect, data.MaxSpecialMeter <= 0 ? 0f : fighter.PlaceholderSpecialMeter / data.MaxSpecialMeter,
			data.SpecialMeterColor, reverse, zoom);
		float meterTextWidth = 90f / zoom;
		DrawString(ThemeDB.FallbackFont, meterRect.Position + new Vector2(reverse ? meterRect.Size.X - meterTextWidth : 0f, 27f / zoom),
			data.SpecialMeterName, HorizontalAlignment.Left, meterTextWidth, Mathf.RoundToInt(13f / zoom), new Color(0.75f, 0.88f, 1f));
	}

	private void DrawComboCounter(FighterController fighter, Rect2 lifeRect, bool reverse, float zoom)
	{
		if (fighter.ComboCount < 2 || fighter.ComboDisplayFramesLeft <= 0) return;
		string comboText = $"{fighter.ComboCount} HIT COMBO";
		float width = 170f / zoom;
		Vector2 position = lifeRect.Position + new Vector2(reverse ? lifeRect.Size.X - width : 0f, ComboCounterOffset / zoom);
		DrawString(ThemeDB.FallbackFont, position, comboText, reverse ? HorizontalAlignment.Right : HorizontalAlignment.Left,
			width, Mathf.RoundToInt(18f / zoom), new Color(1f, 0.84f, 0.18f, 1f));
	}

	private void DrawTimer(float centerX, float y, float zoom)
	{
		int seconds = Mathf.CeilToInt(_stageRules?.RoundSecondsRemaining ?? 99f);
		string timerText = _stageRules?.IsKoActive == true ? "KO" : seconds.ToString("00");
		float width = 96f / zoom;
		Rect2 timerRect = new(new Vector2(centerX - width * 0.5f, y - 12f / zoom), new Vector2(width, 48f / zoom));
		DrawRect(timerRect, new Color(0.025f, 0.03f, 0.055f, 0.96f), true);
		DrawRect(timerRect, new Color(0.8f, 0.84f, 0.95f, 0.7f), false, 2f / zoom);
		DrawString(ThemeDB.FallbackFont, timerRect.Position + new Vector2(0f, 34f / zoom), timerText,
			HorizontalAlignment.Center, width, Mathf.RoundToInt(28f / zoom), Colors.White);
	}

	private void UpdateDamageTrail(FighterController fighter, ref float trailLife, ref float hold, float delta)
	{
		if (fighter == null) return;
		float life = fighter.PlaceholderLife;
		if (life < trailLife)
			hold = DamageTrailHoldSeconds;
		else if (life > trailLife)
			trailLife = life;
		if (fighter.ComboCount > 0 || fighter.HitstunFramesLeft > 0)
		{
			hold = DamageTrailHoldSeconds;
			return;
		}
		if (hold > 0f)
			hold -= delta;
		else
			trailLife = Mathf.MoveToward(trailLife, life, DamageTrailDrainPerSecond * delta);
	}

	private void DrawBar(Rect2 rect, float ratio, Color color, bool reverse, float zoom = 1f)
	{
		DrawRect(rect.Grow(3f / zoom), new Color(0.03f, 0.04f, 0.07f, 0.92f), true);
		DrawRect(rect, new Color(0.16f, 0.17f, 0.2f, 0.95f), true);
		DrawBarFill(rect, ratio, color, reverse);
		DrawLine(rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Color(1f, 1f, 1f, 0.35f), 2f / zoom);
	}

	private void DrawBarFill(Rect2 rect, float ratio, Color color, bool reverse)
	{
		float width = rect.Size.X * Mathf.Clamp(ratio, 0f, 1f);
		Rect2 fill = reverse
			? new Rect2(rect.End.X - width, rect.Position.Y, width, rect.Size.Y)
			: new Rect2(rect.Position, new Vector2(width, rect.Size.Y));
		DrawRect(fill, color, true);
	}
}

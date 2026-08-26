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
	[Export] public float DamageTrailDrainSeconds { get; set; } = 0.35f;
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
	private float _fighterOneLastObservedLife;
	private float _fighterTwoLastObservedLife;
	private float _fighterOneTrailDrainRate;
	private float _fighterTwoTrailDrainRate;
	private int _fighterOneLastComboCount;
	private int _fighterTwoLastComboCount;
	private float _fighterOneComboFlashSeconds;
	private float _fighterTwoComboFlashSeconds;
	private Texture2D _revolveGaugeSheet;
	private Texture2D _revolveNumberSheet;
	private Texture2D _lifeStockMarker;
	private readonly System.Collections.Generic.Dictionary<string, Texture2D> _faceTextures = new();

	public override void _Ready()
	{
		_fighterOne = GetNodeOrNull<FighterController>(FighterOnePath);
		_fighterTwo = GetNodeOrNull<FighterController>(FighterTwoPath);
		_stageCamera = GetNodeOrNull<StageCamera>(StageCameraPath);
		_stageRules = GetNodeOrNull<VersusStageRules>(StageRulesPath);
		_fighterOneTrailLife = _fighterOne?.PlaceholderLife ?? 0f;
		_fighterTwoTrailLife = _fighterTwo?.PlaceholderLife ?? 0f;
		_fighterOneLastObservedLife = _fighterOneTrailLife;
		_fighterTwoLastObservedLife = _fighterTwoTrailLife;
		_revolveGaugeSheet = ResourceLoader.Load<Texture2D>("res://Assets/Hud/BigBangBeatRevolve/gauge.png");
		_revolveNumberSheet = ResourceLoader.Load<Texture2D>("res://Assets/Hud/BigBangBeatRevolve/num.png");
		_lifeStockMarker = ResourceLoader.Load<Texture2D>("res://Review/BBB1IExactHud/life_stock_marker_100.png");
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = -80;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		UpdateDamageTrail(_fighterOne, ref _fighterOneTrailLife, ref _fighterOneTrailHold,
			ref _fighterOneLastObservedLife, ref _fighterOneTrailDrainRate, (float)delta);
		UpdateDamageTrail(_fighterTwo, ref _fighterTwoTrailLife, ref _fighterTwoTrailHold,
			ref _fighterTwoLastObservedLife, ref _fighterTwoTrailDrainRate, (float)delta);
		UpdateComboFlash(_fighterOne, ref _fighterOneLastComboCount, ref _fighterOneComboFlashSeconds, (float)delta);
		UpdateComboFlash(_fighterTwo, ref _fighterTwoLastComboCount, ref _fighterTwoComboFlashSeconds, (float)delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 cameraRect = _stageCamera?.CurrentFightBox ?? new Rect2(Vector2.Zero, GetViewportRect().Size);
		float zoom = Mathf.Max(0.01f, _stageCamera?.Zoom.X ?? 1f);
		float y = cameraRect.Position.Y + LifeBarTopOffset / zoom;
		float centerX = cameraRect.GetCenter().X;
		// Preserve Revolve's original vertical scale and center medallion size.
		// Only the long side rails expand on widescreen displays.
		float sideMargin = 72f / zoom;
		float centerWidth = 58f / zoom;
		float sideWidth = Mathf.Max(291f / zoom,
			(cameraRect.Size.X - centerWidth - sideMargin * 2f) * 0.5f);
		float frameLeft = centerX - centerWidth * 0.5f - sideWidth;
		float frameTop = y - 9f / zoom;
		Rect2 frameRect = new(frameLeft, frameTop, sideWidth * 2f + centerWidth, 70f / zoom);
		DrawRevolveLifeFrame(frameRect, sideWidth, centerWidth, zoom);
		Rect2 fighterOneLife = new(frameLeft + 2f / zoom, frameTop + 16f / zoom,
			sideWidth - 7f / zoom, 12f / zoom);
		Rect2 fighterTwoLife = new(frameLeft + sideWidth + centerWidth + 5f / zoom, frameTop + 16f / zoom,
			sideWidth - 7f / zoom, 12f / zoom);
		DrawFighterGauge(_fighterOne, fighterOneLife, false, _fighterOneTrailLife,
			_fighterOneComboFlashSeconds, zoom);
		DrawFighterGauge(_fighterTwo, fighterTwoLife, true, _fighterTwoTrailLife,
			_fighterTwoComboFlashSeconds, zoom);
		DrawTimer(centerX, frameTop, zoom);
		DrawLifeStockMarkers(_stageRules?.FighterOneLifeStocksRemaining ?? 1,
			centerX, frameTop, true, zoom);
		DrawLifeStockMarkers(_stageRules?.FighterTwoLifeStocksRemaining ?? 1,
			centerX, frameTop, false, zoom);
		DrawBPowerGauge(_fighterOne, cameraRect, true, zoom);
		DrawBPowerGauge(_fighterTwo, cameraRect, false, zoom);
	}

	private void DrawFighterGauge(FighterController fighter, Rect2 lifeRect, bool reverse, float trailLife,
		float comboFlashSeconds, float zoom)
	{
		FighterGaugeData data = fighter?.Definition?.Gauges;
		if (data == null) return;
		float portraitWidth = 72f / zoom;
		float portraitHeight = 68f / zoom;
		Rect2 portraitRect = new(!reverse ? lifeRect.Position.X - 54f / zoom : lifeRect.End.X - 18f / zoom,
			lifeRect.Position.Y - 18f / zoom, portraitWidth, portraitHeight);
		Texture2D face = ResolveFaceTexture(fighter);
		if (face != null) DrawTextureRect(face, portraitRect, false);
		float lifeRatio = data.MaxLife <= 0 ? 0f : fighter.PlaceholderLife / data.MaxLife;
		float trailRatio = data.MaxLife <= 0 ? 0f : trailLife / data.MaxLife;
		DrawRevolveLifeFill(lifeRect, trailRatio, lifeRatio, !reverse);
		DrawRevolveNamePlate(fighter.Definition.FighterName, lifeRect, zoom);
		DrawComboCounter(fighter, lifeRect, reverse, trailLife, comboFlashSeconds, zoom);
		if (fighter.UsesSeparateGasMeter)
		{
			float gasWidth = lifeRect.Size.X * 0.42f;
			Rect2 gasRect = new(reverse ? lifeRect.End.X - gasWidth : lifeRect.Position.X,
				lifeRect.Position.Y + 43f / zoom, gasWidth, 7f / zoom);
			DrawBar(gasRect, fighter.PlaceholderGasMeter / Mathf.Max(1f, fighter.PlaceholderMaxGasMeter),
				new Color(1f, 0.58f, 0.12f), reverse, zoom);
			DrawString(ThemeDB.FallbackFont, gasRect.Position + new Vector2(0f, 18f / zoom), "GAS",
				reverse ? HorizontalAlignment.Right : HorizontalAlignment.Left, gasRect.Size.X,
				Mathf.RoundToInt(11f / zoom), new Color(1f, 0.72f, 0.3f));
		}
	}

	private void DrawRevolveLifeFrame(Rect2 frameRect, float sideWidth, float centerWidth, float zoom)
	{
		if (_revolveGaugeSheet == null) return;
		float height = 70f / zoom;
		DrawTextureRectRegion(_revolveGaugeSheet,
			new Rect2(frameRect.Position, new Vector2(sideWidth, height)),
			new Rect2(0f, 0f, 291f, 70f));
		DrawTextureRectRegion(_revolveGaugeSheet,
			new Rect2(frameRect.Position + new Vector2(sideWidth, 0f), new Vector2(centerWidth, height)),
			new Rect2(291f, 0f, 58f, 70f));
		DrawTextureRectRegion(_revolveGaugeSheet,
			new Rect2(frameRect.Position + new Vector2(sideWidth + centerWidth, 0f), new Vector2(sideWidth, height)),
			new Rect2(349f, 0f, 291f, 70f));
	}

	private void DrawRevolveLifeFill(Rect2 rect, float trailRatio, float lifeRatio, bool reverse)
	{
		if (_revolveGaugeSheet == null) return;
		DrawSheetMeterRegion(_revolveGaugeSheet, new Rect2(0f, 143f, 285f, 12f), rect,
			trailRatio, reverse, reverse);
		DrawSheetMeterRegion(_revolveGaugeSheet, new Rect2(1f, 80f, 284f, 11f), rect,
			lifeRatio, reverse, reverse);
	}

	private void DrawRevolveNamePlate(string fighterName, Rect2 lifeRect, float zoom)
	{
		float plateWidth = Mathf.Min(240f / zoom, Mathf.Max(20f / zoom, lifeRect.Size.X - 80f / zoom));
		Rect2 plate = new(lifeRect.Position + new Vector2((lifeRect.Size.X - plateWidth) * 0.5f, 18f / zoom),
			new Vector2(plateWidth, 14f / zoom));
		DrawRect(plate.Grow(2f / zoom), new Color(0.38f, 0.38f, 0.44f), true);
		DrawRect(plate, new Color(0.025f, 0.022f, 0.04f, 0.98f), true);
		DrawString(ThemeDB.FallbackFont, plate.Position + new Vector2(0f, 11f / zoom), fighterName.ToUpperInvariant(),
			HorizontalAlignment.Center, plate.Size.X, Mathf.RoundToInt(10f / zoom), Colors.White);
	}

	private void DrawBPowerGauge(FighterController fighter, Rect2 cameraRect, bool left, float zoom)
	{
		FighterGaugeData data = fighter?.Definition?.Gauges;
		if (data == null || _revolveGaugeSheet == null) return;
		const float exactMaximum = 300f;
		float meterPoints = Mathf.Clamp(fighter.PlaceholderSpecialMeter, 0f, exactMaximum);
		float stockRemainder = Mathf.PosMod(meterPoints, 100f);
		float ratio = meterPoints >= exactMaximum - 0.001f
			? 1f
			: meterPoints > 0f && stockRemainder <= 0.001f ? 1f : stockRemainder / 100f;
		Vector2 frameSize = new(230f / zoom, 26f / zoom);
		Vector2 position = new(left ? cameraRect.Position.X + 10f / zoom : cameraRect.End.X - 240f / zoom,
			cameraRect.End.Y - 31f / zoom);
		Rect2 frameRect = new(position, frameSize);
		Rect2 frameSource = left ? new Rect2(10f, 100f, 230f, 26f) : new Rect2(399f, 100f, 230f, 26f);
		DrawTextureRectRegion(_revolveGaugeSheet, frameRect, frameSource);
		// gauge_in is stored below the frame in the source sheet, but the game
		// draws it inside the frame's dark meter channel. Its width continuously
		// represents the complete 0..300 B-POWER value.
		Rect2 meter = new(position + new Vector2(left ? 39f : 1f, 16f) / zoom,
			new Vector2(190f, 7f) / zoom);
		DrawSheetMeterRegion(_revolveGaugeSheet, new Rect2(49f, 131f, 190f, 7f), meter,
			ratio, left, left);
		if (meterPoints >= exactMaximum - 0.001f)
		{
			float flash = 0.12f + (Mathf.Sin(Time.GetTicksMsec() * 0.012f) + 1f) * 0.16f;
			DrawRect(meter, new Color(1f, 1f, 1f, flash), true);
		}

		float pointsPerStock = exactMaximum / 3f;
		int stocks = Mathf.Clamp(Mathf.FloorToInt(meterPoints / pointsPerStock), 0, 3);
		Rect2 numberRect = new(left ? position.X - 2f / zoom : position.X + frameSize.X - 30f / zoom,
			position.Y - 5f / zoom, 32f / zoom, 32f / zoom);
		DrawRevolvePowerNumber(stocks, numberRect);
	}

	private void DrawHudNumber(int number, Rect2 destination)
	{
		if (_revolveNumberSheet == null)
		{
			DrawString(ThemeDB.FallbackFont, destination.Position + new Vector2(0f, destination.Size.Y * 0.84f), number.ToString(),
				HorizontalAlignment.Center, destination.Size.X, Mathf.RoundToInt(destination.Size.Y), Colors.White);
			return;
		}
		int digit = Mathf.Clamp(number, 0, 9);
		DrawTextureRectRegion(_revolveNumberSheet, destination, new Rect2(digit * 16f, 0f, 16f, 16f));
	}

	private void DrawLifeStockMarkers(int remaining, float centerX, float frameTop, bool left, float zoom)
	{
		if (_lifeStockMarker == null) return;
		Vector2 markerSize = new Vector2(16f, 16f) / zoom;
		for (int index = 0; index < remaining; index++)
		{
			float x = left
				? centerX - (58f + index * 18f) / zoom
				: centerX + (42f + index * 18f) / zoom;
			DrawTextureRect(_lifeStockMarker,
				new Rect2(new Vector2(x, frameTop + 48f / zoom), markerSize), false);
		}
	}

	private void DrawRevolvePowerNumber(int number, Rect2 destination)
	{
		if (_revolveNumberSheet == null) return;
		int digit = Mathf.Clamp(number, 0, 9);
		float sourceX = digit < 8 ? digit * 32f : (digit - 8) * 32f;
		float sourceY = digit < 8 ? 32f : 64f;
		DrawTextureRectRegion(_revolveNumberSheet, destination, new Rect2(sourceX, sourceY, 32f, 32f));
	}

	private void DrawMeterTexture(Texture2D texture, Rect2 rect, float ratio, bool reverse)
	{
		if (texture == null) return;
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		float width = rect.Size.X * ratio;
		float sourceWidth = texture.GetWidth() * ratio;
		if (width <= 0f || sourceWidth <= 0f) return;
		Rect2 source = reverse
			? new Rect2(texture.GetWidth() - sourceWidth, 0f, sourceWidth, texture.GetHeight())
			: new Rect2(0f, 0f, sourceWidth, texture.GetHeight());
		Rect2 destination = reverse
			? new Rect2(new Vector2(rect.End.X - width, rect.Position.Y), new Vector2(width, rect.Size.Y))
			: new Rect2(rect.Position, new Vector2(width, rect.Size.Y));
		DrawTextureRectRegion(texture, destination, source);
	}

	private void DrawSheetMeterRegion(Texture2D texture, Rect2 sourceRect, Rect2 destinationRect,
		float ratio, bool reverse, bool mirrorSource)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		float sourceWidth = sourceRect.Size.X * ratio;
		float destinationWidth = destinationRect.Size.X * ratio;
		if (sourceWidth <= 0f || destinationWidth <= 0f) return;
		Rect2 source = reverse
			? new Rect2(sourceRect.End.X - sourceWidth, sourceRect.Position.Y, sourceWidth, sourceRect.Size.Y)
			: new Rect2(sourceRect.Position, new Vector2(sourceWidth, sourceRect.Size.Y));
		Rect2 destination = reverse
			? new Rect2(destinationRect.End.X - destinationWidth, destinationRect.Position.Y,
				destinationWidth, destinationRect.Size.Y)
			: new Rect2(destinationRect.Position, new Vector2(destinationWidth, destinationRect.Size.Y));
		if (!mirrorSource)
		{
			DrawTextureRectRegion(texture, destination, source);
			return;
		}
		DrawSetTransform(new Vector2(destination.End.X, destination.Position.Y), 0f, new Vector2(-1f, 1f));
		DrawTextureRectRegion(texture, new Rect2(Vector2.Zero, destination.Size), source);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void DrawTexturePossiblyMirrored(Texture2D texture, Rect2 rect, bool mirrored)
	{
		if (texture == null) return;
		if (!mirrored)
		{
			DrawTextureRect(texture, rect, false);
			return;
		}
		DrawSetTransform(new Vector2(rect.End.X, rect.Position.Y), 0f, new Vector2(-1f, 1f));
		DrawTextureRect(texture, new Rect2(Vector2.Zero, rect.Size), false);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private Texture2D ResolveFaceTexture(FighterController fighter)
	{
		string name = fighter?.Definition?.FighterName ?? "";
		if (_faceTextures.TryGetValue(name, out Texture2D cached)) return cached;
		string archive = name switch
		{
			"Kamui" => "_kamui_pct",
			"Mecha Heita" => "_m_heita_pct",
			"Heita" => "_heita_pct",
			"Agito" => "_agito_pct",
			"Daigo" => "_daigo_pct",
			"Kinako" => "_kinako_pct",
			"Kunagi" => "_kunagi_pct",
			"Rouga" => "_rouga_pct",
			"Senna" => "_senna_pct",
			"Sanzou Kongoumaru" => "_sanzou_pct",
			_ => ""
		};
		Texture2D texture = string.IsNullOrEmpty(archive) ? null :
			ResourceLoader.Load<Texture2D>($"res://Extraction/BigBangBeatRevolve/{archive}/face.png");
		_faceTextures[name] = texture;
		return texture;
	}

	private void DrawComboCounter(FighterController fighter, Rect2 lifeRect, bool reverse, float trailLife,
		float flashSeconds, float zoom)
	{
		if (fighter.ComboCount < 2 || fighter.ComboDisplayFramesLeft <= 0) return;
		int beats = Mathf.Clamp(fighter.ComboCount, 0, 99);
		int firstDigit = beats >= 10 ? beats / 10 : -1;
		int secondDigit = beats % 10;
		float digitGap = 2f / zoom;
		float digitsWidth = GetComboDigitSource(secondDigit, false).Size.X / zoom;
		if (firstDigit >= 0)
			digitsWidth += GetComboDigitSource(firstDigit, false).Size.X / zoom + digitGap;
		float beatsWidth = 64f / zoom;
		float totalWidth = digitsWidth + beatsWidth;
		float x = reverse ? lifeRect.End.X - totalWidth - 30f / zoom : lifeRect.Position.X + 30f / zoom;
		float y = lifeRect.Position.Y + 68f / zoom;
		Vector2 position = new(x, y);
		bool flashing = flashSeconds > 0f;
		if (firstDigit >= 0)
		{
			position.X += DrawRevolveComboDigit(firstDigit, position, flashing, zoom) + digitGap;
		}
		position.X += DrawRevolveComboDigit(secondDigit, position, flashing, zoom);
		DrawTextureRectRegion(_revolveNumberSheet,
			new Rect2(position + new Vector2(0f, 7f / zoom), new Vector2(beatsWidth, 32f / zoom)),
			new Rect2(64f, 64f, 64f, 32f));

		int comboDamage = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, trailLife - fighter.PlaceholderLife)), 0, 999);
		DrawSmallRevolveNumber(comboDamage,
			position + new Vector2(beatsWidth + 4f / zoom, 25f / zoom), zoom);
	}

	private static readonly int[] ComboNormalX = { 4, 57, 98, 149, 196, 4, 52, 101, 148, 195 };
	private static readonly int[] ComboNormalWidth = { 42, 30, 44, 41, 42, 42, 43, 36, 42, 43 };
	private static readonly int[] ComboFlashX = { 2, 55, 96, 147, 194, 2, 50, 99, 146, 193 };
	private static readonly int[] ComboFlashY = { 249, 250, 249, 249, 250, 314, 314, 314, 314, 314 };
	private static readonly int[] ComboFlashWidth = { 45, 33, 47, 44, 45, 45, 46, 39, 45, 46 };
	private static readonly int[] ComboFlashHeight = { 48, 46, 47, 48, 46, 47, 47, 46, 47, 47 };

	private static Rect2 GetComboDigitSource(int digit, bool flashing)
	{
		digit = Mathf.Clamp(digit, 0, 9);
		if (flashing)
			return new Rect2(ComboFlashX[digit], ComboFlashY[digit],
				ComboFlashWidth[digit], ComboFlashHeight[digit]);
		return new Rect2(ComboNormalX[digit], digit < 5 ? 123f : 187f,
			ComboNormalWidth[digit], 43f);
	}

	private float DrawRevolveComboDigit(int digit, Vector2 position, bool flashing, float zoom)
	{
		if (_revolveNumberSheet == null) return 0f;
		Rect2 normalSource = GetComboDigitSource(digit, false);
		Rect2 source = GetComboDigitSource(digit, flashing);
		Vector2 size = source.Size / zoom;
		Vector2 drawPosition = position + new Vector2(
			(normalSource.Size.X - source.Size.X) * 0.5f / zoom,
			(43f - source.Size.Y) * 0.5f / zoom);
		DrawTextureRectRegion(_revolveNumberSheet, new Rect2(drawPosition, size), source);
		return normalSource.Size.X / zoom;
	}

	private void DrawSmallRevolveNumber(int number, Vector2 position, float zoom)
	{
		string text = number.ToString();
		Vector2 digitSize = new Vector2(12f, 12f) / zoom;
		for (int index = 0; index < text.Length; index++)
		{
			int digit = text[index] - '0';
			DrawTextureRectRegion(_revolveNumberSheet,
				new Rect2(position + new Vector2(index * digitSize.X, 0f), digitSize),
				new Rect2(digit * 16f, 0f, 16f, 16f));
		}
	}

	private static void UpdateComboFlash(FighterController fighter, ref int lastComboCount,
		ref float flashSeconds, float delta)
	{
		if (fighter == null) return;
		if (fighter.ComboCount > lastComboCount)
			flashSeconds = 0.12f;
		else
			flashSeconds = Mathf.Max(0f, flashSeconds - delta);
		lastComboCount = fighter.ComboCount;
	}

	private void DrawTimer(float centerX, float frameTop, float zoom)
	{
		int seconds = Mathf.CeilToInt(_stageRules?.RoundSecondsRemaining ?? 99f);
		Vector2 center = new(centerX, frameTop + 30f / zoom);
		if (_stageRules?.IsKoActive == true)
		{
			DrawString(ThemeDB.FallbackFont, center + new Vector2(-24f, 6f) / zoom, "KO",
				HorizontalAlignment.Center, 48f / zoom, Mathf.RoundToInt(16f / zoom), Colors.White);
			return;
		}
		seconds = Mathf.Clamp(seconds, 0, 99);
		Vector2 digitSize = new Vector2(16f, 16f) / zoom;
		Vector2 start = center - new Vector2(digitSize.X, digitSize.Y * 0.5f);
		DrawHudNumber(seconds / 10, new Rect2(start, digitSize));
		DrawHudNumber(seconds % 10, new Rect2(start + new Vector2(digitSize.X, 0f), digitSize));
	}

	private void UpdateDamageTrail(FighterController fighter, ref float trailLife, ref float hold,
		ref float lastObservedLife, ref float drainRate, float delta)
	{
		if (fighter == null) return;
		float life = fighter.PlaceholderLife;
		if (life < lastObservedLife - 0.001f)
		{
			// The first hit owns one delayed-red segment for the entire combo.
			// Further hits move only green; a genuinely new combo retires any old
			// pending trail and begins again at that combo's pre-hit life.
			if (fighter.ComboCount <= 1)
				trailLife = lastObservedLife;
			hold = DamageTrailHoldSeconds;
			drainRate = 0f;
		}
		else if (life > lastObservedLife + 0.001f)
		{
			trailLife = life;
			drainRate = 0f;
		}
		lastObservedLife = life;
		if (fighter.HitstunFramesLeft > 0)
		{
			hold = DamageTrailHoldSeconds;
			drainRate = 0f;
			return;
		}
		if (hold > 0f)
		{
			hold = Mathf.Max(0f, hold - delta);
			return;
		}
		if (trailLife > life)
		{
			if (drainRate <= 0f)
				drainRate = Mathf.Max(DamageTrailDrainPerSecond,
					(trailLife - life) / Mathf.Max(0.01f, DamageTrailDrainSeconds));
			trailLife = Mathf.MoveToward(trailLife, life, drainRate * delta);
		}
		else
			drainRate = 0f;
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

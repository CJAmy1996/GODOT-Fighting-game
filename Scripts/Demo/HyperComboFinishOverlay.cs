using Godot;

namespace ModularFighter.Demo;

/// <summary>MVC-style finish: explosion, tunnel, arena dissolve, then foreground kanji impact.</summary>
public partial class HyperComboFinishOverlay : Node
{
	[Signal] public delegate void TunnelEndedEventHandler();
	[Export(PropertyHint.Range, "1.0,8.0,0.1")] public float MinimumPresentationSeconds { get; set; } = 4f;
	private const string NormalFramesPath = "res://Assets/Effects/BigBangCommon/hyper_combo_finish_frames.tres";
	private const string Level3FramesPath = "res://Assets/Effects/BigBangCommon/hyper_combo_finish_level3_frames.tres";
	private const string KanjiPath = "res://Assets/Effects/BigBangCommon/hyper_combo_finish_kanji.png";
	private const string AnnouncerPath = "res://Audio/Sound Effects/(BIG BANG FINISH)PS2.DAT_00882.wav";
	private static readonly StringName ExplosionAnimation = "hyper_combo_finish_explosion";
	private static readonly StringName TunnelAnimation = "hyper_combo_finish_activation";
	private const float CrossfadeTicks = 3f;
	private const float ArenaFadeTicks = 18f;
	private const float KanjiSlamTicks = 9f;
	private const float KanjiHoldTicks = 30f;
	private const float KanjiFadeTicks = 12f;
	private const float WhiteFlashTicks = 7f;

	private enum FinishPhase
	{
		Explosion,
		Crossfade447,
		Tunnel,
		ReturnToArena,
		KanjiSlam,
		KanjiFade,
		Complete
	}

	private SpriteFrames _frames;
	private CanvasLayer _backgroundLayer;
	private CanvasLayer _foregroundLayer;
	private TextureRect _backgroundDisplay;
	private TextureRect _crossfadeDisplay;
	private TextureRect _kanjiDisplay;
	private ColorRect _whiteFlash;
	private AudioStreamPlayer _announcer;
	private CanvasItem _arenaBackdrop;
	private StageCamera _fightCamera;
	private Color _arenaOriginalModulate = Colors.White;
	private FinishPhase _phase;
	private int _frame;
	private float _ticksLeft;
	private float _phaseTicks;
	private bool _outroRequested;
	private bool _tunnelCycleComplete;
	private float _elapsedRealSeconds;
	private float _tunnelElapsedRealSeconds;
	private bool _musicDucked;

	public bool IsFinished { get; private set; }
	public bool UseLevel3Palette { get; set; }
	public bool PlayAnnouncerVoice { get; set; } = true;

	public void SetArenaBackdrop(CanvasItem backdrop)
	{
		_arenaBackdrop = backdrop;
		if (_arenaBackdrop == null) return;
		_arenaOriginalModulate = _arenaBackdrop.Modulate;
		_arenaBackdrop.Visible = true;
		SetArenaAlpha(0f);
	}

	public void RequestOutro() => _outroRequested = true;
	public void SetFightCamera(StageCamera camera) => _fightCamera = camera;

	public void StartNormalKoImpact()
	{
		if (_backgroundLayer == null || _kanjiDisplay == null) return;
		_announcer?.Stop();
		RestoreMusic();
		SetArenaAlpha(1f);
		_backgroundLayer.Visible = false;
		BeginKanjiImpact();
	}

	public override void _Ready()
	{
		_frames = ResourceLoader.Load<SpriteFrames>(UseLevel3Palette ? Level3FramesPath : NormalFramesPath);
		_announcer = new AudioStreamPlayer
		{
			Name = "HyperComboFinishAnnouncer",
			Stream = ResourceLoader.Load<AudioStream>(AnnouncerPath),
			VolumeDb = 3.0f
		};
		AddChild(_announcer);
		_backgroundLayer = new CanvasLayer { Name = "HyperComboFinishBackground", Layer = -1 };
		_foregroundLayer = new CanvasLayer { Name = "HyperComboFinishForeground", Layer = 150 };
		AddChild(_backgroundLayer);
		AddChild(_foregroundLayer);

		_backgroundDisplay = CreateFullscreenDisplay("FinishBackground");
		_crossfadeDisplay = CreateFullscreenDisplay("FinishCrossfade447");
		_crossfadeDisplay.Modulate = new Color(1f, 1f, 1f, 0f);
		_backgroundLayer.AddChild(_backgroundDisplay);
		_backgroundLayer.AddChild(_crossfadeDisplay);

		_whiteFlash = new ColorRect
		{
			Name = "FinishWhiteFlash",
			Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_whiteFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_foregroundLayer.AddChild(_whiteFlash);

		_kanjiDisplay = CreateFullscreenDisplay("FinishKanji");
		_kanjiDisplay.Texture = ResourceLoader.Load<Texture2D>(KanjiPath);
		_kanjiDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_kanjiDisplay.Visible = false;
		_kanjiDisplay.Modulate = new Color(1f, 1f, 1f, 0f);
		_foregroundLayer.AddChild(_kanjiDisplay);
		CallDeferred(MethodName.SetKanjiPivot);

		if (UseLevel3Palette)
		{
			ShowBackgroundFrame(ExplosionAnimation, 0);
		}
		else
		{
			BeginTunnel();
		}
	}

	public override void _Process(double delta)
	{
		if (_frames == null || IsFinished) return;
		// The match may enter dramatic slow motion, but the finish background is
		// a real-time presentation and must still complete in a predictable four seconds.
		float realDelta = (float)delta / Mathf.Max(0.01f, (float)Engine.TimeScale);
		_elapsedRealSeconds += realDelta;
		float ticks = realDelta * 60f;
		switch (_phase)
		{
			case FinishPhase.Explosion:
				AdvanceExplosion(ticks);
				break;
			case FinishPhase.Crossfade447:
				Advance447Crossfade(ticks);
				break;
			case FinishPhase.Tunnel:
				AdvanceTunnel(ticks);
				break;
			case FinishPhase.ReturnToArena:
				AdvanceArenaFade(ticks);
				break;
			case FinishPhase.KanjiSlam:
				AdvanceKanjiSlam(ticks);
				break;
			case FinishPhase.KanjiFade:
				AdvanceKanjiFade(ticks);
				break;
		}
	}

	private void AdvanceExplosion(float ticks)
	{
		_ticksLeft -= ticks;
		int crossfadeFrame = Mathf.Max(0, _frames.GetFrameCount(ExplosionAnimation) - 1);
		while (_ticksLeft <= 0f && _frame + 1 < crossfadeFrame)
		{
			_frame++;
			ShowBackgroundFrame(ExplosionAnimation, _frame);
		}
		if (_ticksLeft > 0f) return;

		_crossfadeDisplay.Texture = _frames.GetFrameTexture(ExplosionAnimation, crossfadeFrame);
		_phase = FinishPhase.Crossfade447;
		_phaseTicks = 0f;
	}

	private void Advance447Crossfade(float ticks)
	{
		_phaseTicks += ticks;
		float amount = Mathf.Clamp(_phaseTicks / CrossfadeTicks, 0f, 1f);
		_crossfadeDisplay.Modulate = new Color(1f, 1f, 1f, amount);
		if (amount < 1f) return;

		_backgroundDisplay.Texture = _crossfadeDisplay.Texture;
		_crossfadeDisplay.Modulate = new Color(1f, 1f, 1f, 0f);
		BeginTunnel();
	}

	private void BeginTunnel()
	{
		_phase = FinishPhase.Tunnel;
		_frame = 0;
		_ticksLeft = 0f;
		_tunnelElapsedRealSeconds = 0f;
		_tunnelCycleComplete = false;
		ShowBackgroundFrame(TunnelAnimation, 0);
		if (!PlayAnnouncerVoice || _announcer?.Stream == null) return;
		_announcer.Play();
		GetNodeOrNull<Node>("/root/AudioController")?.Call("duck_music_for_hyper_finish");
		_musicDucked = true;
	}

	private void AdvanceTunnel(float ticks)
	{
		_tunnelElapsedRealSeconds += ticks / 60f;
		_ticksLeft -= ticks;
		while (_ticksLeft <= 0f)
		{
			if (_frame + 1 < _frames.GetFrameCount(TunnelAnimation))
			{
				_frame++;
				ShowBackgroundFrame(TunnelAnimation, _frame);
				continue;
			}

			_tunnelCycleComplete = true;
			_frame = 0;
			ShowBackgroundFrame(TunnelAnimation, _frame);
		}
		float soundSeconds = _announcer?.Stream?.GetLength() > 0.0
			? (float)_announcer.Stream.GetLength()
			: Mathf.Max(0.1f, MinimumPresentationSeconds);
		if (_tunnelElapsedRealSeconds < soundSeconds) return;
		_announcer?.Stop();
		RestoreMusic();
		EmitSignal(SignalName.TunnelEnded);
		_phase = FinishPhase.ReturnToArena;
		_phaseTicks = 0f;
	}

	private void AdvanceArenaFade(float ticks)
	{
		_phaseTicks += ticks;
		float amount = Mathf.Clamp(_phaseTicks / ArenaFadeTicks, 0f, 1f);
		_backgroundDisplay.Modulate = new Color(1f, 1f, 1f, 1f - amount);
		_crossfadeDisplay.Modulate = new Color(1f, 1f, 1f, 0f);
		SetArenaAlpha(amount);
		if (amount < 1f) return;

		_backgroundLayer.Visible = false;
		BeginKanjiImpact();
	}

	private void BeginKanjiImpact()
	{
		_whiteFlash.Visible = true;
		_whiteFlash.Modulate = Colors.White;
		_kanjiDisplay.Visible = true;
		_fightCamera?.ShakeSuper(16f, 14);
		_phase = FinishPhase.KanjiSlam;
		_phaseTicks = 0f;
	}

	private void AdvanceKanjiSlam(float ticks)
	{
		_phaseTicks += ticks;
		float flashAlpha = 1f - Mathf.Clamp(_phaseTicks / WhiteFlashTicks, 0f, 1f);
		_whiteFlash.Modulate = new Color(1f, 1f, 1f, flashAlpha);
		if (flashAlpha <= 0f) _whiteFlash.Visible = false;
		float slam = Mathf.Clamp(_phaseTicks / KanjiSlamTicks, 0f, 1f);
		float eased = 1f - Mathf.Pow(1f - slam, 3f);
		float scale = Mathf.Lerp(2.75f, 1f, eased);
		_kanjiDisplay.Scale = Vector2.One * scale;
		_kanjiDisplay.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(slam * 2f, 0f, 1f));
		if (_phaseTicks < KanjiSlamTicks + KanjiHoldTicks) return;
		_phase = FinishPhase.KanjiFade;
		_phaseTicks = 0f;
	}

	private void AdvanceKanjiFade(float ticks)
	{
		_phaseTicks += ticks;
		float amount = Mathf.Clamp(_phaseTicks / KanjiFadeTicks, 0f, 1f);
		_kanjiDisplay.Modulate = new Color(1f, 1f, 1f, 1f - amount);
		if (amount < 1f) return;

		_kanjiDisplay.Visible = false;
		_phase = FinishPhase.Complete;
		IsFinished = true;
	}

	private void ShowBackgroundFrame(StringName animation, int frame)
	{
		_backgroundDisplay.Texture = _frames.GetFrameTexture(animation, frame);
		_ticksLeft += Mathf.Max(1f, _frames.GetFrameDuration(animation, frame));
	}

	private static TextureRect CreateFullscreenDisplay(string name)
	{
		var display = new TextureRect
		{
			Name = name,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale
		};
		display.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return display;
	}

	private void SetKanjiPivot()
	{
		if (_kanjiDisplay != null)
			_kanjiDisplay.PivotOffset = _kanjiDisplay.Size * 0.5f;
	}

	private void SetArenaAlpha(float alpha)
	{
		if (_arenaBackdrop == null) return;
		Color color = _arenaOriginalModulate;
		color.A *= Mathf.Clamp(alpha, 0f, 1f);
		_arenaBackdrop.Modulate = color;
	}

	private void RestoreMusic()
	{
		if (!_musicDucked) return;
		GetNodeOrNull<Node>("/root/AudioController")?.Call("restore_music_after_hyper_finish");
		_musicDucked = false;
	}

	public override void _ExitTree() => RestoreMusic();
}

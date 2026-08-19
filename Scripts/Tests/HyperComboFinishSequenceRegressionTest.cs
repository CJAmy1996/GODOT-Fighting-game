using System;
using Godot;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

public partial class HyperComboFinishSequenceRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			SpriteFrames normalFrames = ResourceLoader.Load<SpriteFrames>(
				"res://Assets/Effects/BigBangCommon/hyper_combo_finish_frames.tres");
			SpriteFrames level3Frames = ResourceLoader.Load<SpriteFrames>(
				"res://Assets/Effects/BigBangCommon/hyper_combo_finish_level3_frames.tres");
			Expect(normalFrames.HasAnimation("hyper_combo_finish_activation") &&
				!normalFrames.HasAnimation("hyper_combo_finish_explosion"),
				"normal hyper finish still contains the Level 3-only explosion");
			Expect(level3Frames.HasAnimation("hyper_combo_finish_activation") &&
				level3Frames.HasAnimation("hyper_combo_finish_explosion"),
				"Level 3 hyper finish lost its explosion sequence");

			var arena = new ColorRect { Name = "ArenaBackdrop", Modulate = new Color(0.8f, 0.9f, 1f, 0.75f) };
			AddChild(arena);
			var finish = new HyperComboFinishOverlay { Name = "Finish", UseLevel3Palette = true };
			finish.SetArenaBackdrop(arena);
			AddChild(finish);
			finish.SetProcess(false);
			AudioStreamPlayer announcer = finish.GetNode<AudioStreamPlayer>("HyperComboFinishAnnouncer");
			Expect(announcer.Stream != null, "hyper-combo finish announcer voice was not loaded");
			Expect(Mathf.IsZeroApprox(arena.Modulate.A), "arena was not hidden beneath the finish background");

			for (int tick = 0; tick < 50; tick++) finish._Process(1.0 / 60.0);
			Expect(!finish.IsFinished, "finish ended instead of looping its tunnel while the super remained active");
			finish.RequestOutro();
			for (int tick = 0; tick < 189; tick++) finish._Process(1.0 / 60.0);
			Expect(!finish.IsFinished, "finish ignored its guaranteed four-second presentation");
			for (int tick = 0; tick < 180 && !finish.IsFinished; tick++) finish._Process(1.0 / 60.0);

			TextureRect kanji = finish.GetNode<TextureRect>(
				"HyperComboFinishForeground/FinishKanji");
			ColorRect flash = finish.GetNode<ColorRect>(
				"HyperComboFinishForeground/FinishWhiteFlash");
			Expect(finish.IsFinished && !kanji.Visible && kanji.Texture != null &&
				Mathf.IsZeroApprox(kanji.Modulate.A),
				"kanji did not fade completely before making the win animation ready");
			Expect(!flash.Visible && Mathf.IsZeroApprox(flash.Modulate.A),
				"kanji impact white flash did not dissipate");
			Expect(Mathf.IsEqualApprox(arena.Modulate.A, 0.75f),
				"normal arena background did not finish fading back in");
			GD.Print("HYPER_COMBO_FINISH_SEQUENCE_TEST_PASS 440-446>crossfade447>tunnel-loop>arena-fade>white-flash+shake+kanji-slam>kanji-fade>win-ready");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"HYPER_COMBO_FINISH_SEQUENCE_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

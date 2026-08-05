using System;
using Godot;

namespace ModularFighter.Tests;

/// <summary>Verifies that Sanzou's airborne SPD cape loop is game-ready.</summary>
public partial class SanzouSpdCapeAnimationRegressionTest : Node
{
	private static readonly StringName AnimationName = "spd_air_grab";

	public override void _Ready()
	{
		try
		{
			SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(
				"res://Assets/TestFighter/Sanzo/sanzo_sprite_frames.tres");
			Expect(frames != null, "Sanzou SpriteFrames resource did not load");
			Expect(frames.HasAnimation(AnimationName), "spd_air_grab animation is missing");
			Expect(frames.GetFrameCount(AnimationName) == 4, "cape loop does not contain four drawings");
			Expect(frames.GetAnimationLoop(AnimationName), "cape animation is not configured to loop");
			Expect(Mathf.IsEqualApprox((float)frames.GetAnimationSpeed(AnimationName), 60f),
				"cape animation is not evaluated on the 60 Hz timeline");

			for (int drawing = 0; drawing < 4; drawing++)
			{
				Texture2D texture = frames.GetFrameTexture(AnimationName, drawing);
				Expect(texture != null, $"cape drawing {drawing} has no texture");
				Expect(texture.GetSize() == new Vector2(320f, 384f),
					$"cape drawing {drawing} is {texture.GetSize()} instead of 320x384");
				Expect(Mathf.IsEqualApprox((float)frames.GetFrameDuration(AnimationName, drawing), 4f),
					$"cape drawing {drawing} does not last four simulation ticks");
			}

			GD.Print("SANZOU SPD CAPE TEST PASSED: four cape-only drawings load at 320x384 on the 60 Hz timeline.");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"SANZOU SPD CAPE TEST FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}

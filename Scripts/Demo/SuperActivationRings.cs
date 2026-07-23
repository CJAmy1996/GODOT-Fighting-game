using Godot;

namespace ModularFighter.Demo;

/// <summary>Foreground energy and portrait-container strokes for a super activation cut-in.</summary>
public partial class SuperActivationRings : Node2D
{
	public Sprite2D PortraitSprite { get; set; }
	public Vector2 FocusPosition { get; set; }
	public Rect2 CameraRect { get; set; }
	public bool PortraitEntersFromLeft { get; set; }
	public int LifetimeFrames { get; set; } = 1;
	public int FramesElapsed { get; set; }

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (PortraitSprite == null) return;
		float progress = Mathf.Clamp(FramesElapsed / (float)Mathf.Max(1, LifetimeFrames), 0f, 1f);
		float pulse = 0.72f + Mathf.Sin(progress * Mathf.Pi * 5f) * 0.2f;
		Color blue = new Color(0.18f, 0.7f, 1f, pulse);
		Color white = new Color(1f, 1f, 1f, Mathf.Clamp(pulse + 0.14f, 0f, 1f));

		// The activation wave collapses toward the fighter in front of the portrait.
		float startRadius = Mathf.Max(CameraRect.Size.X, CameraRect.Size.Y) * 0.48f;
		float radius = Mathf.Lerp(startRadius, 42f, 1f - Mathf.Pow(1f - progress, 2.4f));
		DrawArc(FocusPosition, radius * 1.12f, 0f, Mathf.Tau, 128, new Color(0.35f, 0.86f, 1f, pulse * 0.5f), 18f, true);
		DrawArc(FocusPosition, radius, 0f, Mathf.Tau, 128, blue, 12f, true);
		DrawArc(FocusPosition, radius * 0.82f, 0f, Mathf.Tau, 128, white, 3.5f, true);

		// MVC1-style portrait panel: the circle is deliberately much taller than
		// the camera. Only its inward-facing curved edge can be seen; there is no
		// visible top or bottom edge that reads as a separate circular badge.
		Vector2 portraitCenter = PortraitSprite.Position;
		float panelCenterX = portraitCenter.X + (PortraitEntersFromLeft ? -CameraRect.Size.X * 0.25f : CameraRect.Size.X * 0.25f);
		Vector2 panelCenter = new(panelCenterX, CameraRect.GetCenter().Y);
		float panelRadius = Mathf.Max(CameraRect.Size.Y * 0.72f, CameraRect.Size.X * 0.48f);
		float startAngle = PortraitEntersFromLeft ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f;
		float endAngle = startAngle + Mathf.Pi;
		DrawArc(panelCenter, panelRadius + 4f, startAngle, endAngle, 96, Colors.Black, 24f, true);
		DrawArc(panelCenter, panelRadius - 7f, startAngle, endAngle, 96, new Color(0.78f, 0.82f, 0.88f, 0.95f), 5f, true);
	}
}

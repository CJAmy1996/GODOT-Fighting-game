#if TOOLS
using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Editor;

[Tool]
public partial class KungFuManBoxPreview : Control
{
	private const float SpriteOffsetY = -60f;
	private SpriteFrames _spriteFrames;
	private StringName _animation = "idle";
	private int _frame;
	private int _spriteFrame;
	private NormalMoveData _move;
	private int _selectedBox = -1;
	private bool _drawing;
	private bool _dragging;
	private Vector2 _dragStart;
	private Vector2 _dragEnd;
	private float _scale = 1f;
	private Vector2 _origin;

	public event Action<Rect2> BoxDrawn;

	public KungFuManBoxPreview()
	{
		CustomMinimumSize = new Vector2(360f, 260f);
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
	}

	public void SetSpriteFrames(SpriteFrames spriteFrames)
	{
		_spriteFrames = spriteFrames;
		QueueRedraw();
	}

	public void SetAnimation(StringName animation)
	{
		_animation = animation;
		QueueRedraw();
	}

	public void SetFrame(int frame, int spriteFrame = -1)
	{
		_frame = Mathf.Max(0, frame);
		_spriteFrame = spriteFrame < 0 ? _frame : Mathf.Max(0, spriteFrame);
		QueueRedraw();
	}

	public void SetMove(NormalMoveData move)
	{
		_move = move;
		_selectedBox = -1;
		QueueRedraw();
	}

	public void SetSelectedBox(int index)
	{
		_selectedBox = index;
		QueueRedraw();
	}

	public void SetDrawing(bool enabled)
	{
		_drawing = enabled;
		if (!enabled) _dragging = false;
		MouseDefaultCursorShape = enabled ? CursorShape.Cross : CursorShape.Arrow;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (!_drawing) return;
		if (inputEvent is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
		{
			if (mouse.Pressed)
			{
				_dragging = true;
				_dragStart = mouse.Position;
				_dragEnd = mouse.Position;
			}
			else if (_dragging)
			{
				_dragEnd = mouse.Position;
				_dragging = false;
				Rect2 rect = MakeRect(ScreenToFighter(_dragStart), ScreenToFighter(_dragEnd));
				if (rect.Size.X >= 2f && rect.Size.Y >= 2f) BoxDrawn?.Invoke(rect);
			}
			AcceptEvent();
			QueueRedraw();
		}
		else if (inputEvent is InputEventMouseMotion motion && _dragging)
		{
			_dragEnd = motion.Position;
			AcceptEvent();
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.055f, 0.065f, 0.08f), true);
		_scale = Mathf.Max(0.5f, Mathf.Min((Size.X - 24f) / 210f, (Size.Y - 34f) / 150f));
		_origin = new Vector2(Size.X * 0.5f, Size.Y - 18f);
		DrawLine(new Vector2(8f, _origin.Y), new Vector2(Size.X - 8f, _origin.Y), new Color(0.45f, 0.5f, 0.58f), 1f);
		DrawLine(_origin + new Vector2(-5f, 0f), _origin + new Vector2(5f, 0f), Colors.White, 2f);

		Texture2D texture = GetCurrentTexture();
		if (texture != null)
		{
			Vector2 textureSize = texture.GetSize();
			Vector2 localTopLeft = new(-textureSize.X * 0.5f, SpriteOffsetY - textureSize.Y * 0.5f);
			Rect2 destination = new(FighterToScreen(localTopLeft), textureSize * _scale);
			DrawTextureRect(texture, destination, false);
		}

		if (_move?.BoxTimeline != null)
		{
			for (int i = 0; i < _move.BoxTimeline.Length; i++)
			{
				FighterBoxFrame box = _move.BoxTimeline[i];
				if (box == null || (!box.IsActiveOnFrame(_frame) && i != _selectedBox)) continue;
				DrawCombatRect(box.LocalRect, box.Kind, i == _selectedBox, box.IsActiveOnFrame(_frame));
			}
		}

		if (_dragging)
		{
			Rect2 dragRect = MakeRect(_dragStart, _dragEnd);
			DrawRect(dragRect, new Color(1f, 0.3f, 0.18f, 0.18f), true);
			DrawRect(dragRect, new Color(1f, 0.72f, 0.18f), false, 2f);
		}
	}

	private Texture2D GetCurrentTexture()
	{
		if (_spriteFrames == null || !_spriteFrames.HasAnimation(_animation)) return null;
		int count = _spriteFrames.GetFrameCount(_animation);
		return count > 0 ? _spriteFrames.GetFrameTexture(_animation, Mathf.Clamp(_spriteFrame, 0, count - 1)) : null;
	}

	private void DrawCombatRect(Rect2 localRect, FighterBoxKind kind, bool selected, bool active)
	{
		Color color = KindColor(kind);
		if (!active) color.A = 0.3f;
		Rect2 screenRect = new(FighterToScreen(localRect.Position), localRect.Size * _scale);
		DrawRect(screenRect, color with { A = active ? 0.2f : 0.08f }, true);
		DrawRect(screenRect, selected ? Colors.White : color, false, selected ? 3f : 2f);
	}

	private Vector2 FighterToScreen(Vector2 point) => _origin + point * _scale;
	private Vector2 ScreenToFighter(Vector2 point) => (point - _origin) / _scale;

	private static Rect2 MakeRect(Vector2 a, Vector2 b) => new(
		new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)),
		new Vector2(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y)));

	private static Color KindColor(FighterBoxKind kind) => kind switch
	{
		FighterBoxKind.Hitbox => new Color(1f, 0.2f, 0.12f, 0.95f),
		FighterBoxKind.Hurtbox => new Color(0.1f, 0.68f, 1f, 0.95f),
		FighterBoxKind.Pushbox => new Color(0.2f, 1f, 0.4f, 0.95f),
		FighterBoxKind.Throwbox => new Color(1f, 0.78f, 0.12f, 0.95f),
		FighterBoxKind.ThrowHurtbox => new Color(0.72f, 0.45f, 1f, 0.95f),
		FighterBoxKind.Clashbox => new Color(1f, 0.3f, 0.75f, 0.95f),
		FighterBoxKind.ThrowVictimAnchor => new Color(0.2f, 1f, 0.95f, 0.95f),
		_ => new Color(0.2f, 1f, 0.9f, 0.95f)
	};
}
#endif

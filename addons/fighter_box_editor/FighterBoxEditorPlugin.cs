#if TOOLS
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Editor;

[Tool]
public partial class FighterBoxEditorPlugin : EditorPlugin
{
	private VBoxContainer _dock;
	private Button _drawButton;
	private OptionButton _kind;
	private SpinBox _startFrame;
	private SpinBox _endFrame;
	private LineEdit _tag;
	private EditorResourcePicker _attackPicker;
	private Label _status;
	private bool _dragging;
	private Vector2 _dragStartScreen;
	private Vector2 _dragEndScreen;
	private CollisionShape2D _lastShape;

	public override void _EnterTree()
	{
		BuildDock();
		AddControlToDock(DockSlot.RightUl, _dock);
	}

	public override void _ExitTree()
	{
		if (_dock != null)
		{
			RemoveControlFromDocks(_dock);
			_dock.QueueFree();
		}
	}

	public override bool _ForwardCanvasGuiInput(InputEvent inputEvent)
	{
		if (_drawButton?.ButtonPressed != true) return false;
		if (inputEvent is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
		{
			_dragging = false;
			_drawButton.ButtonPressed = false;
			_status.Text = "Drawing cancelled.";
			UpdateOverlays();
			return true;
		}
		if (inputEvent is InputEventMouseMotion motion && _dragging)
		{
			_dragEndScreen = motion.Position;
			UpdateOverlays();
			return true;
		}
		if (inputEvent is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left) return _dragging;

		if (mouse.Pressed)
		{
			if (GetSelectedParent() == null)
			{
				_status.Text = "Select a Node2D in the scene first.";
				return true;
			}
			_dragging = true;
			_dragStartScreen = mouse.Position;
			_dragEndScreen = mouse.Position;
			UpdateOverlays();
			return true;
		}

		if (!_dragging) return false;
		_dragEndScreen = mouse.Position;
		_dragging = false;
		CreateShapeFromDrag();
		UpdateOverlays();
		return true;
	}

	public override void _ForwardCanvasDrawOverViewport(Control overlay)
	{
		if (!_dragging) return;
		Rect2 rect = MakeRect(_dragStartScreen, _dragEndScreen);
		Color color = SelectedKind == FighterBoxKind.Hitbox
			? new Color(1f, 0.15f, 0.1f, 0.22f)
			: new Color(0f, 0.75f, 1f, 0.22f);
		overlay.DrawRect(rect, color, true);
		overlay.DrawRect(rect, color with { A = 0.95f }, false, 2f);
	}

	private void BuildDock()
	{
		_dock = new VBoxContainer { Name = "Fighter Boxes" };
		_dock.AddChild(new Label { Text = "Fighter Box Painter" });
		_dock.AddChild(new Label { Text = "Select a Node2D, enable Draw, then drag in the 2D viewport." });

		_kind = new OptionButton();
		foreach (string name in System.Enum.GetNames<FighterBoxKind>()) _kind.AddItem(name);
		_kind.Select((int)FighterBoxKind.Hitbox);
		AddLabeledControl("Kind", _kind);

		_startFrame = MakeFrameSpinBox(0);
		_endFrame = MakeFrameSpinBox(-1);
		AddLabeledControl("Start frame", _startFrame);
		AddLabeledControl("End frame (-1 = forever)", _endFrame);

		_tag = new LineEdit { PlaceholderText = "jab, head, torso..." };
		AddLabeledControl("Tag", _tag);

		_drawButton = new Button { Text = "Draw rectangle", ToggleMode = true };
		_drawButton.Toggled += enabled => _status.Text = enabled
			? "Drag a rectangle in the 2D viewport. Esc cancels."
			: "Drawing disabled.";
		_dock.AddChild(_drawButton);

		_dock.AddChild(new HSeparator());
		_attackPicker = new EditorResourcePicker { BaseType = nameof(NormalMoveData) };
		AddLabeledControl("Attack resource", _attackPicker);
		var addButton = new Button { Text = "Add selected/drawn shape to attack" };
		addButton.Pressed += AddShapeToAttack;
		_dock.AddChild(addButton);

		_status = new Label { Text = "Ready.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_dock.AddChild(_status);
	}

	private void AddLabeledControl(string label, Control control)
	{
		_dock.AddChild(new Label { Text = label });
		_dock.AddChild(control);
	}

	private static SpinBox MakeFrameSpinBox(double value) => new()
	{
		MinValue = -1,
		MaxValue = 999,
		Step = 1,
		Value = value,
		AllowGreater = true
	};

	private FighterBoxKind SelectedKind => (FighterBoxKind)_kind.Selected;

	private Node2D GetSelectedParent()
	{
		Godot.Collections.Array<Node> selected = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
		return selected.Count > 0 ? selected[0] as Node2D : null;
	}

	private void CreateShapeFromDrag()
	{
		Node2D parent = GetSelectedParent();
		if (parent == null) return;
		SubViewport viewport = EditorInterface.Singleton.GetEditorViewport2D();
		Transform2D screenToWorld = viewport.GetCanvasTransform().AffineInverse();
		Vector2 worldA = screenToWorld * _dragStartScreen;
		Vector2 worldB = screenToWorld * _dragEndScreen;
		Vector2 localA = parent.ToLocal(worldA);
		Vector2 localB = parent.ToLocal(worldB);
		Rect2 localRect = MakeRect(localA, localB);
		if (localRect.Size.X < 2f || localRect.Size.Y < 2f)
		{
			_status.Text = "Box was too small; drag a larger rectangle.";
			return;
		}

		var shape = new CollisionShape2D
		{
			Name = $"{SelectedKind}_{_startFrame.Value:0}_{_endFrame.Value:0}",
			Position = localRect.GetCenter(),
			Shape = new RectangleShape2D { Size = localRect.Size }
		};
		shape.SetMeta("fighter_box_kind", (int)SelectedKind);
		shape.SetMeta("fighter_box_start_frame", (int)_startFrame.Value);
		shape.SetMeta("fighter_box_end_frame", (int)_endFrame.Value);
		shape.SetMeta("fighter_box_tag", _tag.Text);

		EditorUndoRedoManager undo = GetUndoRedo();
		undo.CreateAction("Draw fighter combat box");
		undo.AddDoMethod(parent, Node.MethodName.AddChild, shape, true);
		undo.AddDoProperty(shape, Node.PropertyName.Owner, EditorInterface.Singleton.GetEditedSceneRoot());
		undo.AddUndoMethod(parent, Node.MethodName.RemoveChild, shape);
		undo.CommitAction();
		_lastShape = shape;
		EditorInterface.Singleton.GetSelection().Clear();
		EditorInterface.Singleton.GetSelection().AddNode(shape);
		_status.Text = $"Created {SelectedKind} {localRect.Size.Round()} on {parent.Name}.";
	}

	private void AddShapeToAttack()
	{
		NormalMoveData attack = _attackPicker.EditedResource as NormalMoveData;
		CollisionShape2D shape = EditorInterface.Singleton.GetSelection().GetSelectedNodes().Count > 0
			? EditorInterface.Singleton.GetSelection().GetSelectedNodes()[0] as CollisionShape2D
			: _lastShape;
		if (attack == null)
		{
			_status.Text = "Choose a NormalMoveData attack resource first.";
			return;
		}
		if (shape?.Shape == null)
		{
			_status.Text = "Select a CollisionShape2D or draw a box first.";
			return;
		}

		attack.AddBox(shape, SelectedKind, (int)_startFrame.Value, (int)_endFrame.Value, true, _tag.Text);
		attack.EmitChanged();
		if (!string.IsNullOrEmpty(attack.ResourcePath)) ResourceSaver.Save(attack, attack.ResourcePath);
		_status.Text = $"Added {SelectedKind} to {attack.AttackName} ({attack.BoxTimeline.Length} timeline boxes).";
	}

	private static Rect2 MakeRect(Vector2 a, Vector2 b)
	{
		Vector2 position = new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
		return new Rect2(position, new Vector2(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y)));
	}
}
#endif

using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Pause overlay containing all training and debug controls.</summary>
public partial class TrainingBlockControls : Control
{
	[Export] public NodePath FighterOnePath { get; set; }
	[Export] public NodePath OpponentPath { get; set; }
	[Export] public NodePath StageRulesPath { get; set; }
	[Export] public NodePath CombatHudPath { get; set; }

	private FighterController _fighterOne;
	private FighterController _opponent;
	private VersusStageRules _stageRules;
	private CanvasItem _combatHud;
	private CheckButton _airBlockToggle;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_fighterOne = GetNodeOrNull<FighterController>(FighterOnePath);
		_opponent = GetNodeOrNull<FighterController>(OpponentPath);
		_stageRules = GetNodeOrNull<VersusStageRules>(StageRulesPath);
		_combatHud = GetNodeOrNull<CanvasItem>(CombatHudPath);
		BuildMenu();
		Visible = false;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		ToggleMenu();
		GetViewport().SetInputAsHandled();
	}

	private void ToggleMenu()
	{
		Visible = !Visible;
		GetTree().Paused = Visible;
	}

	private void BuildMenu()
	{
		var shade = new ColorRect { Color = new Color(0.01f, 0.015f, 0.03f, 0.78f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(570f, 560f) };
		center.AddChild(panel);
		var content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		panel.AddChild(content);

		var title = new Label { Text = "TRAINING PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 28);
		content.AddChild(title);
		content.AddChild(new Label { Text = "Press Esc to resume", HorizontalAlignment = HorizontalAlignment.Center });
		content.AddChild(new HSeparator());

		AddToggle(content, "Allow health to empty / KO", _stageRules?.AllowHealthToReachZero ?? false,
			enabled => { if (_stageRules != null) _stageRules.AllowHealthToReachZero = enabled; });
		CheckButton block = AddToggle(content, "Opponent auto-block", _opponent?.TrainingAutoBlock ?? false, enabled =>
		{
			if (_opponent != null) _opponent.TrainingAutoBlock = enabled;
			UpdateAirToggleAvailability(enabled);
		});
		_airBlockToggle = AddToggle(content, "Opponent air-block", _opponent?.TrainingAirBlock ?? false,
			enabled => { if (_opponent != null) _opponent.TrainingAirBlock = enabled; });
		UpdateAirToggleAvailability(block.ButtonPressed);

		content.AddChild(new HSeparator());
		AddToggle(content, "Show combat HUD", _combatHud?.Visible ?? true,
			enabled => { if (_combatHud != null) _combatHud.Visible = enabled; });
		AddToggle(content, "Show fighter collision boxes", false, enabled =>
		{
			if (_fighterOne != null) _fighterOne.DebugDrawCombatBoxes = enabled;
			if (_opponent != null) _opponent.DebugDrawCombatBoxes = enabled;
		});

		content.AddChild(new HSeparator());
		var controls = new Label
		{
			Text = "CONTROLS\nA / D: Move    W: Jump    S then W: Super jump\nU / J / I / K: Attacks    QCF + U/I: Projectile\nQCF + U+I: Super 1    QCF + J+K: Super 2\nSanzou: O = SPD    L = Parry    Super 1 = Mega SPD",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(controls);
	}

	private static CheckButton AddToggle(VBoxContainer parent, string text, bool initial, System.Action<bool> changed)
	{
		var toggle = new CheckButton { Text = text, ButtonPressed = initial };
		toggle.Toggled += enabled => changed(enabled);
		parent.AddChild(toggle);
		return toggle;
	}

	private void UpdateAirToggleAvailability(bool blockEnabled)
	{
		if (_airBlockToggle == null) return;
		_airBlockToggle.Disabled = !blockEnabled;
		if (!blockEnabled && _airBlockToggle.ButtonPressed) _airBlockToggle.ButtonPressed = false;
	}
}

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
	private CheckButton _dummyCrouchToggle;
	private CheckButton _dummyJumpToggle;
	private CheckButton _dummySuperJumpToggle;
	private CheckButton _dummyLightPunchToggle;
	private DummyAction _dummyAction;
	private int _superJumpCommandStep;
	private bool _updatingDummyToggles;

	private enum DummyAction
	{
		None,
		Crouch,
		Jump,
		SuperJump,
		LightPunch
	}

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

	public override void _Process(double delta)
	{
		if (NativeInputRouter.GetUiFrame().WasPressed(NativeInputButtons.Pause)) ToggleMenu();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_opponent == null || _opponent.ReadLocalInput) return;
		FighterInput input = default;
		switch (_dummyAction)
		{
			case DummyAction.Crouch:
				input = new FighterInput(0f, 1f, false, false, false, false);
				break;
			case DummyAction.Jump:
				bool jumpNow = _opponent.WasGrounded;
				// Keep jump held for the full airtime. Releasing it immediately after
				// takeoff intentionally invokes the variable-height short-hop cut.
				input = new FighterInput(0f, jumpNow ? -1f : 0f, jumpNow, true, false, false);
				break;
			case DummyAction.SuperJump:
				if (!_opponent.WasGrounded)
				{
					_superJumpCommandStep = 0;
					break;
				}
				if (_superJumpCommandStep == 0)
				{
					input = new FighterInput(0f, 1f, false, false, false, false);
					_superJumpCommandStep = 1;
				}
				else
				{
					input = new FighterInput(0f, -1f, true, true, false, false);
					_superJumpCommandStep = 2;
				}
				break;
			case DummyAction.LightPunch:
				if (!_opponent.IsAttacking && _opponent.HitstunFramesLeft <= 0)
					input = new FighterInput(0f, 0f, false, false, false, false, lightPunchPressed: true);
				break;
		}
		_opponent.SetExternalInput(input);
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
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(570f, 680f) };
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
		AddToggle(content, "Enable instant block (auto-block dummy perfects)",
			(_fighterOne?.InstantBlockEnabled ?? false) || (_opponent?.InstantBlockEnabled ?? false), enabled =>
			{
				if (_fighterOne != null) _fighterOne.InstantBlockEnabled = enabled;
				if (_opponent != null) _opponent.InstantBlockEnabled = enabled;
			});
		_dummyCrouchToggle = AddToggle(content, "Dummy crouch", false,
			enabled => SetDummyAction(DummyAction.Crouch, enabled));
		_dummyJumpToggle = AddToggle(content, "Dummy jump", false,
			enabled => SetDummyAction(DummyAction.Jump, enabled));
		_dummySuperJumpToggle = AddToggle(content, "Dummy super jump", false,
			enabled => SetDummyAction(DummyAction.SuperJump, enabled));
		_dummyLightPunchToggle = AddToggle(content, "Dummy light punch", false,
			enabled => SetDummyAction(DummyAction.LightPunch, enabled));

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

	private void SetDummyAction(DummyAction action, bool enabled)
	{
		if (_updatingDummyToggles) return;
		_dummyAction = enabled ? action : _dummyAction == action ? DummyAction.None : _dummyAction;
		_superJumpCommandStep = 0;
		_updatingDummyToggles = true;
		if (_dummyCrouchToggle != null) _dummyCrouchToggle.ButtonPressed = _dummyAction == DummyAction.Crouch;
		if (_dummyJumpToggle != null) _dummyJumpToggle.ButtonPressed = _dummyAction == DummyAction.Jump;
		if (_dummySuperJumpToggle != null) _dummySuperJumpToggle.ButtonPressed = _dummyAction == DummyAction.SuperJump;
		if (_dummyLightPunchToggle != null) _dummyLightPunchToggle.ButtonPressed = _dummyAction == DummyAction.LightPunch;
		_updatingDummyToggles = false;
		if (_dummyAction == DummyAction.None && _opponent != null)
			_opponent.SetExternalInput(default);
	}
}

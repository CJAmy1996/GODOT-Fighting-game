using Godot;
using ModularFighter.Core;

namespace ModularFighter.Demo;

/// <summary>Test-arena toggles for forcing the dummy into grounded and airborne blockstun.</summary>
public partial class TrainingBlockControls : VBoxContainer
{
	[Export] public NodePath OpponentPath { get; set; }

	private FighterController _opponent;
	private CheckButton _blockToggle;
	private CheckButton _airBlockToggle;

	public override void _Ready()
	{
		_opponent = GetNodeOrNull<FighterController>(OpponentPath);
		var title = new Label { Text = "TRAINING GUARD" };
		title.AddThemeFontSizeOverride("font_size", 17);
		AddChild(title);

		_blockToggle = new CheckButton { Text = "Opponent Blocking: OFF" };
		_blockToggle.Toggled += enabled =>
		{
			if (_opponent != null) _opponent.TrainingAutoBlock = enabled;
			_blockToggle.Text = $"Opponent Blocking: {(enabled ? "ON" : "OFF")}";
			UpdateAirToggleAvailability();
		};
		AddChild(_blockToggle);

		_airBlockToggle = new CheckButton { Text = "Opponent Air Blocking: OFF" };
		_airBlockToggle.Toggled += enabled =>
		{
			if (_opponent != null) _opponent.TrainingAirBlock = enabled;
			_airBlockToggle.Text = $"Opponent Air Blocking: {(enabled ? "ON" : "OFF")}";
		};
		AddChild(_airBlockToggle);
		UpdateAirToggleAvailability();
	}

	private void UpdateAirToggleAvailability()
	{
		if (_airBlockToggle == null) return;
		_airBlockToggle.Disabled = !_blockToggle.ButtonPressed;
		_airBlockToggle.TooltipText = _airBlockToggle.Disabled
			? "Turn on Opponent Blocking first."
			: "When enabled, the dummy also blocks while airborne.";
	}
}

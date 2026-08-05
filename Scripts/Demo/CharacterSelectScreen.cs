using Godot;

namespace ModularFighter.Demo;

/// <summary>Small keyboard/mouse roster screen used before entering the test arena.</summary>
public partial class CharacterSelectScreen : Control
{
	private static readonly string[] CharacterNames = { "KUNG FU MAN", "SANZOU KONGOUMARU" };
	private int _selectedIndex;
	private Label _choiceLabel;

	public override void _Ready()
	{
		_choiceLabel = GetNode<Label>("Center/Panel/Layout/Choice");
		GetNode<Button>("Center/Panel/Layout/KungFuMan").Pressed += () => Confirm(0);
		GetNode<Button>("Center/Panel/Layout/Sanzou").Pressed += () => Confirm(1);
		RefreshChoice();
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (input.IsActionPressed("ui_left") || input.IsActionPressed("move_left"))
		{
			_selectedIndex = 0;
			RefreshChoice();
			GetViewport().SetInputAsHandled();
		}
		else if (input.IsActionPressed("ui_right") || input.IsActionPressed("move_right"))
		{
			_selectedIndex = 1;
			RefreshChoice();
			GetViewport().SetInputAsHandled();
		}
		else if (input.IsActionPressed("ui_accept"))
		{
			Confirm(_selectedIndex);
			GetViewport().SetInputAsHandled();
		}
	}

	private void RefreshChoice()
	{
		_choiceLabel.Text = $"<  {CharacterNames[_selectedIndex]}  >";
	}

	private void Confirm(int index)
	{
		ArenaCharacterLoader.SelectedCharacter = index == 1
			? ArenaCharacterLoader.CharacterChoice.Sanzou
			: ArenaCharacterLoader.CharacterChoice.KungFuMan;
		GetTree().ChangeSceneToFile("res://TestArena.tscn");
	}
}

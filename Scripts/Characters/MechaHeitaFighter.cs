using Godot;
using ModularFighter.Demo;

namespace ModularFighter.Characters;

/// <summary>Mecha Heita-specific presentation hooks.</summary>
public partial class MechaHeitaFighter : SpriteTestFighter
{
	private const string ElectricityName = "MECHA ELECTRICITY";
	private Sprite2D _electricityGreyBody;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateElectricityGreyBodyFlash();
	}

	protected override void OnCharacterAttackActiveFrame()
	{
		if (CurrentAttackName == ElectricityName)
			GetNodeOrNull<Node>("/root/AudioController")?.Call("play_electricity");
	}

	private void UpdateElectricityGreyBodyFlash()
	{
		if (CharacterSprite == null) return;
		bool electricityActive = CurrentAttackName == ElectricityName && IsAttackActive &&
			CurrentAttackActiveLoopAnimationName == "anim_149";
		if (!electricityActive)
		{
			if (_electricityGreyBody != null) _electricityGreyBody.Visible = false;
			return;
		}
		if (_electricityGreyBody == null)
		{
			Texture2D greyBody = CharacterSprite.SpriteFrames?.HasAnimation("anim_148") == true &&
				CharacterSprite.SpriteFrames.GetFrameCount("anim_148") > 4
				? CharacterSprite.SpriteFrames.GetFrameTexture("anim_148", 4)
				: null;
			if (greyBody == null) return;
			_electricityGreyBody = new Sprite2D
			{
				Name = "ElectricityGreyBody",
				Texture = greyBody,
				Centered = true,
				ZIndex = 1
			};
			CharacterSprite.AddChild(_electricityGreyBody);
		}
		_electricityGreyBody.FlipH = CharacterSprite.FlipH;
		_electricityGreyBody.Visible = Mathf.PosMod(CurrentAttackFrame - CurrentAttackStartupFrames, 3) != 2;
	}
}

namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterThrowbox : FighterBoxFrame
{
	public FighterThrowbox()
	{
		Kind = FighterBoxKind.Throwbox;
		Attributes = FighterBoxAttribute.Throw;
		InteractsWith = FighterBoxAttribute.Throw;
		AttackLevel = FighterAttackLevel.Any;
		ReceivesHits = true;
	}
}

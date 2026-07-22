namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterProximityBlockbox : FighterBoxFrame
{
	public FighterProximityBlockbox()
	{
		Kind = FighterBoxKind.ProximityBlockbox;
		Attributes = FighterBoxAttribute.Proximity;
		InteractsWith = FighterBoxAttribute.Proximity;
		AttackLevel = FighterAttackLevel.Any;
		ReceivesHits = true;
	}
}

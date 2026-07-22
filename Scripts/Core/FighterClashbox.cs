namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterClashbox : FighterBoxFrame
{
	public FighterClashbox()
	{
		Kind = FighterBoxKind.Clashbox;
		Attributes = FighterBoxAttribute.Strike | FighterBoxAttribute.Projectile;
		InteractsWith = FighterBoxAttribute.Strike | FighterBoxAttribute.Projectile;
		AttackLevel = FighterAttackLevel.Any;
		ReceivesHits = true;
		CanClash = true;
	}
}

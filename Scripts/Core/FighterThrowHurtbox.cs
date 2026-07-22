namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterThrowHurtbox : FighterBoxFrame
{
	public FighterThrowHurtbox()
	{
		Kind = FighterBoxKind.ThrowHurtbox;
		MirrorWithFacing = false;
		Attributes = FighterBoxAttribute.Throw;
		InteractsWith = FighterBoxAttribute.Throw;
		AttackLevel = FighterAttackLevel.Any;
		ReceivesHits = true;
	}
}

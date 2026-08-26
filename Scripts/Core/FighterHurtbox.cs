namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterHurtbox : FighterCollisionBox
{
	public FighterHurtbox()
	{
		Kind = FighterBoxKind.Hurtbox;
		CollisionLayer = 1u << 5;
		CollisionMask = 1u << 4;
	}
}

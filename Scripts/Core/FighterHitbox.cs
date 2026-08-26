namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterHitbox : FighterCollisionBox
{
	public FighterHitbox()
	{
		Kind = FighterBoxKind.Hitbox;
		CollisionLayer = 1u << 4;
		CollisionMask = 1u << 5;
	}
}

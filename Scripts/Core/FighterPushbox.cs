namespace ModularFighter.Core;

[Godot.GlobalClass]
public partial class FighterPushbox : FighterCollisionBox
{
	public FighterPushbox()
	{
		Kind = FighterBoxKind.Pushbox;
		CollisionLayer = 1u << 6;
		CollisionMask = 1u << 6;
	}
}

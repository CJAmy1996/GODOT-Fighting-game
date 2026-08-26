using Godot;
using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Characters;

public partial class KamuiFighter : SpriteTestFighter
{
	private const string SpecialTraitAttackName = "KAMUI S2 ROCK ATTACK";
	[Export] public bool SwordInHand { get; private set; } = true;

	public void SetSwordInHand(bool value) => SwordInHand = value;

	protected override bool CanUseCharacterMove(NormalMoveData move) =>
		move is not SpecialMoveData { RequiresSwordInHand: true } || SwordInHand;

	protected override string ResolveCharacterSpecificAttack(FighterInput input)
	{
		if (input.Special2Pressed && WasGrounded &&
			Definition?.SpecialMoves?.FindMove(SpecialTraitAttackName, false, false) != null)
			return SpecialTraitAttackName;
		return "";
	}

	protected override void OnCharacterAttackStarted(string attackName)
	{
		base.OnCharacterAttackStarted(attackName);
		if (attackName is "TRAIT 1" or "SWORDLESS STANCE") SwordInHand = false;
		else if (attackName == "SWORD RECALL") SwordInHand = true;
	}
}

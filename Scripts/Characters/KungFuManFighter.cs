using ModularFighter.Core;
using ModularFighter.Demo;

namespace ModularFighter.Characters;

/// <summary>Owns the prototype Kung Fu Man command quirks; none leak into universal routing.</summary>
public partial class KungFuManFighter : SpriteTestFighter
{
	private const string LightProjectile = "LIGHT PROJECTILE";
	private const string HeavyProjectile = "HEAVY PROJECTILE";
	private const string PowerPunchLight = "QCF POWER PUNCH LIGHT";
	private const string PowerPunchHeavy = "QCF POWER PUNCH HEAVY";
	private const string PowerPunchRekka = "QCF POWER PUNCH REKKA";
	private const string SuperRush = "SUPER RUSH";
	private const string SuperFireball = "SUPER FIREBALL";
	private const int SuperChordGraceFrames = 2;

	protected override string ResolveCharacterSpecificAttack(FighterInput input)
	{
		if (IsAttacking && CurrentAttackName is LightProjectile or HeavyProjectile or
			PowerPunchLight or PowerPunchHeavy &&
			(CurrentInput.LightPunchPressed || CurrentInput.HeavyPunchPressed))
			return PowerPunchRekka;

		if (HasQuarterCircleForwardCommand)
		{
			if (input.LightPunchPressed && input.HeavyPunchPressed && IsOnFloor()) return SuperRush;
			if (input.LightKickPressed && input.HeavyKickPressed) return SuperFireball;
		}

		// Kung Fu Man alone owns the down-forward LP low launcher.
		if (input.LightPunchPressed && WasGrounded && input.Vertical > 0.5f && input.Horizontal * Facing > 0.5f)
			return CrouchingMediumJabName;

		if (input.LightPunchPressed && CanUseMotionSpecialCommand())
			return Definition?.SpecialMoves?.FindMove(PowerPunchLight, false, false) != null
				? PowerPunchLight : LightProjectile;
		if (input.HeavyPunchPressed && CanUseMotionSpecialCommand())
			return Definition?.SpecialMoves?.FindMove(PowerPunchHeavy, false, false) != null
				? PowerPunchHeavy : HeavyProjectile;
		return "";
	}

	protected override bool ShouldDeferCharacterAttackResolution(FighterInput input) =>
		HasQuarterCircleForwardCommand && QuarterCircleForwardCommandAgeFrames < SuperChordGraceFrames &&
		(input.LightPunchPressed || input.HeavyPunchPressed || input.LightKickPressed || input.HeavyKickPressed);

	protected override bool IsCharacterSpecialAttack(string attackName) =>
		attackName is PowerPunchLight or PowerPunchHeavy or PowerPunchRekka;
	protected override bool IsCharacterProjectileAttack(string attackName) =>
		attackName is LightProjectile or HeavyProjectile or SuperFireball;
	protected override bool IsCharacterSuperAttack(string attackName) =>
		attackName is SuperRush or SuperFireball;
}

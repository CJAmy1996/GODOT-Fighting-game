# Basic Character Template Guide

This project is now ready for a simple character-template workflow, but not yet a full move-editor workflow.

The safest rule is:

- Designers tune character data, boxes, movement abilities, and exported numbers.
- Programmers add new ability classes only when a character needs a genuinely new archetype.
- Avoid adding character-specific `if` statements inside `FighterController`.

## Current recommended character package

For each playable character, create or maintain these pieces:

1. Character scene
   - Example today: `Scripts/Demo/StickFigureFighter.cs` attached to a fighter node.
   - Future target: one `.tscn` per character.

2. Fighter definition
   - Contains the character name.
   - Points to `MovementTuning`.
   - Holds the list of movement abilities: jumps, run, backdash, airdash, super jump, special movement.

3. Movement tuning
   - Walk speed, back walk speed, gravity, air action rules, friction, input buffer.

4. Ability list
   - `JumpAbility` for neutral jump, forward jump, backward jump, and optional air jump.
   - `SuperJumpAbility` for Marvel-style down-up super jump.
   - `RunAbility` for forward run.
   - `DashAbility` for backdash, airdash, special dashes.

5. Combat boxes
   - Pushbox.
   - Airborne pushbox.
   - Hurtbox.
   - Position box / crossup tracker.
   - Per-button hitboxes.

6. Attack feel values
   - Startup, active, recovery.
   - Hitstun.
   - Hitstop.
   - Pushback.
   - Shake.
   - Air-combo pop/spike behavior.

7. Normal move set
   - `NormalMoveSet` contains the character's combo/cancel rules.
   - `NormalMoveData` defines rules for one exact normal or a broad group such as `LIGHT`, `HEAVY`, `SPECIAL`, or `ANY`.
   - Rule order matters: put specific rules before broad category rules.

## Designer-safe files

These are safe to tune for character feel:

- `Scripts/Core/MovementTuning.cs`
- `Scripts/Movement/JumpAbility.cs`
- `Scripts/Movement/SuperJumpAbility.cs`
- `Scripts/Movement/RunAbility.cs`
- `Scripts/Movement/DashAbility.cs`
- `Scripts/Core/NormalMoveSet.cs`
- `Scripts/Core/NormalMoveData.cs`
- character scene / character definition assets once we move the baseline out of code
- exported values on `FighterController`

## Be careful files

These are core rules. Touch only when changing the whole game:

- `Scripts/Core/FighterController.cs`
- `Scripts/Demo/VersusStageRules.cs`
- `Scripts/Demo/StageCamera.cs`

## Per-character combo rules

Combo rules should be character data, not engine code.

Use `NormalMoveSet` for things like:

- light chains
- air chains
- exact move-to-move chain targets
- how many times a move can be used in one chain/combo route
- whether a normal can cancel into heavy
- whether a normal can cancel into special
- when the cancel window opens and closes
- whether chains require hit/block contact
- whether a crouching heavy punch launches
- whether a launcher is jump-cancelable
- damage, hitstun, blockstun, hitstop, pushback, knockdown data
- hit reaction states such as normal hitstun, tumble, and knockdown
- combo gravity scaling through fighter tuning

Example: a strict Slayer-like character can simply have fewer `NormalMoveData` chain flags enabled.

Example: a Marvel-like character can have generous air `ANY` rules with light/heavy/special chains enabled.

If a character should not cancel a particular normal, make a specific `NormalMoveData` rule for that normal and leave its cancel flags off.

If a character should have an exact route, fill `AllowedChainTargets`, for example:

- `LIGHT KICK`
- `HEAVY PUNCH`
- `SPECIAL 1`

If `AllowedChainTargets` is filled, it overrides the broad light/heavy/special chain flags.

## Hit reactions and combo gravity

The controller currently supports these runtime hit states:

- `Hitstun`
- `CounterHit`
- `Tumble`
- `Knockdown`
- `GroundedKnockdown`
- `WallBounce`
- `GroundBounce`
- `Crumple`

The controller also tracks a separate `KnockdownType`:

- `None`
- `Sweep`
- `AirKnockdown`
- `HardKnockdown`
- `SoftKnockdown`
- `WallBounce`
- `GroundBounce`
- `Crumple`

Counter hit is automatic when a character is hit during an attack.

Normal/tumble combo hitstun recovers when the defender touches the ground. `Knockdown` is the airborne falling state; when that character touches the floor it becomes `GroundedKnockdown`, the lying-down state. Sweep-style grounded knockdowns enter `GroundedKnockdown` immediately. `KnockdownType` decides the subtype/recovery family, while `FighterHitState` says what the body is doing right now.

`GroundedKnockdown` is invincible by default. A move must set `CanHitGroundedKnockdown` to true to behave like an OTG move.

Every continued hit in a combo increases the defender's gravity during that combo only. This makes long juggles naturally fall out unless the attacker has routes designed to keep them going.

## Current test special move

The prototype includes one command special for cancel testing:

- Input: quarter-circle-forward + `Special 1`
- Move: `ELECTRIC WIND GOD FIST`
- Behavior: moderate launcher, heavy/electric hit spark

Lights currently include `ELECTRIC WIND GOD FIST` in their allowed chain targets so light-normal cancel tests are available immediately.

## Add a new ability class when

Create a new `MovementAbility` subclass only when the existing dials cannot express the character.

Good reasons:

- air walk
- flight
- teleport
- wall cling
- puppet swap movement
- clone-command movement
- unique dash type with different physics ownership

Bad reasons:

- one character has a higher jump
- one character has faster run
- one character has more airdash recovery
- one character has a different backdash speed

Those should be data values, not new code.

## Current limitations

The current template is good enough for a basic playable character prototype.

It is not yet enough for final character production because we still need:

- per-frame hitbox/hurtbox tracks
- real move data assets
- blockstun/blockstop
- throws
- knockdown/wakeup
- hitbox editor
- animation-driven boxes
- frame-step training/debug tools

## Suggested next engineering step

Move the current `CreateBaselineDefinition()` data out of `StickFigureFighter.cs` and into reusable character assets:

- `Characters/Templates/BasicFighter.tscn`
- `Characters/Templates/BasicFighterDefinition.tres`
- `Characters/Templates/BasicMovementTuning.tres`
- `Characters/Templates/BasicNormalMoveSet.tres`
- `Characters/Templates/Normals/*.tres`

That will let a designer duplicate a folder instead of editing C#.

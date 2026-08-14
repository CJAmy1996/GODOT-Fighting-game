# Modular Godot C# Fighter Movement Framework

This is a Godot 4 C# starting point for a fast, responsive 2D fighter whose characters can have radically different movement without modifying shared controller code.

## The design

`FighterController` owns universal simulation: input sampling, velocity, gravity, floor collision, air resources, and the fixed-tick movement loop. It knows nothing about double jumps, flight, clones, or puppets.

`FighterDefinition` is a per-character Resource. It owns a `MovementTuning` Resource and a list of `MovementAbility` Resources. A character becomes unique by combining and configuring abilities, not by adding conditional statements to the controller.

```text
FighterDefinition
 ├─ MovementTuning          (speed, acceleration, gravity, forgiveness)
 └─ MovementAbility[]
	 ├─ JumpAbility          (air jumps and variable jump)
	 ├─ DashAbility          (ground, air, omnidirectional, charges)
	 ├─ FlightAbility
	 ├─ AirWalkAbility
	 └─ YourNewAbility       (a separate class; core stays untouched)
```

Resources are definitions and must be stateless. Every live timer, charge, and counter is stored in `FighterController.Runtime`, keyed by ability ID. This prevents one fighter's resource state leaking into another fighter using the same asset.

## Install in Godot

1. Open this folder in Godot 4 with the .NET/C# build installed. Godot will generate the solution/project files.
2. Create a `CharacterBody2D` scene with collision shape, sprite, and the `FighterController` script.
3. Create `MovementTuning` and `FighterDefinition` resources in the Inspector; assign them to the controller.
4. Create ability resources and add them to the definition's `Abilities` array. Give every ability a unique `Id`.
5. Gameplay input is polled by `NativeInputRouter`. The Windows defaults are A/D/W/S, Shift, U/J/I/K,
   O/L, Escape, Enter, and Tab; XInput pads use the d-pad/stick, X/A/Y/B, shoulders, triggers, Start,
   and Back. Godot InputMap is only the fallback on unsupported platforms.

## Native input and rollback

`NativeInputRouter` samples Win32 keyboard and XInput state once per 60 Hz simulation frame. It stores
720 immutable `NativeInputFrame` packets per player, and the fighter consumes only the cached packet for
the current simulation frame. This prevents different gameplay systems from polling at different times.

For replays or rollback networking, transport `NativeInputFrame.NetworkWord`, submit received packets
with `SubmitNetworkWord`, call `InvalidateAfter` when a prediction changes, then resimulate from stored
frames. `MotionInputDefinition` and `MotionInputBinding` consume the resulting `FighterInput`, so local,
remote, and replay inputs all recognize the same motions.

## Recommended character recipes

| Archetype | Ability setup |
|---|---|
| Standard fighter | `JumpAbility(MaxAirJumps=0)`, a ground `DashAbility`, and an air `DashAbility(MaxAirUses=1)` |
| High-mobility fighter | More air jumps, fast low-gravity tuning, omnidirectional `DashAbility(AimWithStick=true)` |
| Flyer | `FlightAbility`; optionally add air dash with a higher priority so dash can interrupt flight |
| Air walker | `AirWalkAbility` using the flight input. It is hover/ground-style steering, not a fake platform. |
| Puppet fighter | Two FighterController bodies under `PuppetCoordinator`; each body gets its own fighter definition and hitboxes. |
| Clone fighter | Spawn a second FighterController scene and give it `CloneCommandRelay`; swap in delayed or scripted input whenever desired. |

## Adding an archetype

Create a `MovementAbility` subclass. Its only job is to answer `CanStart`, set its initial velocity in `Start`, and update itself in `Tick`. Set `OwnsHorizontalVelocity` and/or `OwnsGravity` only when it must replace the shared rules. Its mutable state belongs in the provided `AbilityRuntime`.

Do **not** add `if (fighter is AirWalker)` or `if (definition.Name == ...)` branches to `FighterController`. An archetype is a new ability, entity coordinator, or data configuration.

## Tuning for the requested feel

For sharp Alpha-style response, favor high ground acceleration/deceleration and 2–4 frames of coyote/jump buffer. For the slight Guilty Gear-like float, lower gravity moderately, preserve air momentum during selected actions, and give air steering enough acceleration to matter. Test one normal character first; treat them as the baseline all unusual fighters are allowed to violate.

## Next systems to add

Build attacks as another data-driven layer (`AttackDefinition` + frame events) rather than placing them in movement abilities. Have hitstun/blockstun temporarily deny ability activation through a combat-state gate. The input side is rollback-ready; full online rollback still needs fighter/world state snapshots, correction, and deterministic resimulation orchestration.

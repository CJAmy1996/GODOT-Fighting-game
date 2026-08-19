# FighterController refactor plan

## Objective

Reduce `FighterController` from a 4,689-line combat monolith into a stable Godot-facing
orchestrator without changing game behavior. The target is approximately 1,800–2,500 lines,
with deterministic rules in stateless resolvers and state transitions in narrowly-owned
runtime components.

This is an incremental extraction project, not a rewrite.

## Current baseline

Already extracted:

- `FacingResolver`: neutral/action facing policy and cross-up/cross-under decisions.
- `ChainResolver`: normal-chain and special-cancel decision order.
- `HitResolver`: deterministic hitstun, pushback, blockstun, juggle, and reaction-selection policy.

Still concentrated in `FighterController`:

- Hit-reaction state mutation.
- Attack lifecycle and move runtime state.
- Input buffering and command-to-action coordination.
- Jump, flight, and landing route state.
- Velocity and movement resolution.
- Collision-box queries.
- Throws and super sequences.
- Animation and effect presentation requests.
- 167 exported tuning properties.

## Non-negotiable constraints

1. Do not intentionally rebalance gameplay during an extraction.
2. Preserve rule precedence, frame counters, and mutation order exactly.
3. Do not add character-name checks to new core components.
4. Do not create multiple components that can independently mutate the same state.
5. `FighterController` remains the authoritative fighter and simulation orchestrator.
6. Each extraction must compile and pass focused regressions before the next begins.
7. Existing unrelated worktree changes must not be rewritten or reverted.
8. Avoid serialized-property migrations until runtime behavior is stable.

## Component ownership model

| Component | Owns | Must not own |
|---|---|---|
| `FighterController` | Godot lifecycle, authoritative identity/state, simulation order, component coordination | Detailed combat policy |
| Resolvers | Pure deterministic decisions and calculations | Fighter mutation, nodes, effects, logging |
| Runtime controllers | One cohesive state machine and its counters | Unrelated fighter state or character-specific routing |
| Abilities/move data | Character and move-specific behavior | Global combat rules |
| Presentation controller | Animation/effect requests and visual state | Combat outcomes |

## Phase 0: strengthen the safety net

### Goal

Make failures identify rule regressions instead of unreliable scene positioning.

### Work

- Keep pure resolver tests for facing, chains, and hit resolution.
- Add deterministic tests for hit-reaction transitions without requiring hitbox overlap.
- Separate collision-contact tests from combat-policy tests.
- Record known failing integration tests and whether they fail before reaching their assertion target.
- Create a small documented smoke-test list that must pass after every extraction.

### Exit criteria

- Every component scheduled for extraction has at least one focused regression.
- Tests can directly exercise state transitions without depending on character sprite placement.
- Known unrelated failures are documented and do not obscure new failures.

## Phase 1: extract HitReactionController

### Goal

Complete the boundary started by `HitResolver`: the resolver chooses a reaction and the reaction
controller applies it.

### Move

- Hitstun and blockstun application.
- Launch, air-pop, spike, and juggle transitions.
- Blow-away, stumble, hit-fall, and special reactions.
- Knockdown, wall splat, wall bounce, and ground bounce.
- Landing transitions for airborne reactions.
- Combo recovery, wakeup, and reaction-state clearing.
- Juggle and ground-normal-juggle counters.

### Keep in FighterController

- Contact validation.
- Calling `HitResolver`.
- Passing the resolved reaction to `HitReactionController`.
- High-level hit logging and presentation notification.

### Implementation slices

1. Introduce `HitReactionState` containing only reaction-owned fields.
2. Move simple hitstun/blockstun transitions.
3. Move launch/juggle transitions.
4. Move knockdown/bounce/wakeup transitions.
5. Move landing recovery and state clearing.
6. Replace compatibility wrappers only after all callers use the component.

### Exit criteria

- `FighterController` contains no direct assignments to reaction-owned fields outside delegation/setup.
- Reaction precedence remains in `HitResolver`.
- Ground, air, juggle, bounce, block, parry-adjacent, and knockdown regressions pass.

## Phase 2: extract AttackStateMachine

### Goal

Give attack startup, active, recovery, and per-move runtime bookkeeping one owner.

### Move

- Startup/active/recovery counters.
- Attack start and clear operations.
- Current move runtime data.
- Per-attack hit flags and hit groups.
- Charge and sustained-mash counters.
- Projectile/effect spawn flags and timing.
- Normal-use counters for chain and airtime limits.
- Air-attack landing metadata.

### Keep in FighterController

- Selecting requested actions.
- Asking `ChainResolver` whether transitions are legal.
- Coordinating movement abilities with attack start/end.

### Data structure

Create one `AttackRuntimeState` instead of moving dozens of individual fields. It should expose
read-only queries such as `IsActive`, `ElapsedFrames`, and `HasConfirmedHit`.

### Exit criteria

- One component owns all attack timeline counters.
- No duplicate current-move state exists in controller and component.
- Ground, air, flight, boost, charge, projectile, super, and chain regressions pass.

## Phase 3: extract InputCoordinator

### Goal

Separate raw/sampled input and motion recognition from action selection.

### Move

- Button-buffer counters.
- `ActionInput` construction.
- Buffered jump facing/horizontal values.
- Reusable motion-command selection and consumption intent.
- Command priority and special-cancel buffer coordination.
- Jump/dash/attack buffer clearing.

### Keep

- `MotionInputBuffer` as the low-level command recognizer.
- `FighterController` as the consumer that starts attacks or abilities.

### Exit criteria

- Simulation obtains one immutable action snapshot per frame.
- Hitstop buffer aging remains identical.
- QCF/dash separation, charge, reusable motions, super commands, and negative-edge flight tests pass.

## Phase 4: extract AirStateController

### Goal

Give jump-route and airtime resources one owner.

### Move

- Normal jump, short-hop, and super-jump route flags.
- Double-jump and air-dash availability.
- Air-action counts and height gating.
- Flight-used state.
- Jump/short-hop pushbox rules.
- Landing-lag queues and reset behavior.
- Airtime and coyote-time tracking.

### Required invariants

- Frame-one super-jump release never becomes a short hop.
- Short hops cannot normal-chain.
- Full normal jumps can chain on hit.
- Super-jump facing correction remains distinct from normal-jump facing.
- Button flight and negative-edge flight retain their separate landing/fall behavior.

### Exit criteria

- All route flags reset in one method owned by `AirStateController`.
- Facing, chain, flight, and movement code only query the air-state API.

## Phase 5: extract MovementResolver

### Goal

Centralize final velocity calculation while preserving ability ownership.

### Move

- Ground acceleration/deceleration and back-walk speed.
- Run-stop and crouch-slide friction.
- Air acceleration and drift.
- Gravity and terminal velocity.
- Juggle/combo gravity scaling.
- Forced descent.
- Vertical movement during hitstop.

### Interface

Movement abilities provide intent and ownership flags. `MovementResolver` calculates the next
velocity from a snapshot. `FighterController` performs the Godot `MoveAndSlide` call and applies
the result.

### Exit criteria

- Only one path calculates ordinary gravity and locomotion velocity.
- Flight position hold, air momentum preservation, drift, run, and landing regressions pass.

## Phase 6: isolate supporting systems

Perform these as separate small extractions:

### ThrowController

- Capture/release ownership.
- Victim anchoring.
- SPD flight/landing sequence.
- Throw launch and impact events.

### SuperSequenceController

- Confirmation state.
- Defender lock and offsets.
- Rush hit intervals.
- Activation freeze/backdrop requests.
- Blocked-super resolution and final-hit bookkeeping.

### CollisionBoxProvider

- State and move box selection.
- Local/world mirroring.
- Box merging.
- Contact lookup.

### FighterPresentationController

- Animation/state-name selection.
- Drawing offsets and timelines.
- Jump/run/state-impact presentation requests.
- Move contact effects and burn presentation.

Presentation may observe combat state but must never decide damage, hitstun, facing, or movement.

## Phase 7: consolidate tuning and authored behavior

### Goal

Reduce controller configuration surface and eliminate character-specific core branches.

### Work

- Group exported values into `CombatTuning`, `HitstopTuning`, `ReactionTuning`, and air/movement tuning resources.
- Preserve old serialized fields through a migration or compatibility layer until scenes are converted.
- Replace string-based attack categories with authored identifiers/tags where practical.
- Move Sanzou/Mecha-specific routing into move data, abilities, or explicit character hooks.
- Replace the private `NormalMoveRule` duplication with a shared immutable runtime move model.
- Make the public ability-runtime dictionary read-only outside its owner.

### Exit criteria

- Core components contain no character-name checks.
- Scene/resource migration is verified before compatibility fields are removed.
- Exported controller configuration is substantially smaller and grouped by ownership.

## Required smoke suite

Run after every extraction:

- Project build with zero warnings.
- `NeutralOpponentFacingRegressionTest`.
- `ChainResolverRegressionTest`.
- `HitResolverRegressionTest`.
- `QcfDashSeparationRegressionTest`.
- `ReusableMotionInputsRegressionTest`.
- `MechaChainRulesRegressionTest`.
- `MechaFlightRegressionTest`.
- `SanzouJabRegressionTest`.
- `ContactLayeringRegressionTest`.
- Relevant focused tests for the component being changed.

Do not change gameplay merely to make an unrelated or already-failing integration test green.

## Change-size policy

Each implementation slice should:

- Move one ownership boundary.
- Avoid simultaneous balance changes.
- Avoid resource-format migration.
- Add or update focused tests.
- Leave compatibility delegates when other systems still depend on the old API.
- Remove compatibility code only in a later verified slice.

If a slice requires widespread changes across attacks, movement, input, and presentation at once,
the boundary is too large and should be divided.

## Completion criteria

The project is sufficiently clean when:

- `FighterController` primarily orchestrates components and Godot lifecycle work.
- It is approximately 1,800–2,500 lines.
- Every mutable gameplay field has one clear owner.
- Resolver inputs/outputs are immutable and independently testable.
- Character-specific behavior is authored outside core policy.
- Adding a normal, special, jump type, or hit reaction does not require editing unrelated systems.
- All required smoke tests pass and remaining known failures are documented.

## Recommended next milestone

Begin Phase 0 and Phase 1 together in a narrow slice:

1. Add deterministic tests for ordinary hitstun, launch, juggle, knockdown, and wakeup transitions.
2. Introduce `HitReactionState`.
3. Move only `ApplyHitstun`, `ApplyBlockstun`, and `ApplyHitReaction` behind `HitReactionController`.
4. Verify, then move launch and juggle behavior in the following slice.

Do not begin `AttackStateMachine` until hit-reaction ownership is complete.

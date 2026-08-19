# Hit resolution

Hit resolution is split between `FighterController` and the stateless `HitResolver`.

## Ownership boundary

`FighterController.TryApplyBasicAttackHit` owns stateful work:

1. Validate attacker, defender, team, wake-up, and invulnerability state.
2. Find hitbox/hurtbox contact.
3. Resolve throws, grounded-knockdown eligibility, hit groups, and parries.
4. Ask the defender whether the strike is blocked.
5. Build immutable resolver requests from the current move, hitbox, and defender state.
6. Apply the selected block/hit reaction to the defender.
7. Apply hitstop, effects, attacker momentum, super bookkeeping, and hit logging.

`HitResolver` owns deterministic policy and performs no mutation:

- Grounded light/medium/heavy hitstun tiers.
- Counter-hit and air-to-air hitstun modifiers.
- Pushback scaling order.
- Normal and instant blockstun.
- Juggle-bounce decay.
- Mutually-exclusive hit-reaction precedence.

## Reaction precedence

The current precedence is:

1. Blow-away
2. Launcher
3. Unguarded special reaction
4. Stumble
5. Hit-fall
6. Authored knockdown/wall splat
7. Final Super Rush launch
8. Final-super knockdown
9. Air-heavy juggle
10. Continuing juggle bounce
11. Ordinary air pop
12. Ground hitstun

Order is gameplay. Do not reorder branches to make the code look simpler.

## Safely changing combat behavior

1. Add or update a focused `HitResolverRegressionTest` assertion for pure numerical or precedence changes.
2. Change `HitResolver` when the rule is deterministic and does not mutate a fighter.
3. Change `FighterController` only when the rule consumes state, applies a reaction, or triggers presentation.
4. Run the focused resolver test and relevant character/contact regressions.

Do not place character-name checks in `HitResolver`. Character differences should arrive through authored move data or explicit request values.

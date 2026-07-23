# Collision architecture

The runtime uses one fighting-game collision model. Each box type has exactly one job.

## 1. Stage body

Every fighter is a `CharacterBody2D` on **Fighter Bodies** (layer 2) with a mask containing only **Stage Geometry** (layer 1). `MoveAndSlide()` handles floors and walls. Fighter bodies never physically collide with one another.

`FighterCollisionPolicy.Apply()` enforces this for scene fighters and dynamically spawned clones.

## 2. Pushbox

`FighterController.WorldPushbox` is the spacing box. `VersusStageRules` resolves point-fighter overlap once per fixed physics frame, after both fighters move. This avoids the friction, stacking, and stage-tunnelling produced by `CharacterBody2D`-versus-`CharacterBody2D` collision.

An authored pushbox in a move timeline replaces the fighter's fallback pushbox while that timeline entry is active. Outside its frame range, the fallback returns automatically.

Only the currently controlled point fighter participates in opponent spacing. `SetPointCollisionParticipation(false)` removes inactive same-team clones/helpers from pushbox and incoming-hit resolution while allowing their already-started hitboxes and projectiles to persist. This prevents team collision piles without cancelling an attack during a control switch.

## 3. Hurtbox and hitbox

Combat uses deterministic `Rect2` data from `FighterBoxFrame` resources:

- hitbox versus hurtbox is an attack contact;
- pushbox or body overlap never causes damage;
- fighters with the same nonzero `TeamId` cannot hit one another;
- one move can have several boxes for coverage and still records only one hit per target;
- selecting **Replace other boxes of this kind while active** makes the selected replacement boxes win for their active frames;
- when no authored box of a kind is active, the fighter fallback box is restored.

The optional `Area2D`/`CollisionShape2D` classes are editor and initialization adapters. They can be used to draw a shape in Godot and convert it into `FighterBoxFrame` data, but they are not a second combat resolver.

## Fixed-step order

1. Fighters simulate and collide with the stage.
2. `StageCamera` updates the fight box at physics priority 50.
3. `VersusStageRules` resolves spacing, hits, projectiles, and camera-corner limits at priority 100.

This keeps collision gameplay at 60 Hz even when rendering at a different frame rate.

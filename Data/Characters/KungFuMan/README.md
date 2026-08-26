# Kung Fu Man move editing

Kung Fu Man is the only fighter targeted by the **Kung Fu Man Moves** Godot dock.
The dock edits these resources directly:

- `kung_fu_man_normal_moves.tres` — all current grounded, crouching, and airborne normals
- `kung_fu_man_special_moves.tres` — light and heavy projectile specials
- `../kung_fu_man_test.tres` — the fighter definition that owns both sets

## Editing a box

1. Open the project in Godot and find **Kung Fu Man Moves** in the upper-right dock.
2. Choose a move. Its assigned animation appears automatically.
3. Scrub to a frame with the slider or frame number.
4. Press **+ DRAW HITBOX** or **+ DRAW HURTBOX**, then drag directly on the sprite preview.
5. Select the new box in the list to edit its frame range, rectangle, tag, or replacement behavior.
6. To remove one, select it and press **− DELETE SELECTED BOX**.

To use the same box on several frames instantly, select it, enter **First frame** and **Last frame**, then press **APPLY BOX TO EVERY FRAME IN RANGE**. Both endpoints are included: First 4 and Last 8 covers frames 4–8. Use Last `-1` to keep the box active for the remainder of the move.

Overlapping hitboxes combine into one coverage area but still resolve as one contact, so they do not cause duplicate hits. Enable **Replace other boxes of this kind while active** when the selected box should temporarily suppress the move's other active boxes of the same kind. This replacement applies only for the selected box's inclusive Start–End frame range and does not suppress boxes of another kind.

Every timeline is 60 Hz: one editor frame is one gameplay frame. Box end frames are
inclusive. A hitbox on frames 4–5 therefore lasts two gameplay frames.

The editor autosaves box changes. **Apply timing & animation** saves the three move
phases and animation assignment. **Copy +1f** makes a per-frame copy of the selected
box. Advanced hit reaction and interaction settings remain available through
**Open selected move in Inspector**.

If a move has any authored hurtbox, its hurtboxes must cover the entire move. The dock
shows a red warning for the first uncovered frame so accidental invulnerability is
visible immediately.

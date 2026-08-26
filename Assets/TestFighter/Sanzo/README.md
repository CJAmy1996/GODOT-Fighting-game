# Sanzo Kongoumaru test character

The original numbered BMP/PNG frames and OGG clips are preserved in
`sanzou_kongoumaru/`. Run `Tools/import_sanzo_character.py` with Pillow to
rebuild the transparent PNG frames and `sanzo_sprite_frames.tres`.

Gameplay aliases now follow the user-confirmed mappings in
`SANZO_GROUP_CATEGORIZATION.md`. Sanzo owns independent normal and special move
resources, has no standard run or air dash, and uses the split command-run
sequence only as reserved special-move animation data.

All runtime frames are baked onto a 320x384 transparent canvas. Their lowest
visible sandal/foot pixel is aligned to Y=250, which matches the scene's
AnimatedSprite2D floor offset and prevents frame-to-frame vertical jumping.
The SpriteFrames library runs on a 60 FPS timeline. Original 10 FPS drawings
use six-tick holds, keeping their visual cadence while synchronizing animation,
combat timelines, hitboxes, hitstop, and movement to the engine's 60 Hz clock.

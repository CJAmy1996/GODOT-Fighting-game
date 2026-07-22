# Kung Fu Man sprite test

The source sheet supplied for this test is credited on the image to Elecbyte, with the rip credited to Logan Abner.

Run the reproducible importer from the project root with:

```powershell
python Tools/import_kfm_sprites.py "path/to/Kung Fu Man.png"
```

The importer extracts initial `idle`, `walk`, `walk_back`, `jump`, and `attack` sequences, removes the exact green and magenta color keys, aligns frames to a common bottom-center origin, and regenerates `kung_fu_man_sprite_frames.tres`.

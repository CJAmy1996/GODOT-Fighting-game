# Big Bang Beat Revolve common-effect source guide

## What was found

The extraction has no original `.dat` or `.csv` lookup table for `_common_pct`.
Its authoritative mapping is the Shift-JIS action script
`Extraction/BigBangBeatRevolve/_common_scr/script.txt`; `kir.txt` is the image
dimension/index companion. `backup.txt` is a duplicate of the action script.
The other common script files are empty or contain only small global values.

The project now generates two UTF-8 CSV references from that source using
`Tools/catalog_bigbang_common.py`. Their stable identity is the raw zero-based
DXA source section used by `Ｏ` commands; the old filtered visual number is
retained only in the explicitly labelled `legacy_visual_id` column:

- `Assets/Effects/BigBangCommon/common_animation_catalog.csv` — every raw
  source section, including deleted/drawing-less sections, with its name,
  source frames, 60 Hz holds, origins, child source sections, system references,
  sounds, and missing drawings.
- `Assets/Effects/BigBangCommon/common_resource_usage.csv` — every numeric PNG
  or BMP, dimensions, every action/drawing that uses it, holds, and its current
  Godot runtime role.

Regenerate both with the bundled workspace Python runtime:

```powershell
& 'C:\Users\casey\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Tools/catalog_bigbang_common.py
```

## Script notation used by the port

These are the source commands needed for the implemented effects. Unknown
fields remain in the original script and CSV rather than being guessed.

| Command | Use in this port |
| --- | --- |
| `Ｉ` | Draw image for N 60 Hz ticks. The next fields are image ID and source origin X/Y. |
| `Ｏ` | Spawn another visual action. This is how hit debris and the fourteen blood droplets are created. |
| `Ｍ` | Motion command. The DP and blood ports preserve the confirmed initial velocities and per-tick acceleration/deceleration values. |
| `大` | Scale and scale-per-tick command. Guard grows from 30% to 100%; blood droplets shrink 2% each tick. |
| `色` | Color/alpha mode. The imported art uses the source green color key. |
| `SE` | Sound reference. IDs are catalogued, but the archive contains no decoded audio implementation in this project. |
| `FD` / `FA` | Defensive and attack collision definitions in character actions. The current fighter resources keep their editable Godot box timelines. |

## Runtime mappings

All timings below are 60 Hz source ticks.

| Purpose | Common source section | Exact source resources and behavior | Godot resource |
| --- | ---: | --- | --- |
| Weak hit | 0 `打撃ヒット_弱` | Cropped PNG 062–069 animation; holds `1,2,2,2,2,2,2,2`; horizontally flipped for gameplay | `Effects/BigBangHitImpact.tscn` |
| Medium hit | 3 `打撃ヒット_中` | Same core drawings/timing; source changes sound to 112 | Catalogued; regular light/heavy routing currently selects by attack strength |
| Strong hit | 4 `打撃ヒット_強` | Shares the cropped PNG 062–069 gameplay impact animation | `Effects/BigBangHitImpact.tscn` |
| Guard/block | 20 `ガード` | 192–199; first drawing 8 ticks, rest 2; source-flipped; scale 30%→100% during the first 8 ticks | `Effects/BigBangGuardImpact.tscn` |
| Dust | 11/12 `煙_2` | 085–094; 3 ticks each; source origins preserved; mirrored action supported by facing | `Effects/BigBangDust.tscn` |
| Blood controller | 26 `出血` | 200–210; holds `3,2,3,4,3,3,3,3,3,3,3`; source origins and upward motion preserved | `Effects/BigBangBloodHitSpark.tscn` |
| Blood droplet | 27 `血しぶき` | Fourteen frame-211 children; 60 ticks; random X `-500..500`, Y `-1000..-150`, +30/tick gravity, -2% scale/tick | Spawned by `BigBangCommonEffect` |
| BB background | 90 `[演出]BBモード背景` | References 385–388 at 1 tick each, then 385 for 60; source scale 110% | Drawings 385–388 are missing from `_common_pct`; see fallback below |

`Scripts/Demo/HitSparkLayer.cs` is the single routing point for normal hit,
block, and dust scenes. A move-specific `HitSparkScene` can override the
regular spark, and `FighterBoxFrame.HitSparkScene` can now override one authored
hit group without changing the other contacts in a multi-hit move.

The light/heavy runtime nodes above intentionally mean the source's primary
hit-spark drawing. The action's `Ｏ` children are separate source sections:
section 7 is Particle A, section 71 is the rear hit-spark layer, and sections
77/79 are B-stone controllers. Their exact source-section references are
retained instead of being silently flattened into the spark. They are not
spawned by the single regular hit-spark node because this framework does not
yet implement the source game's complete particle/B-stone system.

## Universal super presentation

The source action for `[演出]BBモード背景` exists, but its referenced image
files 385–388 do not. The universal super backdrop therefore uses the two
available authored galaxy animations:

- `Assets/Backgrounds/ALidej.gif`
- `Assets/Backgrounds/ALidej2.gif`

`VersusStageRules` applies that moving galaxy backdrop to every super. Each
imported Big Bang fighter now sets `FighterDefinition.SuperPortrait` to copied
archive art in its own asset directory. Archives with `1000.png` use that
cut-in; Kamui and Kinako use their full-size `victory.png` because their
archives contain no `1000.png` cut-in. The existing portrait overlay, blackout,
ring, and gameplay layering remain universal rather than move-specific.

## Mecha Heita DP source and C# mapping

The authoritative source actions are:

- visual 130 / source section 134: `[必殺]対空` — grounded DP
- visual 132 / source section 136: `[必殺]対空_空中版` — airborne DP
- visual 131 / source section 135: `[必殺]対空着地` — landing recovery

Both DP versions hold drawings 417, 418, and 419 for three ticks each. The
launch `Ｍ` command occurs after those nine startup ticks:

| Version | Source `Ｍ` command | Ported special-move data |
| --- | --- | --- |
| Grounded action 130 | X `1000`, Y `-800`, horizontal change `-40` per tick | launch frame 9, facing X 1000, Y -800, braking 2400 units/s² |
| Airborne action 132 | X `0`, Y `-800` | air-only, launch frame 9, X 0, Y -800 |

`SpecialMoveData` now exposes launch frame, facing ownership, and horizontal
deceleration instead of hard-coding Mecha Heita in the controller. The C#
controller applies the launch on the authored frame and does not run grounded
attack friction on that takeoff tick. The source animation then runs the
420–423 spin loop twice, frame 424, the 309/311–315 descent/recovery drawings,
and the existing action-131 landing resource.

The `DS 1 135 0` command immediately before the launch is retained in the
source catalog. Its exact legacy engine-side meaning is not documented by this
extraction, so it has not been assigned a speculative gameplay effect.

### DP impact-effect selection

The effect selector in the source `FA` records is not move-wide. Grounded
action 130 changes to common visual 26 (`出血`, blood) at the launch/spin `FA`
after the nine startup ticks, then changes to visual 37 for its finishing `FA`.
The port therefore assigns `BigBangBloodHitSpark.tscn` only to grounded DP
hitboxes 1–8 (frames 9–32). Hitboxes 9–13 use the normal spark fallback. The
airborne action's corresponding spin `FA` selects visual 36, not blood, so the
air DP does not inherit the grounded DP's blood scene. The super DP similarly
uses visual 60 for its repeated spin records and no longer incorrectly applies
blood to its entire move.

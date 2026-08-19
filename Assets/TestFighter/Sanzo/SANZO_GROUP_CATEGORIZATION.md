# Sanzo Kongoumaru group categorization — pass 1

This is the working classification of the original numbered sequences. Nothing
in this document changes gameplay yet. Confirmed mappings can be promoted into
`sanzo_sprite_frames.tres` and Sanzo's move resources one category at a time.

Confidence: **High** means the action is visually clear, **Medium** means the
general family is clear but the exact command/role is uncertain, and **Low**
means it should be checked in motion or against the original character.

| Group | Frames | Visual description | Suggested framework/KFM role | Confidence |
|---|---:|---|---|---|
| `group_00` | 0–5 | Crouching hit reaction | `crouch_hit` | Confirmed by user |
| `group_01` | 7–13 | Ground impact; may be reused for ground bounce | knockdown landing / ground bounce | Confirmed by user |
| `group_02` | 15–20 | Gets up from knockdown | knockdown recovery / wake-up | Confirmed by user |
| `group_03` | 22–29 | Launched into the air | airborne launch reaction | Confirmed by user |
| `group_04` | 31–35 | Standing reaction to light attacks | standing light-hit hitstun | Confirmed by user |
| `group_05` | 37–41 | Standing reaction to heavy attacks | standing heavy-hit hitstun | Confirmed by user |
| `group_06` | 43–48 | Three base/impact pairs: 0/1 standing block, 2/3 crouching block, 4/5 air block | standing, crouching, and airborne block reactions | Confirmed by user |
| `group_07` | 50–57 | Drawings 1–5 enter crouch; reversed 5–1 is an interruptible standing-state exit flourish | `crouch_start` and `crouch_end` | Confirmed by user |
| `group_08` | 59–67 | Jump sequence | jumping animation | Confirmed by user |
| `group_09` | 69–76 | Forward/backward air dash | Benched; no air dash for grappler design, reserve for possible special | Confirmed by user |
| `group_10` | 78–89 | Backward stepping cycle | `walk_back` | Confirmed by user |
| `group_11` | 91–102 | Forward stepping cycle | `walk` | Confirmed by user |
| `group_12` | 104–115 | Standing combat loop | `idle` | Confirmed by user |
| `group_13` | 117–124 | Held crouching stance | `crouch_hold` | Confirmed by user |
| `group_14` | 126–132 | Drawings 0–1 turn around once; drawings 2–6 loop back and forth as the back-facing victory pose | `win_start`, then `win`/`win_loop` | Confirmed by user |
| `group_15` | 134–144 | Enters and settles into combat stance | intro animation | Confirmed by user |
| `group_16` | 146–149 | `[BB] Big Bang Mode Activation`; Sanzou Trait 2 on S2/L | `trait_2` | Confirmed from Revolve source and by user |
| `group_17` | 151–162 | Heavy attack performed from crouch | crouching heavy | Confirmed by user |
| `group_18` | 164–168 | Quick punch from crouch | crouching jab / `crouching_light_punch` | Confirmed by user |
| `group_19` | 170–178 | Command normal performed from crouch | crouching medium command normal | Confirmed by user |
| `group_20` | 180–187 | Heavy kick performed in the air | jumping heavy kick / `air_heavy_kick` | Confirmed by user |
| `group_21` | 189–192 | Quick punch performed in the air | jumping jab / `air_light_punch` | Confirmed by user |
| `group_22` | 194–200 | Airborne body splash | jumping heavy / body splash; body remains active through splash frames like Zangief | Confirmed by user |
| `group_23` | 202–211 | Standard throw sequence | normal grab / throw | Confirmed by user |
| `group_24` | 213–224 | Heavy punch performed while standing | standing heavy punch / `heavy_punch` | Confirmed by user |
| `group_25` | 226–229 | Quick punch performed while standing | standing jab / `light_punch` | Confirmed by user |
| `group_26` | 231–240 | Advancing powered straight punch | QCF Power Punch special | Confirmed by user |
| `group_27` | 242–252 | Follow-up strike after QCF Power Punch | Power Punch Rekka second hit | Confirmed by user |
| `group_28` | 254–263 | Heavy kick performed while standing | standing heavy kick | Confirmed by user |
| `group_29` | 265–277 | Shared reflector casting sequence | complete sequence reused by on-block Aegis-like projectile special and direct Aegis Reflector super | Confirmed by user |
| `group_30` | 279–290 | Stomping special attack | Stomp special; charge down, then up | Confirmed by user |
| `group_31` | 292–311 | Frames 292–299: command run. Frames 300–311: punch follow-up | charge back, then forward + LP/HP activates; activation button changes run distance; during run LP branches to Group 10 hop and HP branches to frames 300–311 punch | Confirmed by user |

## Recommended first assignment batch

These are the safest mappings to implement first:

| Framework animation | Sanzo group |
|---|---|
| `crouching_light_punch` | `group_18` |

## Needs motion review before assignment

- Jump and fall roles: `group_20`, `group_22`, and parts of `group_30`.
- Heavy hit versus parry: `group_25`.
- Exact command roles for `group_17`, `group_27`, and `group_30`.
- Whether `group_07` or `group_12` is the canonical idle loop instead of `group_00`.

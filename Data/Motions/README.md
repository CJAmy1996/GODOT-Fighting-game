# Reusable motion inputs

Assign one of these `.tres` assets to a special or super move's `Command Input > Motion` field.
Create a new `MotionInputBinding` in that field, select the accepted attack-button flags, and choose
`Any Selected Button` for LP-or-HP / LK-or-HK or `All Selected Buttons` for a chord.

Direction notation is facing-relative and supports names or numpad notation:
`N/5`, `F/6`, `DF/3`, `D/2`, `DB/1`, `B/4`, `UB/7`, `U/8`, `UF/9`.

All included charge motions require 45 sampled frames and use five frames of release/button leniency.
The mash resource requires five accepted button presses in 30 frames; its punch/kick choice comes from
the per-move binding rather than the shared motion asset. Set `Mash Window Frames Override` on a move's
binding to give that move its own expiration range; zero uses the motion resource's `MashWindowFrames`.

`double_circle_720.tres` accepts clockwise and counterclockwise rotations, including the same
cardinal-direction leniency provided by the SPD/360 resource.

## Native and rollback input path

Gameplay directions and buttons arrive through `NativeInputRouter` as immutable `NativeInputFrame`
packets. On Windows, keyboard state comes directly from Win32 and controllers come directly from
XInput; Godot's InputMap is not consulted. Every packet is sampled once per 60 Hz simulation frame,
then converted to `FighterInput` before this motion library sees it.

Local hardware, replay, and network inputs therefore use the same motion API. A rollback host can send
`NativeInputFrame.NetworkWord`, restore it with `NativeInputRouter.SubmitNetworkWord(...)`, invalidate
incorrect predictions with `InvalidateAfter(...)`, and resimulate using the stored frame. Special and
super move resources require no online-specific motion code.

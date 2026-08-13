# Frontier Aja

## MVP Combat Design

*Working design document — Big Bang Beat sequel concept*

## 1. Design Thesis

The MVP should present an unmistakable evolution of Big Bang Beat rather than merely a modernized doujin fighter or a Guilty Gear derivative. Its identity comes from a familiar fighting-game foundation being disrupted by character-specific mechanics: four conventional attack buttons, two unique Trait buttons, a universal Blue Cancel that can route special moves into Traits, deliberate juggle rules, and defensive systems that prevent offense from becoming deterministic.

The guiding principle is that familiarity should make the game readable, while Traits, supers, and their interactions make it memorable. A player should be able to understand the basic combat language quickly and then discover that each character uses the last two buttons—and the universal systems surrounding them—in fundamentally different ways.

## 2. Core Control Language

- LP / HP / LK / HK: the shared attack language. These establish immediately readable fighting-game fundamentals.
- Trait 1 / Trait 2: character-specific language. A Trait is not required to be an attack, stance, resource, or movement action. Its category may differ completely between characters.
- Special moves: conventional character techniques that form the bridge between fundamentals and character-specific systems.
- Blue Cancel: a universal system allowing special moves to cancel into either Trait, signaled by a blue visual flash.

## 3. Trait Design Rules

Traits are not bonus special moves. Each Trait button should expose enough design space that removing it would materially damage the character's identity. Two Traits may form a single interconnected system, or they may represent two different facets of the character.

Every Trait should pass two tests:

- **Raw Test** — Is there a meaningful reason to press the Trait without using Blue Cancel?
- **Blue Test** — Does Blue Canceling into the Trait create possibilities meaningfully different from simply canceling into another attack?

Traits should not inherently exist merely to cancel other actions. Because Blue Cancel already provides the universal cancel layer, a Trait whose primary identity is “a cancel” duplicates the system rather than interacting with it.

## 4. Blue Cancel and Universal Meter

Blue Cancel requires a meaningful limitation. The preferred MVP direction is to test a single universal meter rather than adding a dedicated Blue Cancel gauge. The same resource can create tension between offense, defense, and supers.

- **Blue Cancel:** spend meter to cancel a special into Trait 1 or Trait 2.
- **Guard Cancel:** spend the same meter defensively to contest or terminate pressure.
- **Supers:** spend the same meter for character-specific high-impact tools.

The desired decision is not simply “Do I have meter?” but “What is this meter worth right now?” Spending heavily on Blue Cancels should leave the player poorer defensively and may delay access to important supers.

## 5. Combo and Juggle Philosophy

The combo system should move toward the deliberate juggle structure associated with games such as Guilty Gear and Street Fighter Alpha while retaining Frontier Aja's own pacing. Air ukemi/recovery is removed so the middle of a combo does not become an arbitrary recovery scramble.

- Launches create predictable juggle states.
- Gravity, hitstun, juggle limits, knockdown properties, pushback, and resource costs regulate routes.
- Combo decisions should offer tradeoffs among damage, corner carry, knockdown, resource economy, Trait setup, and positional advantage.
- Defensive agency returns after the combo through wake-up systems rather than interrupting the combo through air recovery.

## 6. Defensive Systems

### Instant Yellow Block

A precisely timed block that modifies/reduces blockstun and creates opportunities to challenge pressure. The current direction is for execution/timing—not meter—to be its primary cost. It provides systemic counterplay against oppressive or unforeseen pressure sequences.

### Guard Cancel

A meter-spending defensive escape or challenge. Guard Cancel should coexist with Yellow Block: Yellow Block rewards precision, while Guard Cancel provides a more expensive emergency answer. Because it shares meter with Blue Cancel and supers, defensive security directly competes with offensive ambition.

### Wake-up Roll

Wake-up rolling prevents optimal okizeme from becoming completely deterministic. The system should be tuned so rolling does not merely replace oki with repetitive left/right guesses. A possible distinction is ordinary knockdowns allowing wake-up movement while specific hard-knockdown enders restrict it, creating another combo-routing tradeoff.

## 7. Supers: More Than Damage Exhaust

Supers are a major part of character identity, not merely the final damage dump at the end of a combo. Modern fighting games often concentrate supers around conversion and damage efficiency. Frontier Aja should deliberately restore the broader design vocabulary of supers.

- **Damage / Cash-out Super** — The conventional high-damage option remains useful, but should be only one category.
- **Setup Super** — Creates a powerful future situation: objects, traps, positioning, delayed attacks, unusual knockdown, or a temporary battlefield condition.
- **Install Super** — Temporarily changes how the character plays, unlocking altered moves, properties, routes, or strategic priorities rather than simply increasing damage.
- **Puzzle-Piece Super** — A specialized tool whose value emerges through interactions with the rest of the kit. It may solve a matchup problem, complete a setup, manipulate a character resource, or enable routes unavailable through raw damage supers.
- **Utility / Control Super** — Uses meter for movement, screen control, resource manipulation, defensive leverage, or another strategically meaningful effect.

A good super should sometimes make the player willingly sacrifice immediate damage because its strategic effect is more valuable. The question after earning meter should therefore be broader than “When do I cash out?”

## 8. Character Trait Directions — MVP

### Mecha Heita — Mobile Weapons Platform

**Trait 1: Flight** — Sentinel-like flight that fundamentally changes his positioning and aerial combat.

**Trait 2: Armament** — Access to an ammunition-managed ranged arsenal rather than a single gun attack. Directional inputs can access different weapons such as vulcans, beam fire, missiles, or anti-air ordnance. Weapons should remain usable during flight so the two Traits multiply one another.

**Identity:** Flight controls where the machine can operate; Armament controls what areas it threatens. Blue Cancel allows specials to route directly into either side of the mobile-weapons-platform system.

### Kunagi — Spatial Predator

**MVP role:** one of the more approachable Ryu/Ken/Sakura-style characters, but her Traits preserve a supernatural, predatory identity.

**Current direction:** one Trait governs unusual repositioning/vanishing while the other governs pursuit or an opponent-relative predatory action. Neither should inherently be a cancel; Blue Cancel is what permits special moves to route into these actions.

**Identity target:** simple enough to understand immediately, but capable of creating unusual spatial relationships and attack vectors.

### Rouga — Relentless Pursuer

Rouga's historical B-Dash identity should survive without duplicating Blue Cancel. A Trait can preserve explosive pursuit movement as a real action with its own movement properties rather than functioning as an inherent attack cancel.

A complementary Trait should reinforce hunting/chasing the opponent so that his two buttons communicate relentless forward momentum. The exact implementation remains open and should be tested against his original kit before locking.

### Heita — Fundamentals and Guts

**MVP role:** potentially the purest fundamentals character. His Traits should remain immediately understandable while expressing his heroic, relentless personality.

Current directions include elevating Konjou Hashiri / Guts Run into a meaningful movement Trait and exploring a second Guts-oriented action that represents standing his ground or forcing his way through opposition. Exact properties remain prototyping targets.

## 9. Trait Philosophy Beyond the MVP

Characters with already-extreme identities—such as Kamui, Kinako, and Agito—may be easier Trait-design cases because their existing concepts can be promoted into dedicated interfaces. The goal is not to force every character into an offensive Trait and defensive Trait template. One character may manipulate a persistent weapon; another may control deployed objects; another may use the two buttons as access points to different portions of a weapon system.

Consistency comes from everyone having two Trait buttons and everyone being able to Blue Cancel specials into them. Variety comes from the fact that pressing those buttons can mean entirely different categories of action for different characters.

## 10. MVP Success Criteria

- The game remains readable to players familiar with traditional 2D fighters.
- Within minutes, players notice that the two Trait buttons radically change meaning between characters.
- Blue Cancel feels like an amplifier of character identity rather than a generic Roman Cancel imitation.
- Defense provides multiple answers to pressure without making sustained offense unrewarding.
- Juggles are predictable enough to learn but contain meaningful routing decisions.
- Meter creates genuine tension among Blue Cancel, Guard Cancel, and supers.
- Supers regularly create decisions beyond maximizing combo damage.
- Straightforward characters remain distinctive without requiring excessive meters, stances, or subsystems.
- Complex characters demonstrate how far the same universal framework can stretch.
- Players can describe the game's unusual Trait/Blue-Cancel interactions without needing comparisons to another franchise.

## 11. Prototype Questions

- How expensive must Blue Cancel be before it becomes a decision rather than the default end of every special?
- Does sharing meter among Blue Cancel, Guard Cancel, and supers produce healthy tension or excessive hoarding?
- How lenient can Yellow Block be before high-level offense becomes too weak?
- How strong can Guard Cancel be without making pressure disposable?
- Which knockdowns permit rolling, and what should earn a hard knockdown?
- What juggle limiter produces expressive routes without forcing identical optimal combos?
- Does each Trait pass both the Raw Test and Blue Test?
- Does every MVP character have at least one super whose optimal use is not simply combo damage?
- Can a new player understand the simple characters quickly while still discovering advanced Trait interactions later?

**STATUS: Working MVP combat specification — mechanics and character implementations subject to playtest.**

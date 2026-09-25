# M9A Phase 2 Weapon Selection and Target Scoring Audit

Date: 2026-09-25
Branch: `feature/session-save-load`
Pre-M9A-Phase-2 HEAD: `3f15f5b5ed2cb6cd35f4a67dc609a8460e9268dc`

## Boundary

M9A Phase 2 closes the bounded NPC combat-AI subset authorized after Phase 1: supported melee-weapon use,
authoritative weapon/ammunition selection and fallback, and the source target-scoring behavior that existing
OpenArcanum authorities can represent. It does not create a second combat, equipment, inventory, vitality, timing, or
target authority.

`CombatAiController` still owns only transient stable-ID decisions. Equipment changes use
`WorldMapSessionCoordinator` inventory/equipment authority, attacks use `CombatStateService`, real-time scheduling uses
M8H, and all damage, defeat, death, and consequences continue through M4B and M8D/M8E. Save format remains V1.

The implemented weapon boundary is deliberately narrow: unarmed attacks, Bow attacks already supported by M8C-M8H,
and one-handed or otherwise currently equippable melee weapons whose source data expresses range one, no ammunition,
and only normal/fatigue damage. Firearms, throwing weapons, grenades, special elemental damage, spell and technology
AI, world-item scavenging, backoff, fleeing, surrender, followers/party behavior, and general simulation remain
deferred.

## Original-source archaeology

The implementation was preceded by a call-path audit of the local `arcanum-ce` source, principally `src/game/ai.c`,
the item wield/equipment path, and combat animation selection.

### Weapon-selection trigger and cost

- Combat entry marks the actor for a wield check. The AI preflight calls `item_wield_best_all(ai->obj,
  ai->danger_source)` before choosing its combat action.
- This preflight is immediate: it is not an action goal, has no AP charge, and schedules no time event. Phase 2
  therefore performs equipment selection before attack submission and does not invent a switch cost or cooldown.
- Source ammunition failure marks the NPC to check or look for a weapon/ammunition alternative. Phase 2 reselects
  before retrying when the currently equipped weapon becomes unusable, including after its last compatible arrow is
  consumed.
- Source world-item lookup and pickup exist, but require a broader world-search and movement policy. Phase 2 searches
  only the actor's authoritative equipped items and inventory children and does not synthesize scavenging.

### Candidate legality, ammunition, and ranking

The source considers the current weapon and inventory candidates, checks wieldability, and excludes an ammunition
weapon unless compatible inventory quantity meets its consumption. Its effective-damage comparison combines the
weapon's damage data with the relevant skill effectiveness. A candidate that can reach the current danger source is
preferred over an otherwise stronger out-of-range candidate; stable object-handle order resolves equal worth.

OpenArcanum mirrors that behavior within the representable boundary:

- candidates come only from authoritative equipped slots and `ChildrenOf(actor)`;
- broken weapons, unsupported damage profiles, firearms, throwing weapons, and Bows without a compatible arrow are
  skipped;
- supported melee and Bow data use source-shaped effective-damage scoring;
- a best reachable candidate wins when one exists, otherwise the best supported candidate wins;
- equal scores use descending stable ObjectID as the deterministic substitute for source object-handle order;
- every attempted equipment change passes through normal `EquipItem`/`UnequipItem` validation, so hand conflicts and
  ownership remain authoritative; and
- if no candidate can be selected, the actor unequips the unusable weapon and falls back to the existing unarmed
  attack instead of repeatedly issuing `NoAmmo` or bypassing equipment rules.

The source wieldability path also checks polymorph, item allergy, crippled arms, and critter weapon-animation ART.
Those states are not currently represented as selection-time gameplay authorities. Phase 2 does not invent them.
Broken ART state, item ownership/type, hand conflicts, weapon class/data, and compatible ammunition are represented
and enforced. Missing real-time weapon animation timing still fails through the existing M8H `TimingUnavailable`
boundary rather than falling back to made-up production timing.

Two source-port ambiguities are recorded rather than hidden. The audited effective-damage loop assigns `total_dam`
for each damage type instead of visibly accumulating it; the bounded implementation mirrors the final supported
damage entry and rejects mixed unsupported profiles. The audited ranged-selection variable is not updated where its
name implies it should be; Phase 2 applies the apparent intended effective-damage comparison and a stable ObjectID
tie rather than reproducing nondeterministic handle behavior.

### Supported melee attack transaction

`CombatStateService` now accepts an equipped supported melee weapon for `BasicMelee`. The request uses the weapon's
source AP cost, normal/fatigue damage ranges, minimum-strength penalty, and to-hit bonus. Those modifiers appear in
the existing ordered ledger and its final effectiveness remains the authority used by hit, Dodge, and M8F critical
classification. Damage continues through resistance and M4B vitality; death and consequences remain exactly-once
M8D/M8E work.

Unsupported or malformed weapon data is rejected before AP, ammunition, vitality, turn, or real-time-scheduler
mutation. No weapon path bypasses the structured attack transaction.

The real-time loader now derives the source weapon animation nibble from equipped melee item ART and applies the
weapon-speed input to both melee and ranged authored timing. If the required critter/weapon animation is absent, the
existing fail-closed timing result remains authoritative.

## Target discovery, focus, and danger scoring

The source separates ordinary target discovery from reactive danger-source comparison:

- ordinary `ai_find_target` behavior retains a valid combat focus and otherwise finds a perceived kill-on-sight
  candidate in distance/perception order;
- `ai_choose_target` is not a continuous best-target optimizer. It is invoked when a new danger source challenges an
  existing focus;
- one injected random branch compares negative distance; the other compares level minus distance; and
- equal scores retain the current focus.

Phase 1's nearest eligible discovery, 20-tile bound, stable source-order/ObjectID tie, and valid-focus retention are
therefore preserved. Phase 2 treats a successfully committed attack transaction against an NPC as the smallest
representable danger event. Once per new attack-resolution sequence, the attacked NPC compares that attacker with
its retained focus through the production combat RNG and exact distance or level-minus-distance branch. A tie keeps
the current target. Runtime enrollment, polling, presentation rebuild, and an unsuccessful/rejected attack cannot
replay the event.

Dead, unconscious, off, invalid, non-hostile, or otherwise ineligible actors cannot be selected. The broader source
inputs for decoys, party leaders, follower relationships, concealment/perception contests, social protection/guard
behavior, and generic danger events do not yet have matching authorities and remain explicitly deferred.

## Lifecycle and save policy

Weapon choice is recomputed when the actor first requires it or its equipped weapon becomes unusable. A still-usable
choice is retained, so polling, roster refresh, presentation rebuild, and turn transitions do not churn equipment or
duplicate inventory entries. Selection never changes current-turn ownership and costs no combat AP.

The latest processed attack-resolution sequence, weapon-selection flags, retained target, previews, intents, and
diagnostics are transient. Save V1 still serializes committed inventory/equipment, ammunition quantity, vitality,
death, corpse, and world state, but normalizes active combat and every AI/attack/critical transaction. A restored
session therefore keeps a committed equipped weapon or depleted arrow stack without restoring a stale selection or
danger reaction.

## Focused automated validation

The new `M9APhase2WeaponTargeting` category contains 12 tests. They prove:

- supported melee damage, AP, weapon identity, ordered modifier ledger, and vitality mutation through the existing
  attack transaction;
- authoritative inventory-to-equipped weapon selection;
- preference for a reachable Bow with compatible ammunition;
- no-ammunition fallback to a supported melee weapon;
- last-arrow depletion followed by unarmed fallback before another attack is submitted;
- broken/unsupported firearm rejection and source-supported unarmed fallback;
- deterministic equal-score ObjectID ordering;
- zero AP mutation for equipment selection;
- a committed danger event using the level-minus-distance branch;
- the distance branch and source tie-keeps-current behavior;
- real-time READY/BUSY scheduling with the selected weapon; and
- Save V1 equipment persistence with transient AI/attack normalization.

Accepted focused result: **12/12 passed**, 0 failed, 0 skipped, 0 inconclusive.

The retained Phase 1 category passed **15/15** after its former no-ammunition yield fixture was updated to the exact
Phase 2 fallback contract.

## Physical Play Mode validation

Computer Use drove the already-open Unity 6000.0.71f1 Editor and production `TestTerrain` composition; no second
Editor was launched. The accepted representative run used source-authentic objects and production authorities:

- authentic Greater Skeleton `G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848`, prototype 28460, from
  `maps/arcanum1-024-fixed/59726889458.sec`;
- its authentic equipped Sword `G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91`, prototype 6050;
- authentic Bow and arrow objects from `maps/arcanum1-024-fixed/101602821844.sec`; and
- the production PC and authentic Polar Bear Cub combat fixture.

The physical run proved:

- the Greater Skeleton retained/selected its authentic Sword and issued a turn-based weapon attack through
  production AP, modifier-ledger, damage, resistance, and vitality authority;
- the same equipped item ART selected the source melee animation family and weapon speed in real-time combat;
- the Bear selected an authoritative inventory Bow only when one compatible split arrow existed, with no AP spent on
  the switch;
- the Bow attack consumed that arrow through the existing ammunition transaction;
- after depletion, selection unequipped the unusable Bow and chose unarmed before retrying, without a repeated
  `NoAmmo` attack;
- READY scheduled one real-time action, BUSY rejected another, exact source-time recovery enabled reevaluation, and
  target scoring used deterministic production-service state; and
- the accepted run produced 0 warnings and 0 errors.

The authentic Polar Bear Cub still has no authored Bear-with-Bow timing profile. As in Phase 1, the bounded Bow
scheduler proof used the deterministic validation timing provider while retaining authentic items, production
equipment/ammunition, `CombatStateService`, M8H scheduling, and vitality authority. The production timing provider was
restored afterward. This is a documented presentation-fixture limitation, not alternate combat AI.

## Regression and complete-suite validation

- M9A Phase 1 combat AI: **15/15**.
- M8I combat UI: **17/17**.
- M8H real-time combat: **22/22**.
- M8G Phase 4 Bow/Critical Dodge: **17/17**.
- M8G Phase 3 cover/Bow Master: **12/12**.
- M8G Phase 2 structured attacks: **17/17**.
- M8G Phase 1 combat loop: **16/16**.
- M8F critical resolution: **9/9**.
- M8E death consequences: **7/7**.
- M8D defeat state: **11/11**.
- M8C ranged combat: **12/12**.
- M8B turn-based combat: **23/23**.
- M8A core combat state: **22/22**.

The combined M8A-M8I regression result was **185/185**. Together with Phase 1, the requested regression result was
**200/200**. The complete EditMode suite passed **869/869**, with 0 failed, 0 skipped, and 0 inconclusive. The complete
suite emitted the same five intentional fail-closed dialogue compatibility warnings and 0 errors. After clearing
expected test output, the final Unity Console was **0 logs, 0 warnings, 0 errors**. Unity compilation was clean and
`git diff --check` was clean.

## Final status and deferred work

M9A Phase 2 is complete. Together, Phases 1 and 2 establish the bounded source-supported NPC combat-AI kernel:
stable autonomous target/approach/attack/yield behavior, supported unarmed/Bow/melee attacks, authoritative
weapon/ammunition selection and fallback, and representable danger-source target scoring. No Phase 3 is required to
claim this bounded M9A kernel complete.

This status does not claim the complete original AI subsystem. World-item scavenging, rare backoff, grenades,
fleeing/surrender, spell and technology decisions, followers/party/leader relationships, decoys, concealment and
perception contests, social guard/protection behavior, schedules, dialogue AI, general simulation, and source states
without current authorities remain later work. M9B was not started.

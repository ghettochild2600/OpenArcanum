# M8D defeat-state audit and validation

## Scope and source

This bounded audit uses the OpenArcanum clean-room source mirror at
`D:/OpenArcanum/Research/Repositories/arcanum-ce` and the shipped Polar Bear Cub
fixture already established by M8A-M8C. M8D implements vitality-derived defeat,
combat consequences, the retained corpse domain, and the minimum fall-down
presentation/blocking projection. Death scripts, rewards, looting, recovery,
resurrection, AI, and quest consequences remain later milestones.

## Authoritative state

`critter_is_dead` is exactly current hit points `<= 0`. There is no independent
dead bit. `critter_is_unconscious` is current fatigue `<= 0` for ordinary
critters; undead are excluded. `critter_is_active` additionally rejects dead,
unconscious, stunned, paralyzed, destroyed/off, and similar source conditions.
Therefore M4B vitality remains the sole HP/fatigue authority and M8D adds typed,
derived queries rather than a second persistent death field.

Destroyed/off is distinct from death. Death does not replace the critter with a
container or a newly identified corpse. The same object, object type, stable
ObjectID, inventory parent relationships, and equipped-item relationships are
retained. Save V1 already stores the underlying damage and needs no format bump.

## Combat consequences

An inactive critter cannot act or be selected for a turn. A transition to
unconsciousness explicitly ends that critter's current turn. Unconscious
participants remain in the combat population and are skipped while ineligible.
A dead participant is deactivated and removed from the active participant set;
if it owned the current turn, selection advances once from its former position.
M8D does not invent immediate victory: the existing explicit `EndCombat` check
remains the boundary and succeeds once no eligible hostile participant remains.

## Corpse and presentation

Ordinary death schedules the source fall-down action
`TIG_ART_ANIM_FALL_DOWN` (animation 7). On completion the source holds the final
frame and adds `OF_FLAT | OF_NO_BLOCK`. This bounded implementation projects the
fall-down action while retaining facing, marks the existing `WorldObject` as a
dead presentation, and removes its dynamic navigation occupancy. Presentation
is a projection only; destroying or rebuilding it does not change vitality.

Unconsciousness uses the same fall-down action but does not add `OF_NO_BLOCK`,
so an unconscious critter remains an ordinary movement blocker. Wake-up and
fatigue recovery scheduling are explicitly deferred.

## Deferred integration boundaries

`SAP_DYING` runs before the remainder of source kill handling and can veto it.
That script dispatch is not broadened in M8D. Damage XP and the remaining kill
XP are awarded around the lethal transition in source combat; M8D records this
as the later reward boundary and awards neither. Corpse interaction/loot UI,
quest kill credit, reputation, follower death, decay, resurrection, and special
bloody-death actions also remain deferred.

## Authentic fixture

| Field | Value |
| --- | --- |
| Description | Polar Bear Cub |
| Sector | `maps/arcanum1-024-fixed/47781512457.sec` |
| ObjectID | `G_9B807B01_A142_4949_80CE_5A085F3BEEB1` |
| Prototype | `28422` |
| HP / fatigue | `48 / 29` |
| Natural normal damage | `3-6` |
| Death script | no M8D-required authored dispatch |

## Implemented ownership and transitions

`CharacterVitalityService` remains the sole HP/fatigue authority. Its typed change event reports only committed
numeric changes, while `IsDead`, `IsAlive`, `IsUnconscious`, and `IsConscious` derive state from current vitality.
No persistent dead flag, corpse identity, replacement container, or Save V2 field was added.

`CombatStateService` consumes those authoritative transitions. Death removes the stable participant identity and, if
it owned the current turn, selects the next eligible participant once from the removed slot. Unconsciousness retains
the participant but ends its current turn and excludes it from later turn selection. Attack/movement completion
guards prevent a vitality callback and the command tail from both advancing the turn. Neither transition invents
automatic victory; the existing explicit `EndCombat` policy succeeds after no eligible hostile remains.

The existing `WorldObject` projects fall-down animation 7 while preserving facing. A dead actor loses dynamic
occupancy through `SectorNavigationMap.SetRegisteredObjectBlocking`; an unconscious actor remains blocking. The map
keeps stable identities registered independently of whether they currently contribute occupancy, so rebuild,
movement, unregister, and controlled-PC bookkeeping cannot leave a ghost blocker. `WorldObjectSectorLoader` derives
the same presentation and occupancy on initial load, graphics rebuild, sector reload, and V1 restore.

## Validation results

The physical fixture was the authentic Polar Bear Cub
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in
`maps/arcanum1-024-fixed/47781512457.sec`. The production PC used the authentic equipped bow and arrow stack already
audited by M8C. Six deterministic production ranged attacks routed normal/fatigue damage through M4B and crossed the
bear's HP threshold exactly once. The resulting corpse retained the same NPC ObjectID/type and all containment and
equipment relationships, projected animation 7, left the participant set, could not attack, and stopped blocking its
tile. Repeated lethal damage created neither another transition nor another `WorldObject`.

The same physical run drove the bear's fatigue to zero through M4B while it owned the current turn. It remained an
unconscious, non-dead participant and movement blocker, could not act, and advanced once to the PC. A separate
current-actor lethal transition advanced once without double-incrementing the round or duplicating a participant.
Explicit `EndCombat` succeeded only after the hostile was no longer eligible.

Original -> Enhanced -> Original rebuilds preserved death and unconsciousness, vitality, ObjectID, containment,
equipment, occupancy, and unique presentation. Save V1 round trips preserved both derived states; active combat
normalized to Inactive as designed. Sector unload/reload preserved corpse state and produced no resurrection or ghost
blocker. The physical run recorded 0 warnings and 0 errors.

Final Unity validation on 2026-09-21:

- focused M8D EditMode: **11 passed, 0 failed, 0 skipped, 0 inconclusive**;
- required M8C/M8B/M8A/M4B/navigation/M6A/world-session regressions: **150 passed, 0 failed, 0 skipped, 0 inconclusive**;
- complete EditMode: **725 passed, 0 failed, 0 skipped, 0 inconclusive**;
- complete-suite console: 5 known intentional fail-closed dialogue compatibility warnings, 0 errors;
- physical Play Mode: pass, 0 warnings, 0 errors.

## Remaining boundaries and next milestone

M8D deliberately does not dispatch `SAP_DYING`, award damage/kill XP, produce quest/reputation/follower consequences,
open corpse-looting UI, transfer loot, schedule wake-up/regeneration, decay corpses, resurrect actors, expand critical
effects, or add AI/real-time combat. The recommended next separately authorized milestone is **M8E: bounded
source-authentic death consequences and corpse interaction/loot**, beginning with script/reward ordering and one
audited corpse inventory transaction rather than broadening combat or presentation authority.

# M8E death consequence source audit

## Scope

M8E is deliberately limited to one authoritative lethal-combat consequence path:

- preserve the M8D HP-derived corpse and combat-participant removal;
- resolve a PC killer against one source-authored hostile NPC;
- enforce the source death-script boundary;
- award the source's final kill share exactly once;
- retain the dead NPC's ordinary inventory and equipment on the same persistent ObjectID;
- move one authentic ordinary item and one authentic equipped item from that corpse to the PC through the existing inventory authority; and
- persist the processed marker in save format V1 so loading cannot replay the award.

Decay, resurrection, alignment, logbook kills, follower notifications, generated loot, damage-proportional XP, and broader death-script execution remain deferred.

## arcanum-ce ordering

The authoritative order is split between `combat_dmg` and `critter_notify_killed`:

1. `combat.c:1836-1851` awards damage-proportional XP from `OBJ_F_NPC_EXPERIENCE_POOL` before testing whether the target is dead.
2. `combat.c:1854-1867` detects the lethal HP result and calls `critter_notify_killed(victim, killer, anim)`.
3. `critter.c:667-673` notifies the text-floater system, then executes `SAP_DYING`; a false/skip-default result immediately vetoes the remaining default kill handling.
4. `critter.c:675-687` rejects a script-destroyed victim, deactivates combat mode, stores death time, recalculates reaction, records combat focus, and notifies AI.
5. `critter.c:689-719` resolves a direct PC killer or an NPC's PC leader. The final award is `20 * OBJ_F_NPC_EXPERIENCE_WORTH / 100`, after which the worth is zeroed; alignment, kill-log, and follower notifications follow.
6. `critter.c:722-748` detaches a victim from its leader, removes the weapon from the death animation, starts the dying animation, and schedules body decay unless `OCF2_NO_DECAY` is set.
7. `critter.c:751-755` clamps lethal HP damage and notifies worn magic/tech items.

`critter_is_dead` (`critter.c:608-615`) remains purely HP-derived. No separate death boolean is source authority.

## Corpse inventory semantics

The source kill path does not destroy, detach, or regenerate the NPC inventory. The same critter object becomes the corpse. The inventory UI reads the target critter's inventory slots directly (`inven_ui.c:983-1002`, `2727-2747`) and resolves worn locations with `item_wield_get`, so ordinary and equipped children remain attached to the corpse until an explicit inventory transaction moves them.

OpenArcanum already models that source shape: M8D retains `Contained` and `Equipped` placements under the dead NPC's stable ObjectID. M8E therefore adds no corpse container and generates no loot. Corpse looting is a narrow transaction layered over `WorldMapSessionCoordinator`, which remains the sole inventory mutation authority.

## GREEN fixture

The selected retail fixture is a hostile Greater Skeleton from the existing Arcanum module:

| Fact | Source value |
|---|---|
| Sector | `maps/arcanum1-024-fixed/59726889458.sec` |
| MOB | `g_a334de5a_fb86_407a_98c0_ddeb6335e848.mob` |
| ObjectID | `G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848` |
| Prototype | `28460` |
| Description | `Greater Skeleton` (`description.mes` 28440) |
| Map coordinates | `(31883, 56988)` |
| Object flags | `16434` (`0x4032`, including the source `OFF` encounter gate) |
| Effective NPC flags | `4418` (`ONF_KOS` is set) |
| Source XP worth | `440` |
| Final kill share | `20 * 440 / 100 = 88` XP |
| Instance `SAP_DYING` | `0` |
| Prototype `SAP_DYING` | `0` |

The instance has an `OBJ_F_SCRIPTS` array, but sparse-array lookup at attachment index 12 (`SAP_DYING`) is absent. Prototype 28460 has no script array. The GREEN path therefore proves the no-script boundary; it does not pretend to execute a script. A nonzero effective `SAP_DYING` is an explicit pre-commit failure for lethal attacks until that VM path is supported.

This inventory-rich Greater Skeleton begins behind an authentic source encounter gate (`OFF`). The physical validator
only clears that gate, then unloads and reloads the sector so the ordinary production presentation path can bind the
NPC. It does not set HP damage, dead state, corpse state, or consequences directly. All death state is subsequently
reached through the production M8C ranged-attack and M4B/M8D vitality path.

Authentic child objects retained by the corpse include:

| ObjectID | Item | Prototype | Source location |
|---|---|---:|---:|
| `G_6413F64C_29FD_4A44_8E2C_E9A414888116` | Gold (89) | 9076 | 0 |
| `G_D4322488_E424_304C_B618_768F45AF694F` | Kathorn Crystal | 15180 | 2 |
| `G_027714F9_21FE_E54B_81E7_B889E074229F` | Liquid of Skin Thickening | 10162 | 4 |
| `G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91` | Sword | 6050 | 1004 (weapon) |
| `G_214C4DF7_9936_7A4A_ABE8_D68BD5EFF3C6` | Ruby Ring | 8300 | 1001 (ring) |

The focused path transfers the exact 89-gold object from corpse to PC. A second focused assertion transfers the exact equipped sword directly from corpse ownership to PC containment, proving equipped loot is not discarded or cloned.

## OpenArcanum responsibility boundary

- `CharacterVitalityService` remains the HP/fatigue authority.
- `CombatStateService` retains transient turn/AP ownership, supplies killer attribution, and preflights unsupported lethal death scripts before any attack resources or vitality change are committed.
- A coordinator-owned death-consequence service performs only the no-script/default reward transaction and exact-once guard.
- `CharacterProgressionService.AwardExperience` remains the only XP mutator.
- `WorldMapSessionCoordinator` remains the only inventory-placement mutator, including the narrow atomic corpse-owner transfer.
- `PersistentObjectState` carries the per-victim processed marker. `ObjectSaveData` persists it as an additive V1 field; the save version remains 1.

The marker is committed only after the supported consequence transaction succeeds. Re-entry returns an already-processed result and awards no XP. A V1 round trip restores the marker, so load cannot replay the award.

## Final implementation and ordering

The admitted implementation preserves the source boundary while remaining deliberately smaller than the retail kill
pipeline:

1. `CombatStateService` resolves the seeded attack, Dodge, damage, and resistance values.
2. A would-be lethal hit preflights the victim's effective `SAP_DYING`. A nonzero script is rejected as
   `UnresolvedDeathScript` before AP, ammunition, vitality, XP, inventory, or campaign state changes.
3. The supported no-script hit spends combat resources and applies damage through `CharacterVitalityService`.
4. M8D derives death from HP `<= 0`, removes the victim from active participation, and retains the same persistent
   object as the corpse.
5. `DeathConsequenceService` attributes the kill to the attacking production PC, awards the final source share through
   `CharacterProgressionService`, and commits the exact-once marker only after the transaction succeeds.
6. Corpse inventory is unchanged until an explicit loot request. Loot then delegates to the existing coordinator-owned
   inventory and capacity authorities.

The selected skeleton's source worth is 440, so the supported default final share is exactly `20 * 440 / 100 = 88`
XP. M8E never writes Level directly; any threshold crossing remains M4C-owned. The fixture starts at PC XP 0 and ends
at XP 88 without crossing a level threshold.

## Corpse interaction and loot contract

M8E exposes corpse access through `DeathConsequenceService` rather than manufacturing a container. The dead NPC's
ObjectID remains the owner of both contained and equipped children. An ordinary corpse-owned item uses the existing
atomic inventory transfer. An equipped item first validates ownership, slot, item flags, destination grid, and weight,
then moves directly from `Equipped(deadActor, slot)` to `Contained(looter)` with one placement event. Capacity and
invalid-equipment failures leave ownership, worn slot, inventory location, quantity, and presentation unchanged.

The physical proof moved the authentic 89-gold object and authentic equipped sword to the production PC. Both retained
their original ObjectIDs; the sword's worn slot cleared; no item or corpse identity was cloned. A repeated loot request
was rejected because the item was no longer corpse-owned.

## Validation evidence

Final validation used Unity 6000.0.71f1 on `feature/session-save-load`:

- live Unity compilation: clean for production, tests, and editor validation assemblies;
- focused M8E EditMode: **7/7 passed**, 0 failed, 0 skipped, 0 inconclusive;
- complete EditMode: **732/732 passed**, 0 failed, 0 skipped, 0 inconclusive;
- the complete suite emitted five known intentional fail-closed dialogue compatibility warnings and zero errors;
- physical Play Mode: **PASS**, with zero new warnings and zero errors.

The physical trace used Greater Skeleton `G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848` in
`maps/arcanum1-024-fixed/59726889458.sec`. Six deterministic production bow attacks produced one lethal transition.
The production PC received XP `0 -> 88`; the corpse retained the same identity; the skeleton left active combat; the
89-gold object and equipped sword moved to the PC; repeat processing and repeat loot were blocked. Explicit
`EndCombat` remained required and restored ordinary gameplay.

The same run passed Original -> Enhanced -> Original presentation rebuild, corpse-sector unload/reload, and a V1
save -> reset -> load cycle. Death, consequence marker, XP, corpse ownership, and looted ownership survived without
replay or duplicate presentation. Focused failure coverage additionally proves living/invalid targets, invalid killer,
duplicate processing, non-corpse and foreign-item access, capacity rejection, invalid equipped loot, and unsupported
death-script preflight all roll back atomically.

## Remaining boundaries

M8E does not execute nonzero `SAP_DYING`, distribute damage-proportional XP, credit followers, apply alignment,
reputation, kill-log, follower, AI, decay, resurrection, wake-up, generated-loot, or corpse-UI behavior. It does not add
automatic victory; combat still ends only through the existing explicit command. Save format remains V1 because the
processed marker is an additive field in the current V1 snapshot rather than a new schema contract.

The recommended next separately authorized milestone is **M8F: bounded source-authentic critical success and critical
failure resolution**. It should audit and admit one deterministic melee/ranged critical table slice, preserve the
current attack/death transaction boundaries, and avoid broadening into M9 real-time scheduling, general status-effect
execution, combat UI/audio, AI, spells, or technology.

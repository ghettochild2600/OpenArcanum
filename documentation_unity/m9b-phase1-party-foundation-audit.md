# M9B Phase 1 Follower / Party Foundation Audit

Date: 2026-09-27

## Result

M9B Phase 1 is complete. OpenArcanum now has one session-owned, stable-ObjectID party authority and a production
vertical slice using the retail Virgil fixture. Party state is independent of scene hierarchy and presentation, and
all movement, travel, combat, vitality, inventory, progression, and save mutations remain owned by their established
systems.

The accepted retail fixture is Virgil, ObjectID `G_A09DCD63_7A15_D411_8F1D_00E02920220C`, prototype 17102, dialogue
1324, at tile `(31,34)` in `maps/arcanum1-024-fixed/86570436012.sec`.

## Source Findings

The bounded audit covered the relevant follower paths in `critter.c`, `party.c`, `combat.c`, `anim.c`, `teleport.c`,
`level.c`, `object.c`, `script.c`, and `sector_object_list.c`, plus the retail DAT records and dialogue resources.

- A single-player follower is an NPC whose leader relationship ultimately resolves to the PC. Multiplayer `party.c`
  tables are not the single-player follower authority.
- Ordinary capacity is `Charisma / 4`; Expert or Master Persuasion adds one. The already-authoritative M4D
  `MaximumFollowers` derived statistic implements that bounded rule. Script-forced followers bypass ordinary capacity.
- Source local following uses a desired distance of 4 and spread distance of 7. Ordinary movement remains path-based;
  a source catch-up path exists beyond distance 30 but is not required for this first vertical slice.
- Eligible followers accompany sector, map, and world-travel transitions. Unconscious followers wait behind; death
  and unconsciousness do not themselves erase membership, preserving future resurrection semantics.
- Party members share the PC side for hostility. Followers act autonomously through normal combat AI and must not
  target the PC or another party member.
- The available source does not establish a reliable party-tail turn order. The bounded deterministic projection is
  source-order hostiles, party-membership order, then the PC.
- A follower kill credits the PC with 20% of the victim's XP. Follower level schemes exist in source but require a
  later, separately audited progression slice.
- Followers retain their own inventory and equipment identity. No party-inventory or follower-equipment UI is implied.
- Loyalty, reaction, alignment, scripted departures, resurrection, and campaign-specific special followers are not
  core membership authority and remain with later social, magic, or campaign systems.

## Implemented Authority

`PartyStateService` is owned by `WorldMapSessionCoordinator`. It owns the PC leader projection, ordered membership,
forced-member flag, capacity, join, remove, transition snapshots, eligibility, and stable identity. Join rejects an
invalid, dead, unconscious, duplicate, or over-capacity ordinary follower before mutation. Removal changes only the
relationship; it never destroys or clones the NPC.

`PartyFollowerMovementService` advances an eligible follower by one production-navigation step toward the leader when
outside the source distance. It never parents transforms or teleports during ordinary following, and a blocked route
fails safely. `ProductionPartyFollowerDriver` is an idempotently attached presentation adapter.

The M1/M7 transition pipeline captures party placements, relocates eligible followers only after destination
preflight/presentation succeeds, and rolls their placements back with the PC on failure. This covers ordinary sector
movement, the M7 Bates entrance/return path, and the M7E Bates-to-Tarant route without a second travel authority.

The authentic Play Mode run found one production-only defect not visible in the synthetic test fixture: sector unload
correctly clears transient combat sources, but a retained follower moved into a foreign sector was initially rebuilt
without re-registering its NPC combat metadata. `WorldObjectSectorLoader` now rebuilds that source from the retained
object's original source sector and prototype. The same path covers Save V1 presentation restoration.

M8 projects party members as allies and enrolls an eligible follower in deterministic party order. M9A remains the
only autonomous combat-decision authority in turn-based and real-time combat. Friendly attacks reject before AP,
ammo, vitality, or turn mutation. Followers stop acting while dead or unconscious. M8E awards the exact source 20%
follower-kill reward to the production PC once, using the existing processed-death marker.

Save V1 now has an optional ordered `party` payload containing only leader identity, member identities, and forced
flags. Older V1 documents restore an empty party. Load validates identity, NPC/character/vitality references,
duplicates, and capacity before replacing service roots. Paths, targets, combat decisions, and presentations remain
transient.

## Validation

- Unity 6000.0.71f1 production, test, and editor compilation: clean.
- M9B Phase 1 focused EditMode: **16/16**.
- M9A Phase 2: **12/12**; M9A Phase 1: **15/15**.
- M8A-M8I combat regressions: **185/185**.
- Combined M9A/M8 regression matrix: **212/212**.
- Complete EditMode suite: **885/885**.
- Failed/skipped/inconclusive: **0/0/0**.
- Final cleared Unity Console: **0 logs, 0 warnings, 0 errors**.
- `git diff --check`: clean.

The physical production proof passed with the authentic Virgil and Polar Bear Cub fixtures: authoritative join and
ordered membership; one-step local path following; stable identity and one presentation across ordinary sector,
Bates entrance/return, and Bates-to-Tarant world travel; automatic turn-based enrollment and ally policy; autonomous
M9A action; M8H real-time READY/BUSY action; unconscious membership retention; Save V1 identity/membership restore
with transient normalization; and Original -> Enhanced -> Original presentation rebuild. The accepted run reported
zero warnings and zero errors.

## Deferred / Ambiguous Boundary

- The dialogue parser recognized source `fo`/`jo`/`lv` follower operations, but the Phase-1 production M5 execution
  context did not yet route them to `PartyStateService`. The bounded Phase-2 closure has now completed that bridge.
- Source distance-30 catch-up teleport is deferred unless closure review proves it essential; ordinary following and
  every authoritative transition already work without inventing formation behavior.
- Follower leveling schemes, loyalty/alignment departures, resurrection, full orders/stances, portraits/HUD,
  inventory UI, magic, technology, and campaign quests belong to later systems.
- The available port does not justify a more specific party-tail turn order or leader-target preference, so neither
  was invented.

Phase 1 was committed before Phase 2 work. The closure assessment found the authoritative dialogue join/leave bridge
required and sufficient; it is now implemented and M9B is complete. See
[`m9b-phase2-dialogue-party-audit.md`](m9b-phase2-dialogue-party-audit.md).

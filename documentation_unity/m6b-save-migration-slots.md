# M6B Save Migration Boundaries and Domain Slots

## Pre-implementation M6A format audit (2026-09-13)

M6A writes deterministic indented JSON with the exact top-level envelope `format`, `version`, `world`, `objects`,
`characters`, and `campaign`. Format is `OpenArcanum.SessionSave`; version is `1`. All six domains are required. The
world domain requires current map, selected sector, next dynamic identity, production-PC identity/map position/ART,
and the complete tombstone list. Object records require reconstruction/source fields, mutable ART/flags, placement,
stack, and runtime-created state. Character records require attributes, vitality, progression/training, and derived
inputs. Campaign requires sparse variables/flags, quests/timestamps/clock, script attachments, and reactions.

Persistent identities are canonical uppercase `A_`, `G_`, `P_`, or `D_` keys. Live identities, tombstones, parents,
character owners, attachments, and reactions are validated as one graph. Lists are deterministically ordered. The
dynamic allocator must be nonzero and greater than every live or tombstoned `D_` sequence.

M6A load ordering is read -> direct V1 DTO deserialization -> format/version check -> world/object/reference plan ->
character service roots -> derived/capacity roots -> campaign/reactions -> transactional coordinator apply. Apply
tears down presentation, swaps authoritative roots, rebinds dependent services, selects the restored sector, and
rebuilds presentation. Dialogue, pending interaction/navigation, portal transition fractions, and Unity objects are
deliberately absent and normalize to idle/stable projections.

### Migration and slot risks identified before editing

- Format/version dispatch and current-domain validation are coupled in `SessionSaveService.TryBuildPlan`; adding a
  future reader there would spread version-specific compatibility logic through domain restoration.
- `SessionSaveData` is both the serialized V1 DTO and current validated input. A boundary is needed even while
  Current remains V1 so future migrations can produce one current snapshot shape.
- Raw path save/load is intentionally low level and has no authoritative save directory, safe slot identifier,
  enumeration, metadata, overwrite/delete lifecycle, or stale-temporary cleanup.
- Adding metadata directly to `OpenArcanum.SessionSave` would change the V1 schema. M6B should keep V1 unchanged and
  use one atomic slot wrapper containing derived metadata plus the unchanged session payload.
- The M6A external-parent exception permits only an unchanged authored child whose current parent equals its exact
  authored parent. M6B must prove relocated/changed/tombstoned/dynamic placement never regains authored containment.
- A corrupt slot must fail before `LoadJson` applies a plan; slot parsing and metadata validation therefore cannot
  reset or partially rebuild the live session.
- Slot timestamps need an injected UTC clock in tests. Map, sector, PC identity, and level should be derived from the
  authoritative snapshot rather than copied from presentation or independently mutated.

## Implemented boundary

M6B keeps `OpenArcanum.SessionSave` at version 1 and introduces two explicit, separate compatibility layers:

1. `SessionSaveMigrator` reads only the raw session envelope, dispatches by exact format/version, and produces the one
   current `SessionSaveData` shape consumed by the existing domain validator. V1 currently migrates identity-to-
   identity. Unsupported future versions and malformed or incomplete envelopes fail with typed `SessionLoadFailure`
   values before a restoration plan exists.
2. `SessionSaveSlotService` wraps the unchanged session snapshot in `OpenArcanum.SaveSlot` version 1. Slot metadata is
   derived from that authoritative snapshot and cannot redefine it. A slot must pass wrapper, metadata, migration,
   domain, and reference validation before `SessionSaveService.LoadJson` can transactionally replace the live roots.

The resulting load call graph is:

```text
slot file
  -> SessionSaveSlotService (safe name + wrapper/metadata validation)
  -> SessionSaveMigrator (format/version dispatch -> current snapshot)
  -> SessionSaveService.TryBuildPlan (domain/reference validation)
  -> SessionSaveService.LoadJson
  -> WorldMapSessionCoordinator.ApplyLoadedSession
  -> authoritative M1-M5 service roots
  -> selected-sector presentation rebuild
```

Raw-path `SessionSaveService` remains available as the low-level session serializer. Unity presenters, scene objects,
navigation, interaction, dialogue views, and graphics rebuilds do not own or serialize gameplay state.

## Slot contract

The production default catalog is `Application.persistentDataPath/Saves`. A custom root overload exists for tests and
tools. Slot IDs are deliberately narrow: 1-32 lowercase ASCII letters/digits, with `-` and `_` permitted only after
the first character. Resolution uses a full canonical path plus a save-root containment check; separators, traversal,
uppercase names, whitespace, absolute paths, and leading punctuation are rejected.

Each `.oaslot` file is one JSON document with:

- `slotFormat = OpenArcanum.SaveSlot` and `slotVersion = 1`;
- metadata containing slot ID, canonical UTC timestamp, embedded session format/version, current map, selected sector,
  production-PC ObjectID, and PC level;
- the complete unchanged `OpenArcanum.SessionSave` payload.

Metadata is recomputed from the captured session on save and matched back to the embedded snapshot on enumeration and
load. It cannot select another map, PC, or level. Listing is ordinally sorted and returns both valid entries and
corrupt entries with an error instead of hiding damaged files. Create/overwrite, enumerate, load, delete, missing-slot,
invalid-name, read/write/delete/enumeration, malformed wrapper, unknown format, unsupported version, invalid metadata,
and underlying session-load failures have typed results.

Save uses a unique sibling temporary file, UTF-8 without BOM, write-through plus durable flush, reread and complete
validation, then `File.Replace` for overwrite or `File.Move` for first creation. Owned stale `*.oaslot.*.tmp` files are
cleaned during normal slot operations. A failed write cannot partially replace an existing slot. A failed parse,
migration, metadata check, domain check, or corrupt-slot load occurs before live root replacement; the active session
and its object references remain unchanged.

`WorldMapSessionCoordinator.SaveSlots` is the production entry point. It creates a domain service over the existing
coordinator and does not make a Unity component the save authority.

## Placement and identity contract

M6B does not loosen M6A's reference rules. The only unresolved-parent exception remains an unchanged source-authored
child whose current parent is its exact authored parent. Current authoritative placement wins whenever an item has
moved, changed containment, been equipped, been dropped in a foreign sector, been tombstoned, or was created with a
dynamic `D_` identity. Reloading the source sector cannot resurrect a relocated item or silently restore its authored
containment.

The tests cover an unresolved unchanged authored parent, changed containment, a relocated authored object retaining
its original `G_` identity in another sector, exact multi-NPC registries, tombstones/dynamic allocator continuity, and
idempotent quest/training/Gold restoration.

## Validation (2026-09-13)

Unity 6000.0.71f1 compiled with zero compiler errors.

- focused M6B EditMode: **32 passed, 0 failed, 0 skipped, 0 inconclusive**;
- required M1-M6B regression matrix: **364 passed, 0 failed, 0 skipped, 0 inconclusive**;
- complete EditMode suite: **535 passed, 0 failed, 0 skipped, 0 inconclusive**.

The required matrix was M6B 32, M6A 25, M5C 21, M5B 18, M5A 16, M4D 25, M4C 29, M4B 20, M4A 13,
M3E 21, M3D 18, M3C 13, M3B 13, M3A 8, M2B 8, M2A 16, player navigation 21, M1A 7, M1B 11,
world-session state 27, and portal ART resolution 2. The complete suite's compatibility warnings were expected by
their tests; they were not compiler or test failures.

The dedicated TestTerrain Play Mode proof used only real source data:

- food `G_8781D726_74FE_0846_AD0A_88EE591B6383`, prototype 10078, from
  `maps/arcanum1-024-fixed/101602821845.sec`;
- armor `G_0435F503_6600_6342_97B2_6D9E1A85A2F2`, prototype 8127, and authored container
  `G_8F454608_E327_1341_B85B_E7A5402D4758`, prototype 3052, from
  `maps/arcanum1-024-fixed/101602821844.sec`;
- positional portal `P_00019096_0001C870_00000164_00000001` from
  `maps/arcanum1-024-fixed/122473678402.sec`;
- Black Root mayor `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`, prototype 17088, and a second source NPC
  `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, prototype 17101, with the mayor in
  `maps/arcanum1-024-fixed/96636765255.sec`.

Slot A held the food, equipped the relocated armor, kept the door open, preserved quest 1005 Accepted through the
authentic mayor response-3 transition, stored campaign variable 111, PC/mayor Gold 100/0, and the second NPC's exact
damage/alignment. Slot B used another PC tile, dropped the food in the mayor sector, returned armor to its authored
container, closed the door, set quest 1005 Completed, stored variable 222, trained Persuasion/Apprentice, and stored
Gold 1/99. Loading A -> B -> A restored every value without leakage. Accepted/no-dagger dialogue reopened on the
authentic response-11 branch and excluded the Completed-only response 5; B exposed response 5 and the completed
journal/training state.

After corrupting B, its load returned `MalformedSlot` while the active A `PersistentPlayerState` reference and state
remained exact. The run then completed drop/pickup, unequip/equip, click-navigation authority, portal close/open,
source-sector reload suppression, multi-NPC restoration, and Original -> Enhanced -> Original/current graphics
rebuilds. There was one coordinator, loader, navigation controller, interaction controller, PC presentation, and view
per ObjectID after rebuild. Final Play Mode result: **passed with 0 new warnings and 0 errors**. Temporary validation
slots were outside the repository and deleted by the harness.

## Deliberate limits and next milestone

M6B provides no player-facing save/load UI, autosave, quicksave, cloud synchronization, cross-process slot locking,
original Arcanum save compatibility, or recovery/backup browser. There is no pre-V1 session format to transform, so
the migration table contains only the explicit V1-to-current step until a later schema actually changes. Dialogue
cursor/view state, pending paths/interactions, fractional portal animation, and Unity presentation still normalize on
load as specified by M6A. Combat, followers, world travel, and broader gameplay remain outside this slice.

The exact recommended next milestone is **M6C - bounded player-facing manual save/load presentation and corrupt-slot
recovery UX over `SessionSaveSlotService`**. It should expose create/overwrite/list/load/delete with clear typed errors
while leaving all authority in the existing services. Autosave, quicksave, cloud sync, original-save compatibility,
combat, followers, and world travel should remain deferred.

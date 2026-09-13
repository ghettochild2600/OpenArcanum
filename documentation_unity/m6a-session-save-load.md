# M6A Versioned Authoritative Session Save / Load

## Pre-implementation authoritative-state audit (2026-09-13)

This inventory was recorded before implementation. M6A persists only state already owned by the M1-M5 runtime
domains. Unity scene objects remain disposable projections and the original Steam data remains read-only.

| Domain | Authoritative owner | Persist in V1? | Serialize or derive | Stable key | Versioning risk |
|---|---|---:|---|---|---|
| Current map and selected sector | `WorldMapSessionCoordinator` | Yes | Direct | normalized source map/sector path | map-name normalization |
| Production PC identity/location/ART | `PersistentPlayerState` | Yes | Direct map-global position; derive sector/local tile | persistent `ObjectID` | coordinate precision and future maps |
| Navigation destination/path | `PlayerNavigationController` / `PersistentPlayerState.Destination` | No | Normalize to standing with no destination | PC `ObjectID` | future resumable movement policy |
| Authored and dynamic object state | `WorldMapSessionCoordinator` / `PersistentObjectState` | Yes | Direct mutable state plus reconstruction/source identity | persistent `ObjectID` | new object fields and prototypes |
| Portal transition | `PortalTransitionScheduler` + object stable fields | Stable result only | Direct stable open/closed ART; discard fractional transition | portal `ObjectID` | future timed-state persistence |
| Lock/off state | `PersistentObjectState` | Yes | Direct | object `ObjectID` | future flag families |
| Placement, containment, equipment | `PersistentObjectState.Placement` | Yes | Direct typed placement/parent/worn slot | item + parent `ObjectID` | containment graph evolution |
| Stack quantity and tombstones | coordinator object/tombstone registries | Yes | Direct | object `ObjectID` | future stack semantics |
| Dynamic allocation | coordinator monotonic allocator | Yes | Direct next sequence, validated above all live/tombstoned `D_` IDs | `D_` sequence | allocator width/exhaustion |
| Character base attributes/race/gender | `CharacterStatService` | Yes | Direct authoritative inputs; effective attributes derive | critter `ObjectID` | future modifier sources |
| HP/fatigue | `CharacterVitalityService` | Yes | Direct damage and immutable source inputs; maxima/current values derive | critter `ObjectID` | death/unconsciousness later |
| XP/level/points/skills/training | `CharacterProgressionService` | Yes | Direct authoritative inputs and mutable values | critter `ObjectID` | level-cap and skill expansion |
| Alignment and reaction mutations | `CharacterDerivedStatService` | Yes | Direct mutable inputs/deltas; all calculated stats derive | critter or NPC/PC pair | future effect sources |
| Global/PC vars and flags | `CampaignStateService` | Yes | Sparse direct values | source numeric index | source range expansion |
| Quest state/timestamps/clock | `CampaignStateService` | Yes | Sparse direct values plus deterministic clock | source quest number | new quest-state values |
| Object/SAP flags and counters | `CampaignStateService` | Yes | Sparse direct values | object `ObjectID` + SAP point | new attachment families |
| Journal | `JournalProjectionService` | No | Recompute from campaign state and source quest resources | quest number | localization/resources |
| Active dialogue/trainer subview | `ProductionDialogueSession` | No | Normalize to closed | NPC/PC IDs only while active | future conversation resume |
| Unity presentation | terrain/object/player/sprite/presenter components | No | Rebuild from restored owners | none | intentionally out of format |

### Smallest V1 format

The first format is deterministic, indented JSON with an explicit `OpenArcanum.SessionSave` identifier and schema
version `1`. Its domain-oriented envelope contains `world`, `objects`, `characters`, and `campaign`. Lists are emitted
in ordinal identity or numeric-key order; dictionaries and Unity object references are not serialized. `ObjectID`
values use canonical existing keys (`A_`, `G_`, `P_`, and `D_`); source-valid null parents are JSON null. HANDLE,
Blocked, unknown, and noncanonical encodings are rejected.

Save writes a sibling temporary file, flushes it to disk, reparses and validates it, then atomically replaces an
existing destination (or atomically renames into a new destination). Failure leaves the previous valid destination
unchanged and returns a typed error.

Load reads, parses, validates every required field and relationship, and constructs a typed restore plan before it
touches the live session. Only a valid plan can replace the coordinator/service roots. Presentation is torn down,
authoritative roots are replaced in dependency order, transient dialogue/navigation/portal-transition state is
normalized, and the restored selected sector is presented again. Unknown formats, future versions, malformed fields,
duplicate IDs, missing parents, cycles, invalid worn slots/quantities/quests, and allocator collisions fail closed.

### V1 transient normalization policy

- Active click, pending interaction approach, current path nodes, and PC destination are discarded; the PC loads
  standing at the saved map-global position.
- Active dialogue and trainer subviews are closed. Persistent dialogue consequences live in campaign, inventory,
  progression, alignment, and reaction domains and are saved there.
- Opening/closing portal animation is rolled back to its last authoritative stable state; fractional animation time is
  not saved.
- Sprite frame phase, GameObjects, Transforms, renderers, MonoBehaviours, instance IDs, validation counters, and test
  state are never serialized.

### Implemented production change set

1. Strict canonical `ObjectID` parsing now covers authored numeric, GUID, positional, and session-dynamic variants.
2. Character, campaign, reaction, object, and player owners expose explicit internal export/restore boundaries; the
   serializer does not reflect private state.
3. `SessionSaveService` supplies `SaveSession(path)` / `LoadSession(path)`, typed failures, deterministic V1 JSON,
   atomic replacement, full pre-commit validation, and semantic rollback if presentation rebuild fails.
4. `WorldMapSessionCoordinator.ResetAuthoritativeSession()` unloads presentation and replaces every authoritative
   root while retaining data-source and presentation-owner bindings. A validated restore plan swaps roots in
   dependency order and only then selects/rebuilds the saved sector.
5. Dialogue, journal, use-script, portal, and inventory-capacity services are rebound to the restored owners. Unity
   presentation components remain disposable consumers.
6. Focused EditMode tests and one real-data PlayMode validator cover V1 and the M1-M5 golden session. No retail menu,
   original-save compatibility, autosave, quicksave, or cloud behavior was added.

## Implementation and validation

### V1 schema and version policy

The V1 envelope is:

```text
format: "OpenArcanum.SessionSave"
version: 1
world: currentMap, selectedSector, nextDynamicIdentity, player, tombstones
objects: source/reconstruction fields + mutable flags/ART/placement/quantity
characters: base/source inputs + vitality damage + progression/skills/training + derived inputs
campaign: sparse flags/variables + quests/timestamps + SAP attachments + reaction adjustments
```

All ordered collections serialize deterministically by canonical identity or numeric index. The loader rejects an
unknown format, any version other than 1, malformed JSON, missing top-level domains, invalid/noncanonical identity,
duplicate live/tombstoned identity, allocator collision, invalid placement/stack/character/campaign state, invalid
equipment slot, and containment cycles before touching the live session.

One source-data exception is deliberate: an unchanged authored inventory child may retain its exact
`AuthoredParentIdentity` when that parent belongs to an unloaded source sector and is therefore absent from the
in-memory snapshot. Runtime-created or relocated objects never receive this exception; their parents must be present.
The source parent is reconciled normally when its sector loads.

There is no V0 migration. Future schema work must add an explicit migration step before V1 plan construction; a
future-version file currently fails closed.

### Atomic save and transactional load

Save serializes to a uniquely named sibling temporary file, writes through and flushes it, reads it back, constructs a
validated restore plan, and then uses atomic replace/rename. A serialization, validation, or I/O failure returns a
typed result and cannot overwrite the previous destination. The failed-save test preserves exact prior bytes.

Load performs read -> strict parse -> full domain/referential validation -> isolated restore-plan construction before
commit. Validation failures preserve the same live player, campaign, and presentation. At commit, the coordinator
unloads the old projection, replaces object/player/character/campaign roots, rebuilds dependent services, and presents
the selected sector. If presentation fails, the previously captured valid semantic snapshot is reapplied.

### Golden real-data session

The PlayMode fixture used the normal `TestTerrain` production composition and source data, then established:

- production PC moved to a real walkable mayor-sector tile;
- A-to-B source-sector selection and retained state across four real sectors;
- picked-up food `G_8781D726_74FE_0846_AD0A_88EE591B6383` (prototype 10078);
- equipped armor `G_0435F503_6600_6342_97B2_6D9E1A85A2F2` (prototype 8127);
- Ammo `G_9239E097_A8D2_C147_9F58_76077340C60E` (prototype 7059), split, merged to 60, with the consumed split
  `D_0000000000000001` tombstoned;
- live runtime item `D_0000000000000002`;
- real positional door `P_00019096_0001C870_00000164_00000001` changed to open;
- female PC input, HP/Fatigue damage, 3,000 XP / level 2, points, one purchased Persuasion point, Persuasion
  Apprentice training, Alignment 50, and mayor reaction 53;
- completed quest 1005 with timestamp and journal projection, sparse campaign variables/flags, mayor SAP-local state,
  and the authentic training-payment consequence of 1 PC Gold / 99 mayor Gold;
- an active mayor conversation and pending navigation destination immediately before save, to prove both normalize.

The generated validation file was 2,268,049 bytes with 2,167 serialized identity records. Inspection found the V1
marker and canonical readable keys, and found no `GameObject`, presentation instance ID, project path, proprietary
asset payload, or dependency on the original data location. It remains in Unity's temporary cache and is not committed.

### Destroy/restore and continued-gameplay proof

The validator saved to Unity's temporary cache outside `GameData`, captured old root references, called
`ResetAuthoritativeSession`, waited for destruction, and verified no player, selected sector, object states, or old PC
presentation remained. Loading created new player/object/character/campaign roots, rebuilt the mayor sector, projected
one PC, and rebound navigation to it. It restored the exact map-global position, item/equipment/stack/door/character/
campaign values above; dialogue loaded idle and destination loaded null.

Post-load operations all succeeded through production APIs: authentic mayor dialogue selected the Completed/trainer
branch, the real food dropped and was picked up again, the PC completed a real navigation route, the next item received
`D_0000000000000005`, and the real door closed/reopened. Original -> Enhanced -> Original rebuilds retained state and
left one coordinator, object owner, navigation controller, interaction controller, WorldObjects root, PC, and
presentation per persistent identity.

### Validation results (2026-09-13)

- Unity 6000.0.71f1 compilation: clean.
- M6A focused EditMode: 25 passed, 0 failed/skipped/inconclusive.
- Required M1-M5/M6A regression matrix: 332 passed, 0 failed/skipped/inconclusive.
- Complete EditMode suite: 503 passed, 0 failed/skipped/inconclusive.
- Golden save/reset/load/post-load PlayMode validation: passed.
- PlayMode log tracking: 0 warnings, 0 errors.
- Graphics: Original -> Enhanced -> Original passed; the user-owned serialized `graphicsMode: 1` asset was neither
  staged nor normalized.

### Known omissions and next milestone

V1 is an OpenArcanum session snapshot, not an original Arcanum save and not a final retail format. There are no slots
UI, migration readers, autosaves/quicksaves, cloud sync, combat/follower/travel state, thumbnails, metadata browser,
or partial/map-streamed snapshots. Active dialogue/path/interaction and fractional animation resume are intentionally
out of scope.

The recommended next milestone is **M6B — version migration/slot orchestration and broader real-session coverage**:
define explicit V1-to-future migration boundaries and a domain-level slot catalog without building final UI, then add
coverage for more maps and source-authored external-parent relationships. Do not start combat, followers, barter, or
original Arcanum save compatibility as part of M6B.

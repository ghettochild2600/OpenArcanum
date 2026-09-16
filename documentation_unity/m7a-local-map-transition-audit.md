# M7A Local Map Transition Audit and Implementation

## Scope and outcome

M7A is complete (2026-09-15). This retained source audit and implementation record covers the first bounded
authoritative local map transition only. It excludes world-map
travel, encounters, followers, combat, broad script support, transition effects, and save-format changes.

The selected GREEN fixture is the passive exit from **Bates Mansion Lev 1** (MapList id 12) to the overland
**Arcanum1-024-fixed** map (MapList id 1):

- source resource: `maps/bates mansion lev 1/map.jmp`
- source trigger band: global tiles `x=109..110`, `y=92..94`, all in
  `maps/bates mansion lev 1/67108865.sec`
- destination: map id 1, global tile `(61976,65664)`, sector
  `maps/arcanum1-024-fixed/68853695432.sec`
- flags: 0 on all six records
- activation: passive arrival on an invisible jump-point tile; there is no target object or SAP_USE script
- facing: not encoded by `JumpPoint`; M7A preserves the PC's facing and normalizes animation to stand
- physical return: not in this slice. The overland map has no reciprocal `map.jmp`; returning to this mansion belongs
  to later area/world-map entrance work.

Both exact sector resources exist in the mounted clean retail corpus. The destination is a normal loaded sector, not a
Unity scene, fabricated trigger, or NPC-derived location.

## Retail data and engine semantics

`rules/MapList.mes` contains 81 consecutive entries starting at key 5000. Runtime map ids are the one-based positions
in that run (`key - 4999`); the folder name is the first comma-separated value. A playable tile is a packed signed
64-bit location with X in the low 32 bits and Y in the high 32 bits. A sector is 64x64 tiles, so the sector filename is
`(x >> 6) | ((y >> 6) << 26)` and local coordinates are the low six bits.

`map.jmp` is an Int32 record count followed by 32-byte records: UInt32 flags, padding, packed source location, Int32
destination map, padding, and packed destination location. The clean corpus audit found:

- 81 MapList entries;
- 79 maps with a `map.jmp` resource;
- 17 non-empty jump tables;
- 45 jump-point records;
- no missing source sectors for any record;
- all observed flags equal to zero.

The decompiled retail path is `map_process_jumppoint`: lookup the player's current tile, copy the record's destination
map and location into `TeleportData`, then call `teleport_do`. Map change is therefore gameplay/session lifecycle, not
presentation-owned navigation. `map_open` closes the old map before loading the new map's properties, mobiles, dynamic
objects, and terrain. M7A mirrors only destination resolution and authoritative lifecycle; fades, sound, events,
followers, and broader map-module behavior remain deferred.

The mounted corpus also contains 22 records whose signed destination map is non-positive. Existing documentation only
proves zero as a same-map sentinel; M7A does not guess at the additional negative-value cases. They remain explicitly
unsupported until that source branch is traced. This does not affect the selected Bates fixture, whose destination is
the ordinary positive MapList id 1.

## Candidate classification

| Class | Candidate | Evidence | Decision |
|---|---|---|---|
| GREEN | Bates Mansion Lev 1 id 12 -> Arcanum1-024-fixed id 1 | six passive tiles; both sectors exist; positive destination id; no script/object/quest/combat dependency | selected |
| YELLOW | K'na Tha / Dernholm pits / Arronax scripted teleport portals (scripts 2591/2595/2643) | real placed SAP_USE portals and `SAT_TELEPORT`; requires production opcode admission and active-object interaction policy | defer until the passive lifecycle is proven |
| YELLOW | Tulla battle-map remote jumps | real records, but several use a non-positive destination-map value and sit inside battle progression | do not infer same-map semantics or begin combat-state work |
| YELLOW | VOID-Kerghans Castle remote jumps | valid same-map remote pairs, but do not prove the cross-map lifecycle and live in a late-game combat context | defer |
| RED | world-map/area entrances back into Bates/Tarant | no reciprocal passive record; requires area selection, known-area state, and world-map travel | M7B or later |

## Final ownership and call graph

```text
click/global destination
  -> PlayerNavigationController owns transient route following
  -> TileRouteFollower reports EVERY entered source-grid waypoint
  -> WorldMapSessionCoordinator.SetMovementState
       -> PersistentPlayerState owns map-global PC position/ART/destination

exact entered passive jump tile
  -> PlayerNavigationController asks coordinator.RequestCurrentJumpPoint(PC ObjectID)
  -> WorldMapSessionCoordinator.RequestMapTransition(map id + exact global tile)
       -> MapTransitionResolver: MapList -> map.jmp -> map.prp bounds -> sector existence
       -> reject invalid/busy requests BEFORE teardown
       -> clear destination -> ClearSelectedSector
            -> cancel dialogue -> SectorUnloading cancels pending approach
            -> navigation unbind/cancel -> terrain + object owners unload
       -> same PersistentPlayerState.RestoreMapPosition(destination, STAND + source facing)
       -> SelectSector -> both presentation owners rebuild
       -> ProductionPlayerLifecycle rebinds same PC -> one-frame standing entry hold
       -> discard residual movement from the old route; normal new-map navigation resumes

contiguous sector edge
  -> PlayerNavigationController
  -> WorldMapSessionCoordinator.TryTransitionPlayer (same map only)
       -> ClearSelectedSector
       -> terrain + object presentation owners unload
       -> relocate same PersistentPlayerState
       -> SelectSector
       -> both owners rebuild
       -> ProductionPlayerLifecycle binds the same PC identity

source data
  -> WorldObjectSectorLoader mounts the read-only VFS and binds parsers/services
  -> WorldMapSessionCoordinator remains the only map/sector and gameplay-lifecycle authority
```

`WorldObjectSectorLoader`, terrain owners, `ProductionPlayerLifecycle`, `WorldObject`, sprite owners, and the Unity
scene are disposable presentation/data adapters. None may become the map-transition authority.

## Implemented bounded change set

1. Add a presentation-independent typed jump source id, destination, result/failure model, and strict retail resolver over the
   existing `MapList` and `JumpPointReader`.
2. Bind that resolver and a read-only sector-existence probe from the mounted source-data adapter to the coordinator.
3. Add one coordinator-owned cross-map transition request that validates actor, current map/tile, source destination,
   and destination sector before teardown; rejects busy/invalid requests without changing the live map.
4. Reuse the existing presentation teardown/select/rebind lifecycle while allowing a validated map change. Preserve
   the same `PersistentPlayerState`, ObjectID, character/campaign/inventory state, and ART facing; clear transient route,
   interaction, and dialogue state.
5. Have normal source-grid movement ask the coordinator for a passive jump only when the PC lands exactly on a tile.
6. Add focused EditMode tests for decoding/resolution, request guards, success/rollback, stable identity/domain state,
   destination map/sector, transient cancellation, and post-transition save/load compatibility.

No scene reload, special renderer, second map owner, new PC identity, or M7B world-map path is needed.

The waypoint callback is optional for existing `TileRouteFollower` callers. A large frame cannot skip an intermediate
passive jump: successful activation stops remaining source-route movement immediately. Fractional source-grid movement,
eight-direction step costs, contiguous-sector navigation, ART resolution, and rendering remain unchanged.

## Identity, failures, and persistence contract

`MapTransitionSourceId` is `(positive MapList id, exact global source tile)`, not a Unity object instance. The destination
contains map id/path, global tile, derived sector/local tile, and optional facing. Retail jump records have no facing;
the coordinator preserves departure facing and clears WALK/frame state to STAND. Gameplay PC identity remains
`G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`; no NPC is selected, cloned, or substituted.

The resolver is a read-only VFS adapter bound by `WorldObjectSectorLoader`. The coordinator alone authorizes actor,
selected source map, exact PC tile, and lifecycle. Missing/malformed tables or properties, missing source/destination
entries, conflicting destinations, invalid coordinates, missing sectors, non-positive sentinels, and remote same-map
destinations have explicit failures. Reentrant requests are Busy. Failed preflight never clears either presentation
owner or mutates authoritative gameplay state. A false destination presentation result restores the previous map,
position, ART, and both source owners with the same PC (`PresentationFailed`); failure of that restoration is explicitly
`RollbackFailed`. Transient route/dialogue/approach state is deliberately normalized rather than resurrected on rollback.
The focused suite exercises destination presentation rejection with test owners; no retail resource was removed to
simulate failure in Play Mode.

M3-M6 domain owners and retained source/destination object states are not replaced by a successful map transition.
V1 already stores map path, selected sector, and map-global PC state, so no save schema/version change is needed.
Loading V1 uses the existing transactional service-root restoration, with stable identities rather than promises of
the same in-memory references across save/load.

## Final validation (Unity 6000.0.71f1, 2026-09-15)

Computer Use physically clicked the authentic Bates floor tile through `PlayerClickMoveInput`, then physically clicked
destination ground to resume navigation. The editor-only harness only prepares real-data state and observes the
transition under test; it never calls a transition/teleport request to fake activation.

- Source id 12, sector `67108865.sec`, authentic MapList start global `(104,92)`, local `(40,28)`.
- Literal click at local `(45,28)` entered source global `(109,92)` and selected destination id 1 exactly once.
- Destination sector `68853695432.sec`, global `(61976,65664)`, local `(24,0)`; same PC, departure facing **5**, STAND.
- Subsequent literal destination click to local `(23,0)` moved the PC normally.
- Same PC/character references and all **1807** pre-registered object-state references survived transition. Exact V1
  snapshot comparison excludes only intentional map/PC movement/ART fields. Attributes, vitality damage, XP/level/points,
  purchased skill/training, derived inputs/alignment, equipped authentic armor, quantity-60 Ammo, Gold, dynamic Food,
  campaign flags/variables, accepted quest/timestamp, and retained world state were unchanged. Journal remains a
  read-only projection of the preserved quest state.
- A second independent normal pickup-approach route targeted dynamic Food on later local `(46,28)`. The first crossed
  passive tile `(45,28)` activated once; pending pickup became Cancelled and its item remained in the source world.
  Attempted stale source pickup in the destination was rejected (`ItemNotInWorld`). Old route/destinations were clear.
  Dialogue cancellation remains the existing `ClearSelectedSector` lifecycle, covered by the dialogue/save regressions;
  this passive floor fixture does not itself start a live conversation.
- One coordinator, terrain owner, loader, WorldObjects root (including inactive roots), PC lifecycle/runtime/view,
  navigation and interaction controller; no duplicate sprite-owner identities after both activations, loads, or rebuilds.
- V1 temporary test slot saved the **exact authored arrival**, then location/Gold changed and load restored exact JSON,
  destination map, position, identity, and domain state. Post-load navigation and production dynamic Food drop/pickup
  succeeded. Test slots are under Unity's temporary cache, not player save directories, and are not committed.
- Original -> Enhanced -> Original rebuild and destination unload/reload preserved exact position and normalized
  domain snapshot; ownership remained unique. The user's graphics configuration asset was not changed or staged.
- No reciprocal overland `map.jmp` exists. A source V1 snapshot was restored solely to set up the second independent
  activation; this is **not** an authentic A -> B -> A return. No reverse transition was invented.

| Validation | Passed | Failed | Skipped | Inconclusive |
|---|---:|---:|---:|---:|
| Resumed original focused baseline | 14 | 0 | 0 | 0 |
| Final focused M7A (eight complete-state preflight cases + two waypoint cases added) | 24 | 0 | 0 | 0 |
| Required M1-M6C regression matrix | 390 | 0 | 0 | 0 |
| Complete EditMode | 585 | 0 | 0 | 0 |

Regression breakdown: M6C 26, M6B 32, M6A 25, M5C 21, M5B 18, M5A 16, M4D 25, M4C 29, M4B 20, M4A 13,
M3E 21, M3D 18, M3C 13, M3B 13, M3A 8, M2B 8, M2A 16, player navigation 21, M1A 7, M1B 11,
world session 27, portal ART 2. Final compilation is clean. Final real Play Mode validation: **PASS, warnings 0/errors 0**.
EditMode dialogue compatibility fixtures intentionally emit warnings for unsupported effects; these are not new
runtime warnings from M7A.

## Remaining boundaries and exact next task

M7A proves positive-id passive cross-map lifecycle, not every teleport branch. Non-positive destination-map semantics,
remote same-map jumps, scripted `SAT_TELEPORT`, transition effects/events/followers, world-map/area entrances and return,
travel time/encounters, and broader day/night behavior remain deferred. Observed flags are zero; this slice does not
claim arbitrary jump flags are audited. No M7B work was begun.

Recommended next task: **M7B — source-audit and implement one bounded authentic overland/area entrance and return
path**, beginning with the Bates entrance relationship. Trace the actual entrance/discovery and destination-coordinate
policy before selecting the smallest supported fixture. Reuse the coordinator and V1 state; do not fabricate a
reciprocal passive jump or bundle encounters, combat, followers, or broad travel/time systems into that slice.

## Primary references

- `arcanum-ce/src/game/map.c`: `map_process_jumppoint`, `map_open`, `map_get_name`, and MapList loading.
- `arcanum-ce/src/game/jumppoint.c` / `jumppoint.h`: jump-table storage and lookup.
- `arcanum-ce/src/game/teleport.c` / `teleport.h`: authoritative teleport lifecycle.
- Mounted clean Steam 1.0.7.4 `modules/Arcanum.dat` and `rules/MapList.mes`.

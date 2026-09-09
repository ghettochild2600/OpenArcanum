# M1B Cross-Sector Navigation

Completed on 2026-09-08 in Unity 6000.0.71f1 on `feature/player-navigation`.

## Coordinate and ownership model

Arcanum's canonical map location is a global tile coordinate `(x,y)`. A source sector is 64×64 tiles:

- `sectorX = globalX >> 6`
- `sectorY = globalY >> 6`
- local coordinates are the low six bits
- the decimal sector filename is `sectorX | (sectorY << 26)`

`PersistentPlayerState.MapPosition` is authoritative. Its sector path and fractional local presentation position are
derived through `SectorCoordinate`; they are not competing state. The state also owns one map-global final destination.
Unity objects remain disposable projections of this session state.

## Final ownership and call graph

```text
local click or map-global destination
  -> PlayerNavigationController records one global intent in PersistentPlayerState
  -> destination in current sector?
       yes -> DeterministicTilePathfinder -> TileRouteFollower
       no  -> CrossSectorBoundaryPlanner
              -> cardinal neighbor reducing remaining sector distance
              -> deterministic reachable source-side boundary
  -> walk to boundary with source WALK/facing ART
  -> WorldMapSessionCoordinator.TryTransitionPlayer
       capture/unload A
       relocate the same session PC to wrapped entry
       select terrain + world objects for B
       ProductionPlayerLifecycle projects/binds the same GUID
  -> validate B-side entry and final-sector reachability
       rejected -> coordinator returns to A, excludes that exit for this intent, tries next legal exit
       accepted -> present exact entry for one Update, then resume the preserved intent
  -> repeat for additional sectors, or arrive and clear intent in STAND
```

The coordinator is the only sector-selection authority. `TileMapDemo`, `WorldObjectSectorLoader`, `WorldObject`,
`WorldObjectSpriteOwner`, and `ProductionPlayerLifecycle` provide terrain/object/PC presentation and binding only.

## Deterministic routing contract

`CrossSectorBoundaryPlanner` considers cardinal neighbors that reduce distance to the destination sector. Candidate
source exits must exist, be traversable, and be reachable through the current `SectorNavigationMap`. Stable cost,
direction, cross-track, and boundary-index ordering choose one result. If the loaded target entry is blocked or cannot
reach a final destination in that sector, navigation returns through the coordinator, excludes only that source exit
for the active intent, and deterministically tries another. If none remains, the PC stops safely in the last valid
sector and the intent is cleared.

The destination stays map-global across unload/rebind. Each loaded sector receives a normal 64×64 source-grid route;
there is no Unity NavMesh, global A*, presentation-owned position, or special-case PC renderer.

## Exact entry presentation contract

Sector loading is synchronous and can enlarge the following frame's `Time.deltaTime`. Without a boundary hold, the
route follower could advance before the exact wrapped entry was ever presented. A boundary transition therefore arms
`SectorEntryFrameHold`. The first `Update` after the successful rebind presents the exact entry with the preserved WALK
facing and does not consume movement distance. The next movement update resumes normally. The hold is armed only by a
sector transition/retry; within-sector routes are unchanged. It does not mutate global position, clear destination
intent, reset the accepted route, or add a time-based gameplay delay.

## Production PC and persistent state

The M1A gameplay identity remains `G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`; presentation ART remains source-valid
`0x28100000` through the ordinary critter resolver. A transition relocates and re-projects that same
`PersistentPlayerState`. Returning to a visited sector reuses the same ObjectID-indexed `PersistentObjectState`
instances and preserves their ART, off, lock, and portal-open values. Visual rebuilds preserve PC global position,
selected sector, GUID, destination, facing/ART, and movement state.

## Validation

- Focused `M1BNavigation`: 11 passed, 0 failed, 0 skipped.
- Focused `M1Lifecycle`: 7 passed, 0 failed, 0 skipped.
- Focused `PlayerNavigation`: 21 passed, 0 failed, 0 skipped.
- `WorldSessionStateTests`: 27 passed, 0 failed; `PortalArtResolverTests`: 2 passed, 0 failed.
- Complete EditMode suite: 239 passed, 0 failed, 0 skipped.
- Real sectors: `maps/arcanum1-024-fixed/101602821844.sec` (A) and adjacent
  `maps/arcanum1-024-fixed/101602821845.sec` (B).
- Computer Use validation completed A→B→A→B→A. Every leg showed the exact route-selected wrapped entry, retained the
  same PC identity, continued automatically, and arrived in STAND. The repeated trip retained one coordinator,
  terrain owner, object loader/root, lifecycle, navigation controller, PC runtime/presentation, and PC sprite owner.
- A missing west neighbor was rejected without crossing or position loss. Target-side-invalid exits support
  deterministic fallback.
- Original/Enhanced/reverted visual rebuilds during the target route preserved the full navigation state.
- Returning to A reused every captured persistent object state and preserved object/portal values.
- Final Unity Console: 0 warnings, 0 errors.

## Remaining limitations

M1 is complete for contiguous source-sector traversal. Normal click input remains current-sector ground targeting;
there is no world-map destination UI, map-to-map teleport/jump flow, save serialization, floating origin, dynamic
critter reservations/avoidance, or scene-reload continuation. Interaction, inventory, combat, dialogue, character
progression, scripts, and save/load remain later roadmap milestones and were not started.

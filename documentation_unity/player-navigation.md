# Player navigation

Implemented on `feature/player-navigation` and validated in Unity 6000.0.71f1.

## Runtime boundary

Player navigation uses the decoded Arcanum sector grid; it does not use Unity NavMesh geometry or colliders:

`PlayerClickMoveInput` -> `PlayerNavigationController` -> `DeterministicTilePathfinder` -> `TileRouteFollower` ->
`WorldMapSessionCoordinator` -> `WorldObject` -> existing sprite presentation

`WorldObjectSectorLoader` builds one `SectorNavigationMap` from the same sector bytes and object records used for the
visible map. `ProductionPlayerLifecycle` projects the deterministic session-owned PC and explicitly binds navigation
to it. No production or validation path silently selects a sector-authored NPC.

Click input converts Game-view screen coordinates through the existing camera and `IsoProjection.WorldToTile`. A mouse
gesture exceeding six pixels remains a camera drag and is not movement intent. Invalid or blocked destinations are
rejected before replacing the active route; a valid later click cancels the old route and starts a new one.

## Source walkability semantics

The sector map combines the original data layers without changing world coordinates or placement:

- the explicit 64x64 sector block mask;
- tile `/b` flags from `art/tile/tilename.mes`;
- non-walkable facade ART;
- placed containers, scenery, projectiles, PCs, NPCs and traps unless `OF_NO_BLOCK` is set;
- directional wall edges and live portal open/closed state.

Walls and portals block an authored crossing edge rather than their whole tile. Diagonal traversal checks both adjacent
odd-direction edge routes, matching the original traversal-cost corner rule and preventing wall corner cutting. The
controlled critter is removed from static occupancy without ignoring another occupant on the same tile.

## Routing and movement

`DeterministicTilePathfinder` performs eight-direction A* over each loaded 64x64 sector. Every source-grid step has equal
base cost, a one-point direction-change tie breaker, fixed neighbor order and stable index tie resolution. Identical
inputs therefore return identical routes. Failed searches return no partial route.

`TileRouteFollower` advances smoothly in fractional tile coordinates at a configured tiles-per-second rate. All eight
directions consume one tile step, preserving the grid's movement semantics. Facing comes from the existing source
direction mapping. While moving, critter ART uses action 1 (WALK); arrival, cancellation and reload use action 0
(STAND). The runtime frame bits remain owned by the existing sprite animator.

Position, ART state, and map-global destination intent pass through `WorldMapSessionCoordinator`. `SectorCoordinate`
derives the current 64×64 sector and local presentation position from the authoritative global position.
`CrossSectorBoundaryPlanner` chooses deterministic cardinal exits and the coordinator switches terrain and objects
together. The same PC is re-projected at the wrapped entry, held there for one Update so a load-inflated delta cannot
skip the pose, and then automatically continues. Target-side blocked or unreachable final-sector entries are rejected
and the next legal source exit is tried deterministically. Original/Enhanced rebuilds cannot change navigation state.

## Validation

The focused Unity `PlayerNavigation` EditMode category passed 21/21 tests; `M1BNavigation` passed 11/11. Coverage includes tile/world round trips, explicit blocks,
directional wall edges, obstacle detours, unreachable destinations, deterministic output, all eight facing mappings,
smooth completion, route replacement, WALK/STAND ART bits and interrupted-movement persistence. The complete EditMode
suite passed 239/239 with zero failures or skips.

Computer Use validation ran TestTerrain on real sectors `101602821844.sec` and `101602821845.sec`. The production PC
completed A→B→A→B→A with strict exact-entry observations, preserved global intent, correct WALK/facing and STAND,
graphics rebuilds during traversal, missing-edge rejection, stable persistent object/portal values, and no duplicate
runtime or presentation owners. The final Console contained informational asset/load messages only, with no warnings
or errors. See [`m1b-cross-sector-navigation.md`](m1b-cross-sector-navigation.md).

## Remaining limitations

- Contiguous source-sector traversal is implemented; map-to-map jump/teleport flow and a world-map destination UI are not.
- Character-creation/save-data initialization of the production PC remains future work; the runtime lifecycle itself is authoritative.
- Dynamic blocker movement, critter-to-critter avoidance, interaction range, combat movement and script events are not
  implemented.
- Save-game serialization remains outside the in-memory session state.

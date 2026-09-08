# Player navigation

Implemented on `feature/player-navigation` and validated in Unity 6000.0.71f1.

## Runtime boundary

Player navigation uses the decoded Arcanum sector grid; it does not use Unity NavMesh geometry or colliders:

`PlayerClickMoveInput` -> `PlayerNavigationController` -> `DeterministicTilePathfinder` -> `TileRouteFollower` ->
`WorldMapSessionCoordinator` -> `WorldObject` -> existing sprite presentation

`WorldObjectSectorLoader` builds one `SectorNavigationMap` from the same sector bytes and object records used for the
visible map. The controller selects a real persistent PC runtime object when one exists. Because the current test sector
does not author the dynamically-created original player, TestTerrain alone enables a narrowly-scoped development
fallback to the stable first real NPC. Shipping behavior does not silently select an NPC.

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

`DeterministicTilePathfinder` performs eight-direction A* over a single 64x64 sector. Every source-grid step has equal
base cost, a one-point direction-change tie breaker, fixed neighbor order and stable index tie resolution. Identical
inputs therefore return identical routes. Failed searches return no partial route.

`TileRouteFollower` advances smoothly in fractional tile coordinates at a configured tiles-per-second rate. All eight
directions consume one tile step, preserving the grid's movement semantics. Facing comes from the existing source
direction mapping. While moving, critter ART uses action 1 (WALK); arrival, cancellation and reload use action 0
(STAND). The runtime frame bits remain owned by the existing sprite animator.

Position and ART state pass through `WorldMapSessionCoordinator`, so Original/Enhanced visual rebuilds cannot change
the route position. Unloading mid-route retains the exact fractional position and restores STAND at the same facing;
the player can bind and route again after reload.

## Validation

The focused Unity EditMode category passed 21/21 tests. Coverage includes tile/world round trips, explicit blocks,
directional wall edges, obstacle detours, unreachable destinations, deterministic output, all eight facing mappings,
smooth completion, route replacement, WALK/STAND ART bits and interrupted-movement persistence. The complete EditMode
suite passed 221/221 with zero failures or skips.

Computer Use validation ran TestTerrain with real `maps/arcanum1-024-fixed/101602821844.sec` data. A physical Game-view
ground click was accepted by `PlayerClickMoveInput`. The repeatable real-sector validator then verified a persistent
NPC fallback (`G_1CA8B264_6113_F24C_BFDD_ED869173A4A7`), valid source edges, active-route replacement, blocked-target
rejection, continued movement across a graphics rebuild, fractional position plus STAND restoration across
unload/reload, and successful movement after reload. It completed at tile `(36,58)` and logged PASS. The final Console
contained informational asset/load messages only, with no warnings or errors.

## Remaining limitations

- Navigation is currently sector-local; crossing sector/map boundaries is not yet connected to a shared map owner.
- The production player lifecycle still needs to create/bind the actual PC from character selection or save data.
- Dynamic blocker movement, critter-to-critter avoidance, interaction range, combat movement and script events are not
  implemented.
- Save-game serialization remains outside the in-memory session state.

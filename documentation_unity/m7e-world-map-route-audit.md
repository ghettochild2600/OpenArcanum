# M7E bounded authoritative world-map route/travel audit

This document records the source findings, final implementation contract, and
validation evidence for M7E. The implementation is limited to route geometry,
a synchronous authoritative travel lifecycle, and authentic arrival. Time,
encounters, followers, and world-map animation remain outside this slice.

## Primary evidence

The engine reference is the local `arcanum-ce` repository at commit
`a7ff41b300ef712f0e7d088183a3d08957110cdd`, principally `src/game/path.c`,
`src/game/sector.c`, `src/game/terrain.c`, `src/game/area.c`,
`src/game/teleport.c`, `src/ui/wmap_ui.c`, and `src/ui/wmap_rnd.c`. Retail
records are read from the mounted clean Steam module without extraction or
modification.

## A. Authoritative route geometry

World travel uses the START_MAP's global gameplay-tile coordinate space, not
world-map bitmap pixels and not a local sector's `0..63` coordinates. A route
starts at `sector_id_from_loc(source)` and ends at
`sector_id_from_loc(destination)`, so each route step crosses one 64x64 world
sector. The area's exact `gamearea.mes` location remains the final waypoint.

`wmap_path_make` is deterministic sector routing, not the local tile A* used by
ordinary character navigation and not a graph of authored world-map nodes. It
walks in the source `sector_rot` direction toward the target. When the next
sector is blocked it scans forward for a clear sector and invokes the source's
bounded 16x16 A* detour, with cost 10 per sector and cost 1 for a direction
change. Routes are capped at 5,000 rotations. There is no separate route-node
resource.

A sector is unavailable when it is listed in the current map's `map.sbf`, or
when no authored `.sec` exists and either packed terrain base in
`terrain.tdf` has the `/b` flag from `terrain/terrain.mes`. This is the source
water/mountain/other impassability boundary. An authored sector overrides the
generated terrain blocker exactly as the original condition does.

## B. Authoritative travel lifecycle

The original UI stores up to 30 route waypoints and one contiguous direction
buffer. Pressing Travel enters `WMAP_UI_STATE_MOVING`, hides the PC, stops its
local attack/movement presentation, and schedules a world-map time event.
Each callback advances one sector and keeps a derived current world location.
Finishing the final waypoint snaps to an eligible nearby area and leaves the
moving state. Pressing Travel while already moving cancels at the current
derived route location.

M7E has no time-driven world-map UI, so it will represent the complete source
route and the Planning/Travelling/Arriving/Completed phases but execute them
synchronously. Mid-route cancellation depends on a future incremental driver
and current-route-position ownership and is therefore deliberately deferred;
no invented cancellation rule will be exposed.

## C. Destination arrival

World-map arrival does **not** resolve `AreaId` to one of the local maps whose
`MapList` entry carries that area. On completion, `wmap_ui_teleport` always
teleports the same PC to `map_by_type(MAP_TYPE_START_MAP)` at the exact area
global location. That position determines the destination sector and local
tile. Any scenery, portal, or passive jump from the overland position into a
separate local map is a later independent transition.

Accordingly M7E must preflight and reuse the existing M7 transition pipeline
for same-START_MAP relocation. It must not invent an area-to-local-map table or
reuse the M7B Bates physical entrance as K'na Tha arrival metadata.

## D. Travel time (audited, deferred)

The world-map callback computes distance between successive derived world-map
positions, divides by 64, and advances the game clock by that many hours. Its
real-time callback delay is 62 milliseconds after the first 50-millisecond
event. The audited path contains no party-speed statistic or terrain-speed
multiplier. M7E emits no elapsed time and does not advance the clock.

## E. Random encounters (audited, deferred)

Random encounters are a separate `TIMEEVENT_TYPE_RANDOM_ENCOUNTER` subsystem.
It schedules checks 300-700 in-game minutes apart, reads the current derived
world location from the moving world-map UI, then applies frequency, power,
day/night, town-map, terrain, and encounter-table rules. An encounter closes
the world map and interrupts travel. M7E neither schedules nor rolls encounters.

## F. Followers/party (audited, deferred)

The final source teleport recursively relocates eligible followers and carried
objects. OpenArcanum has no bounded authoritative follower domain yet. M7E
preserves the production PC and all of its existing persistent domains but does
not fabricate follower state or relocation.

## G. World-map presentation (audited, deferred)

Bitmap coordinate conversion mirrors global sector X, uses sector Y directly,
draws waypoints and compass state, and animates one route sector per callback.
These are presentation/time-driver concerns. The existing M7D proof presenter
may submit the typed request, but it must not own the route or travel phase.

## Selected GREEN route

The preferred K'na Tha area 58 candidate was tested first against the retail
`terrain.tdf`, `terrain/terrain.mes`, `map.sbf`, and authored-sector overlay.
Both endpoints are passable, but the direct source algorithm cannot resolve
the intervening blocked-terrain geometry with its bounded 16x16 detour. M7E
does not replace that result with an unrelated global pathfinder.

The bounded GREEN proof route is the canonical Tarant destination:

- source START_MAP/overland tile: authentic Bates return position
  `(61976,65664)`, sector `(968,1026)`, local tile `(24,0)`;
- destination: Tarant, area 21;
- destination START_MAP tile: `(62243,65664)`, sector `(972,1026)`, local tile
  `(35,0)`;
- route: four deterministic east-sector rotations, from sector `(968,1026)`
  through `(969,1026)`, `(970,1026)`, `(971,1026)` to `(972,1026)`;
- arrival map: retail START_MAP id 1 (`Arcanum1-024-fixed`), sector resource
  `maps/arcanum1-024-fixed/68853695436.sec`;
- arrival facing: no world-map source facing is defined, so the existing M7
  transition preserves the PC's current facing.

Tarant is a valid immutable AreaList destination and becomes selectable only
through the existing CampaignStateService discovery authority. The proof does
not infer Tarant as the source, reuse bitmap coordinates, or bypass arrival
preflight.

## Save/load policy

The bounded execution is synchronous and returns to Idle before its command
returns. Only the already-persistent PC map position, selected sector, domain
state, and M7C known-area set change. No active mid-route state can be saved;
V1 therefore remains unchanged. Stable post-arrival save/load is required.

## H. Final implementation and ownership

`WorldObjectSectorLoader` binds one immutable `WorldMapTravelSource` from the
retail `MapList`, shared M7 transition resolver, VFS existence query, and VFS
reader. `WorldMapSessionCoordinator` owns the resulting
`WorldMapTravelService`; authoritative reset or V1 restore discards only the
transient service instance and retains the immutable source binding.

The existing M7D presenter still derives a source-backed
`WorldMapTravelRequest` and emits its observer event, but it now submits that
request to the coordinator. It owns no route, current sector, travel phase,
arrival, or rollback state. A successful command closes the presenter; a
failed command leaves it available for another selection.

The production call graph is:

```text
literal destination click
  -> ProductionWorldMapDestinationPresenter.Select
  -> WorldMapDestinationProjection.TrySelectWorldArea
  -> WorldMapSessionCoordinator.RequestWorldMapTravel
  -> WorldMapTravelService.Execute
       -> WorldMapTravelSource.TryResolve
       -> WorldMapRoutePlanner.TryPlan
       -> MapTransitionResolver.ResolveDestination
       -> WorldMapSessionCoordinator.ApplyResolvedWorldMapTravel
       -> existing transactional M7 transition pipeline
```

`WorldMapTravelService` admits only the same persistent production PC on the
retail START_MAP, standing on an exact global gameplay tile. It revalidates the
request against the current immutable area projection, verifies the selected
sector belongs to the one START_MAP, plans against the decoded terrain, and
preflights the destination sector before exposing any active lifecycle phase.

The successful synchronous lifecycle is exactly:

```text
Idle -> Planning -> Travelling -> Arriving -> Completed -> Idle
```

A reentrant command observed during `Travelling` returns `Busy`; it cannot
replace the active route or cause a second arrival. The route and phase live in
the coordinator-owned service, never in Unity presentation.

## I. Transaction and failure contract

Missing or malformed source data, missing production PC, wrong actor,
non-START_MAP context, unknown destination, tampered request coordinates,
unroutable terrain, and unavailable arrival all fail before session or
presentation mutation. K'na Tha exercises the authentic `RouteUnavailable`
path and remains at the Bates return position with byte-for-byte identical V1
session JSON.

After arrival begins, the existing M7 transaction snapshots the complete
authoritative session, clears the source presentation, applies the exact
destination sector/tile, and asks the registered presentation owner to render
it. Destination presentation rejection restores the complete source session
and source presentation. A failed restore is surfaced distinctly as
`RollbackFailed`; no partial success is reported.

The same persistent PC object and stable identity cross travel. M3 inventory,
M4 character/vitality/progression/derived state, M5 campaign state, M6 save
state, M7 discovery, and deterministic dynamic identities remain owned by the
session. Local navigation intent is transient and is normalized by the shared
transition pipeline.

## J. Physical and automated validation

Validation used Unity 6000.0.71f1 and the mounted clean Steam 1.0.7.4 data:

- production, test, and editor assemblies compiled cleanly;
- focused M7E EditMode: 15 passed, 0 failed, 0 skipped, 0 inconclusive;
- required M7D/M7C/M7B/M7A/M6C/M6B/M6A/player-navigation/world-session
  regressions: 212 passed, 0 failed, 0 skipped, 0 inconclusive;
- complete EditMode: 657 passed, 0 failed, 0 skipped, 0 inconclusive.

The physical Play Mode harness began at the authentic Bates return tile,
mutated representative campaign, progression, inventory, and local-navigation
state, then proved invalid-request and K'na Tha route failures left the complete
session unchanged. A literal click on `Tarant [area 21]` produced route
`5,5,5,5` and arrived at `(62243,65664)` in
`maps/arcanum1-024-fixed/68853695436.sec`.

The run retained the same production PC and M3-M7 state objects, normalized
local navigation, preserved unique coordinator/presenter/runtime/sprite
ownership, serialized V1 with no travel field, reset and restored the exact
post-arrival state, and passed Original -> Enhanced -> Original rebuilds. The
final physical run recorded 0 warnings and 0 errors. The user-owned
`OpenArcanumGraphicsConfig.asset` value `graphicsMode: 1` and the metadata-only
`EditorBuildSettings.asset` change were preserved locally and excluded from
M7E commits.

## K. Deliberate boundary

M7E does not add travel time, random encounters, followers or party relocation,
mid-route cancellation, route animation, world-map camera/polish, clock or
day/night advancement, autosave, or any M8 combat behavior. It adds no new save
field and does not reinterpret K'na Tha's rejected route. Those remain separate
future milestones rather than hidden behavior in the bounded travel service.

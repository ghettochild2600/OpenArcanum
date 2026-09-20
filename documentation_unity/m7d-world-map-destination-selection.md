# M7D read-only world-map destination projection and selection

Source audit, implementation contract, and validation record for the bounded
destination-selection boundary. This bounded slice is complete.

## Primary evidence

Retail data is read without extraction or modification from the mounted clean
Steam data under `D:/OpenArcanum/Source/Steam-Clean`, using the existing VFS and
Formats readers. Engine semantics were inspected in
`D:/OpenArcanum/Research/Repositories/arcanum-ce`, commit
`a7ff41b300ef712f0e7d088183a3d08957110cdd`: `src/game/area.c`,
`src/game/area.h`, and `src/ui/wmap_ui.c`.

## Pre-production source audit

`mes/gamearea.mes` is the sole source for an area's stable integer identity,
display name, short description, START_MAP tile coordinate, world-map label
pixel offset, and proximity-discovery radius. Area 0 is the special unknown
sentinel. A missing radius defaults to five sectors (320 gameplay tiles), while
`Radius:-1` prevents automatic proximity discovery. The negative radius does
not make an already-known location unselectable.

The area table contains no destination/local-map identity, authored entrance,
area category/type, disabled flag, route, travel duration, encounter data, or
arrival point. `rules/MapList.mes` independently associates some local maps with
an area, often many maps to one area; it is not a canonical world-map
destination mapping and must not be folded into this projection. Likewise, the
M7B Bates scenery/SAP_USE entrance is a physical placed-object path, not area
destination metadata.

The original world-map creates location notes by iterating area indices from
`area_count() - 1` down through 1 and adding a marker only when
`area_is_known(local_pc, id)` succeeds. Area 0 and unknown areas receive no
marker. Selection snaps to a nearest known area and its source coordinate.
Therefore M7D will retain canonical source order in its complete internal
projection, expose unknown entries as unavailable for explicit queries, and
show only known entries in the minimal player-facing destination list. It will
not present invented disabled markers.

The retail audit found 82 records in canonical contiguous ID order `0..81`,
with no duplicate identity. IDs 36 and 46 are two distinct non-destination
aliases named `A Place Unimportant`, both at `(0,0)` with description `This is
the unknown area.` They account for the one duplicate name and one duplicate
coordinate group and are explicitly classified as invalid destination records.
The remaining 79 nonzero entries have usable names and coordinates. The map
list contains 73 local-map associations spanning only 31 areas, confirming that
map association is many-to-one supplementary metadata rather than an area
destination field.

The selected fixtures are K'na Tha area 58 at `(91902,39305)`, with source
`Radius:-1`, and Tarant area 21 at `(62243,65664)`, with the default 320-tile
discovery radius. M7C authentically reveals K'na Tha; Tarant remains unknown in
the same controlled session even though the independent M7B Bates entrance
uses Tarant's area association.

## Pre-change ownership and intended boundary

`AreaList` owns immutable retail metadata. `CampaignStateService` owns the only
known-area set and already exposes `CanSelectWorldArea(AreaId)`.
`WorldMapSessionCoordinator` retains and rebinds both across save restore.
Unity currently has no world-map destination presenter.

The smallest intended call graph is:

`AreaList + CampaignStateService -> read-only destination projection ->`
`typed selection result / future travel request`.

The future travel request ends this slice. It cannot relocate the PC, select a
sector or map, advance time, roll an encounter, or mutate campaign state.

## Final projection and selection contract

`WorldMapDestinationProjection` is a presentation-independent derived view over
the immutable `AreaList` and the coordinator's current `CampaignStateService`.
It owns no mutable state. `TryProjectAll` emits the 79 valid nonzero retail
destinations in `AreaList` source order; `TryProjectVisible` filters that same
order to known areas, matching the source UI's rule that unknown areas receive
no marker. Explicit lookup retains unknown destinations as typed, unavailable
results instead of making them disappear from the domain boundary.

Each immutable `WorldMapDestination` carries only source-backed fields:

- typed `AreaId`;
- display name and description;
- typed world tile coordinate;
- world-map label X/Y offsets;
- proximity-discovery radius;
- derived `IsKnown` and `IsSelectable` flags.

Area 0 and missing/non-positive IDs fail as `InvalidArea`. The two retail
zero-coordinate aliases, areas 36 and 46, fail as
`InvalidDestinationRecord`. A duplicate source ID fails the whole catalog and
direct selection as `DuplicateAreaIdentity`; a missing `AreaList` fails as
`AreaSourceUnavailable`. A valid unknown record returns `Unavailable`.

`TrySelectWorldArea` delegates availability to
`CampaignStateService.CanSelectWorldArea`. A successful selection returns an
immutable `WorldMapTravelRequest` containing the `AreaId` and source world tile.
It does not change discovery, player position, selected sector, map-transition
state, time, encounters, or presentation. Repeated selection produces the same
request.

## Final ownership and call graph

`WorldObjectSectorLoader` still constructs and binds one immutable `AreaList`.
`WorldMapSessionCoordinator` owns the active campaign root and exposes a cached
projection bound to that root. Authoritative reset, area-source rebinding, and
transactional save restore invalidate the cached projection so the next query
uses the replacement campaign service. The campaign service alone owns known
areas.

The production read/selection flow is:

`AreaList + coordinator CampaignStateService -> WorldMapDestinationProjection`
`-> TryProjectVisible / TrySelectWorldArea -> WorldMapSelectionResult`
`-> WorldMapTravelRequest`.

`ProductionWorldMapDestinationPresenter` is an idempotently composed, transient
F7/IMGUI proof surface. It cancels transient navigation/interaction while open,
reads the projection, and emits `TravelRequested`; it does not retain discovery
or consume the request. Terrain, sprite owners, world objects, and demo scene
components remain presentation-only.

## Authentic fixture and persistence result

The physical fixture began from a reset campaign. The internal projection held
79 destinations, but the visible projection was empty: K'na Tha area 58 and
Tarant area 21 were both unavailable, and areas 36/46 were invalid rather than
disabled markers. A literal click on Clarissa followed the authored dialogue
1497 responses `2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93`.
Response 93 executed `mm58`; the already-open projection immediately changed to
one visible/selectable K'na Tha entry while Tarant remained unavailable.

The literal K'na Tha button click emitted exactly one request:
`area 58 @ (91902,39305)`. The PC position and selected Clarissa sector did not
change, no map transition began, and the campaign still contained exactly the
one authored discovery. An existing V1 slot then saved, authoritative reset
made K'na Tha unavailable, and load restored the same PC, sector, known-area
state, and selectable projection without any new save field.

Original -> Enhanced -> Original rebuilds preserved the selection state and
left exactly one coordinator, terrain owner, loader, PC lifecycle, navigation
controller, interaction controller, destination presenter, `WorldObjects`
root, PC runtime, and sprite presentation per persistent identity.

## Validation

Unity 6000.0.71f1 compiled cleanly.

- source audit: 82 areas in canonical `0..81` order; 79 valid destinations;
  zero duplicate IDs; one duplicate name group and one duplicate coordinate
  group (the invalid 36/46 aliases); 73 map associations over 31 areas;
- focused M7D EditMode: 16 passed, 0 failed, 0 skipped, 0 inconclusive;
- required M7C/M7B/M7A/M6C/M6B/M6A/M5C/M5B/M5A/player-navigation/
  world-session regressions: 251 passed, 0 failed, 0 skipped, 0 inconclusive;
- complete EditMode: 642 passed, 0 failed, 0 skipped, 0 inconclusive;
- physical Play Mode: passed with `projected=79`, hidden unknowns, one exact
  K'na Tha request, no travel, Tarant unavailable, V1 restore, and
  Original -> Enhanced -> Original;
- final physical Play Mode warnings/errors: 0/0.

The complete EditMode run still emits expected fail-closed compatibility
warnings from deliberately unsupported dialogue effects; they are not compiler
or test failures.

## Deliberate omissions and next milestone

M7D does not resolve an area to a local map/entrance because the audited area
source contains no such field. It does not simulate a route, move the party,
advance the clock, roll encounters, relocate followers, animate a world map, or
provide final world-map visuals. `WorldMapTravelRequest` is intentionally an
unconsumed boundary value.

The exact recommended next milestone is **M7E — source-audit and implement the
bounded world-map route/travel execution lifecycle that consumes
`WorldMapTravelRequest`**, beginning with authoritative route and arrival-source
semantics. Travel time, random encounters, follower relocation, and polished
world-map animation must remain separate later slices unless the audit proves
they are inseparable from the smallest valid execution boundary.

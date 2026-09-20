# M7B bounded Bates physical entrance / return slice

Source audit, implementation contract, and completed validation record.

## Primary evidence

Retail data was read without extraction/modification from mounted
`modules/Arcanum.dat` and `modules/arcanum1.dat` through `arcanum4.dat`, under
`D:/OpenArcanum/Source/Steam-Clean`, using existing Formats readers. Engine
semantics were inspected in `D:/OpenArcanum/Research/Repositories/arcanum-ce`,
commit `a7ff41b300ef712f0e7d088183a3d08957110cdd`: `src/game/map.c`, `area.c`,
`script.c`, `teleport.c`, and `ui/wmap_ui.c`.

## Source semantics and scope

MapList key 5000 is runtime map 1, `Arcanum1-024-fixed`, START_MAP, WorldMap 0.
Key 5011 is runtime map 12, `Bates Mansion Lev 1`, default start (104,92),
WorldMap 0, Area 21. SAT_TELEPORT uses MES keys; the engine subtracts 4999.
Default map starts are not substitutes for explicit script destinations.

Area 21 is Tarant, with overland origin (62243,65664). It is not the Bates
entrance or return. Area labels/radii have separate UI/selection semantics.
World-map entry from a local map uses its area origin; entry from overland uses
the PC's actual overland position. Image projection is X = 2000 - (tileX >> 6),
Y = tileY >> 6; inverse uses tile-center offset +32. These image coordinates
must not become local gameplay coordinates. World-map arrival teleports to
START_MAP at a resolved overland location, not an inferred local map.

Known-area flags initially default to zero and are changed by source scripts
and travel. World-map destination selection uses known-area policy. This placed
SAP_USE entrance has no discovery condition: no discovery state is necessary,
and Tarant is not declared initially known by this implementation.

Travel time, background route simulation and encounter callbacks are separate
world-map systems. This SAT_TELEPORT supplies flags 0, not TIME; no elapsed
time/fatigue/encounters are fabricated. Source teleport also relocates party/
followers and handles unconscious-party waiting. All of those systems remain
out of scope. Future callers may use the shared resolved-destination boundary
after implementing their own simulation/party/discovery policy.

## Candidate audit

| Candidate | Source evidence | Decision |
| --- | --- | --- |
| Bates Level 1 | Scenery SAP_USE 1267, unconditional constant teleport and skip-default, authored passive return | GREEN; selected |
| Scourge's lair | Script 1276, unconditional constant teleport to map 15 / MES 5014 at (101,95) | GREEN entry candidate; not admitted |
| Wheel Clan | Script 1201, conditional gate before teleport to map 7 / MES 5006 | YELLOW; cannot discard source conditions |
| Bates tunnel | Map 10 / MES 5009, gated scripted path | Not selected |

Only Bates is supported. No broad teleport VM support or inferred reciprocals.

## Exact loop

- Source sector: `maps/arcanum1-024-fixed/68853695432.sec`.
- Entrance: zero-based record 0, retail NULL ObjectID, Scenery prototype 4036,
  flags `0x00400000`, global (61974,65664), sector-local (22,0).
- Existing positional normalization yields
  `P_0000F216_00010080_00000000_00000001`.
- `scr/01267bates mansion to 1st floor_tel.scr`: exactly TRUE / SAT_TELEPORT
  Triggerer, constant MES key 5011, X 104, Y 92; then TRUE /
  ReturnAndSkipDefault. Header flags zero. Unused operand bytes contain retail
  editor garbage and are ignored, never evaluated.
- Destination: map 12 at global local-map tile (104,92), sector-local (40,28),
  `maps/bates mansion lev 1/67108865.sec`.
- Return: existing `maps/bates mansion lev 1/map.jmp`, six flags-0 passive
  tiles X 109..110, Y 92..94 -> map 1 at (61976,65664), sector-local (24,0).
  It is not the entrance coordinate or a remembered scene-entry coordinate.
- PC movement normalizes to STAND with existing ART identity/facing; no
  unauthored facing instruction is introduced.

M7A's lack of a reciprocal passive jump remains true. This entry is scripted Use.

## Ownership / smallest change set

Before: navigation -> coordinator RequestCurrentJumpPoint ->
MapTransitionResolver (map.jmp, MapList/map.prp/sector preflight) -> coordinator
transition teardown/PC relocation/shared SelectSector -> terrain/object owners
-> production PC lifecycle/navigation binding. Services retain M3-M6 state;
WorldObject/SpriteOwners/demo objects are presentation, never travel authority.

New branch: alpha-aware scenery selection -> PlayerInteractionController.TryUse
-> session ExecuteUse/RequestAreaEntrance -> narrow AreaEntranceResolver ->
shared MapTransitionResolver.ResolveDestination -> SAME coordinator
ApplyResolvedMapTransition -> terrain/object owners -> SAME PC/navigation bind.
Return continues through M7A passive-jump navigation. ResolveReturn additionally
exposes authored return preflight for focused tests. Loader binds MapList,
AreaList and existing ScriptDatabase; the production script interpreter is not
expanded. Only the exact admitted scenery becomes a Use target.

Preflight checks busy/actor/target/range/source location/script shape/area/map/
properties/bounds/sector before teardown. Existing lifecycle cancels route,
dialogue and transient approach, captures state, unloads views, relocates the
same PC, presents both owners and rebinds. Existing source rollback handles
presentation failure. Executing Use completes once without replay at arrival;
the entry-frame movement hold also applies to scripted entry. No second travel
pipeline, sector authority or scene-owned state was added.

Every loaded runtime is now given its owning session by the coordinator's
generic `Bind` path. This is required because alpha-aware target selection asks
the runtime's session whether a Scenery object is the one admitted entrance.
The original portal-only binding happened later and left real entrance scenery
without that association. The fix does not grant authority to the runtime: the
coordinator still performs identity admission, source decoding, preflight,
transition, rollback and persistence.

## State / V1 contract

TravelLocation explicitly distinguishes OverlandTile from LocalMapTile and
includes map ID; neither means world-map pixels. It is a resolved command value,
not another persistent position. PC position remains normalized map/sector plus
sector-local position, with global position derived by SectorCoordinate;
MapList START_MAP identifies overland. No new persistent field, discovery flag,
remembered entrance, clock or PC is necessary.

V1 already stores map/sector, PC position/ART, retained object states,
inventory/equipment/stacks/dynamic allocation, character and campaign state.
Schema remains V1, no migration. Source resolvers remain runtime bindings across
restore. Service roots/state references remain intact during travel; save/load
uses the existing M6 restore boundary.

## Completed physical validation

Unity 6000.0.71f1 compiled cleanly. Computer Use drove the production scene and
literal Game-view clicks; no editor teleport or direct validation-only map switch
was used for either physical entrance activation or the passive return.

- A literal click on `P_0000F216_00010080_00000000_00000001` approached and used
  the real prototype-4036 Scenery through SAP_USE 1267, then arrived on map 12 at
  exactly (104,92). The same production PC
  `G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01` was rebound standing with valid facing.
- A literal ground click navigated to the authored Bates exit and M7A returned to
  map 1 at exactly (61976,65664). Normal overland navigation resumed. A second
  literal entrance click independently re-entered Bates without accumulation.
- Preloaded persistent object references and the complete M3-M6 domain snapshot
  survived every transition. Representative authentic armor/ammunition,
  equipment, Gold, inventory/stacks, dynamic Food, attributes, HP/Fatigue,
  XP/level/points, Persuasion rank/training, derived alignment, campaign flag/
  variable, and quest/journal state remained authoritative and unchanged.
- A Bates dynamic world item survived B -> A -> B at its Bates placement; an
  overland dynamic world item survived A -> B and an overland save restore. No
  retained source identity was duplicated or re-created.
- Transition entry cancelled old navigation. A normal pending pickup that crossed
  the passive exit was cancelled, could not replay, and its stale Bates target was
  rejected on overland. Two entrance clicks produced exactly two entries; the one
  physical exit and one pending-pickup route produced exactly two returns.
- Focused invalid-source, missing/malformed destination, unsupported script,
  wrong actor, out-of-range, busy/reentrant, and presentation-rejection cases
  preserved map, PC location, serialized gameplay state and both presentation
  owners. Presentation rejection rolled back to the same PC and source map.
- V1 saves made inside Bates and on overland after return restored the exact side,
  position, PC identity and domain state. Runtime resolver bindings remained live;
  V1 and its migration boundary did not change.
- Original -> Enhanced -> Original rebuilds on both maps preserved map, position,
  identity and complete authoritative state, and the entrance/return still worked.
- Repeated transitions, rebuilds and loads retained exactly one coordinator,
  terrain owner, object loader/root, production lifecycle, PC runtime/presentation,
  navigation controller, interaction controller and dialogue presenter, with one
  sprite owner per active persistent identity.
- The final physical run recorded 0 warnings and 0 errors. Its exact trace was:
  entrance `(61974,65664) -> map 12:(104,92)`, return
  `-> map 1:(61976,65664)`, two physical entries, one physical return, one normal
  route return, A/B retention, both V1 restores, and graphics rebuilds both sides.

## Automated validation

- Focused M7B EditMode: 27 passed / 0 failed / 0 skipped / 0 inconclusive.
- Required regression: 179 passed / 0 failed / 0 skipped / 0 inconclusive:
  M7A 24, M6C 26, M6B 32, M6A 25, PlayerNavigation 21, M2B SAP_USE 8,
  M2A interaction 16, and WorldSessionState 27.
- Complete EditMode: 612 passed / 0 failed / 0 skipped / 0 inconclusive.
- Full-suite compatibility fixtures emitted their existing intentional
  fail-closed dialogue warnings; the physical M7B run itself was 0 warnings /
  0 errors and Unity reported no compiler errors.

## Remaining work / exact next slice

World-map destination UI/projection, known-area policy, game clock/day-night,
travel duration, route simulation, random encounters, party/followers, broad
teleport semantics and autosave remain absent. M7B does not infer Tarant's area
origin as the entrance, add a discovery gate to this ungated object, or serialize
transient route/presentation state.

The exact recommended next task is **M7C — source-audit and implement bounded
authoritative known-area/discovery state with one authentic discovery path and
versioned save persistence**, without beginning world-map route simulation,
travel time, encounters, party movement, or M8 combat.

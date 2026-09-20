# M7C bounded authoritative area discovery

Source audit, implementation contract, and validation record for one authentic
known-area mutation path. This bounded slice is complete.

## Primary evidence

Retail data was read without extraction or modification from the mounted clean
Steam data under `D:/OpenArcanum/Source/Steam-Clean`, using the existing VFS and
Formats readers. Engine semantics were inspected in
`D:/OpenArcanum/Research/Repositories/arcanum-ce`, commit
`a7ff41b300ef712f0e7d088183a3d08957110cdd`: `src/game/area.c`,
`script.c`, `dialog.c`, and `ui/wmap_ui.c`.

## Source semantics

`area.c` creates 82 stable integer areas from `mes/gamearea.mes`. Area 0 is the
special unknown placeholder. Each campaign owns a byte of flags per area;
`AREA_KNOWN` is bit `0x01`. `area_reset` clears every flag, so a new campaign has
no initially known area. `area_is_known` and `area_set_known` are local-PC
operations. Setting an already-known area succeeds without another mutation.
There is no ordinary undiscover operation; resetting `area_last_known_area`
does not clear the known bit.

Known-area flags are saved and loaded as campaign state. They are not inferred
from current map, PC position, global flags, quest state, WorldObject presence,
or Unity presentation.

The source has three distinct discovery paths:

- dialogue condition `ar N` queries the known bit and dialogue effect `mm N`
  calls `area_set_known`;
- SAT_MARK_MAP_LOCATION calls `area_set_known` from a script;
- world-map travel can discover a nearby area by its radius, with a separate
  sector-change hook only where town-map/world-map conditions permit it.

World-map rendering and destination selection query `area_is_known`. Route
simulation, travel time, encounters, destination UI, and map presentation are
separate consumers and remain out of scope. In particular, the ungated M7B
Bates entrance neither proves nor implies that Tarant is known.

## Retail audit and selected authentic event

The retail audit found 167 dialogue `mm` effects across 54 dialogue resources,
146 placed dialogue NPCs, and 22 SAT_MARK_MAP_LOCATION scripts. Every constant
area reference resolved to the 82-entry `gamearea.mes` table.

The admitted event is Clarissa Shalmo's real dialogue 1497:

- NPC ObjectID: `G_48830599_2627_9E4B_9996_FEB1835EDCDD`
- prototype: 17229
- source sector: `maps/arcanum1-024-fixed/96502547529.sec`
- source script 1497 opens dialogue at line 1
- normal intelligent-PC path: 3 -> 43 -> 53 -> 69 -> 75 -> 80 -> 95 ->
  86 -> 92 -> 93
- response line 93 has exactly `mm58`; low-intelligence line 94 is the same
  source effect but is not the selected physical fixture
- area 58 is `K'na Tha`, world tile `(91902,39305)`, radius `-1`, so this
  explicit source event is especially unambiguous and cannot be replaced by
  proximity discovery

The route also exercises existing local flags, reaction, and quest 1097 state.
Those already-authoritative operations remain owned by their existing services;
M7C admits only `mm` in this specifically audited dialogue compatibility set.

## Pre-change ownership and call graph

Before M7C, `AreaList` was immutable source metadata used by M7B's
`AreaEntranceResolver`. `CampaignStateService` owned all implemented
presentation-independent campaign mutations, snapshots, and V1 campaign save
data, but had no known-area domain. `ProductionDialogueSession` already parsed
`ar`/`mm` through the shared dialogue evaluator; its production context rejected
both as outside the admitted vocabulary. The loader independently constructed
the AreaList only while binding M7B. Unity objects, sprite owners, and demo
components owned no campaign policy.

The intended bounded call graph is:

`real NPC click -> PlayerInteractionController -> ProductionDialogueSession ->`
`strict dialog 1497 response 93 -> ProductionDialogueContext.MarkAreaKnown ->`
`CampaignStateService.DiscoverArea(AreaId(58))`.

Queries flow from dialogue `ar` and the future read-only
`CanSelectWorldArea(AreaId)` boundary to the same campaign-owned known set. The
loader binds one immutable AreaList source to the session; restore builds and
validates replacement campaign state against that same runtime source before
the coordinator swaps roots and rebuilds presentation.

## Smallest change set

- introduce a typed `AreaId` over the existing integer metadata key;
- extend `CampaignStateService` with source-bound validation, default-unknown,
  idempotent discovery, read-only known/selection queries, and snapshot support;
- reuse the loader's one AreaList instance for M7B entrance resolution and M7C
  campaign binding;
- admit `mm` only for dialogue 1497 and connect the existing `mm`/`ar`
  evaluator operations to campaign state;
- add sorted known-area IDs to V1 campaign JSON without changing the format
  version; absent `knownAreas` in an older V1 snapshot means the source-correct
  empty set, while explicit null, duplicate, zero, or unknown IDs fail before
  authoritative state changes;
- add focused EditMode coverage and a Play Mode harness around the exact retail
  NPC/event, save/reset/load, sector/map changes, and graphics rebuilds.

No route simulation, travel clock, encounter roll, area UI, automatic proximity
discovery, follower behavior, broad script admission, M8 combat, or presentation-
owned gameplay state is part of this slice.

## Final ownership and call graph

`WorldMapSessionCoordinator` owns the active `CampaignStateService` root and
retains the immutable `AreaList` metadata source supplied by
`WorldObjectSectorLoader`. The loader constructs that source once and shares it
with both M7B entrance resolution and campaign area validation; it does not own
known state. `CampaignStateService` alone owns the typed `HashSet<AreaId>`,
idempotent discovery mutation, discovery event, and read-only known/selection
queries. Unity objects, sprite owners, presenters, terrain, and the current map
do not infer or mutate discovery.

The admitted production mutation path is:

`literal Clarissa click -> PlayerInteractionController -> production Talk ->`
`ProductionDialogueSession(dialogue 1497) -> response 93 effect mm58 ->`
`ProductionDialogueContext.MarkAreaKnown ->`
`CampaignStateService.TryDiscoverArea(AreaId(58))`.

The matching `ar` query and future `CanSelectWorldArea` consumers read that
same set. Dialogue preflight verifies the exact admitted `mm58` operation and
source metadata before any response mutation, so an unsupported or invalid
effect cannot partially change quest, reaction, local flags, or known areas.
The previously unsupported `tr` test now queries the existing M4C training
authority with source comparison semantics; no duplicate progression state was
introduced.

V1 JSON stores sorted integer IDs in `campaign.knownAreas`. Loading validates
every non-empty ID against the bound 82-entry source, rejects zero, unknown,
duplicate, or explicit-null collections transactionally, and preserves the
active session on failure. An absent field in an older V1 document deserializes
to the source-correct empty set. Empty legacy state can be validated before the
runtime metadata source is bound; any non-empty state requires that source.
Restore creates a replacement campaign root, binds the retained source when
available, restores state, and only then swaps the authoritative roots and
rebuilds disposable presentation. The schema version remains V1.

## Validation (2026-09-20)

- Unity 6000.0.71f1 compilation: clean, 0 compiler errors.
- retail source audit: 82 areas, 167 dialogue marks in 54 resources, 146 placed
  dialogue NPCs, 22 script marks, and 0 invalid constant area references.
- focused M7C EditMode: 14 passed / 0 failed / 0 skipped / 0 inconclusive.
- required regressions: 237 passed / 0 failed / 0 skipped / 0 inconclusive:
  M7B 27, M7A 24, M6C 26, M6B 32, M6A 25, M5C 21, M5B 18, M5A 16,
  player navigation 21, and world-session state 27.
- complete EditMode: 626 passed / 0 failed / 0 skipped / 0 inconclusive.
- physical Play Mode used the visible retail Clarissa and a literal Game-view
  click, normal PC approach, and authored response sequence
  `2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93`.
- response 93 executed exact `mm58`, set quest 1097 Accepted, emitted one
  discovery event, made K'na Tha known/selectable, and a duplicate discovery
  produced no second mutation or event.
- known state survived Clarissa-sector rebuilds, same-map A -> B, the authentic
  M7B overland -> Bates transition, and Bates rebuilds. Entering Bates did not
  invent Tarant discovery.
- authoritative reset restored the default empty set; loading the temporary V1
  slot restored the same PC identity, Clarissa sector, and exactly K'na Tha.
- Original -> Enhanced -> Original rebuilds preserved state and unique
  coordinator, loader, controller, PC, root, and sprite presentation ownership.
- final Play Mode warnings/errors: 0/0. EditMode compatibility tests retain
  their explicitly asserted fail-closed unsupported-effect warnings.

## Remaining M7 scope

M7C deliberately does not provide a world-map screen, destination marker
projection, route finding, travel clock, proximity discovery, encounters,
followers, broad script admission, or combat. The exact recommended next slice
is **M7D — source-audit and implement a read-only world-map destination
projection/selection boundary gated by `CampaignStateService.CanSelectWorldArea`,
without route simulation, time passage, encounters, follower relocation, or
M8 combat**.

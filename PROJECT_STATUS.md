# OpenArcanum Project Status

## Gameplay-Parity Audit

A comprehensive research-only audit was completed on 2026-09-07. The current branch contains all 24 commits from the
published upstream `master` and is 31 commits ahead; no published upstream gameplay branch or release is available to
merge. The audit grades every major gameplay domain, separates parsers/demos from production call paths, identifies
safe/adapt/avoid reuse boundaries, and defines a dependency-ordered M1–M13 roadmap. See
[`documentation_unity/upstream-gameplay-parity-audit.md`](documentation_unity/upstream-gameplay-parity-audit.md).

## Current Objective

M1 traversal, the bounded M2 interaction/SAP_USE kernel, M3A-M3E inventory/command/equipment/stack/capacity state,
M4A-M4D character attributes/vitality/progression/derived statistics, M5A-M5C campaign/dialogue/quest/journal/trainer
vertical slices, M6A authoritative session save/load, M6B migration boundaries/domain save slots, and M6C bounded
player-facing manual save/load presentation, M7A bounded passive local map transitions, and M7B bounded authentic
  overland/Bates area entrance and return, M7C bounded authoritative known-area discovery, M7D read-only world-map
  destination projection/selection, M7E bounded authoritative route/travel execution, M8A bounded authoritative
  core combat state, M8B bounded turn-based movement/basic attack/damage, M8C bounded equipped bow/arrow combat, and
  M8D bounded vitality-derived defeat/death/unconsciousness and corpse state, M8E bounded source-authentic death
  consequences/corpse loot, M8F bounded source-authentic critical success/failure resolution, M8G Phase 1 dynamic
  roster/engagement plus completed-round authority, M8G Phase 2 structured attack requests/called locations/modifier
  ledger, M8G Phase 3 numeric cover/hard line-of-fire/Bow Master range exemption, M8G Phase 4 Expert/Master Bow
  multi-impact plus Critical-Dodge reclassification, and the bounded M8H authoritative real-time scheduler vertical
  slice are complete. All bounded post-M8F M8G audit items are closed.
`WorldMapSessionCoordinator` owns typed `World`, `Contained(parent)`, and `Equipped(parent, wornLocation)` placement,
atomic raw item transfers/equipment replacement/stack merge and split, deterministic session-created item identities,
source-faithful pickup/drop/owner-transfer policy, and pre-mutation weight/grid-capacity guards independently of Unity
presentation.
`CharacterStatService`, `CharacterProgressionService`, `CharacterVitalityService`, and
`CharacterDerivedStatService` provide stable ObjectID-keyed character rules. `CampaignStateService` now owns source-
bounded global/PC flags and variables, monotonic quest state/timestamps, per-object/per-SAP state, and a typed,
source-validated known-area set;
`ProductionDialogueSession` owns stable-ID conversation lifecycle, strict source evaluation, and transient training
subviews independently of Unity presentation. `DialogueTrainingService` routes the audited `t:` transaction through
M4C training, derived reaction, and existing Gold-stack authority. `JournalProjectionService` is a read-only
source-data projection, and quest completion routes dagger/gold, XP, alignment, and reaction through their existing
authoritative domains. `SessionSaveService` now exports those M1-M5 owners into deterministic, human-readable
`OpenArcanum.SessionSave` V1 JSON, atomically writes a validated temporary file, transactionally restores new service
roots, and rebuilds disposable Unity presentation. `SessionSaveMigrator` now owns format/version dispatch into the one
current validated snapshot shape. `SessionSaveSlotService` owns safe IDs, metadata derived from authoritative state,
sorted enumeration, create/overwrite/load/delete, durable single-file atomic replacement, stale-temporary cleanup, and
typed failures under `Application.persistentDataPath/Saves`. M6C adds a transient controller and minimal IMGUI
presenter over that boundary plus a shared input gate; the slot/session services remain authoritative. Canonical
A/G/P/D ObjectIDs, dynamic allocator/tombstones,
inventory/equipment/stacks, character inputs and mutations, campaign/quest/SAP/reaction state, and production PC
map-global state round-trip; dialogue/path/presentation state intentionally normalizes rather than serializes.

M7A adds strict MapList/map.jmp destination preflight and coordinator-owned cross-map teardown/relocation/rebind of the
same production PC. Every entered navigation waypoint can activate a passive source jump; residual old-route movement
is discarded. The real Bates Mansion Lev 1 (id 12) exit reaches overland (id 1) at global `(61976,65664)` without replacing
M3-M6 domain state or changing V1 saves. Non-positive sentinels and remote same-map jumps remain explicit Unsupported.

M7B adds one strict source-decoded physical entrance: overland prototype-4036 Scenery
`P_0000F216_00010080_00000000_00000001` at `(61974,65664)` invokes SAP_USE 1267 and reaches Bates map 12 at
`(104,92)`. It reuses the M7A coordinator lifecycle and authored passive return to `(61976,65664)`, preserving the same
PC and all authoritative M3-M6 state. Tarant's world-map origin remains a distinct coordinate and no discovery gate is
invented for this ungated placed object. V1 remains unchanged.

M7C binds the immutable 82-entry `AreaList` source to coordinator-owned campaign state and adds typed `AreaId`
validation, default-unknown/idempotent discovery, a single discovery event, and read-only known/selection queries.
Clarissa Shalmo's authentic dialogue 1497 response 93 executes exact `mm58` to reveal K'na Tha; no map entry or
presentation state invents discovery. Sorted IDs round-trip in the existing V1 campaign payload, older V1 documents
without the field remain an empty set, and invalid non-empty data fails transactionally before the session changes.

M7D derives 79 valid immutable destinations from that shared source in deterministic source order and gates selection
through `CampaignStateService.CanSelectWorldArea`. Unknown locations follow the source UI and remain hidden; explicit
queries return unavailable, while invalid/source-less/duplicate records fail distinctly. Selecting known K'na Tha
produces one typed `WorldMapTravelRequest` for area 58 at `(91902,39305)` without moving the PC, changing maps,
advancing time, rolling encounters, or mutating campaign state. Projection state recomputes across authoritative reset
and V1 restore; the minimal Unity presenter owns only display/input.

M7E consumes that typed intent through coordinator-owned `WorldMapTravelService`. It resolves the retail START_MAP
terrain topology, applies the source straight-sector path plus bounded 16x16 detour algorithm, preflights arrival, and
reuses the existing transactional M7 transition pipeline. The authentic Bates return `(61976,65664)` to Tarant area 21
route is four eastward sector rotations `5,5,5,5`, arriving on START_MAP id 1 at `(62243,65664)` in
`maps/arcanum1-024-fixed/68853695436.sec`. K'na Tha remains unavailable to this source algorithm rather than being
substituted with an invented route. The synchronous lifecycle is Planning -> Travelling -> Arriving -> Completed ->
Idle; stable post-arrival state remains V1.

M8A adds coordinator-owned turn-based combat lifecycle, source-bounded actor/hostility eligibility, stable-ID
participants, authentic non-PC-first ordering, M4D Speed-derived turn AP, and explicit world-input lockout. Combat is
transient: sector unload, cross-map selection, reset, and V1 restore normalize it to Inactive; V1 remains unchanged.
M8B adds source-AP-aware combat movement, one authentic unarmed melee attack, injected authoritative randomness,
hit/Dodge and normal/fatigue resistance resolution, and damage exclusively through M4B vitality. AP exhaustion advances
the stable turn/round loop; every rejected command is transactional. M8F classifies ordinary results with the source
critical roll, applies the bounded +50%/+100%/+200% damage-only success family after resistance, admits the ordinary
self-hit critical failure, suppresses failure for Master Melee, and rejects unsupported critical effects before any
authoritative mutation. Combat remains transient and Save V1 is unchanged.
M8G Phase 1 discovers eligible nearby source-hostile NPCs at combat start and completed-round refresh, admits explicit
runtime engagement exactly once, preserves stable source-order/PC-tail turns, and emits one deterministic +1,000 ms
hook per completed round. Existing unconscious participants remain enrolled and are skipped; death/removal clears
engagement. Graphics rebuild and Save V1 normalization do not replay discovery or a round boundary.
M8G Phase 2 adds immutable structured melee/ranged requests, exact called-location IDs/modifiers, and one ordered
modifier ledger whose clamped final effectiveness is the attack-roll authority. M8G Phase 3 separates hard projectile
blocks from numeric cover, accumulates source-flagged ordinary and wall/portal-edge cover exactly once in that ledger,
and suppresses only the Perception-range entry for Bow Master. Open portals and wall-passage pieces contribute no
cover; hard blocks remain transactional. Save V1 and presentation ownership remain unchanged.
M8G Phase 4 makes Expert and Master Bow commands produce two ordered, same-target damage impacts inside one AP and
one-ammunition transaction. Both impacts share the request, attack/critical classification, and Phase 2/3 ledger but
roll damage, resistance, and supported critical damage effects independently. The second impact continues after a
lethal first impact while M8D/M8E process death/corpse/XP exactly once. A defender's qualifying critical Dodge uses
the exact 0/10/50/100 training table and reclassifies the cleared hit through the existing critical-failure path;
unsupported secondary effects remain fail-closed. Save V1 remains unchanged and impact/Dodge diagnostics are
transient.
Travel time, encounters, follower relocation, mid-route cancellation, route animation, clock/day-night behavior,
autosave, quicksave, cloud sync, original Arcanum save compatibility, unsupported critical-table effects, broader death
scripts/consequences, AI, spells, technology, followers, and barter remain deferred.

## Current Branch

Expected branch: `feature/session-save-load`

Always verify the actual Git branch before doing work. Git is authoritative if it disagrees with this document.

## M6A Validation Baseline (2026-09-13)

- Unity 6000.0.71f1 compilation: clean
- focused M6A EditMode: 25/25
- required M1-M6A regression matrix: 332/332
- complete EditMode suite: 503/503
- real golden session save -> authoritative reset -> load -> presentation rebuild -> continued gameplay: passed
- post-load dialogue, inventory pickup/drop, navigation, dynamic allocation, portal open/close: passed
- Original -> Enhanced -> Original rebuild: passed
- PlayMode warnings/errors: 0/0

See [`documentation_unity/m6a-session-save-load.md`](documentation_unity/m6a-session-save-load.md) for the state
inventory, V1 schema/version policy, transient normalization, atomic/transactional behavior, referential rules,
golden fixtures, and exact omissions.

## M6B Validation Baseline (2026-09-13)

- Unity 6000.0.71f1 compilation: clean
- focused M6B EditMode: 32/32
- required M1-M6B regression matrix: 364/364
- complete EditMode suite: 535/535
- real source-data alpha -> beta -> alpha slot isolation: passed
- corrupt B load preserves the active A object reference and complete state: passed
- authored-parent relocation, foreign-sector suppression, and multi-NPC exactness: passed
- post-load dialogue/journal, inventory/equipment, navigation, portal, and graphics rebuild: passed
- PlayMode warnings/errors: 0/0

See [`documentation_unity/m6b-save-migration-slots.md`](documentation_unity/m6b-save-migration-slots.md) for the
pre-implementation schema/risk audit, migration and slot call graph, metadata/path/atomicity contract, exact real-data
fixtures, validation results, deliberate omissions, and M6C recommendation.

## M6C Validation Baseline (2026-09-13)

- Unity 6000.0.71f1 compilation: clean
- focused M6C EditMode: 26/26
- required M1-M6C regression matrix: 390/390
- complete EditMode suite: 561/561
- physical UI create/overwrite/load/corrupt/delete/cancel flow: passed
- exact A -> B -> A -> B player-position restoration: passed
- post-panel click navigation and real-food pickup through production input: passed
- unique coordinator/navigation/interaction/presenter/gate/object views after load: passed
- PlayMode warnings/errors: 0/0

See [`documentation_unity/m6c-manual-save-load-ui.md`](documentation_unity/m6c-manual-save-load-ui.md) for the final
ownership graph, presentation/lifecycle contract, typed diagnostics, physical validation evidence, deliberate limits,
and exact M7A recommendation.

## M7A Validation Baseline (2026-09-15)

- Unity 6000.0.71f1 compilation: clean
- resumed focused M7A baseline: 14 passed / 0 failed / 0 skipped / 0 inconclusive
- final expanded focused M7A: 24 passed / 0 failed / 0 skipped / 0 inconclusive
- required M1-M6C regressions: 390 passed / 0 failed / 0 skipped / 0 inconclusive
- complete EditMode: 585 passed / 0 failed / 0 skipped / 0 inconclusive
- physical Bates floor click -> authentic passive overland arrival -> physical destination navigation click: passed
- same PC/character and all 1807 pre-registered object-state references; exact non-movement M3-M6 snapshot: passed
- first crossed jump cancels pending pickup; stale foreign-map target rejected; one activation per crossing: passed
- unique presentation/controllers across repeated activations, V1 loads, graphics rebuild, and sector reload: passed
- V1 exact-authored-arrival slot restore, post-load navigation and dynamic-item drop/pickup: passed
- Original -> Enhanced -> Original: passed; user-owned graphicsMode: 1 asset preserved/excluded
- final real Play Mode warnings/errors: 0/0 (EditMode compatibility fixtures emit expected unsupported-effect warnings)
- no authentic reciprocal overland map.jmp: documented; source snapshot restore is test setup, not invented return

See [`documentation_unity/m7a-local-map-transition-audit.md`](documentation_unity/m7a-local-map-transition-audit.md)
for the retained source audit, final ownership graph, lifecycle/failure contracts, exact physical evidence, test matrix,
save/graphics results, omissions, and M7B recommendation.

## M7B Validation Baseline (2026-09-19)

- Unity 6000.0.71f1 compilation: clean
- focused M7B EditMode: 27 passed / 0 failed / 0 skipped / 0 inconclusive
- required M7A/M6C/M6B/M6A/navigation/interaction/session regressions: 179 passed / 0 failed / 0 skipped /
  0 inconclusive
- complete EditMode: 612 passed / 0 failed / 0 skipped / 0 inconclusive
- literal overland entrance click -> exact Bates map 12 `(104,92)` -> literal passive exit -> exact overland map 1
  `(61976,65664)` -> literal re-entry: passed
- same production PC ObjectID/reference, standing/facing and complete M3-M6 domain snapshot across every transition:
  passed
- Bates and overland dynamic world state survived A/B/A and B/A/B; retained identities/references were not duplicated:
  passed
- pending interaction cancellation, stale-target rejection, single activation, reentrant rejection and route
  normalization: passed
- invalid entrance/preflight and presentation-rejection rollback leave map, PC, gameplay and presentation unchanged:
  passed
- V1 save/load inside Bates and on returned overland restores exact side/location/state: passed; schema unchanged
- Original -> Enhanced -> Original on both sides: passed; user-owned graphicsMode: 1 asset preserved/excluded
- repeated transitions/rebuilds/loads retained unique gameplay and presentation owners
- final physical Play Mode warnings/errors: 0/0 (full EditMode compatibility fixtures retain expected fail-closed
  dialogue warnings)

See [`documentation_unity/m7b-area-entrance-audit.md`](documentation_unity/m7b-area-entrance-audit.md) for the exact
source fixture, coordinate-space distinction, final call graph, lifecycle and state contracts, physical trace, rollback,
save/graphics evidence, tests, omissions, and M7C recommendation.

## M7C Validation Baseline (2026-09-20)

- Unity 6000.0.71f1 compilation: clean
- focused M7C EditMode: 14 passed / 0 failed / 0 skipped / 0 inconclusive
- required M7B/M7A/M6C/M6B/M6A/M5C/M5B/M5A/navigation/world-session regressions: 237 passed / 0 failed /
  0 skipped / 0 inconclusive
- complete EditMode: 626 passed / 0 failed / 0 skipped / 0 inconclusive
- retail audit: 82 areas, 167 dialogue marks, 54 dialogue resources, 146 placed dialogue NPCs, 22 script marks,
  0 invalid constant area references
- literal Clarissa click and physical authored responses `2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93`:
  passed; exact response 93 `mm58` revealed K'na Tha and emitted one idempotent discovery event
- same-map sector transition, authentic overland -> Bates transition, authoritative reset, V1 slot restore, and
  Clarissa/Bates visual rebuilds preserved exactly one known area and the same production PC identity/state
- entering Bates did not invent Tarant discovery; reset restored the source-correct empty known set
- Original -> Enhanced -> Original: passed; unique gameplay/presentation owners retained
- final physical Play Mode warnings/errors: 0/0; user-owned graphicsMode: 1 asset preserved/excluded

See [`documentation_unity/m7c-area-discovery.md`](documentation_unity/m7c-area-discovery.md) for the source semantics,
retail audit and exact fixture, final ownership/call graph, V1 compatibility contract, validation evidence, omissions,
and M7D recommendation.

## M7D Validation Baseline (2026-09-20)

- Unity 6000.0.71f1 compilation: clean
- focused M7D EditMode: 16 passed / 0 failed / 0 skipped / 0 inconclusive
- required M7C/M7B/M7A/M6C/M6B/M6A/M5C/M5B/M5A/player-navigation/world-session regressions: 251 passed /
  0 failed / 0 skipped / 0 inconclusive
- complete EditMode: 642 passed / 0 failed / 0 skipped / 0 inconclusive
- retail destination audit: 82 canonical areas, 79 valid nonzero destinations, zero duplicate IDs, two explicit
  invalid aliases (36/46), 73 map associations across 31 areas
- reset campaign hid K'na Tha and Tarant; literal Clarissa click plus authored responses
  `2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93` made only K'na Tha visible/selectable
- literal K'na Tha selection emitted exactly `WorldMapTravelRequest(area 58, (91902,39305))`; PC position, sector,
  transition state, time/encounters, and campaign state did not change
- V1 save -> authoritative reset -> load restored the same PC/sector and K'na Tha selection state; Tarant remained
  unavailable and no save field was added
- Original -> Enhanced -> Original retained one of every gameplay/presentation owner and one presentation per identity
- final physical Play Mode warnings/errors: 0/0; user-owned graphicsMode: 1 asset preserved/excluded

See [`documentation_unity/m7d-world-map-destination-selection.md`](documentation_unity/m7d-world-map-destination-selection.md)
for source fields and hide semantics, the final ownership graph, typed failures/request contract, authentic fixture,
persistence/graphics evidence, deliberate omissions, and the exact M7E recommendation.

## M7E Validation Baseline (2026-09-20)

- Unity 6000.0.71f1 production/test/editor compilation: clean
- focused M7E EditMode: 15 passed / 0 failed / 0 skipped / 0 inconclusive
- required M7D/M7C/M7B/M7A/M6C/M6B/M6A/player-navigation/world-session regressions: 212 passed /
  0 failed / 0 skipped / 0 inconclusive
- complete EditMode: 657 passed / 0 failed / 0 skipped / 0 inconclusive
- physical Bates return `(61976,65664)` -> literal Tarant area 21 click -> exact START_MAP arrival
  `(62243,65664)` in `maps/arcanum1-024-fixed/68853695436.sec`: passed
- authentic route geometry was exactly four rotations `5,5,5,5`; K'na Tha failed explicitly without substitute routing
- invalid request and unroutable destination rollback preserved the complete source session and presentation
- the same production PC plus M3-M7 inventory, character, campaign, discovery, and navigation state survived arrival
- post-arrival V1 save -> authoritative reset -> load restored exact identity, sector, tile, and discovery; schema stayed V1
- Original -> Enhanced -> Original rebuild retained unique gameplay/presentation owners and one presentation per identity
- final physical Play Mode warnings/errors: 0/0; user-owned graphicsMode: 1 asset preserved/excluded

See [`documentation_unity/m7e-world-map-route-audit.md`](documentation_unity/m7e-world-map-route-audit.md) for the
source route/arrival audit, final ownership and lifecycle contract, authentic fixture, rollback and persistence evidence,
validation matrix, and deliberate omissions.

## Verified Baseline

The following have already been tested successfully:

- Clean Steam 1.0.7.4 data baseline
- Original DAT data loading
- 2,396 dialogue scripts loaded successfully
- Terrain decoding and rendering
- Character ART decoding and rendering
- Tile decoding and rendering
- World-object artwork decoding and rendering
- Native 3840x2160 rendering
- Original game-data access through the project-local GameData junction

## Test Infrastructure

VisualSandbox exists for isolated rendering experiments.

Other test scenes have been used for:

- Dialogues
- Terrain
- Characters
- Objects
- Tiles

## Graphics Infrastructure

OpenArcanum has an Original / Enhanced graphics configuration system.

OpenArcanum-specific code is primarily under:

Assets/_OpenArcanum/

The graphics configuration asset is under:

Assets/_OpenArcanum/Resources/

Original mode is the compatibility baseline.

Enhanced mode is intended for replacement assets and future rendering improvements.

## Texture Policy Work

An enhanced texture rendering policy was implemented.

A terrain experiment using bilinear filtering was performed.

Results:

- Character artwork could appear slightly smoother.
- Terrain developed visible diamond/tile seams.
- Mipmaps, UV inset, atlas extrusion, and transparent RGB dilation were investigated.
- The visual improvement was not significant enough to justify the added complexity.
- The terrain experiment was deliberately reverted.

Do not resume trying to remaster the original terrain simply through bilinear filtering unless specifically asked to revisit that research.

The conclusion was that meaningful improvement requires genuinely higher-resolution artwork.

## Relevant Git History

Important commits include:

42a474d Add OpenArcanum graphics mode configuration
79b7b09 Add enhanced texture rendering policy
f7a9404 Prototype enhanced terrain filtering
4cc2e7b Revert prototype enhanced terrain filtering

The prototype and revert are intentionally retained in history.

## HD Replacement Infrastructure

HDAssets/ exists and is Git-ignored.

The following loader exists and compiled successfully:

Assets/_OpenArcanum/Scripts/Rendering/OpenArcanumHDAssetLoader.cs

It is intended to find replacement PNG files when Enhanced mode is enabled.

When no replacement exists, rendering must fall back to the original Arcanum artwork.

## Intended Flow

Original ART resource
        |
        v
Enhanced mode?
   |          |
   No        Yes
   |          |
Original    Check HDAssets
              |
          +---+---+
          |       |
       Exists   Missing
          |       |
          v       v
       HD PNG   Original ART

## Current Replacement Convention

The loader currently derives replacement paths from original Arcanum resource paths.

Example original:

art/tile/grass/grass01.art

Example replacement:

HDAssets/art/tile/grass/grass01/r0_f0.png

Where:

r0 = rotation 0
f0 = frame 0

Inspect OpenArcanumHDAssetLoader.cs before relying on this convention. The implementation is authoritative.

## Completed Milestone

The complete pipeline has been proven with one asset.

1. Identify one original ART resource.
2. Determine its original resource path, rotation and frame.
3. Produce or supply one 4x PNG replacement.
4. Place it under HDAssets using the expected path.
5. Original mode must render original ART.
6. Enhanced mode must load the HD PNG.
7. The HD image must occupy exactly the same world dimensions.
8. Preserve pivot and hotspot behavior.
9. Removing the PNG must automatically restore original ART fallback.

Do not attempt mass conversion until production asset lifetime/batching and the
replacement authoring contract are documented beyond these proofs.

## Proof of Concept Result

Completed on 2026-08-23 with Unity 6000.0.71f1.

### Architecture Implemented

- `ArtTextureFactory` retains its original compatibility overload.
- A source-aware overload accepts the original ART path, rotation index, and frame index.
- `CharacterArtGallery` supplies that identity for every decoded critter frame.
- Original mode continues through the original texture/atlas code path.
- Enhanced mode asks `OpenArcanumHDAssetLoader` for a replacement before atlas packing.
- Replacements must be exactly 4x the original frame width and height.
- Accepted HD sprites use the original normalized hotspot pivot and 4x pixels-per-unit.
- HD sprites bypass `RuntimeSpriteAtlas` for this initial proof.
- Missing, undecodable, or wrong-size replacements fall back to original ART.
- Invalid replacements are rejected once per cache lifetime to avoid warning spam.

### Files Changed

- `Assets/_OpenArcanum/Scripts/Rendering/OpenArcanumHDAssetLoader.cs`
- `Assets/_Game/Scripts/World/ArtTextureFactory.cs`
- `Assets/_Game/Scripts/Runtime/Demo/CharacterArtGallery.cs`
- `Assets/_OpenArcanum/Editor/HDReplacementProofGenerator.cs`
- `.gitignore`
- `AGENTS.md`
- `PROJECT_STATUS.md`

### Local Proof Asset

Source ART:

`art/critter/hmf/hmfuwxaa.art`

Identity:

- Rotation: 0
- Frame: 0
- Original frame: 28x77
- Hotspot: (9, 75)
- Normalized pivot: approximately (0.3214, 0.0260)

Replacement:

`HDAssets/art/critter/hmf/hmfuwxaa/r0_f0.png`

The replacement is 112x308. It was generated from the user's local game data,
visually tinted for an unambiguous proof, and remains entirely under the
Git-ignored `HDAssets/` directory. No original or derived proprietary artwork
was staged or committed.

### Validation Performed

The `TestCharactersArt` SpriteRenderer scene was run through the existing Unity
Editor and inspected in the Unity Console.

Original mode with the replacement present:

- Sprite: `ArtSprite`
- Texture and rect: 28x77
- Pixels per unit: 100
- World size: 0.28x0.77
- Normalized pivot: approximately (0.32, 0.03)
- Character position: (-5.60, 1.20, 0.00)
- No HD replacement load was attempted.

Enhanced mode with the valid replacement:

- Unity logged the exact replacement path as loaded.
- Sprite: `HDArtSprite_r0_f0`
- Texture and rect: 112x308
- Pixels per unit: 400
- World size: 0.28x0.77
- Normalized pivot: approximately (0.32, 0.03)
- Character position: (-5.60, 1.20, 0.00)

Enhanced mode with the replacement renamed away, on a fresh run:

- Sprite: `ArtSprite`
- Texture and rect: 28x77
- Pixels per unit: 100
- World size, pivot, and character position matched Original mode.

Enhanced mode with a deliberately invalid 1x1 replacement:

- Unity logged one warning explaining that 112x308 was required and 1x1 was found.
- The rendered sprite fell back to `ArtSprite` at 28x77 and 100 pixels per unit.
- World size, pivot, and character position matched Original mode.

The valid 112x308 proof PNG was restored and loaded successfully in a final
Enhanced-mode run after the last code change. The editor graphics configuration
was returned to Original afterward. Unity compiled every meaningful C# change
with zero Console errors.

The gallery's existing `CritterTurntable` continued cycling facings and frames;
the integration does not modify the ART frame arrays, FPS, or timing logic.

### Commits

- `3619eb7` Add HD replacement asset loader groundwork
- `70994f6` Document HD replacement workflow
- `f567fff` Render validated 4x character replacements

### Known Limitations

- `CharacterArtGallery` and `ObjectArtGallery` carry source identity. A production sector owner now does as well;
  see the Production Sector Sprite Ownership milestone below.
- HD sprites bypass `RuntimeSpriteAtlas`, so production batching and lifetime management remain future work.
- Only exact 4x replacements are supported.
- The local proof is validation artwork, not a production-quality remaster.
- Loaded and rejected replacements are cached within one runtime session. Entering a new Play/runtime session
  now clears that cache even when Unity domain reload is disabled.

## Runtime Sprite Integration Milestone

Completed on 2026-08-23 with Unity 6000.0.71f1.

### Call-Site Audit

Every `ArtTextureFactory.CreateSprite` caller was inspected.

- `CharacterArtGallery.LoadRotations` represents characters. It already passes the ART path, rotation and frame,
  uses the original hotspot pivot, participates in facing/frame animation, does not request `mirrorX`, and does not
  activate `RuntimeSpriteAtlas`.
- `ObjectArtGallery.LoadFrames` is the current ordinary SpriteRenderer object path for walls, portals, containers,
  roofs, facades and scenery. It previously lost the ART path and frame identity. It uses a centered pivot override,
  rotation 0, and uses `SpriteFrameAnimator` with the original ART FPS for animated doors/windows. It now passes the
  path, rotation 0 and the real frame index, including the selected middle facade frame.
- `TileMapRenderer.GetFacadeSprite` and `TileMapRenderer.GetSprite` still lose identity. They render facade/terrain
  tiles, own higher-level terrain sprite caches, and were deliberately not modified because terrain is out of scope.
- `TileGallery` is a terrain-only test caller and was deliberately not modified.
- No current caller passes `mirrorX: true`. `ArtTextureFactory` remains the authoritative implementation of the
  engine's horizontal-flip semantics for future callers.
- No current non-terrain caller activates `RuntimeSpriteAtlas`. `TileMapRenderer` only saves, clears and restores
  `ActiveAtlas` while rendering terrain. Source-aware HD sprites continue to bypass atlas packing.
- `WorldObject` exposes a `SpriteRenderer` view and a `ReRender` callback but does not create sprites; there was no
  source-identity call site to change there.

The smallest required caller change was therefore `ObjectArtGallery.LoadFrames`; blindly changing terrain/demo tile
callers was unnecessary.

### Runtime Integration Implemented

- Ordinary object frames now use the source-aware `ArtTextureFactory.CreateSprite` overload.
- Static objects pass frame 0; animated objects pass every actual frame index; middle-frame facades pass their selected
  frame index rather than incorrectly identifying it as frame 0.
- Centered pivot overrides remain centered in Original and Enhanced modes.
- `art.Fps` still drives `SpriteFrameAnimator`; no timing or gameplay transform code changed.
- The dependency direction remains `Arcanum.World` to `OpenArcanum.Rendering`; no reference back to `Arcanum.World`
  was introduced, so there is no assembly cycle.

### Mirror Behavior

HD `mirrorX` is implemented as an exact horizontal row-by-row pixel reversal of the validated replacement texture,
matching the existing original ART `BuildPixels` operation. It does not use `SpriteRenderer.flipX` and does not change
a GameObject transform.

Mirrored HD textures are cached separately. `ArtTextureFactory` retains its existing pivot semantics: without a pivot
override, mirrored pivot X is 0 while pivot Y remains the original hotspot-derived value; a caller-provided pivot
override remains authoritative.

The object proof measured:

- Exact mirrored-pixel comparison: true
- Normal and mirrored world size: 0.44 x 0.42
- Normal hotspot pivot: approximately (0.52, 0.26)
- Mirrored pivot: approximately (0.00, 0.26)
- Anchor/world position: unchanged

Because no checked-in runtime caller currently requests `mirrorX`, this exact factory-level result is ready for the
first such caller but is not presented as an existing gameplay-facing mirror call site.

### Cache and Mode Behavior

- The HD loader still caches accepted textures and rejected paths within a runtime session.
- Mirrored textures have their own cache entries and are destroyed by `ClearCache` with their source textures.
- A `SubsystemRegistration` reset now clears the HD loader cache at the beginning of every runtime/Play session,
  including when Unity domain reload is disabled.
- Character/object sprite arrays are owned by scene instances and are rebuilt on a new Play/session run.
- The only higher-level persistent sprite caches found are in `TileMapRenderer`, which is terrain-only and out of scope.
- Gallery sprite arrays are still rebuilt by recreating their scene instances. Production sector owners now support
  explicit in-place Original/Enhanced rebuilding; see the Production Sector Sprite Ownership milestone below.

The same Unity editor session validated Enhanced with a loaded PNG, Original, and then Enhanced with that PNG renamed
away. The final run rebuilt the original ART sprite instead of reusing the earlier cached HD texture, proving the new
runtime-session invalidation prevents this stale-cache case.

### Runtime Object Validation

The actual `TestObjects` SpriteRenderer scene was used, not only `CharacterArtGallery`. The proof generator resolves
the first existing container through the same `container.mes` and mounted DAT VFS used by the runtime object path.

Local proof identity:

- Source: `art/container/g_junk.art`
- Rotation/frame: r0/f0
- Original frame: 44x42
- Original hotspot: (23,31)
- Local replacement: `HDAssets/art/container/g_junk/r0_f0.png`
- Replacement: 176x168, generated from local game data, visibly tinted, Git-ignored and never staged

Original mode with the replacement present:

- Sprite: `ArtSprite`
- Texture/rect: 44x42
- Pixels per unit: 100
- Sprite and rendered world size: 0.44x0.42
- Centered override pivot: (0.50,0.50)
- Object position: (-11.25,-55.03,0.00)
- No HD load occurred

Enhanced mode with the valid replacement:

- Unity logged the exact local replacement path as loaded
- Sprite: `HDArtSprite_r0_f0`
- Texture/rect: 176x168
- Pixels per unit: 400
- Sprite and rendered world size: 0.44x0.42
- Centered override pivot: (0.50,0.50)
- Object position: (-11.25,-55.03,0.00)

Enhanced mode after renaming the replacement away, in a later Play run in the same editor session:

- Sprite: `ArtSprite`
- Texture/rect: 44x42
- Pixels per unit: 100
- World size, pivot, scale and position matched Original mode
- No HD load or warning occurred

Animated portal sections also ran through the updated frame loop: 111 doors and 260 windows decoded, and their existing
`SpriteFrameAnimator` continued to use the original frame arrays and ART FPS. The selected proof container itself is
static, so animation timing is not applicable to that asset.

Unity compiled the changes successfully. The completed runtime runs showed zero Console warnings and zero Console
errors. The proof PNG was restored after fallback validation, Play mode was stopped, and graphics mode was returned to
Original.

### Runtime Integration Commits

- `5225de6` Propagate HD identity through object sprites
- `9ee11ac` Mirror HD sprites and reset runtime cache
- `a33062d` Add runtime HD object validation tools

## Production Sector Sprite Ownership Milestone

Completed on 2026-08-24 with Unity 6000.0.71f1.

### Production Owner and Architecture

The repository previously parsed sector objects but did not instantiate them:
`SectorReader.ReadObjects` had no runtime consumer, and `WorldObject.View` / `ReRender`
were never assigned. `TileMapDemo` owned terrain only. No hidden production path was
bypassing `ArtTextureFactory` because there was no ordinary world-object presentation
owner yet.

The smallest production path is now:

`WorldObjectSectorLoader` -> `.sec` and sector `.mob` instances -> inherited prototype
ART id -> existing type-specific ART resolver -> `WorldObject` gameplay-tile root ->
`WorldObjectSpriteOwner` visual child -> source-aware `ArtTextureFactory.CreateSprite`.

- The loader reads real sector records and authored mobile records, filters them to the selected sector, and excludes
  inventory children plus destroyed/off/don't-draw records from map presentation.
- Walls, portals, scenery, containers, ground items, critters/monsters/unique NPCs, roofs, facades, lights and eye candy
  use the existing resolver classes. No HD-loading logic is duplicated.
- The `WorldObject` root remains on the projected gameplay tile. Only its visual child receives the engine's exact
  `+40,+20` presentation base and authored object offsets.
- `WorldObject.View` and `WorldObject.ReRender` are assigned. `WorldObject.SetArt` therefore rebuilds the presentation
  from the new ART identity for future state-driven door/window/critter changes.
- Dependency direction remains `Arcanum.Runtime` -> `Arcanum.World` -> `OpenArcanum.Rendering`; no Rendering-to-Runtime
  dependency or assembly cycle was introduced.
- `TestTerrain` now contains a sibling `WorldObjectSectorLoader` root. It is deliberately separate from the terrain
  host so a terrain refresh cannot delete entity visuals.

### Source Identity, Frames and Mirroring

`WorldObjectSpriteOwner` retains the original ART path, requested rotation, decoded source rotation, actual frame
indices, mirror state, FPS, and complete frame array.

- ART-id rotation/frame bit layouts follow the engine's per-art-type rules rather than a generic approximation.
- Critter, monster and unique-NPC facings 1-3 use source rotations 7-5 and exact horizontal pixel reversal.
- Wall/portal/roof ART-id flip behavior is preserved.
- Pivot overrides reproduce `tig_art_frame_data` hotspot transforms, including wall/portal `-40,+20`, facing-mirror
  hotspot reflection, and flipped roof/portal/wall anchors. No `SpriteRenderer.flipX` or transform compensation is used.
- Animation arrays retain the original ART FPS. Rebuilding a loop retains the current frame and fractional phase.
- Portals are built with all state frames but are not incorrectly auto-looped. Scenery obeys `OSCF_NO_AUTO_ANIMATE`;
  critters use the original ART loop timing.

### Rebuild and Cache Behavior

- `OpenArcanumGraphicsSettings.SetRuntimeMode` is a session-only override and clears the HD loader cache whenever the
  mode changes.
- `WorldObjectSectorLoader.RebuildVisuals` asks each owned presentation to recreate its frame array from retained ART
  identity. This is an explicit narrow hook, not a global event or asset manager.
- Original standalone sprite textures are released after a successful rebuild. Cached HD textures remain owned by the
  existing HD loader and are cleared through its existing cache contract.
- Original -> Enhanced -> Original was exercised in one Play session. All 580 active visual owners rebuilt each time;
  a paused animated NPC remained on the same frame across the rebuild.

### Real Map Validation

Actual map content from `maps/arcanum1-024-fixed/101602821844.sec` and its `.mob` records was loaded, not either art
gallery. The final pass read 687 records and produced 580 drawable owners: 290 scenery, 264 walls, eight portals, nine
containers and nine NPCs. Seventy-five inventory children were intentionally not presented; 32 records still had
unresolved/non-renderable ART identities.

Local proof clips were generated from the selected real entities under Git-ignored `HDAssets/` and were never staged:

- Static scenery `art/scenery/fwd1.art`, r0/f0: 23x37 at 100 PPU became 92x148 at 400 PPU. Both modes measured world
  size 0.23x0.37, pivot approximately (0.48,0.19), identical gameplay/visual positions, and scale 1.
- Animated scenery `art/scenery/plushbed1.art`, two frames at 8 FPS: 136x111 became 544x444 for the measured frame.
  Both modes measured world size 1.36x1.11 and pivot approximately (0.47,0.31), with identical positions and scale.
- NPC `art/monster/shp/shpuwxaa.art`, requested facing 3 -> source rotation 5 with mirror: all nine 8-FPS frames were
  replaced. Frozen frame 5 measured 54x51 at 100 PPU versus 216x204 at 400 PPU, world size 0.54x0.51, pivot
  approximately (0.44,0.27), and identical gameplay/visual positions and scale. The sprite name confirmed the exact
  mirror path: `HDArtSprite_r5_f5_MirrorX`.
- A real portal `art/portal/vtnf5bu0.art` was also loaded through the replacement path during validation; its frames
  remain state-driven rather than automatically animated.

Missing fallback was tested by reversibly renaming the selected static scenery PNG, clearing through a mode switch,
and rebuilding Enhanced in the same session. That real placed entity returned to `ArtSprite`, 23x37 at 100 PPU, with
the same world size, pivot, positions and scale, while the other available replacements remained HD. The PNG was
restored afterward. Invalid-replacement warning/fallback remains the same already-validated loader path.

The final post-filter Original and Enhanced map runs completed with zero Unity Console warnings and zero errors.
The editor was returned to Original mode and Play mode was stopped.

### Files Changed

- `Assets/_Game/Scripts/Runtime/World/WorldObjectSectorLoader.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObjectSpriteOwner.cs`
- `Assets/_Game/Scripts/Runtime/World/SpriteFrameAnimator.cs`
- `Assets/_Game/Scripts/Formats/Objects/ObjectInstanceReader.cs`
- `Assets/_Game/Scripts/World/GameDataLocator.cs`
- `Assets/_OpenArcanum/Scripts/Rendering/OpenArcanumGraphicsConfig.cs`
- `Assets/_OpenArcanum/Editor/ProductionHDValidation.cs`
- `Assets/_Game/Scenes/TestTerrain.unity`

### Final CreateSprite Call-Site Classification

- Production runtime: `WorldObjectSpriteOwner` is source-aware and is the sole ordinary sector entity sprite creator.
- Gallery/test: `CharacterArtGallery` and `ObjectArtGallery` remain source-aware.
- Editor/proof: `HDReplacementProofGenerator` intentionally exercises normal and mirrored source-aware factory paths.
- Terrain/batched: `TileMapRenderer` and terrain-only `TileGallery` remain on the compatibility overload by design;
  terrain is explicitly out of scope.
- No remaining production ordinary-object caller lacks ART source identity. `WorldObject.SetArt` reaches the owner through
  `ReRender`; it does not create a sprite independently.
- HD sprites still bypass `RuntimeSpriteAtlas`; its current production use is terrain/batched rendering only.

### Commits

- `10cc88f` Add production sector sprite ownership
- `4908a72` Add real sector HD validation tools

### Remaining Limitations

- The new owner is a production runtime component consuming real map data, but the repository still lacks a complete
  shipping map/gameplay lifecycle. Sector streaming, persistent gameplay state, inventory ownership, scripts and full
  interaction construction remain separate work.
- The former 32 unresolved records were classified and fixed by the subsequent Production Object Lifecycle milestone.
- Portal frame changes are ready through `WorldObject.SetArt`, but full door/window gameplay state wiring is not part of
  this rendering milestone.
- Live mode switching is explicit per sector owner; galleries and future owners are not subscribed through a global event.
- HD sprites bypass `RuntimeSpriteAtlas`, exact 4x PNGs are the only supported replacement scale, and proof artwork is
  validation-only.
- Terrain replacement, bulk extraction/upscaling and original data modification were not started.

## Production Object Lifecycle Milestone

Completed on 2026-08-24 with Unity 6000.0.71f1.

### Unresolved Record Classification

The 32 records left by the first production-sector milestone were classified individually before rendering behavior
was changed:

- All 32 were `ResolverMiss`; none were missing prototypes, zero ART identities, unsupported types, missing ART files,
  sprite-build failures, intentionally nonvisual records, inventory children, or suppressed objects.
- All 32 were portals: 31 instances of prototype 2036 and one instance of prototype 2035.
- Every stored identity was frame 0 and used a generic door-like portal identity that had no direct `portal.mes` entry.
- Every unresolved portal had exactly one same-tile wall in an engine-defined window slot. The existing exact port of
  `a_name_portal_aid_from_wall_aid` derived an authoritative window ART identity and an existing
  `art/portal/*.art` source for all 32.
- Resolution now requires the wall-derived portal number and rotation to match the stored portal's number and rotation.
  If same-tile candidates disagree, the loader leaves the record unresolved rather than guessing.

The final sector result is 612/687 rendered records: 290 scenery, 264 walls, 40 portals, nine containers and nine NPCs.
The other 75 records are inventory children and remain intentionally absent from map presentation. There are zero
unresolved prototypes, zero unresolved ART identities and zero render issues.

### Lifecycle and Ownership Trace

`WorldObjectSectorLoader` remains the sector-level owner for ordinary SpriteRenderer entities:

1. `LoadSector` reads the selected `.sec` plus matching `.mob` records before changing the current presentation.
2. It deactivates and schedules destruction of every previously owned `WorldObjects` root, including roots surviving an
   Editor domain reload, then creates one new root and one `WorldObjectSpriteOwner` per drawable record.
3. `RebuildVisuals` recreates each retained frame array in place from ART source identity and preserves same-clip
   animation phase.
4. `UnloadSector` immediately deactivates the owned root, clears owner/diagnostic state, and returns the number released.
5. `ReloadSector` reloads the retained sector path through the same transactional read-then-replace path.
6. Destroying the loader tears down its roots and disposes its mounted read-only VFS.

State/update ownership discovered during the audit:

- Movement has no gameplay implementation yet. The `WorldObject` root is the placement owner; its visual child carries
  only authored pixel offsets. No transform compensation was introduced.
- Facing and animation-clip identity remain encoded in `WorldObject.ArtId`; `SetArt` reaches the sprite owner through the
  existing narrow `ReRender` callback. Original ART FPS continues to drive `SpriteFrameAnimator`.
- Initial visibility is authoritative from `OF_DESTROYED`, `OF_OFF`, `OF_DONTDRAW` and inventory ownership. There is no
  production script/gameplay host yet to mutate visibility after instantiation.
- Portal/container `Locked` now comes from the instance flag with prototype fallback. Portal `IsOpen` comes from the
  current ART frame; `PortalOpenable` comes from the decoded ART frame count.
- `TileMapDemo` owns terrain demonstration and its own sector browser. It is deliberately a sibling, not the object
  owner, and no shipping map-transition coordinator exists yet. `map.jmp` parsing exists, but nothing currently applies
  jump points to both sector owners. External lifecycle callers now have explicit object load/unload/reload hooks and
  cannot leave an active stale object root when they select another object sector.

### Portal Semantics and Presentation API

The portal behavior was checked against `arcanum-ce` `portal.c` and `a_name.c`, not inferred from appearance:

- Frame 0 is closed. Windows use frame 1 as open and switch directly.
- Doors at rotations 6/0 use frames 4,5,6 to open; rotations 2/4 use frames 1,2,3. Closing reverses the applicable
  sequence. Frame scheduling uses the original portal ART FPS.
- Locked, jammed and magically-held flags are interaction state rather than separate visual frames. Busted windows use
  damaged ART; busted-door destruction is gameplay behavior.

Because the repository has no authoritative gameplay interaction/animation owner yet, this milestone does not invent
one. `WorldObject.TrySetPortalVisualFrame` is a narrow presentation hook: it validates the authored frame range, shows an
already-owned frame, updates only the ART frame bits and `IsOpen`, and rejects invalid indices without changing state.
Gameplay remains responsible for locks, collision, sounds and scheduling the exact frame sequences above.

`WorldObjectSpriteOwner` also reports its last build error and destroys partially built original sprites if a rebuild
throws before adoption. HD texture lifetime remains owned by the existing HD loader cache.

### Runtime and Test Validation

The existing Unity Editor and real `maps/arcanum1-024-fixed/101602821844.sec` content were used.

- Initial load: 612 owners, one active `WorldObjects` root, 10 `SpriteFrameAnimator` components, 762 owned frames,
  32 wall-derived windows and zero render issues.
- In-place rebuild: the same 612 owners, one root, 10 animators, 762 frames and texture count; no duplicate owner or
  animation components were introduced.
- Unload: 612 owners reported released, zero active roots immediately, then zero roots/owners/animators/owned frames
  after deferred destruction. The scene's `ArtFrame` texture count fell by exactly the 762 owned textures (899 to the
  unrelated ambient 137).
- Reload: returned to the exact initial ownership/frame/texture metrics with one root and zero render issues.
- A real two-frame window at 8 FPS changed frame 0 -> 1 -> 0, synchronized `ArtId`/`IsOpen`, and rejected frame 2 without
  mutation.
- The lifecycle validation logged `PASS`. The final Unity Console showed zero warnings and zero errors.
- All 172 edit-mode tests passed, including the new real-sector wall/window derivation case and the ordinary-wall
  rejection case.

### Files Changed

- `Assets/_Game/Scripts/Runtime/World/WorldObjectSectorLoader.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObjectSpriteOwner.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObject.cs`
- `Assets/_Game/Tests/EditMode/PortalArtResolverTests.cs`
- `Assets/_OpenArcanum/Editor/ProductionHDValidation.cs`
- `PROJECT_STATUS.md`

### Commits

- `947676d` Resolve sector portals and stabilize lifecycle
- `5239f0b` Add portal presentation and lifecycle validation

### Remaining Limitations

- `TileMapDemo` and `WorldObjectSectorLoader` are separate sector owners. The former is demo terrain infrastructure,
  not a shipping map/session owner; its browser does not yet coordinate the object loader.
- Stable OID-based object mutation/persistence, dynamic creation/destruction, movement, visibility changes, inventory
  attachment and full script-host integration do not yet exist in the production map path.
- The exact portal presentation frames are available, but gameplay interaction, collision, sounds, damage/destruction
  and the authoritative timed door scheduler remain future work.
- Validation covered one real sector. Other maps may expose additional data/resolver cases and should retain the same
  explicit issue classification instead of being silently skipped.
- HD sprites still bypass `RuntimeSpriteAtlas`; exact 4x PNGs are the only supported replacement scale, and local proof
  artwork remains Git-ignored validation content.
- Terrain replacement, bulk extraction/upscaling and original game-data modification remain out of scope and were not
  started.

## Previous Recommendation (Completed)

Add a narrow map/session coordinator that selects terrain and object sectors together and uses the explicit object
load/unload/reload contract. Introduce stable OID-based state retention across sector transitions, then connect the
existing script/event interfaces to visibility, movement and the exact portal frame scheduler. Validate multiple maps
before considering a small ordinary-sprite atlas/lifetime policy. Keep terrain replacement and bulk conversion separate.

## Authoritative Object Identity Trace (Partial Session-State Milestone)

Completed on 2026-08-24 with Unity 6000.0.71f1.

The production `.sec`/`.mob` reader already retained both serialized 24-byte ObjectIDs: each instance's own ID and an
item's `OBJ_F_ITEM_PARENT` reference. A typed semantic representation now classifies the original engine variants and
uses the same equality fields as `objid_is_equal`, instead of treating all 24 bytes (including unused union/padding
bytes) as identity:

- `A` is an authored permanent numeric ID.
- `GUID` is an authored 128-bit ID.
- `P` is a static positional ID containing full tile location, temporary-at-tile ID and map number; it is map-scoped.
- `NULL` has no authoritative identity and is not eligible for persistent state.
- `HANDLE` is process-local runtime identity and is not eligible for serialized state.
- `BLOCKED` marks a prototype record and is not a placed-instance identity.

Prototype identity remains separate from instance identity. Inventory ownership remains the parent ObjectID
relationship; no inventory record was made visual and no runtime ID was fabricated. The proposed narrow boundary is
documented in `documentation_unity/world-map-session.md`: sector decoding/presentation stays in
`WorldObjectSectorLoader`; an OID-indexed map/session coordinator will retain only effective ART/facing/frame, portal
state, exact lock state and supported visibility; one coordinator-owned scheduler will apply the exact `portal.c`
frame rules at source ART FPS; terrain remains separate.

Two focused identity tests cover padding/unused-byte-insensitive `A` equality and map-scoped positional key formation.
All 174 EditMode tests passed in the existing Unity Editor. The final Console showed zero warnings and zero errors.

Commit:

- `567d2d2` Model authoritative Arcanum object identities

The runtime coordinator/scheduler implementation is not yet present. The agent execution safety reviewer classified
that combined change as a material gameplay-state architecture change and requires an explicit confirmation after
that risk is disclosed. The working tree was left clean after this status update's commit; no terrain, game data,
transforms, gameplay systems or HD assets were changed.

## World Map Session State Milestone

Completed on 2026-09-07 with Unity 6000.0.71f1 on `feature/world-session-state`.

### Architecture and implementation

- `WorldMapSessionCoordinator` owns an in-memory ObjectID-indexed state table, loaded runtime bindings and the single
  coordinator-ticked portal transition scheduler. Sector decoding and presentation remain in
  `WorldObjectSectorLoader` and `WorldObjectSpriteOwner`.
- Persistent state is restored before presentation creation and captured before unload destroys it. The schema is
  deliberately narrow: effective ART ID, stable portal state, lock/off state, source collision metadata and inventory
  parent ObjectID.
- Static `.sec` records serialized with `NULL` receive the original engine's positional identity semantics from full map
  location, zero-based sector record load index and the authoritative `MapList.mes` map number. `.mob` GUIDs and parent
  references remain unchanged. Duplicate/colliding persistent IDs are rejected before presentation replacement.
- Portal transitions use exact facing-dependent source frames and integer `1000 / FPS` timing. Windows switch directly;
  overlapping work is rejected; unload cancellation restores the last stable state before capture. Presentation cannot
  independently author managed portal state.
- Original -> Enhanced -> Original graphics rebuilds preserved runtime/stable ART IDs, portal state, selected frame,
  position, scale and visual offset without transform compensation.

### Real map validation

- Initial production audit: 687 persistent states (594 positional, 93 GUID), zero nonpersistent placed records, 75
  inventory records and 75 retained parent references, with no identity collision.
- The validator loaded `maps/arcanum1-024-fixed/122473678402.sec` and used the seven-frame, 8 FPS door
  `P_000190AE_0001C864_000000FB_00000001` (prototype 2036, `art/portal/toue3au0.art`, ART ID `0x33102800`, rotation 5).
- Two cycles passed for Open -> unload -> reload, Close -> unload -> reload, interrupted Opening rollback and interrupted
  Closing rollback. Frames were 1,2,3 opening and 2,1,0 closing at the original 0.125-second interval.
- Each reload retained the same state record, 549 owners, five animators, 706 frames, transforms, texture counts,
  parent/unrelated state and the sector's one pre-existing `UnsupportedArtType` issue signature. No duplicate runtime
  objects or orphan scheduler jobs remained; final active transition count was zero.
- Final complete EditMode suite: 200 passed, 0 failed, 0 skipped. Final Unity Console: 0 warnings, 0 errors.

### Files changed

- `Assets/_Game/Scripts/Formats/Objects/ArcanumObjectId.cs`
- `Assets/_Game/Scripts/Runtime/World/PersistentObjectState.cs`
- `Assets/_Game/Scripts/Runtime/World/PortalTransitionScheduler.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldMapSessionCoordinator.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObject.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObjectSectorLoader.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObjectSpriteOwner.cs`
- `Assets/_Game/Tests/EditMode/Arcanum.Formats.Tests.asmdef`
- `Assets/_Game/Tests/EditMode/WorldSessionStateTests.cs`
- `Assets/_OpenArcanum/Editor/WorldSessionValidation.cs`
- `documentation_unity/world-map-session.md`
- `PROJECT_STATUS.md`

### Commits

- `1ea9b1a` Add typed in-memory world session state
- `52395dd` Own portal transitions and unload rollback in session state
- `1c75cbc` Derive persistent identities for static sector objects
- `c2049b3` Validate world session persistence and portal timing

### Remaining limitations

- State remains in-memory; save-game serialization is not implemented.
- The production owner presents one object sector at a time. Dynamic creation/destruction,
  inventory attachment, scripts, collision, sounds, portal damage and full gameplay interaction policy are later work.
- The validation sector has one known unsupported ART-type record. Its exact classification remained stable on reload.
- Terrain, bulk extraction/upscaling and original Arcanum data were not modified.

## Player Navigation Milestone

Completed on 2026-09-07 with Unity 6000.0.71f1 on `feature/player-navigation`.

### Architecture and behavior

- TestTerrain now routes undragged ground clicks through the existing camera/isometric projection into a deterministic,
  eight-direction source-grid pathfinder. Unity NavMesh and colliders are not used.
- `SectorNavigationMap` combines the authored sector block mask, tile `/b` flags, non-walkable facades, ordinary object
  blockers, directional wall pieces and live portal state. `OF_NO_BLOCK` is preserved, and diagonal steps check both
  source-adjacent wall edges rather than cutting corners.
- The production lifecycle creates and explicitly binds a deterministic session-owned PC. No production or validation
  path silently selects a sector-authored NPC.
- Valid new destinations replace active routes; blocked and unreachable destinations fail without partial movement.
  Movement uses fractional tile coordinates, source direction/facing mappings, WALK action 1 and STAND action 0.
- The session coordinator owns movement position/ART state. Graphics rebuilds preserve gameplay position, while an
  unload during movement retains the fractional position and normalizes the reloaded action to STAND at the same facing.

### Validation

- Focused PlayerNavigation EditMode category: 21 passed, 0 failed, 0 skipped.
- Complete EditMode suite: 221 passed, 0 failed, 0 skipped.
- Computer Use entered Play mode on real `maps/arcanum1-024-fixed/101602821844.sec`, issued a physical Game-view ground
  click and verified that `PlayerClickMoveInput` accepted it as a route.
- Real-sector validation used persistent NPC `G_1CA8B264_6113_F24C_BFDD_ED869173A4A7` and passed source-edge traversal,
  route replacement, blocked-target rejection, graphics rebuild during movement, fractional unload/reload restoration,
  post-reload routing and WALK -> STAND arrival. It completed at tile `(36,58)`.
- The final Unity Console showed zero warnings and zero errors.

### Files changed

- `Assets/_Game/Scenes/TestTerrain.unity`
- `Assets/_Game/Scripts/Runtime/World/DeterministicTilePathfinder.cs`
- `Assets/_Game/Scripts/Runtime/World/PersistentObjectState.cs`
- `Assets/_Game/Scripts/Runtime/World/PlayerClickMoveInput.cs`
- `Assets/_Game/Scripts/Runtime/World/PlayerNavigationController.cs`
- `Assets/_Game/Scripts/Runtime/World/SectorNavigationMap.cs`
- `Assets/_Game/Scripts/Runtime/World/TileRouteFollower.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldMapSessionCoordinator.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObject.cs`
- `Assets/_Game/Scripts/Runtime/World/WorldObjectSectorLoader.cs`
- `Assets/_Game/Tests/EditMode/Arcanum.Formats.Tests.asmdef`
- `Assets/_Game/Tests/EditMode/PlayerNavigationTests.cs`
- `Assets/_Game/Tests/EditMode/WorldSessionStateTests.cs`
- `Assets/_OpenArcanum/Editor/PlayerNavigationValidation.cs`
- `documentation_unity/player-navigation.md`
- `documentation_unity/world-map-session.md`
- `PROJECT_STATUS.md`

### Commits

- `acf94bc` Implement source-faithful player navigation
- `29ee5e3` Validate physical click navigation

### Remaining limitations

- Navigation remains sector-local; M1A later added shared terrain/object selection, while cross-sector continuation
  remains M1B.
- M1A later added a deterministic production PC lifecycle; character-creation and save-data initialization remain out
  of scope.
- Dynamic blockers, critter avoidance, interaction range, combat movement, scripts and save serialization are later work.
- Original game data, terrain replacement and bulk asset conversion were not modified.

## M1A Shared Sector and Production PC Lifecycle

Completed on 2026-09-07 with Unity 6000.0.71f1 on `feature/player-navigation`.

- `WorldMapSessionCoordinator.SelectSector` owns normalized selection and drives `TileMapDemo` terrain plus
  `WorldObjectSectorLoader` objects through presentation-owner interfaces. Selection and unload events drive the
  dedicated `ProductionPlayerLifecycle`; neither presentation owner is gameplay authority.
- `PersistentPlayerState` owns deterministic GUID identity
  `G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`, sector, fractional position, and critter ART state independently of Unity
  objects. Spawn, bind, unbind, rebuild, unload, and reload preserve that record. Navigation binds only its explicit PC;
  the former validation NPC fallback was removed.
- The production presentation uses source-valid base ART ID `0x28100000` (human male, villager clothes, unarmed,
  `STAND`). The normal critter resolver maps it to `art/critter/hmm/hmmv1xaa.art`. The interrupted `0x18100000`
  placeholder was type `1` (wall), not a critter; no NPC substitution or renderer bypass was introduced.
- Validation: M1 lifecycle 7/7, PlayerNavigation 21/21, complete EditMode 228/228, all with 0 failures or skips. A
  physical Game-view click moved the visible PC; real-sector validation passed shared selection, graphics rebuild,
  unload/reload restoration, post-reload movement, and exactly one coordinator, loader, lifecycle, navigation
  controller, object root, PC presentation, and PC sprite owner. Final Unity Console: 0 warnings, 0 errors.
- Final ownership/call graph and lifecycle contract:
  [`documentation_unity/m1-shared-sector-pc-lifecycle.md`](documentation_unity/m1-shared-sector-pc-lifecycle.md).
- M1B subsequently completed cross-sector destination continuation. Save serialization, dynamic blockers and all
  interaction/inventory/combat/dialogue/progression work remain later milestones.

## M1B Cross-Sector Traversal and Route Continuation

Completed on 2026-09-08 with Unity 6000.0.71f1 on `feature/player-navigation`.

- Canonical PC location and destination intent are map-global tiles. `SectorCoordinate` derives 64×64 sector identity,
  packed source filename, and local presentation position.
- `CrossSectorBoundaryPlanner` deterministically selects reachable cardinal source exits. The coordinator captures and
  unloads the old sector, relocates the same PC state, selects both target owners, and lets the lifecycle re-project the
  same GUID through the ordinary critter renderer.
- Target-side blocked or final-destination-unreachable entries are rolled back through the coordinator and excluded for
  that active intent so the next legal crossing is tried. Exhaustion stops safely in the last valid sector.
- One post-transition Update presents the exact wrapped entry before movement resumes, preventing a load-inflated delta
  from skipping that lifecycle pose. The global intent and accepted local route remain active.
- Real validation completed `101602821844.sec` → `101602821845.sec` → A → B → A with correct WALK/facing, exact entries,
  automatic continuation, STAND arrival, a missing-west-edge rejection, Original/Enhanced/reverted rebuilds, stable
  PC/object/portal state, and exactly one coordinator, terrain owner, loader/root, lifecycle, navigation controller, PC
  runtime/presentation, and PC sprite owner.
- Validation: M1B 11/11, M1A 7/7, PlayerNavigation 21/21, WorldSessionStateTests 27/27,
  PortalArtResolverTests 2/2, and complete EditMode 239/239. Final Unity Console: 0 warnings, 0 errors.
- Architecture and detailed validation: [`documentation_unity/m1b-cross-sector-navigation.md`](documentation_unity/m1b-cross-sector-navigation.md).
- Remaining limitations are map-to-map/world-map travel, scene-reload continuation, dynamic critter reservations, and
  save serialization. Interaction, inventory, combat, dialogue, progression, and M2 work were not started.

## M2A Interaction Kernel and Real Portal Use

Completed on 2026-09-08 with Unity 6000.0.71f1 on `feature/interaction-kernel`.

- `WorldInteractionCommand` carries persistent actor/target identities, `Use`, and an optional map-global interaction
  position. `WorldInteractionResult` returns an explicit success, accepted-approach, validation failure, block,
  unsupported, or cancellation result.
- `PlayerClickMoveInput` performs presentation-assisted alpha hit testing but passes only `ArcanumObjectId` to
  gameplay. Portal overlap resolves by render order then lexical ObjectID; a miss remains ordinary ground movement.
- `PlayerInteractionController` owns only transient `Idle/Approaching/Executing/Completed/Cancelled` intent.
  `InteractionApproachPlanner` reuses deterministic source-grid A* to choose the shortest reachable range tile. Manual
  movement, another target, unload/disappearance, or route failure clears the pending command with no stale execution.
- `WorldMapSessionCoordinator.ExecuteInteraction` is the authoritative boundary. It resolves session-owned actor,
  target, canonical positions, lock state, and effective `SAP_USE`; applies the source range-two Chebyshev rule; and
  delegates unscripted portal toggles to the existing scheduler. Script-bearing doors return `Unsupported`; locked
  doors return `Blocked`. No presentation component owns gameplay state.
- Real Play Mode proof used sector `maps/arcanum1-024-fixed/122473678402.sec`, door
  `P_00019096_0001C870_00000164_00000001`, prototype 2036, `art/portal/toue3au0.art`, ART ID `0x33102800`, rotation 5,
  seven frames at 8 FPS. A physical Game-view click selected that exact ObjectID and opened it. The edge became
  passable, the PC walked through, a second Use closed/reblocked it, distant approach executed automatically, and Open
  state survived unload/reload.
- The same live run passed cancellation, target replacement, Original/Enhanced rebuild during pending intent, stable
  PC/door state identity, unique coordinator/loader/navigation/interaction/PC presentation ownership, and no stale
  portal work. It recorded 0 new warnings and 0 errors.
- Validation: M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionStateTests 27/27,
  PortalArtResolverTests 2/2, complete EditMode 255/255; no failures or skips. Final compilation was clean.
- Full architecture and limitations: [`documentation_unity/m2a-interaction-kernel.md`](documentation_unity/m2a-interaction-kernel.md).

At the M2A checkpoint, remaining M2 limitations were portal-only targeting/default behavior, production `SAP_USE`,
keys/lock resolution, sounds, Examine, containers/items, unloaded cross-sector target discovery, cursor/UI affordances,
and save serialization. M2B addresses only the first bounded `SAP_USE` case below. Inventory, dialogue, combat, quests,
and progression were not started.

## M2B Production SAP_USE Dispatch

Completed on 2026-09-09 with Unity 6000.0.71f1 on `feature/interaction-kernel`.

- `WorldMapSessionCoordinator.ExecuteInteraction` remains authoritative for actor/target identity, range, state, lock
  policy, and portal scheduling. It now delegates attached use scripts to a session-bound `WorldUseScriptDispatcher`
  before deciding whether the existing built-in portal action may run.
- `WorldObjectSectorLoader` loads the production `ScriptDatabase` from the read-only game VFS and binds the resolver to
  the session. Session-owned `ScriptGlobals` and persistent ObjectID state survive visual rebuild and sector reload;
  the loader, `WorldObject`, sprite owners, input, and demo components do not own gameplay decisions.
- Production dispatch uses opaque stable ObjectID references for Triggerer and Attachee. The PC remains the M1
  deterministic identity `G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`; its one-presentation lifecycle and source-valid
  `0x28100000` critter ART contract are unchanged, with no NPC substitution.
- `ScriptVm.ExecuteStrict` distinguishes executed, missing, empty, invalid-context/line, unsupported, runaway, and
  runtime-error outcomes. The bounded production policy preflights the full script and every non-success result fails
  closed. Script success communicates only skip-default or run-default; only the existing coordinator/scheduler path
  can change portal state.
- The authentic target is `scr/01162door_to_the_panarii_offices_use.scr` in
  `maps/caladon-panarrii temple/67108865.sec`. Script 1162 returns run-default only when global flag 2087 equals 1 and
  otherwise suppresses the built-in portal action. Physical validation used portal
  `P_0000005E_0000005F_000000F6_0000003B`.
- A literal Game-view click selected that stable ID, moved the production PC through the existing source-grid route,
  executed SAP_USE on arrival, and scheduled the portal toggle only on the source-authorized branch. Clear-flag
  suppression, rebuild, full unload/reload, authoritative PC/portal/script-state identity, unique owners/controllers,
  and 0 new warnings/errors also passed.
- Validation: M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11, and complete EditMode 263/263; no
  failures or skips. Final compilation was clean.
- Full architecture, lifecycle/default contract, source decision, and limitations:
  [`documentation_unity/m2b-sap-use-dispatch.md`](documentation_unity/m2b-sap-use-dispatch.md).

## M3A Authoritative Inventory State

Completed on 2026-09-11 with Unity 6000.0.71f1 on `feature/inventory-state`.

- `PersistentObjectState` retains immutable authored `OBJ_F_ITEM_PARENT` and separately owns one current typed
  `World(normalized sector, tile)` or `Contained(parent ObjectID)` placement. The session coordinator is authoritative;
  GameObject/Transform hierarchy and all world-object/sprite/loader components are presentation only.
- `TransferItem(itemId, expectedSource, destination)` validates source, item/owner types, destination, self/cycle rules,
  and commits one placement or nothing. Typed failures leave placement, identity, and child relationships unchanged.
  Unresolved authored parents are preserved because real sector data can reference an owner absent from that load;
  newly requested destinations must resolve to a session-owned container, PC, or NPC.
- Runtime creation resolves a real item prototype before registering `SessionDynamic` ObjectID type 4. The monotonic
  per-session sequence begins at `D_0000000000000001`, is stable across transfers/reloads/traversal, and cannot collide
  structurally with authored A/G/P identity variants. It is an in-memory contract pending M6 save-schema work.
- Presentation observes committed placement events. Containment removes ordinary rendering and navigation occupancy;
  returning to world uses the existing object/ART path and restores one projection at the authoritative tile. Visual
  rebuild, unload/reload, and PC A→B→A traversal do not rewrite ownership or duplicate state/presentation.
- Real fixture: sector `maps/arcanum1-024-fixed/101602821844.sec`, container
  `G_8F454608_E327_1341_B85B_E7A5402D4758` (prototype 3052), armor child
  `G_0435F503_6600_6342_97B2_6D9E1A85A2F2` (prototype 8127), with exact decoded parent equal to the container.
- Validation: M3A 8/8, M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11,
  WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode 271/271; zero failures or skips. The real Play
  Mode harness transferred the authored child and dynamic `D_0000000000000001`, proved a rebuild retained containment
  and the next allocation `D_0000000000000002`, reloaded, crossed sectors, returned, and restored exactly one world
  projection for each dropped item. It explicitly found one `WorldObjects` root, PC runtime, and PC sprite owner. Final
  Unity Console/harness: 0 warnings, 0 errors.
- Full source semantics, API/result contract, call graph, validation, and limitations:
  [`documentation_unity/m3a-inventory-state.md`](documentation_unity/m3a-inventory-state.md).

## M3B Pickup, Drop, and Inventory Commands

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Stable-ID `PickUp`, `Drop`, and owner-to-owner `Transfer` commands are validated by
  `WorldMapSessionCoordinator` and commit only through M3A's atomic `TransferItem` primitive. Presentation remains an
  observer of typed persistent placement.
- Ordinary pickup uses the source `AG_PICKUP_ITEM` range of 0. The existing interaction/navigation controllers approach
  the exact item tile and revalidate on arrival; replacement movement cancels without a stale transaction.
- Drop requires exact PC ownership, a valid integer tile in the active sector, and no `OIF_NO_DROP` (`0x20`). Source
  `SAP_GET`/`SAP_DROP` and other script/equipment/capacity/UI consequences remain explicitly deferred.
- Real proof item: Food prototype 10078, `G_8781D726_74FE_0846_AD0A_88EE591B6383`, authored at `(10,46)` in
  `maps/arcanum1-024-fixed/101602821845.sec`. A literal Game-view click selected that exact ObjectID, moved the PC,
  and committed one pickup.
- The same item remained contained through rebuild/reload and A-to-B-to-A traversal, transferred PC-to-container and
  back using real container `G_8F454608_E327_1341_B85B_E7A5402D4758` (prototype 3052), then dropped with its identity,
  prototype, and ordinary ART presentation intact.
- The loader now projects eligible session-retained items moved into a foreign sector even when that sector has no
  authored source record for them. Destination reload restores one presentation, while the original source record
  remains suppressed and cannot duplicate or resurrect the item.
- Original/Enhanced rebuilds preserved contained and world state. Dynamic `D_0000000000000001` used the identical
  command path and survived rebuild/reload; allocation continued at `D_0000000000000002`.
- Validation: M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11,
  WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode 284/284. Every run had zero failures, skips, or
  inconclusive tests. The Play Mode harness recorded 0 new warnings and 0 errors, with unique session, controller,
  root, PC, sprite-owner, item-state, containment, and presentation ownership.
- Full source boundary, ownership graph, fixture, and validation:
  [`documentation_unity/m3b-inventory-commands.md`](documentation_unity/m3b-inventory-commands.md).

## M3C Authoritative Equipment State

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- `ObjectPlacement` now distinguishes `World`, ordinary `Contained(parent)`, and source-typed
  `Equipped(parent, WornLocation)`. The exact values are Helmet 1000, Ring1 1001, Ring2 1002, Medallion 1003,
  Weapon 1004, Shield 1005, Armor 1006, Gauntlet 1007, and Boots 1008.
- `WorldMapSessionCoordinator` owns equip/unequip eligibility and state. It exposes deterministic ordinary-inventory,
  slot-occupant, worn-location, and equipped-item queries; presentation hierarchy is never queried.
- Occupied-slot replacement commits new equipment and displaced ordinary containment before notifying observers.
  `OIF_NO_DROP` removal failures, invalid owners/items/locations, incompatible slots, and fixed-two-hand/shield conflicts
  mutate nothing. Generic transfer cannot remove an equipped item without the equipment command boundary.
- The loader retains inventory ART, weapon flags, and generic flags needed for minimum source eligibility. Authored worn
  locations enter the session directly as equipment, duplicate authored slot membership is rejected, and equipped
  items are never independently world-presented.
- Real fixture: armor `G_0435F503_6600_6342_97B2_6D9E1A85A2F2`, prototype 8127, world ART `0x60040082`, inventory
  ART `0x60041082`, item flags `0`, decoded Armor location 1006, from real container
  `G_8F454608_E327_1341_B85B_E7A5402D4758` in `maps/arcanum1-024-fixed/101602821844.sec`.
- Computer Use literal-click validation passed pickup, Equip, occupied-slot swap with `D_0000000000000001`, reverse
  swap, Original/Enhanced rebuild, A-to-B-to-A traversal, reload, Unequip, Drop, final reload, dynamic equip/reload,
  unique state/membership/presentation, and deterministic production-PC ownership. The harness recorded 0 new warnings
  and 0 errors.
- Validation: M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A 7/7,
  M1B 11/11, WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode 297/297. All had 0 failures,
  skips, or inconclusive tests; final compilation was clean.
- Source boundary, atomic contract, call graph, fixture, and deferred effects:
  [`documentation_unity/m3c-equipment-state.md`](documentation_unity/m3c-equipment-state.md).

## M3D Authoritative Stack Quantities and Atomic Merge/Split

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Source analysis established that only Ammo (`OBJ_F_AMMO_QUANTITY`) and Gold (`OBJ_F_GOLD_QUANTITY`) are stacks.
  Both serialize as signed Int32 instance/prototype fields; quantity one remains a stack, canonical Ammo defaults to 10,
  Gold defaults to 1, and no smaller type/prototype maximum exists. M3D validates `1..Int32.MaxValue` and rejects
  overflow or invalid loaded/requested values explicitly.
- `PersistentObjectState.StackQuantity` is the sole runtime quantity authority. Loader registration applies the exact
  instance override or prototype default. Placement transfers, sector traversal/reload, and graphics rebuild never
  derive or rewrite quantity through GameObjects, sprite owners, or duplicate presentation.
- State restore projects quantity one-way into the existing `WorldObject.AmmoQuantity`/`GoldQuantity` cache for
  source-compatible runtime consumers. Capture never reads those cache values back, so they remain non-authoritative.
- Source stack compatibility is exact prototype equality between positive Ammo/Gold stacks. Instance flags,
  descriptions, scripts, condition/charge, magic/tech state, owner, and inventory cell are not source compatibility
  fields. Placement and same-owner requirements are validated separately by the transaction.
- `MergeStacks` atomically moves a requested quantity between same-owner ordinary `Contained` stacks. The destination
  ObjectID survives; a partial source retains its identity/remainder, while a fully consumed source is removed and
  tombstoned. `SplitStack` retains the source ObjectID/remainder and allocates exactly one new `D_` identity through
  M3A's existing monotonic allocator. Every failure leaves quantity, placement, membership, and identity state intact.
- M3A/M3B insertion into a PC/NPC/container now automatically merges a compatible incoming stack exactly as source
  `item_insert`: the pre-existing destination-owner identity survives and the incoming identity is consumed. World
  drops do not merge; explicit world/equipped split or merge is rejected. Loader removal observes only committed state,
  and authored tombstones suppress reload resurrection/presentation.
- Real fixture: Ammo `G_9239E097_A8D2_C147_9F58_76077340C60E`, prototype 7059, source quantity 60, world ART
  `0x60000041`, item flags 0, `SAP_USE` 0, authored in real container
  `G_8F454608_E327_1341_B85B_E7A5402D4758` (prototype 3052) in
  `maps/arcanum1-024-fixed/101602821844.sec`. Real incompatible proof used Ammo
  `G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`, prototype 7058, quantity 70.
- Computer Use literal-click validation passed pickup at quantity 60, a 25/35 split into
  `D_0000000000000001`, PC-to-real-container transfer, source-style transfer-back merge with the authored destination
  surviving, drop, one world projection, reload, A-to-B-to-A traversal, and Original/Enhanced/reverted rebuild. A
  dynamic prototype-7059 stack used `D_0000000000000002`, split to `D_0000000000000003`, merged with `D_2` surviving
  at quantity 10, and the next creation received `D_0000000000000004`. The harness recorded 0 new warnings and 0 errors.
- Validation: M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16,
  PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode
  315/315. All had 0 failures, skips, or inconclusive tests; final compilation was clean.
- Full source evidence, transaction/identity contract, final ownership graph, fixtures, and validation:
  [`documentation_unity/m3d-stack-state.md`](documentation_unity/m3d-stack-state.md).

## M4D Derived Character Statistics and Alignment/Reaction Inputs

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- A pre-production source audit classified every remaining source stat and recovered exact formulas, bounds, ordering,
  PC/NPC exceptions, and deferred dependencies. Common base AC field 26 is now parsed alongside the existing
  resistance array, reaction base, NPC/critter flags, and 28-slot stat array.
- `WorldMapSessionCoordinator.DerivedStats` owns one typed `CharacterDerivedStatService`; persistent source inputs and
  mutable Alignment are keyed by stable ObjectID outside Unity presentation. Real source initialization uses whole-field
  instance-over-prototype precedence and changed-source collisions fail explicitly.
- Implemented deterministic queries: delegated Carry Weight, Melee Damage Bonus, AC Adjustment/final base Armor Class,
  Speed statistic, Heal Rate, Poison Recovery, Beauty Reaction Modifier, Maximum Followers, Magick/Tech Aptitude, five
  innate resistances, and Alignment. Audited race aptitude/resistance effects and the monstrous-NPC resistance clamp
  exception are preserved. Equipment/environment/status/spell-effect stages remain neutral.
- Alignment is slot 19, neutral 0, evil negative, good positive, clamped -1000..1000 with controlled atomic mutation.
  Magick/Tech Points are immutable slot 22/23 inputs clamped 0..210. Pairwise reaction input exposes NPC base + PC
  Beauty + the exact NPC-race/PC-race matrix only; it deliberately excludes reputation, faction, memory, AI, and social
  consequences.
- Production Human Male PC result at controlled Level 2: Carry 4000, Damage -1, AC adjustment/final -2/0, Speed 8,
  Heal 3, Poison Recovery 8, Beauty Reaction -7, Followers 2, Aptitude 0, Alignment 0, resistances 0/0/0/20/0.
  Authentic NPC `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, prototype 17101, resolves Damage 0, AC -1/0,
  Speed 9, Heal 5, Poison Recovery 16, Beauty Reaction 0, Followers 2, Aptitude -5, Alignment 100, resistances
  0/0/0/60/0; its bounded initial reaction toward the PC is 43.
- Computer Use Play Mode proved immediate Race/Gender invalidation/restoration, correct Level non-dependence, neutral
  authentic inventory/equipment transitions, Original→Enhanced→Original, NPC reload, PC A→B→A, presentation
  recreation, 14 matching unique character/progression/vitality/derived records, and no duplicate scene ownership.
  It recorded 0 new warnings and 0 errors.
- Validation: M4D 25/25; required regression matrix 227/227 across M4C, M4B, M4A, M3E-M3A, M2B-M2A,
  PlayerNavigation, M1A-M1B, WorldSessionState, and PortalArtResolver; complete EditMode 423/423. Every final run had
  0 failures, skips, or inconclusive tests; final compilation was clean.
- Full audit, formulas, ownership graph, source/fixture contract, lifecycle proof, and deferred boundaries:
  [`documentation_unity/m4d-derived-character-stats.md`](documentation_unity/m4d-derived-character-stats.md).

## M4C Authoritative Skills and Progression

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Source analysis established the exact 16-skill numeric mapping: Bow 0, Dodge 1, Melee 2, Throwing 3, Backstab 4,
  Pick Pocket 5, Prowling 6, Spot Trap 7, Gambling 8, Haggle 9, Heal 10, Persuasion 11, Repair 12, Firearms 13,
  Pick Locks 14, and Disarm Traps 15. Basic and technical arrays are fields 221/222; bits 0-5 store purchased points
  and bits 6-7 store None/Apprentice/Expert/Master training. One purchased point equals four rank units.
- `WorldMapSessionCoordinator.Progression` owns one `CharacterProgressionService`. Stable ObjectID-keyed records own
  immutable source identity and mutable XP, level, unspent points, purchased skill points, and training independently
  of Unity presentation. Real NPC initialization uses whole-array instance-over-prototype precedence; collisions or
  changed source data fail explicitly.
- Effective skill queries consume M4A governing attributes and the exact cap sequence
  `3,3,3,3,3,7,7,7,11,11,11,15,15,15,19,19,19,20,20,20`. Skill increases cost one character point and commit only
  when the next four-unit rank fits that cap. Training is separate, requires ranks 1/9/18, and advances upward one tier
  at a time. Monstrous melee preserves its audited derived rank/training rules.
- XP and level use stat slots 18 and 17; unspent character points use slot 21. Retail thresholds 1-50 are embedded
  exactly from `rules/xp_level.mes`; level 50 is the playable maximum, while XP retains its independent 2,000,000,000
  field bound. PC-only XP awards can cross multiple levels and award one point per level plus one extra at every fifth
  level. The point pool is bounded at 56.
- The production Human Male PC explicitly begins at level 1, XP 0, five points, zero purchased skills, and no training.
  Real NPC `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, prototype 17101, in
  `maps/arcanum1-024-fixed/101602821844.sec` resolves level 21, XP 162,500, zero points, Bow/Melee/Gambling 8,
  Throwing/Haggle/Heal/Persuasion/Firearms 4, and no training. A second fixture proves Expert and Apprentice packed
  training values.
- M4B vitality now consumes the authoritative progression level through a narrow level-provider interface. A level
  change recomputes maxima without resetting accumulated HP/Fatigue damage; isolated vitality callers retain their
  source-level fallback.
- Computer Use Play Mode validation awarded the PC 2,100 XP, reached level 2, awarded one point, spent one point to
  raise Bow to rank 4, and rejected a second increase at the Dexterity-8 governing cap atomically. HP/Fatigue maxima
  became 32/32 while damaged current values were preserved. State/reference identity survived
  Original/Enhanced/Original rebuild, NPC unload/reload, and PC A-to-B-to-A traversal. The session held 14 unique
  progression records with unique presentation/session owners and recorded 0 new warnings and 0 errors.
- Validation: M4C 29/29; required regression matrix 227/227 across M4B, M4A, M3E-M3A, M2B-M2A, PlayerNavigation,
  M1A-M1B, WorldSessionState, and PortalArtResolver; complete EditMode 398/398. Every run had 0 failures, skips, or
  inconclusive tests; final compilation was clean.
- Full source mapping, threshold table, rank/training model, ownership graph, fixture, lifecycle, validation, and
  deferred boundaries: [`documentation_unity/m4c-skill-progression.md`](documentation_unity/m4c-skill-progression.md).
- Deferred: attribute purchase, technical-aptitude side effects, temporary/background/equipment/spell modifiers,
  XP-producing gameplay, auto-level schemes, interactive trainers, character creation/leveling UI, crafting,
  magic/technology progression, and save serialization.

## M4B Authoritative Character Vitality

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Source analysis established six independently inherited Int32 scalars: HP points/adjustment/damage at common fields
  27-29 and fatigue points/adjustment/damage at critter fields 224-226. The whole 28-slot stat array remains inherited
  as one value. Points and damage floor at zero; adjustments remain signed; Level is clamped to 0..51.
- Maximum HP is `4 * HP points + HP adjustment + effective Willpower + 2 * (effective Strength + Level) + 4`.
  Maximum Fatigue is `4 * fatigue points + fatigue adjustment + 2 * (Level + effective Constitution) + effective
  Willpower + 4`. Current values are maximum minus accumulated damage; over-damage is retained without introducing
  death or unconsciousness policy.
- `WorldMapSessionCoordinator.Vitality` owns one `CharacterVitalityService`. Persistent records are keyed by stable
  `ArcanumObjectId`, consume M4A effective attributes rather than decoding them again, and survive sector and Unity
  presentation lifecycles. Race/Gender maximum changes shift damage by the maximum delta to preserve current values
  where possible. `WorldObject` and sprite/GameObject owners contain no vitality counter or authority.
- The production Human Male PC is explicitly Level 1 with zero vitality points/adjustments/damage and starts at HP
  30/30 and Fatigue 30/30. Real Human Female NPC `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, prototype 17101, in
  `maps/arcanum1-024-fixed/101602821844.sec` resolves Level 21, HP adjustment 4 and fatigue damage 4, producing HP
  76/76 and Fatigue 82/86.
- Physical Play Mode validation applied controlled NPC damage to HP 71/76 and Fatigue 75/86 and production-PC
  damage/restoration to HP 26/30 and Fatigue 25/30. Exact state/reference identity survived Original/Enhanced/Original
  rebuild, NPC unload/reload, PC A-to-B-to-A traversal, and presentation recreation. There was one vitality record and
  presentation per identity, unique session/presentation owners, 0 new warnings, and 0 errors.
- Validation: M4B 20/20, M4A 13/13, M3E 21/21, M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8,
  M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionState 27/27, PortalArtResolver 2/2, and complete
  EditMode 369/369. Every run had 0 failures, skips, or inconclusive tests; final compilation was clean.
- Full source audit, formulas, representation, lifecycle contract, final ownership graph, fixture, and validation:
  [`documentation_unity/m4b-character-vitality.md`](documentation_unity/m4b-character-vitality.md).
- Deferred: death, unconsciousness, regeneration, combat, healing items/spells, poison, equipment/spell/background
  effects, leveling, skills, UI, resting, encumbrance consequences, script-host integration, and save serialization.

## M4A Authoritative PC/NPC Primary Attributes

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Source analysis established that PC and NPC base stats share `OBJ_F_CRITTER_STAT_BASE_IDX` (field 220), a 28-slot
  Int32 array whose first eight source IDs are Strength 0, Dexterity 1, Constitution 2, Beauty 3, Intelligence 4,
  Perception 5, Willpower 6, and Charisma 7. All default to 8 and ordinarily range from 1 to 20, with the documented
  source race-specific maxima. An instance inherits the prototype array wholesale and the engine copies that whole
  array on first override, so initialization uses `instance.StatBase ?? prototype.StatBase`, never per-slot merging.
- `WorldMapSessionCoordinator.Characters` owns one `CharacterStatService`. Its `PersistentCharacterState` records are
  keyed by stable `ArcanumObjectId`, carry immutable typed base attributes plus explicit race/gender inputs, and survive
  graphics/presentation teardown and sector lifecycle independently of `WorldObject`, GameObject, Transform, or sprite
  owners. Invalid identities, object types, source arrays, IDs, ranges, collisions, and unknown queries fail explicitly.
- Effective queries implement only fully audited retail Race effects 64-74 and Female effect 330, followed by the
  original race-aware final clamp. Race/Gender replacement removes the prior contribution without mutating base.
  Background/environment, poison, item/equipment, spell, injury, tech, class, bless/curse, and other effect operators
  remain explicit deferred inputs.
- The deterministic development PC is explicitly Human Male with all eight base/effective values 8; ART is not an
  attribute source. Effective Strength is available through the typed character service for the next carry-capacity
  consumer, but M4A computes neither carry weight nor damage.
- Real fixture: female Human NPC `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, prototype 17101, from
  `maps/arcanum1-024-fixed/101602821844.sec`. Its prototype is all 8s; its whole instance base override is
  `[10,9,15,10,10,10,8,10]` and its effective values are `[9,9,16,10,10,10,8,10]` in source order.
- Computer Use Play Mode validation passed the real NPC and production PC queries, Female add/remove proof,
  Original/Enhanced/reverted rebuild, NPC unload/reload, PC A-to-B-to-A traversal, stable state reference/identity,
  unique presentation ownership, and production-PC navigation binding. It recorded 0 new warnings and 0 errors.
- Validation: M4A 13/13, M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16,
  PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode
  328/328. All had 0 failures, skips, or inconclusive tests; final compilation was clean.
- Full source mapping, limits, effect boundary, fixture, ownership graph, and validation:
  [`documentation_unity/m4a-character-attributes.md`](documentation_unity/m4a-character-attributes.md).

## M3E Source-Faithful Inventory Weight and Capacity

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Retail `OBJ_F_ITEM_WEIGHT` is retained as an integer in tenths of a pound after instance-over-prototype resolution.
  Ordinary items contribute it once; Gold contributes zero; Ammo contributes `storedWeight * floor(quantity / 4)`.
  Direct contained and equipped items count, with no recursive nested-container load.
- The session-owned `InventoryCapacityService` derives all load and capacity from persistent state. PC/NPC carry
  capacity is `clamp(500 * effective Strength, 300, 10000)` through M4A's typed service; the Human Male production PC
  is Strength 8/capacity 4000 and the real Human Female NPC fixture is Strength 9/capacity 4500.
- Source container room is geometry rather than an invented weight field: critters use a 10x12 grid and ordinary
  containers use 10x96. Inventory ART first-frame dimensions round up to 32-pixel cells with a 1x1 source fallback;
  deterministic first-fit placement and compatible-stack footprint reuse match the admitted source behavior.
- Transfer, dynamic creation, automatic/explicit merge, split, unequip, and slot replacement validate the committed
  final weight/grid state before placement, quantity, tombstone, identity allocation, callback, or presentation
  mutation. Explicit `TooHeavy` and `NoRoom` outcomes preserve the complete pre-command state.
- Authentic fixtures were Food 10078 `G_8781D726_74FE_0846_AD0A_88EE591B6383` (weight 50, footprint 1x2), Ammo 7059
  `G_9239E097_A8D2_C147_9F58_76077340C60E` (quantity 60, total weight 15, footprint 2x1), Container 3052
  `G_8F454608_E327_1341_B85B_E7A5402D4758` (960 cells), and NPC 17101
  `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`.
- Computer Use literal-click validation succeeded exactly at capacity, then proved an over-capacity dynamic pickup
  returned `TooHeavy` with unchanged identity/placement/load/presentation and no stale pending command. The same run
  passed PC/container transfer, finite-grid `NoRoom`, Ammo split/merge, weighted equip/unequip, dynamic `D_` behavior,
  foreign-sector relocation, Original/Enhanced/reverted rebuild, unload/reload, B-to-A-to-B traversal, and unique
  presentation/session/controller ownership. It recorded 0 new warnings and 0 errors.
- Validation: M3E 21/21, M4A 13/13, M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16,
  PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionState 27/27, PortalArtResolver 2/2, and complete EditMode
  349/349. Every run had 0 failures, skips, or inconclusive tests; final compilation was clean.
- Full source audit, runtime contract, ownership graph, boundary behavior, fixtures, validation, and deferred effects:
  [`documentation_unity/m3e-capacity-state.md`](documentation_unity/m3e-capacity-state.md).

## M5A Production Dialogue and Quest-State Slice

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- The corpus audit parsed 2,399 source scripts and 12,723 mobile records without failures, finding 3,792 NPCs, 789
  dialogue-bearing NPCs, and 533 bounded closed dialogue/state loops. The selected authentic fixture is Thomgrak,
  ObjectID `G_DF753C8F_B655_D411_8F1D_00A0CC6511C6`, prototype 17232, script/dialogue 1760, in
  `maps/arcanum1-024-fixed/68786586569.sec` with `dlg/01760thomgrak.dlg`.
- `WorldMapSessionCoordinator` owns one `CampaignStateService` and one `ProductionDialogueSession`.
  `CampaignStateService` provides stable source-bounded global/PC flags and variables, quest state, and per-object/per-
  SAP state. Dialogue targets and campaign mutations are keyed by stable `ArcanumObjectId`; Unity objects, sprites,
  presenters, and views do not own authoritative gameplay state.
- Production Talk targets NPCs only, uses the source distance rule (`distance < 5`, represented by a range of 4), and
  approaches to range 1 before execution. Missing, unloaded, invalid, or replaced targets and competing commands fail
  or cancel explicitly; there is no NPC substitution or presentation-owned target identity.
- Dialogue evaluation is strict and fail-closed. The admitted slice uses only the audited `LocalFlag`, `Quest`, `True`,
  `Dialog`, `Goto`, and `DoNothing` condition/effect/action vocabulary plus ordinary dialogue returns. Unsupported or
  malformed tokens cannot partially mutate state and produce explicit diagnostic telemetry.
- Thomgrak's initial Human Male PC conversation opens source line 1 with exact response lines `{2,11,12,19}`. Choosing
  line 11 sets object-local flag 1 and advances to line 60; choosing line 61 sets quest 1130 to `Accepted` and local
  flag 70; line 70 closes. The next conversation opens line 140, exposes accepted-quest response line 143, and omits
  unavailable line 145.
- Computer Use physical Play Mode validation literally clicked the visible authentic NPC, observed normal PC approach,
  selected the real dialogue responses, and proved the quest/local-state mutation. It also passed duplicate-command
  rejection, pending-approach cancellation, target loss, sector reload cancellation, changed-state reopening,
  Original/Enhanced/Original rebuild, NPC unload/reload, stable identity/state, and unique coordinator/loader/
  navigation/interaction/presenter/root/PC/NPC/sprite ownership with 0 new warnings and 0 errors.
- Validation: M5A 16/16; required regression matrix M4D 25/25, M4C 29/29, M4B 20/20, M4A 13/13, M3E 21/21,
  M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A 7/7,
  M1B 11/11, WorldSessionState 27/27, and PortalArtResolver 2/2; complete EditMode 439/439. Every run had zero
  failures, skips, or inconclusive tests; final compilation was clean.
- Full corpus evidence, ownership/call graph, campaign and dialogue contracts, authentic closed loop, validation, and
  deferred boundaries: [`documentation_unity/m5a-dialogue-quest-slice.md`](documentation_unity/m5a-dialogue-quest-slice.md).
- Deferred: broad opcode/dialogue/quest support, quest completion/botch and journal projection, combat, followers,
  travel, economy, inventory/equipment UI, character progression integration, and save serialization.

## M5B Authentic Quest Completion and Journal Projection

Completed on 2026-09-13 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- Quest 1130 was rejected for this slice because its completion conversation is not terminal-state-specific and its
  botch route requires combat/death. The selected GREEN path is quest 1005 with the Black Root mayor,
  ObjectID `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`, prototype 17088, dialogue/SAP_DIALOG 1009, sector
  `maps/arcanum1-024-fixed/96636765255.sec`.
- The authentic dagger is stable ObjectID `G_52E2AC87_1A3B_6842_8C2E_5247C9571D11`, weapon prototype 6071, with
  instance `OBJ_F_NAME` 2002. Source dialog `in 2002` is name-ID lookup, not prototype lookup; no NPC is selected,
  cloned, reclassified, or substituted.
- `CampaignStateService` now owns source-shaped monotonic quest state and deterministic session timestamps.
  `ProductionDialogueSession` preflights and snapshots every authoritative domain touched by an admitted response.
  Quest 1005 follows Mentioned -> response 3 -> Accepted; response 4 -> node 100; response 102 executes source
  `qu 1005 4, in 2002`, awarding XP before terminal commit, applying alignment/reaction, and transferring the exact
  dagger to the mayor. Failed preflight rolls back without partial quest, inventory, XP, alignment, reaction, timestamp,
  tombstone, stack, or dynamic-ID mutation.
- Source quest reward is 800 XP and +50 alignment; completion adds +10 mayor reaction. Node 440's `$$100` is an
  authored follow-on response transaction and awards 100 Gold through existing M3 prototype-9056 stack/capacity
  semantics. The source-built-in Gold prototype is available even without a loose 009056 `.pro`.
- `JournalProjectionService` reads campaign state plus the four source quest resources and cannot mutate them. It
  projects source state label, normal/dumb description, and `{days,milliseconds}` ordering metadata. The original
  data has one quest description rather than separate Accepted/Completed prose; the state label supplies the terminal
  distinction. `ProductionJournalPresenter` is a minimal read-only view only.
- Re-engagement after Completed exposes source response 5 and excludes response 4. Response 5 is `t:`; its visible
  label resolves from the original generated-dialog 500-599 range. Training remains outside M5B, so selecting it emits
  exact dialogue/line/token telemetry and fails closed with every authoritative value unchanged.
- Computer Use Play Mode used the real dagger and real mayor. A literal mayor click drove normal Talk approach;
  physical choices 4 -> 102 -> 431 -> 443 completed the quest, transferred the dagger, awarded 100 Gold/800 XP,
  applied alignment +50 and source reaction 43 -> 53, and changed the journal to Completed. A second literal click
  showed response 5 and no response 4. Original -> Enhanced -> Original, dagger-sector -> mayor-sector traversal in
  both directions, foreign-source suppression, unload/reload, stable references, and unique coordinator/loader/
  navigation/interaction/dialogue/journal/root/PC/NPC/sprite ownership all passed. Final Play Mode recorded 0 warnings
  and 0 errors.
- Validation: M5B 18/18; M5A 16/16; M4D 25/25; M4C 29/29; M4B 20/20; M4A 13/13; M3E 21/21;
  M3D 18/18; M3C 13/13; M3B 13/13; M3A 8/8; M2B 8/8; M2A 16/16; PlayerNavigation 21/21;
  M1A 7/7; M1B 11/11; WorldSessionState 27/27; PortalArtResolver 2/2; complete EditMode 457/457. Every final
  run had zero failures, skips, or inconclusive tests; final compilation was clean.
- Full candidate evidence, final ownership graph, source contract, transaction ordering, lifecycle proof, and deferred
  boundaries: [`documentation_unity/m5b-quest-journal-slice.md`](documentation_unity/m5b-quest-journal-slice.md).

## M5C Authentic Production Trainer Dialogue

Completed on 2026-09-13 with Unity 6000.0.71f1 on `feature/inventory-commands`.

- The source audit recovered the exact `t:` grammar and special-handler sequence from `dialog.c`, `reaction.c`, and
  `skill.c`, then scanned the mounted retail corpus. It found 58 dialogue resources containing `t:`, 56 with placed
  dialogue-bearing resources, and 236 placed NPC candidates. The selected GREEN fixture is the Black Root mayor,
  ObjectID `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`, prototype 17088, dialogue/SAP_DIALOG 1009, sector
  `maps/arcanum1-024-fixed/96636765255.sec`, response 5 `T:11`, returning to line 20.
- `DialogLine` now preserves the authored token payload after generated display-text replacement and the production
  parser strictly converts decimal IDs and inclusive ranges to typed `CharacterSkill` values. Malformed, empty,
  over-limit, or out-of-range payloads fail without exposing a partial list or mutating state.
- `ProductionDialogueSession` owns transient source-shaped skill-selection, payment, and result views. The M5C
  compatibility profile admits `t:` only for audited dialogue 1009. Unity's presenter still only observes text/options
  and submits a response index; it owns no trainer identity, eligibility, cost, payment, or training state.
- `DialogueTrainingService` carries a typed stable-ID request and routes eligibility to M4C's
  `CharacterProgressionService`. The source `t:` payload has no tier field and always requests Apprentice. Persuasion
  must be authored in the offer, untrained, and effective rank >= 1; Expert/Master routes remain deferred.
- Source base cost 100 is modified by the exact reaction bands. At the M5B mayor reaction of 53 the result is 99 Gold.
  Payment uses the existing persistent Gold stack/capacity authority and is atomic with training assignment; expanded
  progression/inventory snapshots prevent partial charge or assignment. Cancel, payment No, insufficient rank,
  insufficient Gold, and already-trained paths cannot mutate either domain.
- Source normal/dumb, gender-direction, and social-class generated-dialogue tables plus `mes/skill.mes` provide the
  transient prompt and option text. Effective NPC social class is retained in session-owned persistent object state;
  presentation remains separate.
- Computer Use Play Mode literally clicked the visible real mayor, observed ordinary PC approach/Talk, and physically
  chose the real training response, Persuasion, Yes at 99 Gold, and the success acknowledgement. The PC became
  Persuasion/Apprentice, PC Gold changed 100 -> 1, mayor Gold changed 0 -> 99, and dialogue returned to authored line
  20. A second literal conversation produced the source already-trained rejection with no repeat payment. Original ->
  Enhanced -> restored rebuilds and mayor-sector unload/reload preserved stable state/references and unique gameplay/
  presentation owners. The Play Mode run recorded 0 new warnings and 0 errors.
- Validation: M5C focused **21/21** and complete EditMode **478/478**. Every final run had 0 failures, skips, or
  inconclusive tests; final compilation was clean.
- Full source evidence, candidate classification, final ownership/call graph, transaction contract, fixture, validation,
  and deferred boundaries: [`documentation_unity/m5c-trainer-dialogue.md`](documentation_unity/m5c-trainer-dialogue.md).

## M8A Core Combat State Validation Baseline (2026-09-20)

M8A is complete. `CombatStateService` is coordinator-owned and provides the bounded
`Inactive -> Starting -> Active -> Ending -> Inactive` turn-based lifecycle, eligibility and hostility admission,
stable-ID participants, authentic non-PC-first order, and M4D Speed-derived current-turn AP. The authentic physical
fixture is the Polar Bear Cub in `maps/arcanum1-024-fixed/47781512457.sec`, ObjectID
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1`, prototype 28422. It acts before the PC and receives 5 AP from Speed 4.

Computer Use physical Play Mode validation passed start/end and failure paths, interaction/navigation/dialogue
lockout, graphics rebuild, unload, cross-map transition, and V1 save/load normalization with stable identities and no
duplicates. Final validation was M8A 22/22, required regressions 296/296, complete EditMode 679/679, and physical
Play Mode with 0 warnings and 0 errors. Save format remains V1 and active combat is intentionally not serialized.
See [`documentation_unity/m8a-core-combat-state-audit.md`](documentation_unity/m8a-core-combat-state-audit.md).

## M8B Turn-Based Combat Validation Baseline (2026-09-20)

M8B is complete. The authentic production proof uses Polar Bear Cub
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in
`maps/arcanum1-024-fixed/47781512457.sec` against the production PC. Bear Speed 4 gives 5 AP; unarmed attack costs
5 AP; walking costs 2 AP per cardinal or diagonal step; optional PC always-run costs 1 AP per step; and one admitted
PC overdraw step applies 2 Fatigue. The bear's source natural normal damage 3..6 becomes 2..5 at Strength 7. Its
effective Melee 3 yields a 40% ordinary hit chance against the fixture PC's AC 0/Dodge 0. Normal and fatigue resistance
use the audited integer rules, and all committed damage routes only through M4B vitality.

Computer Use physical Play Mode proved seeded hit and miss, single damage application, exact AP spend, bear -> PC ->
next-round bear advancement, NPC atomic over-budget rejection, PC walk/run/overdraw movement, rollback invariants,
Original -> Enhanced -> Original rebuild, `EndCombat` with damage retention and ordinary gameplay resumption, and
transient save/load/unload/transition normalization. The physical run recorded 0 warnings and 0 errors. Final
compilation was clean; focused M8B was **23/23**, required regressions were **347/347**, and complete EditMode was
**702/702** with 0 failed, 0 skipped, and 0 inconclusive. The complete-suite console contained 6 known compatibility
warnings and 0 errors. Save format remains V1.

See [`documentation_unity/m8b-turn-based-combat-audit.md`](documentation_unity/m8b-turn-based-combat-audit.md).

## M8C Ranged Combat Validation Baseline (2026-09-21)

M8C is complete. The authentic production fixture is bow
`G_1575DBCA_4990_C243_8184_524D51F7D533` (prototype 6055) and the authored 70-arrow stack
`G_FBFA4631_D97D_D740_9636_F131B2FD9F7B` (prototype 7058), acquired from
`maps/arcanum1-024-fixed/101602821844.sec` and equipped through M3C. The bow costs 6 AP, has range 15, and rolls
Normal 1..10 plus Fatigue 2..5 without a melee Strength bonus. Compatible arrows are selected deterministically by
stable ObjectID, consumed once before hit resolution (including misses), and use M3D tombstones at depletion.

Computer Use physical Play Mode validation passed against the authentic Polar Bear Cub target. A seeded hit rolled
10 Normal/5 Fatigue and resistance reduced that to 8/5; a seeded miss spent 6 AP and one arrow with zero vitality
damage. The run proved in-range clear fire, out-of-range and authentic wall/scenery rejection, deterministic
multi-stack selection/fallback, quantity-1 depletion followed by `NoAmmo`, authoritative equipment-only weapon
resolution, a 1 AP move followed by the 6 AP attack from the updated placement, transactional failure rollback,
Original -> Enhanced -> Original rebuild, end/restart, and transient load/unload/map-transition normalization. The
selected authentic sectors contain no portal whose open state alone clears projectile LOS, so closed/open portal
semantics are covered by focused source-grid validation rather than an invented physical fixture.

Final Unity compilation was clean; focused M8C was **12/12**, required regressions were **377/377**, and complete
EditMode was **714/714**, all with 0 failed, 0 skipped, and 0 inconclusive. The complete-suite console contained the
same 6 known dialogue-compatibility warnings and 0 errors; the physical run recorded 0 warnings and 0 errors. Save
format remains V1: committed equipment, ammunition, placement, and vitality persist, while active combat normalizes
to Inactive.

See [`documentation_unity/m8c-ranged-combat-audit.md`](documentation_unity/m8c-ranged-combat-audit.md).

## M8D Defeat, Death, Unconsciousness, and Corpse-State Validation Baseline (2026-09-21)

M8D is complete. M4B `CharacterVitalityService` remains the sole HP/fatigue authority: dead is current HP `<= 0`,
and ordinary unconsciousness is current Fatigue `<= 0` while alive, with the audited undead/fatigue-immune exclusion.
No independent dead flag, replacement corpse identity, or Save V2 field was introduced.

Death removes the stable ObjectID from active combat participation and advances exactly once if it owned the current
turn. Unconscious participants remain registered but are skipped and keep blocking movement. The existing critter
becomes the corpse: it retains object type, ObjectID, inventory, and equipment relationships; projects fall-down
animation 7; and loses dynamic navigation occupancy only when dead. Graphics rebuild, sector reload, and Save V1
restore derive presentation and blocking from the authoritative domain state. Combat does not end automatically;
existing explicit `EndCombat` succeeds once no eligible hostile remains.

Computer Use physical Play Mode used the authentic Polar Bear Cub
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in
`maps/arcanum1-024-fixed/47781512457.sec`. Six deterministic attacks through the real M8C bow/arrow production path
crossed HP zero exactly once, retained the bear's identity and relationships, removed its blocker and participant,
and created no duplicate corpse. The same run proved unconscious/current-actor advancement, idempotent repeats,
Original -> Enhanced -> Original rebuild, sector reload, and dead/unconscious Save V1 round trips. It recorded 0
warnings and 0 errors.

Final validation was focused M8D **11/11**, required regressions **150/150**, and complete EditMode **725/725**, all
with 0 failed, 0 skipped, and 0 inconclusive. The complete suite emitted 5 known intentional fail-closed dialogue
compatibility warnings and 0 errors. Death scripts, XP/rewards, quest/reputation/follower consequences, corpse-looting
UI, loot transfer, wake-up/regeneration, decay, resurrection, AI, and real-time combat remain deferred.

See [`documentation_unity/m8d-defeat-state-audit.md`](documentation_unity/m8d-defeat-state-audit.md).

## M8E Death Consequence and Corpse-Loot Validation Baseline (2026-09-22)

M8E is complete. The authentic fixture is Greater Skeleton
`G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848`, prototype 28460, at `(31883,56988)` in
`maps/arcanum1-024-fixed/59726889458.sec`. It carries source worth 440, no effective `SAP_DYING`, authentic 89-gold
object `G_6413F64C_29FD_4A44_8E2C_E9A414888116`, and equipped sword
`G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91`. Its inventory-rich source instance starts behind object flag `OFF`; the
physical validator clears only that encounter gate before starting ordinary production combat.

Lethal melee/ranged attacks preflight unsupported death scripts before AP, ammunition, vitality, XP, inventory, or
campaign mutation. The supported no-script path applies damage through M4B, lets M8D derive one death/removal, then
uses the coordinator-owned `DeathConsequenceService` to attribute the production-PC kill and award exactly 88 XP
through M4C. The marker commits once and persists as an additive Save V1 field. The original NPC ObjectID remains the
corpse and retains contained/equipped children until explicit, capacity-checked coordinator inventory transactions move
them; no corpse or loot identity is synthesized.

Computer Use physical Play Mode used six deterministic production bow attacks and recorded one death transition,
XP `0 -> 88`, same-identity corpse retention, participant removal, authentic Gold and equipped-sword loot, blocked
repeat processing/loot, explicit `EndCombat`, Original -> Enhanced -> Original rebuild, sector unload/reload, and V1
save/load without consequence replay. It passed with 0 new warnings and 0 errors. Final compilation was clean;
focused M8E was **7/7** and complete EditMode was **732/732**, with 0 failed, 0 skipped, and 0 inconclusive. The complete
suite emitted five known intentional fail-closed dialogue compatibility warnings and 0 errors.

Broader/nonzero death-script execution, damage-proportional XP, follower credit, alignment/reputation/kill-log effects,
decay, resurrection, generated loot, corpse UI, AI, real-time combat, spells, and technology remain deferred.

See [`documentation_unity/m8e-death-consequence-audit.md`](documentation_unity/m8e-death-consequence-audit.md).

## M8F Critical Success / Critical Failure Validation Baseline (2026-09-22)

M8F is complete. Every attack now performs the source-ordered ordinary roll followed by a critical roll. The bounded
success path implements the source damage-only +50%, +100%, and +200% results after normal resistance. The bounded
failure path admits the source ordinary self-hit for a qualifying secondary roll above 50, and Master Melee suppresses
critical failure. Injury, equipment, the secondary critical-hit failure branch, and unsupported NPC-to-PC critical
tables fail closed before AP, ammunition, vitality, death-consequence, or turn mutation.

Computer Use physical Play Mode validation used the production unarmed PC and authentic Polar Bear Cub to prove all
three critical-success thresholds, ordinary attack AP/turn behavior, M4B-only vitality mutation, the bear's ordinary
self-hit critical failure, Master Melee suppression, unsupported-branch rollback, and active-combat
Original -> Enhanced -> Original rebuilds without authority changes. A lethal unarmed critical against the authentic
Greater Skeleton produced exactly one death transition, same-identity corpse, 88 XP reward, and processed consequence
marker with no replay. Combat end/restart and Save V1 load retained committed vitality/death/world consequences while
normalizing the transient attack, critical, participant, turn, and AP state.

Final Unity compilation was clean. Focused M8F was **9/9**; the required M8B-M8E combat regression matrix was
**53/53** (M8B 23, M8C 12, M8D 11, M8E 7); and the complete EditMode suite was **741/741**, all with 0 failed,
0 skipped, and 0 inconclusive. The physical run recorded 0 warnings and 0 errors. The complete suite emitted the same
five known intentional fail-closed dialogue-compatibility warnings and 0 errors; after clearing those expected test
diagnostics, the final Unity Console was 0 warnings and 0 errors. Save format remains V1.

See [`documentation_unity/m8f-critical-resolution-audit.md`](documentation_unity/m8f-critical-resolution-audit.md).

## Post-M8F Turn-Based Combat Gap Audit (2026-09-23)

The bounded read-only comparison of M8A-M8F against the original-source mirror is complete. It found that a narrow
M8G **Turn-Based Combat Kernel Closure** is required before real-time scheduling. The pre-real-time authority gaps are:
source-shaped roster growth and runtime engagement; an exactly-once +1,000 ms round-boundary hook; a structured attack
request with called locations and an inspectable modifier ledger; numeric cover distinct from hard line-of-fire; the
supported Bow Master range exemption and Expert/Master two-impact behavior; and source critical-Dodge
reclassification.

M8G Phases 1-4 are complete and close every required item in this audit. The audit explicitly defers AI, followers,
equipped melee weapons, firearms, throwing,
explosives/AOE, magic, technology, combat UI, projectile presentation, critical injury/equipment effects, and active
combat persistence. Save format remains V1. It also found no source basis for general attacks of opportunity, a
separate initiative/surprise-round system, generic combat stances/reload actions, a generic nonlethal toggle, or
ordinary-attack knockback.

Audit-tail validation retained a clean Unity compile and complete EditMode **741/741**, with 0 failed, 0 skipped,
and 0 inconclusive. The same five expected intentional dialogue-compatibility warnings were cleared after the run;
the final Unity Console was 0 warnings and 0 errors. No production code or tests changed.

See [`documentation_unity/m8-post-m8f-combat-gap-audit.md`](documentation_unity/m8-post-m8f-combat-gap-audit.md).

## M8G Phase 1 Dynamic Combat Loop Validation Baseline (2026-09-23)

M8G Phase 1 is complete. `CombatStateService` now discovers loaded, eligible source-hostile NPCs in the PC perception
square at combat start and completed-round refresh, admits explicit runtime engagement exactly once, preserves stable
source-order/PC-tail ordering, retains enrolled actors that leave range, rejects dead/newly unconscious enrollment,
and retains/skips existing unconscious participants under M8D semantics. Eligible engaged hostiles block explicit
termination until removed or otherwise ineligible.

Each completed round emits exactly one deterministic `CombatRoundBoundary` with a +1,000 ms delta and cumulative
transient source-combat time. Partial rounds, actor enrollment, death/removal, unconscious skipping, graphics rebuild,
combat termination/restart, and Save V1 normalization do not create or replay a hook. Save V1 remains unchanged and
continues to preserve committed vitality/death/world consequences while restoring no transient roster, engagement,
turn, AP, round, or elapsed-boundary state.

Computer Use physical Play Mode validation used the production PC and authentic Polar Bear Cub sector. The authentic
initial roster contained six unique participants. A validation hostile became relevant during active combat and was
discovered once at the next boundary; a second was explicitly engaged once. Source ordering/current-turn ownership,
death/removal, unconscious retention/skipping, out-of-range retention, termination, clean restart, three exact
1,000 ms boundaries, Original -> Enhanced -> Original rebuild independence, and Save V1 transient normalization with
committed vitality retention all passed. The physical run recorded 0 warnings and 0 errors.

Final Unity compilation was clean. Focused M8G Phase 1 was **16/16**. Corrected M8A-M8F combat regressions were
**84/84** (M8A 22, M8B 23, M8C 12, M8D 11, M8E 7, M8F 9). Complete EditMode was **757/757**. Every suite had 0 failed,
0 skipped, and 0 inconclusive. The complete suite emitted the same five intentional fail-closed dialogue warnings and
0 errors; after clearing them, the final Unity Console was 0 warnings and 0 errors.

The M8E test correction advances through the authoritative discovered roster until the PC owns the turn instead of
assuming the scripted target acts immediately. Production ordering and all death-consequence assertions remain
unchanged.

See [`documentation_unity/m8g-phase1-combat-loop-audit.md`](documentation_unity/m8g-phase1-combat-loop-audit.md).

## M8G Phase 2 Structured Attack Resolution Validation Baseline (2026-09-23)

M8G Phase 2 is complete. `CombatStateService` now accepts immutable structured melee/ranged requests containing
stable attacker/target ObjectIDs, attack mode, and called location. Legacy attack callers route through a `None`
request and retain their M8B-M8F behavior and roll order. Requests resolve current roster, turn, position, equipment,
ammunition, AP, range, hard line-of-fire, skills, attributes, and defenses at execution time; they do not carry stale
authority.

Source called-location IDs are exact: Torso 0, Head 1, Arm 2, Leg 3. Their hit modifiers are 0, -50, -30, and -30;
Head adds 10 and Arm/Leg add 6 percentage points to critical-success chance after an ordinary hit. The ordered,
immutable ledger exposes base skill, Intelligence 20, Armor Class, minimum Strength, Perception range, weapon to-hit,
and called-location entries. Its applied/non-suppressed sum, clamped to 0..100, is the single value used for attack
resolution. Numeric cover was not added; M8C hard line-of-fire remains a binary precondition.

Computer Use physical Play Mode validation used the production PC, authentic Bow/70-arrow stack, and authentic Polar
Bear Cub. It proved ordinary and called melee/ranged requests, all four IDs/modifiers, ledger single-application math,
called Arm +50% critical success, called Head self-hit critical failure, malformed-request rollback, ordinary
AP/ammunition/turn/vitality paths, Original -> Enhanced -> Original rebuild independence, one exact Phase 1 round
boundary, and Save V1 transient normalization with committed vitality retention. The physical run recorded 0 warnings
and 0 errors.

Final Unity compilation was clean. Focused M8G Phase 2 was **17/17**. M8A-M8G Phase 1 combat regressions were
**100/100** (M8A 22, M8B 23, M8C 12, M8D 11, M8E 7, M8F 9, M8G Phase 1 16). Complete EditMode was **774/774**.
Every suite had 0 failed, 0 skipped, and 0 inconclusive. The complete suite emitted the same five intentional
fail-closed dialogue warnings and 0 errors; after clearing them, the final Unity Console was 0 warnings and 0 errors.
Save format remains V1 and request/result/ledger diagnostics are transient only.

Weighted ordinary resolved hit location remains deferred because adding its extra source roll before supported
location-effect tables would change the validated M8B-M8F deterministic sequence. Location injury/equipment effects,
numeric cover, Bow mastery changes, Critical-Dodge, combat UI, AI, followers, magic, technology, and real-time combat
remain outside this phase.

See [`documentation_unity/m8g-phase2-attack-request-audit.md`](documentation_unity/m8g-phase2-attack-request-audit.md).

## M8G Phase 3 Numeric Cover / Hard Line-of-Fire / Bow Master Validation Baseline (2026-09-23)

M8G Phase 3 is complete. `SectorNavigationMap` now returns one authoritative projectile traversal result containing
hard-block state plus accumulated numeric cover. It uses source terrain/block masks, ordinary-object tiles, and
wall/portal edge rotation/open state—not Unity colliders or presentation geometry. Non-shoot-through obstacles reject
the attack before mutation. Shoot-through opaque obstacles contribute 50 difficulty; see-through objects contribute
20 only when `OF_PROVIDES_COVER` is set. Contributions stack without an intermediate cover cap and appear once as a
signed `Cover` modifier before the existing final-effectiveness clamp.

The physical audit found and corrected an initial omission of shoot-through wall/portal-edge cover. Qualifying wall
and closed-portal edges now use the same source flag table as ordinary obstacles; open portals and existing
wall-passage pieces contribute neither block nor cover. The authoritative geometry remains the source-grid edge model.

Bow Master preserves the raw long-range Perception modifier in the ledger but marks it suppressed, so it does not
affect the final sum. Cover and called-location penalties remain active, and non-Bow calculations are unchanged. The
ledger final effectiveness controls the ordinary hit decision consumed by M8F; M8F's established base-effectiveness
critical percentage and called-location bonus remain unchanged.

Computer Use physical Play Mode validation used the authentic Bow/70-arrow stack and Polar Bear Cub plus authentic
retail cover geometry in `maps/arcanum1-024-fixed/101535712980.sec`. Clear Bow fire was legal with cover zero; a hard
block returned `LineOfFireBlocked` with zero AP/ammunition/vitality/turn mutation; the cover route produced one active
`-40` ledger entry; and Bow Master suppressed only range while called Arm `-30` remained active. The run recorded 0
warnings and 0 errors.

Final Unity compilation was clean. Focused Phase 3 was **12/12**, Phase 2 was **17/17**, Phase 1 was **16/16**, and
M8A-M8F combat regressions were **84/84**. The combined prior-phase/regression run was **117/117**. Complete EditMode
was **786/786**. Every suite had 0 failed, 0 skipped, and 0 inconclusive; the final cleared Unity Console was 0 logs,
0 warnings, and 0 errors. Save format remains V1.

See [`documentation_unity/m8g-phase3-cover-bow-master-audit.md`](documentation_unity/m8g-phase3-cover-bow-master-audit.md).

## M8G Phase 4 Bow Multi-Impact / Critical-Dodge Validation Baseline (2026-09-24)

M8G Phase 4 is complete. Source Bow training at Expert or Master produces exactly two ordered projectiles inside one
attack command. The command spends one normal ranged AP cost and one weapon ammunition cost; both impacts share the
request, target, attack/critical classification, called location, and Phase 2/3 modifier ledger. Damage, resistance,
and supported critical damage effects resolve independently per impact. Below Expert remains single-impact.

The intended second impact stays on the same target and still resolves after a lethal first impact. M4B remains the
only vitality authority, while the existing M8D positive-to-nonpositive transition and M8E processed marker ensure
death, corpse state, XP, and consequences occur exactly once.

Critical Dodge begins only from an ordinary attack hit followed by a successful defender Dodge whose Dodge invocation
is itself critical. The qualifying secondary 1..100 roll uses the defender's exact training thresholds None 0,
Apprentice 10, Expert 50, and Master 100. The source leaves hit clear and restores the attack critical flag, so the
final bounded result remains `CriticalFailure` with explicit Critical-Dodge diagnostics. Melee and ranged attacks use
the same rule. The supported self-hit branch commits normal AP/ammunition/turn behavior; unsupported injury/equipment
branches fail before all mutation.

Computer Use physical Play Mode validation used the production PC, authentic Bow/70-arrow stack, authentic Polar
Bear Cub, and source-derived Expert Bow/Master Dodge participants. It proved one ordinary single-impact shot, one
Expert two-impact same-target shot for one AP cost and one arrow, shared range/called-Arm ledger diagnostics with
independent damage, and a threshold-100 Critical Dodge routed through the self-hit critical-failure path with normal
remaining-AP turn ownership. The proportional physical run recorded 0 warnings and 0 errors; deterministic focused
coverage owns the lethal-first exact-once M8D/M8E permutation.

Final Unity compilation was clean. Focused Phase 4 was **17/17**, Phase 3 was **12/12**, Phase 2 was **17/17**, Phase 1
was **16/16**, and M8A-M8F combat regressions were **84/84**. The combined prior-phase/regression run was **129/129**.
Complete EditMode was **803/803**. Every suite had 0 failed, 0 skipped, and 0 inconclusive. The full suite emitted the
same five intentional fail-closed dialogue warnings and zero errors; the final cleared Unity Console was 0 logs,
0 warnings, and 0 errors. Save format remains V1 and no attack/impact/Critical-Dodge transient is restored.

The six post-M8F audit gaps are now all closed across M8G Phases 1-4. No required audit item remains before M8G can be
considered complete.

See
[`documentation_unity/m8g-phase4-bow-multi-impact-critical-dodge-audit.md`](documentation_unity/m8g-phase4-bow-multi-impact-critical-dodge-audit.md).

## M8H Real-Time Combat Vertical Slice Validation Baseline (2026-09-24)

The first bounded M8H real-time-combat vertical slice is complete. `CombatStateService` now owns one deterministic
real-time scheduler over the established M8A-M8G roster and attack/movement kernels. Real-time combat has no turn
owner and spends no turn AP, matching the source AP check/consume no-op outside turn-based mode. One pending action
per actor records source-time start, ART action-frame effect, and full-animation readiness. Direct kernel bypass is
rejected, simultaneous effects use stable roster/ObjectID ordering, large time advances remain exactly-once, and the
existing 1,000 ms round hook fires once for every crossed boundary.

Production timing comes from original ART metadata plus the audited Speed interpolation and weapon-speed adjustment,
not Unity animation playback or frame rate. The bounded production profiles are walk, run, unarmed attack, and Bow
attack. Physical validation exposed and corrected the exact source critter-ART weapon fields: unarmed 1 and Bow 8.
Movement atomically commits through existing navigation at its source completion time; melee and Bow effects reuse
the structured M8G request, modifier, cover, Critical-Dodge, multi-impact, critical, ammo, M4B vitality, M8D defeat,
and M8E death-consequence authorities.

Computer Use physical Play Mode validation used the production PC, authentic Bow/arrow objects, authentic Polar Bear
Cub, production loader, and production combat service. Source-timed run, unarmed melee, Bow effect/recovery, readiness,
runtime engagement, exact/hitch-safe round boundaries, Original -> Enhanced -> Original rebuild independence, lethal
death/consequences exactly once, clean termination, and Save V1 transient normalization all passed. The accepted run
recorded 0 warnings and 0 errors.

Final Unity compilation was clean. Focused M8H was **22/22**; M8A-M8G combat regressions were **146/146**; and the
complete EditMode suite was **825/825**, all with 0 failed, 0 skipped, and 0 inconclusive. The full suite emitted the
same five intentional fail-closed dialogue warnings and 0 errors; the final cleared Unity Console was 0 logs,
0 warnings, and 0 errors. Save format remains V1 and no clock, cooldown, pending action, attack, or critical transient
is restored.

See [`documentation_unity/m8h-real-time-combat-audit.md`](documentation_unity/m8h-real-time-combat-audit.md).

## Next Recommended Milestone

M8H's first bounded real-time-combat vertical slice is complete. A next bounded M8H phase is required for the
production wall-clock adapter, progressive movement/interruption, source-safe command replacement, active mode
transfer, broader weapon timing, presentation observation, and deterministic NPC command policy. That work must be
selected and authorized separately. M8I, combat UI, followers, magic, technology, and other later systems have not
started.

# `Suvitruf/unity-arcanum` Comparative Source Audit

- Audit date: 2026-10-05
- OpenArcanum branch / commit: `feature/session-save-load` at
  `bcfaacfce29da7df8c51cbc6a609b8967200c83a`
- Compared repository / commit:
  [`Suvitruf/unity-arcanum` at `2d44a62f3e295640eca08291b485285bc8e0d072`](https://github.com/Suvitruf/unity-arcanum/tree/2d44a62f3e295640eca08291b485285bc8e0d072)
- Original-engine reference:
  [`alexbatalov/arcanum-ce` at `a7ff41b300ef712f0e7d088183a3d08957110cdd`](https://github.com/alexbatalov/arcanum-ce/tree/a7ff41b300ef712f0e7d088183a3d08957110cdd)
- Mode: comparative research and documentation only. No comparison code was copied, no production code or assets were
  changed, and no original game data was modified.

## Executive conclusion

The public `Suvitruf/unity-arcanum` tree contains no implementation that is missing from the current OpenArcanum
branch. It is a direct ancestor of OpenArcanum: Git reports the compared commit as an ancestor, with the public commit
zero commits ahead and OpenArcanum 147 commits ahead.

The file-level result is equally decisive:

- all **100** public C# files are present in OpenArcanum;
- **74** remain byte-identical and **26** have diverged locally;
- none of the public C# files is absent locally; and
- all **17** public Markdown documents are present and byte-identical in OpenArcanum.

Consequently, this audit found **no category-2 item** in the auditable public repository. The project's README says
that world streaming, lighting, shadows, combat, followers, and other systems work "partially," but it also says the
project is unfinished and that code is being published module by module. Those high-level claims are not accompanied
by public production implementations for several named systems and are not treated as authority.

The comparison is still useful. Its source-linked graphics documentation records several original-engine behaviors
that OpenArcanum parses or documents but does not yet present in the production world: roof projection/fading,
source-complete object render ordering, day/night palette tint, placed lights, default sprite shadows, nocturnal
scenery, additive/translucent blends, and equipment-driven critter appearance. These are category-4 findings, traced
below to `arcanum-ce` before any follow-up is recommended. They are gaps against the original engine, not code that
should be copied from the comparison project.

## Classification and evidence rules

1. **OpenArcanum already equivalent or stronger** — the same implementation is present, or OpenArcanum has a connected
   and validated production implementation beyond the public comparison tree.
2. **Other project appears more complete/source-faithful** — concrete public implementation produces a more complete
   result. No finding met this threshold.
3. **Different architecture but equivalent result** — both sides have substantive implementations with different
   ownership or presentation boundaries.
4. **Useful reverse-engineering/documentation evidence** — a claim is backed by original data or `arcanum-ce` and is
   useful for a bounded OpenArcanum follow-up, but the comparison project does not provide a superior public runtime.
5. **Not relevant / speculative / incomplete** — README-only, demo-only, unconnected, or otherwise insufficient to
   establish production behavior.

Evidence was weighted in this order: original retail data and `arcanum-ce`; executable production call paths and
tests; parser/demo code; documentation; README claims. The comparison project is never treated as authoritative by
itself.

## Repository relationship and claim audit

| Evidence | Result | Consequence |
|---|---|---|
| Git ancestry | `2d44a62` is an ancestor of current `HEAD`; divergence is `0 / 147` | This is shared lineage, not an independent implementation available for selective merging. |
| Public C# census | 100 present locally, 0 missing, 74 identical, 26 locally diverged | No public implementation is available there but absent here. |
| Public Markdown census | 17 present locally and byte-identical | Its source research is already retained under `documentation/` and `documentation_unity/`. |
| [README status](https://github.com/Suvitruf/unity-arcanum/blob/2d44a62f3e295640eca08291b485285bc8e0d072/README.md) | Broad systems are described as working partially; the same page calls the project unfinished and says code will be opened module by module | Treat broad claims as category 5 unless a public production call path exists. |
| [Graphics research](https://github.com/Suvitruf/unity-arcanum/blob/2d44a62f3e295640eca08291b485285bc8e0d072/documentation/art-and-graphics.md) | Source-linked ART, layer, roof, light, shadow, and blend notes; byte-identical locally | Use as an index into original evidence, not as independent authority. |
| Published runtime tree | Format readers, resolvers, galleries, a terrain demo, a dialogue/script bench, basic audio helpers, and small data models | Useful foundations and demonstrations, not evidence for the unpublished full-game systems named in the README. |

## Detailed findings

### Terrain and facade rendering

- **Classification:** 1 — OpenArcanum already equivalent or stronger.
- **Their implementation/claim:** `TileMapRenderer` builds one atlas-backed transparent mesh per 64×64 sector,
  orders terrain quads by `x + y`, resolves facade frames, mirrors canonical blend edges by swapping UVs, and exposes
  missing-blend diagnostics. `TileMapDemo` also offers a non-batched diagnostic path.
- **OpenArcanum current state:** The public `TileMapRenderer`, `TileArtPathResolver`, `TileNameTable`, `IsoProjection`,
  and facade resolver are byte-identical locally. OpenArcanum additionally uses the terrain renderer within its
  session-owned sector path and validates authentic adjacent-sector presentation.
- **Source evidence:** [`tile.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/tile.c),
  [`location.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/location.c),
  [`a_name.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/a_name.c), and the shipped tile tables establish 78×40 tiles,
  the `40·(y-x-1), 20·(y+x)` projection, facade-in-tile-grid behavior, and blend canonicalization.
- **Gap:** No comparative gap. Terrain-only `x + y` ordering does not prove source-complete object ordering; that is a
  separate finding below.
- **Action recommended:** No action based on this comparison.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Retain the existing renderer and its original/enhanced asset policy; address
  only a reproduced terrain defect or a separately scoped world-composition milestone.

### Seamless world/sector streaming

- **Classification:** 1 — OpenArcanum is stronger.
- **Their implementation/claim:** The README claims streaming isometric maps. The public `TileMapDemo` selects and
  renders a sector for inspection; it is not a campaign/session streaming authority.
- **OpenArcanum current state:** `SectorStreamingWindow` presents the selected sector plus eight neighbors, while
  `WorldMapSessionCoordinator`, `SectorCoordinate`, cross-sector planning, and the production lifecycle retain one
  authoritative PC and persistent object state across contiguous boundaries and map transitions.
- **Source evidence:** [`sector.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/sector.c)
  establishes 64×64 sector addressing. Current physical validation records authentic adjacent-sector traversal and
  exact wrapped entry behavior.
- **Gap:** The public comparison repository supplies no stronger streaming implementation. Broader original-engine
  precache/performance parity is not established by either the README or its demo.
- **Action recommended:** No comparative action.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** If profiling later exposes a streaming defect, limit work to sector
  presentation lifetime/precache behavior without changing authoritative global coordinates, pathfinding, or saves.

### Walls

- **Classification:** 1 — OpenArcanum is stronger in production integration.
- **Their implementation/claim:** Public code resolves wall structure, piece, damage, variation, and mirrored ART;
  `ObjectArtGallery` displays representative wall pieces.
- **OpenArcanum current state:** The same resolver is present and the production sector loader renders authored wall
  objects, uses directional wall edges in traversal, and preserves persistent object identity. Damage/destruction as
  general wall gameplay is not supplied by the public comparison project.
- **Source evidence:** [`wall.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/wall.c)
  and [`a_name.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/a_name.c)
  define segmented wall art, structures, rotations, and damage variants; shipped `structure.mes` separates structure
  rows below 1000 from editor labels at 1000+.
- **Gap:** No comparative rendering gap. Destructible-wall rules remain a separate original-gameplay boundary, not an
  external implementation to adopt.
- **Action recommended:** No action from this audit.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Only a source-audited destructible-world-object milestone may add wall
  damage; it must preserve current navigation edge ownership and cannot be folded into visual cleanup.

### Doors and portals

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** `PortalArtResolver` and `ObjectArtGallery` resolve and animate door/window frames.
- **OpenArcanum current state:** The same resolver is connected to transactional portal state, authored frame timing,
  lock/open state, collision-edge updates, SAP_USE dispatch, unload rollback, persistence, save/load normalization,
  and physical real-sector validation.
- **Source evidence:** [`object.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/object.c),
  [`anim.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/anim.c), and the portal
  ART/table data support the frame and state semantics.
- **Gap:** None relative to the public comparison tree.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Preserve the current coordinator-owned transaction; future key/lockpick or
  damage behavior belongs to its own gameplay slice.

### Roof decoding and ART resolution

- **Classification:** 1 — equivalent parser/resolver foundation.
- **Their implementation/claim:** `SectorReader.ReadRoofs` decodes the 16×16 roof grid and `RoofArtResolver` maps roof
  art IDs to source files; the object gallery displays roof art.
- **OpenArcanum current state:** Those files are byte-identical locally. The parser exposes roof cells, fill/fade bits
  are documented, and roof ART can be decoded.
- **Source evidence:** [`sector.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/sector.c),
  [`roof.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/roof.c), and
  [`art.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/first_party/tig/src/art.c).
- **Gap:** No parser/resolver gap.
- **Action recommended:** No parser work.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Keep format decoding immutable; production roof presentation is the
  separate category-4 finding below.

### Production roofs and under-roof fading

- **Classification:** 4 — useful source evidence; neither public runtime is complete.
- **Their implementation/claim:** The graphics document says roofs draw after world objects, skip coverage around the
  player, and use roof-piece-dependent per-corner alpha ramps when the fade bit is set. The README claims roofs, but
  no public production roof owner consumes `ReadRoofs`.
- **OpenArcanum current state:** Roof grids are parsed and roof ART resolves, but `ReadRoofs` has no production caller.
  There is no production roof layer, covered-cell test, or source alpha-ramp fade.
- **Source evidence:** [`roof_draw` and roof fade logic in `roof.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/roof.c)
  directly establish late-layer drawing, coverage, fade-bit, and per-corner alpha behavior. This recommendation does
  not rely on the fan implementation.
- **Gap:** Source-authentic roof presentation is absent.
- **Action recommended:** Yes.
- **Priority:** P1 visual fidelity.
- **Exact OpenArcanum follow-up boundary:** Add one disposable production roof presentation owner that consumes the
  existing sector roof grid, applies exact source placement/hotspots and roof-piece fade alpha, rebuilds with graphics
  mode, and never owns collision, player position, time, or save state. Validate one exterior-to-interior traversal.

### Render ordering and compositing passes

- **Classification:** 4 — useful source evidence; public code does not close the gap.
- **Their implementation/claim:** The graphics document summarizes painter ordering by isometric diagonal. Public
  terrain quads are sorted by `x + y`; no published production object compositor implements all source passes.
- **OpenArcanum current state:** Production world objects use `(tile.x + tile.y) * 2 + 1`, while terrain is a sector
  mesh. This provides useful basic interleaving but does not represent the retail underlay, flat, non-flat, shadow,
  overlay/highlight, and roof passes or their source order/tie behavior.
- **Source evidence:** [`object_draw`, `object_enqueue_blit`, and `object_compare_blits` in `object.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/object.c)
  show a queued multi-pass compositor rather than a single universal `x + y` key; `roof.c` provides the final roof
  pass.
- **Gap:** Occlusion can be visually wrong for overlapping flat/non-flat objects, walls, effects, highlights,
  shadows, and future roofs even when their tile diagonals match.
- **Action recommended:** Yes, after a representative overlap corpus is captured.
- **Priority:** P1 visual fidelity.
- **Exact OpenArcanum follow-up boundary:** Audit and implement presentation-only source pass/category ordering and
  deterministic ties for existing rendered object types. Do not change gameplay coordinates, target selection,
  collision, interaction priority, or authority ownership.

### Day/night ambient lighting

- **Classification:** 4 — useful source evidence; README implementation claim is unpublished.
- **Their implementation/claim:** The README claims day/night lighting. The graphics document describes an outdoor
  global palette tint and a `[06:00,18:00)` day interval, but the public tree contains no production lighting
  controller.
- **OpenArcanum current state:** `SourceTimeService` is authoritative and persisted and drives combat/magic/economy
  timing; the current audio projection maps elapsed time zero to noon. No rendering consumer applies the retail
  outdoor tint or sector light scheme.
- **Source evidence:** [`light.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/light.c)
  defines `light_outdoor_color`, the `[06:00,18:00)` test, palette modification, schemes, and nocturnal switching;
  `SectorReader` already exposes the source light-scheme value.
- **Gap:** Time authority exists, but visual day/night presentation does not.
- **Action recommended:** Yes.
- **Priority:** P1 visual fidelity.
- **Exact OpenArcanum follow-up boundary:** Add a presentation-only ambient-light consumer of `SourceTimeService` and
  the current sector's decoded light scheme, matching source tint transitions and Original/Enhanced rebuild behavior.
  Exclude calendar rules, travel-time redesign, weather, NPC schedules, and gameplay visibility.

### Placed lights, additive effects, and nocturnal scenery

- **Classification:** 4 — useful source evidence.
- **Their implementation/claim:** The docs distinguish 48-byte sector lights, object-attached lights, additive glow
  art, auto-animated scenery, and separate nocturnal objects. Public code parses sector lights and resolves light ART
  but does not connect a production lighting/compositing system.
- **OpenArcanum current state:** `SectorReader.ReadLights` and light/eye-candy resolvers exist; scenery multi-frame ART
  can auto-animate. `ReadLights` has no production caller, and there is no source additive blend, light contribution,
  or day/night nocturnal visibility owner.
- **Source evidence:** [`light.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/light.c),
  [`object.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/object.c), and
  [`art.h`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/first_party/tig/include/tig/art.h)
  establish serialized light fields, ART frame offsets, blend flags, auto-animation, and nocturnal switching.
- **Gap:** Placed light presentation, additive/translucent source blending, and nocturnal art switching are absent.
- **Action recommended:** Yes, after ambient time/tint ownership is connected.
- **Priority:** P2.
- **Exact OpenArcanum follow-up boundary:** Project existing decoded sector/object lights and supported source blend
  modes into disposable presentation; bind nocturnal visibility to the same source-time consumer. Do not introduce
  Unity-physics lighting, inferred light sources, gameplay stealth, or AI visibility.

### Shadows

- **Classification:** 4 — useful source evidence. The standalone README implementation claim is category 5.
- **Their implementation/claim:** The docs state that retail uses a dedicated default under-foot shadow sprite and,
  only when optional "real shadows" are enabled, additional pre-rendered directional shadow frames selected per
  nearby light. The public tree contains no production shadow system.
- **OpenArcanum current state:** Terrain mesh shadow casting is disabled and no source shadow-sprite presentation was
  found. Unity URP shadow capabilities are not equivalent to retail's sprite selection.
- **Source evidence:** [`shadow_apply` and light-relative selection in `light.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/light.c)
  and the object compositor in `object.c` establish the behavior. Retail defaults make the under-foot sprite the
  faithful minimum; optional directional shadows are not the first requirement.
- **Gap:** The default retail shadow pass is absent.
- **Action recommended:** Yes, bounded to the source default first.
- **Priority:** P1 for the ambient under-foot sprite; P3 for optional "real shadows."
- **Exact OpenArcanum follow-up boundary:** Add source shadow ART selection/placement and its compositor pass for
  supported visible objects. Defer optional per-light directional frames until placed lights are correct. Do not
  substitute generic realtime Unity shadows.

### Character composite / "paper-doll" ART resolution

- **Classification:** 3 — same result and shared resolver; the public demo uses different ownership.
- **Their implementation/claim:** `CharacterArtGallery` cycles race, gender, armor, shield, weapon, animation, palette,
  and facing, then resolves a single composite critter ART path. Its "paper-doll" wording does not mean independently
  layered runtime sprites; the combinations are precomposed source files selected by art-ID fields.
- **OpenArcanum current state:** The resolver is byte-identical, while production world presentation additionally
  supports critters, monsters, unique NPCs, palette/facing/mirroring, animation rebuilds, and session-owned identity.
- **Source evidence:** [`name.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/name.c)
  composes critter/monster/unique-NPC filenames from art-ID fields and applies female/body/plate/two-handed-shield
  fallbacks; [`art.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/first_party/tig/src/art.c)
  defines the bitfields and mirror transforms.
- **Gap:** No resolver gap and no missing external implementation.
- **Action recommended:** No resolver rewrite and no layered-sprite architecture.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Keep the source composite filename model. Equipment-driven production
  selection is the separate finding below.

### Race/gender/armor/weapon/shield composition in production

- **Classification:** 4 — useful source evidence; the comparison project demonstrates combinations but has no better
  published production equipment bridge.
- **Their implementation/claim:** The gallery proves that the source corpus contains many composite combinations and
  documents exact fallbacks.
- **OpenArcanum current state:** Character identity and equipment are authoritative, the production resolver can draw
  composite art, and combat presentation temporarily projects supported weapon classes into attack ART. General
  equip/unequip still deliberately does not recompute production critter armor/shield/weapon appearance; M3C recorded
  that visual consequence as deferred.
- **Source evidence:** [`item_equipped` / `item_unequipped` in `item.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/item.c)
  update critter art as an equipment consequence; `name.c` supplies the exact composite and fallback rules. Shipped
  critter ART is the authoritative availability corpus.
- **Gap:** Authoritative equipment state and production character appearance are not generally connected.
- **Action recommended:** Yes.
- **Priority:** P1 character visual fidelity.
- **Exact OpenArcanum follow-up boundary:** Add one read-only critter-appearance projection that derives body, gender,
  armor, shield, and weapon fields from existing authoritative character/equipment state, applies source fallback
  rules, and rebuilds the existing sprite owner. It must not change equipment legality, stats, combat weapon choice,
  inventory placement, save schema, or create layered body-part sprites.

### Critter animation and locomotion

- **Classification:** 1 — OpenArcanum is stronger.
- **Their implementation/claim:** The public `SpriteFrameAnimator` loops clips and supports one-shots; the character
  gallery rotates/animates decoded source frames.
- **OpenArcanum current state:** The animator is locally extended with manual source-clock progression and current
  frame retention. `WorldObjectSpriteOwner`, `SourceLocomotionTiming`, player navigation, follower driving, combat,
  death/corpse presentation, mirroring, and exact hotspot transforms connect animations to production authority.
  WALK progress uses authored ART frame offsets and source speed/FPS adjustment.
- **Source evidence:** [`anim.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/anim.c),
  [`object.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/object.c), and `art.c` show frame advancement, object offset
  accumulation, mirrored offset sign, and animation-dependent state.
- **Gap:** No comparative gap. Long-tail action clips and unsupported effects remain content/gameplay validation work,
  not capabilities present in the public comparison tree.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Fix only reproduced animation mismatches with a source ART/`anim.c` trace;
  retain route/session authority outside the animator.

### ART hotspot, origin, mirror, and frame-offset semantics

- **Classification:** 1 — OpenArcanum is stronger. The retained source documentation supports this conclusion.
- **Their implementation/claim:** The ART reader preserves hotspot and frame offsets. `ArtTextureFactory` converts the
  top-left hotspot to a Unity pivot and mirrors pixels for certain paths.
- **OpenArcanum current state:** The same decoder remains. Local `WorldObjectSpriteOwner.ExactPivot` additionally
  implements TIG wall/portal hotspot shifts, facing-mirror transforms, roof/ID-flip behavior, object offsets, and
  source frame offsets for locomotion. Original/Enhanced sprite rebuilds retain the source anchor.
- **Source evidence:** [`tig_art_frame_data` in `art.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/first_party/tig/src/art.c)
  performs the exact hotspot/offset mirror transforms; `object.c` combines location, object offset, frame offset, and
  hotspot when calculating the rendered rectangle and accumulates frame offsets during animation.
- **Gap:** No comparative gap was proven. The comparison document's phrase "per-frame draw offset" is not sufficient
  by itself to justify moving sprites independently of the current source-locomotion path.
- **Action recommended:** No implementation change from this audit.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** If a concrete ART misregistration appears, add a corpus/golden placement
  test for that art type and reproduce the relevant `tig_art_frame_data`/`object.c` path before changing pivots.

### NPC, monster, and unique-NPC presentation

- **Classification:** 1 — OpenArcanum is stronger.
- **Their implementation/claim:** Resolvers and galleries decode critter, monster, and unique-NPC ART and idle frames.
- **OpenArcanum current state:** The same resolvers are connected to production sector loading, stable identities,
  authoritative vitality/death/corpse state, interaction/dialogue, AI/combat, party following, save/load, locomotion,
  and presentation rebuilds.
- **Source evidence:** `name.c`, `art.c`, `object.c`, and the shipped `monster.mes` / `unique_npc.mes` tables.
- **Gap:** No public-comparison gap.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Visual discrepancies should be fixed by art type/ID with no mutation of
  NPC gameplay authority.

### Movement and pathfinding

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** The README claims click-to-move A* pathfinding; no public A* or production movement
  controller is present.
- **OpenArcanum current state:** Deterministic eight-direction A*, source block/tile/facade/wall/portal semantics,
  interaction approach, cross-sector continuation, combat movement, follower movement, source animation timing, and
  physical validation are all connected.
- **Source evidence:** Original sector/block and tile data, `location.c`, `anim.c`, and the current source-audited
  navigation records. The external README alone contributes no additional evidence.
- **Gap:** None relative to public comparison code.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Address future pathing issues only from reproduced source/content cases;
  do not import a second navigation architecture.

### Object interaction, containers, loot, and inventory

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** Galleries display objects and the README claims doors, containers/loot, ground
  items, inventory, and equipment. Public runtime code provides no authoritative transaction service.
- **OpenArcanum current state:** Stable target selection, range approach, SAP_USE, portals, containment, pickup/drop,
  transfer, equipment, stacks, capacity, corpse loot, persistence, audio, UI commands, and save/load are implemented
  through session authority.
- **Source evidence:** `object.c`, `item.c`, `anim.c`, object/prototype data, and the current M2/M3/M8E source audits.
- **Gap:** No comparative gap. README-only features are category 5.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Continue only from a concrete unsupported source interaction or campaign
  defect; preserve transactional command ownership.

### Dialogue

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** Public parsers/evaluators and `DialogScriptGallery` form a useful developer bench;
  world effects such as teleport, item movement, combat, and quest changes are logged/no-op in that harness.
- **OpenArcanum current state:** The same foundations are connected to production NPC identity, strict evaluation,
  campaign state, world actions, party, barter, training, combat, UI, audio, and save/load with fail-closed unsupported
  paths.
- **Source evidence:** [`dialog.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/dialog.c),
  [`dialog_ui.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/ui/dialog_ui.c),
  shipped `.dlg`/`.scr` content, and the current M5/M9B/M13A audits.
- **Gap:** No comparative gap.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Extend only when an authentic reachable dialogue executes an unsupported
  token/opcode; do not promote the gallery harness to runtime authority.

### Quests and journal

- **Classification:** 1 — OpenArcanum is stronger.
- **Their implementation/claim:** Public code reads quest metadata/state text; documentation describes source state
  progression and journal ordering. There is no published production campaign owner or journal UI.
- **OpenArcanum current state:** Coordinator-owned campaign state, monotonic quest transitions, timestamps, rewards,
  journal projection, dialogue integration, save/load, and UI routing are implemented for the bounded source contract.
- **Source evidence:** [`quest.c`](https://github.com/alexbatalov/arcanum-ce/blob/a7ff41b300ef712f0e7d088183a3d08957110cdd/src/game/quest.c),
  source quest tables, scripts/dialogues, and the current M5 audits.
- **Gap:** No public-comparison gap.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Add campaign content coverage only from concrete source quests; do not
  replace the existing campaign authority.

### Followers and party presentation

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** The README claims recruitable followers; public code only exposes relevant dialogue
  concepts and demo-harness toggles.
- **OpenArcanum current state:** Stable ordered membership, source capacity, transactional join/leave, dialogue,
  local/sector/world-map accompaniment, formation intent, autonomous combat integration, XP, defeat retention,
  save/load, UI commands, and source-offset locomotion are implemented.
- **Source evidence:** `dialog.c`, `anim.c`, source follower scripts/dialogues, and current M9B audits.
- **Gap:** No comparative gap. Rich portrait/party HUD art remains an OpenArcanum UI presentation boundary, not a
  published external solution.
- **Action recommended:** No external-code action.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Party portrait/status presentation may be added only as a read-only UI
  projection in a future source-UI slice.

### Combat

- **Classification:** 1 — OpenArcanum is substantially stronger.
- **Their implementation/claim:** The README claims real-time/turn-based combat, AP, criticals, ranged LOS, death, XP,
  and leveling. The public runtime contains only a small `Weapon` model and animation helpers, not a combat service.
- **OpenArcanum current state:** M8A–M8I and M9A provide authoritative turn-based and real-time scheduling, structured
  attack requests, AP, melee/ranged/ammo, cover/line of fire, called locations, criticals, bow mastery/multi-impact,
  defeat/death/corpses/loot/XP, UI commands, NPC target/weapon selection, and deterministic transaction boundaries.
- **Source evidence:** Current M8/M9 source audits trace each bounded rule to `arcanum-ce` and retail data. The external
  README adds no inspectable rules or tests.
- **Gap:** No comparative gap.
- **Action recommended:** No.
- **Priority:** None.
- **Exact OpenArcanum follow-up boundary:** Continue only from a source-audited unsupported combat mechanic or a
  reproduced defect; never infer behavior from the README summary.

### Player-facing UI

- **Classification:** 1 — OpenArcanum is stronger than the published code. Any implication that the comparison project
  has a complete player-facing UI is category 5.
- **Their implementation/claim:** Public presentation is developer IMGUI in galleries/benches and direct demo
  controls. No source-faithful shipping HUD, inventory, dialogue, journal, combat, party, or menu implementation is
  published.
- **OpenArcanum current state:** M12C supplies one controller/presenter and source-shaped command routes; UI-A supplies
  original/enhanced asset resolution and logical scaling; UI-B supplies the retail Main Menu; UI-C supplies the
  retail gameplay HUD implementation. Other modal screens remain explicitly scheduled source-presentation work.
- **Source evidence:** Retail interface ART/MES and locally exported metadata are authoritative. The public comparison
  project contributes no additional UI archaeology beyond the same ART decoder and demos.
- **Gap:** No external UI implementation to adopt. The separate UI-C acceptance gate was subsequently closed by
  UI-C.2 after source audit corrected production to native ID 185 top and ID 184 bottom windows and physical review
  passed at 800×600, 1024×768, 1920×1080, 2560×1440, and 3840×2160.
- **Action recommended:** No further action from this comparison item. UI-C.2 records the authoritative correction.
- **Priority:** Closed.
- **Exact OpenArcanum follow-up boundary:** None. Future UI work must start from a separately authorized screen and
  must not reopen the unproven ID 3 composite as gameplay authority without new source evidence.

## Recommended follow-up order

This audit does not authorize implementation. If the user separately starts visual-fidelity work, the bounded order
supported by original-engine evidence is:

1. UI-C physical target-resolution review and its proven composition defect are closed by UI-C.2.
2. Audit/close source world compositor ordering with representative overlaps.
3. Add the production roof layer and source fade behavior.
4. Connect existing source time and sector schemes to ambient day/night presentation.
5. Add the default source shadow-sprite pass.
6. Connect authoritative equipment to composite critter appearance.
7. Add placed-light/additive/nocturnal presentation; consider optional retail "real shadows" only afterward.

Each item is a presentation boundary. None should redesign gameplay, coordinates, collision, source timing,
inventory/equipment authority, combat, AI, saves, or the Original/Enhanced fallback contract.

## Rejected conclusions

- Do not copy code from `Suvitruf/unity-arcanum`; the public code is already in this branch's ancestry.
- Do not treat the README's unpublished full-world, lighting, shadow, combat, follower, or UI claims as evidence.
- Do not introduce layered character body-part sprites: retail selects precomposed composite ART files.
- Do not replace retail sprite shadows with generic realtime Unity shadows.
- Do not infer roof, lighting, blend, or render-order behavior from gallery screenshots.
- Do not merge developer gallery/bench state into production authority.
- Do not combine the visual follow-ups above into a world-rendering redesign.

## Final audit status

The comparison is complete for the public repository at `2d44a62`. OpenArcanum already contains its full published
code and documentation and is functionally stronger in every player-facing subsystem reviewed. The actionable value
is the retained, source-linked documentation that exposes bounded visual fidelity gaps against original Arcanum.
No production implementation was started and nothing was pushed.

# OpenArcanum Project Status

## Gameplay-Parity Audit

A comprehensive research-only audit was completed on 2026-09-07. The current branch contains all 24 commits from the
published upstream `master` and is 31 commits ahead; no published upstream gameplay branch or release is available to
merge. The audit grades every major gameplay domain, separates parsers/demos from production call paths, identifies
safe/adapt/avoid reuse boundaries, and defines a dependency-ordered M1–M13 roadmap. See
[`documentation_unity/upstream-gameplay-parity-audit.md`](documentation_unity/upstream-gameplay-parity-audit.md).

## Current Objective

M1 traversal, the bounded M2 interaction/SAP_USE kernel, and M3A-M3D inventory/command/equipment/stack state are complete.
`WorldMapSessionCoordinator` owns typed `World`, `Contained(parent)`, and `Equipped(parent, wornLocation)` placement,
atomic raw item transfers/equipment replacement/stack merge and split, deterministic session-created item identities,
and source-faithful pickup/drop/owner-transfer policy independently of Unity presentation.

The next recommended objective is M4A: authoritative PC/NPC base attributes and derived-stat inputs. This supplies the
source-faithful Strength state needed before a later bounded M3E adds item weight and carry/container capacity guards.
Inventory/equipment UI, economy, combat, progression, dialogue, scripts, and save serialization remain deferred.

## Current Branch

Expected branch: `feature/inventory-commands`

Always verify the actual Git branch before doing work. Git is authoritative if it disagrees with this document.

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

## Next Recommended Milestone

The M2C candidate gate was completed on 2026-09-10 against all 22 distinct SAP_USE script numbers attached to placed
portal records. Script 1162 is the only GREEN family within the bounded constraints and is already M2B-supported. Every
additional family requires an excluded owning domain: message/dialog presentation, persistent delayed scripts and
sound, map/sector travel state, quest/lock rules, NPC/combat loops, dynamic object creation, or trap lifecycle. No
production or test code was changed, and the strict whitelist was not widened. See
[`documentation_unity/m2c-sap-use-family.md`](documentation_unity/m2c-sap-use-family.md) for the candidate table and
rejection evidence.

The exact recommended next milestone is M4A: add authoritative PC/NPC base attributes and narrow derived-stat inputs,
beginning with a fresh source audit and retaining raw Strength and related critter state independently of Unity
presentation. This is the prerequisite for a later M3E implementation of item weight, PC carry capacity,
container-capacity transfer guards, and encumbrance without placeholder character rules. M4A must continue to defer
level progression, combat, dialogue, spell/technology effects, inventory UI, economy, script-host expansion, and save
serialization. Additional SAP_USE families remain deferred until their owning domains exist.

# OpenArcanum Project Status

## Current Objective

Production sector objects now have a stable load/rebuild/unload/reload contract.
All 32 formerly unresolved Dernholm records were classified as wall-owned windows
and now resolve through the original engine's exact wall-to-portal derivation.
The real sector renders all 612 non-inventory placed objects with zero unresolved
ART identities, and lifecycle teardown/reload has been validated without duplicate
roots, sprite owners, animation components, or owned texture leaks. Terrain remains
intentionally separate and untouched.

The next objective is a narrow map/session coordinator that owns terrain and object
sector selection together, followed by persistent object identity/state and the
gameplay-side portal animation scheduler. Do not turn the sprite owner into a global
asset manager or begin terrain replacement.

## Current Branch

Expected branch:

feature/hd-texture-replacements

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

## Next Recommended Milestone

Add a narrow map/session coordinator that selects terrain and object sectors together and uses the explicit object
load/unload/reload contract. Introduce stable OID-based state retention across sector transitions, then connect the
existing script/event interfaces to visibility, movement and the exact portal frame scheduler. Validate multiple maps
before considering a small ordinary-sprite atlas/lifetime policy. Keep terrain replacement and bulk conversion separate.

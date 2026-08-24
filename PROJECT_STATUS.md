# OpenArcanum Project Status

## Current Objective

The character and ordinary-object runtime SpriteRenderer integration milestone
is complete for every non-terrain ART-to-Sprite caller that currently exists in
the repository.

The next objective is to reuse the source-aware path when the first production
map character/object sprite constructor is implemented. Terrain remains a
separate future design.

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

Do not attempt mass conversion until source identity has been integrated into
the intended production sprite paths and the replacement authoring contract is
documented beyond this proof.

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

- `CharacterArtGallery` and `ObjectArtGallery` carry source identity, but the repository does not yet contain a
  production map loader that creates character/object sprites for `WorldObject`.
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
- Changing the config does not replace Sprite instances that are already rendered during the same Play run. Live
  in-place Original/Enhanced switching has no supported runtime API yet and will require a small sprite-owner refresh
  contract when a production world renderer exists.

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

## Next Recommended Milestone

Implement or identify the first production map character/object sprite owner, reuse the source-aware factory contract,
and give that owner a narrow rebuild hook for future live graphics-mode switching. Validate a placed animated character
or portal there before designing terrain replacement geometry/atlas behavior. Do not begin bulk conversion or terrain
replacement as part of that milestone.

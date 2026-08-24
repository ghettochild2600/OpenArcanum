# OpenArcanum Project Status

## Current Objective

The single-asset high-resolution replacement proof of concept is complete.

The next objective is to carry source identity through additional production
SpriteRenderer paths without changing Original-mode behavior.

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

- Only `CharacterArtGallery` currently carries source identity into the new overload.
- Mirrored `mirrorX` callers intentionally retain original ART until replacement mirroring semantics are defined.
- HD sprites bypass `RuntimeSpriteAtlas`, so production batching and lifetime management remain future work.
- Only exact 4x replacements are supported.
- The local proof is validation artwork, not a production-quality remaster.
- Loaded and rejected replacements are cached; use `OpenArcanumHDAssetLoader.ClearCache()` after changing an already-seen file during the same runtime session.

## Next Recommended Milestone

Carry ART path, rotation, and frame identity through one production character or
world-object SpriteRenderer pipeline, then repeat the same Original/Enhanced/
missing/invalid A/B validation there. Keep terrain out of scope until its
geometry and atlas assumptions have a dedicated HD design.

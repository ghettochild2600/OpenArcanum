# Original / Enhanced UI Remaster Pipeline

## Purpose

This document defines a presentation-only asset pipeline. It does not authorize artwork generation or production UI
changes. Original and Enhanced modes use identical controllers, view models, screen/modal state, input routing,
logical rectangles, pivots, and hit regions. Only pixels change.

## Stable source identity

Every sprite is addressed by:

`UiAssetKey(sourceId, palette, rotation, frame)`

The key is created from `interface.mes` and ART structure, not from a screen-specific alias. A source asset reused by
several screens has one key and one replacement. Display names and screen categories are catalog metadata only.

Original reference identity:

`D:/OpenArcanum/UIReference/Original/art/interface/<id>-<stem>/p<palette>-r<rotation>-f<frame>.png`

Canonical Enhanced identity:

`HDAssets/ui/by-source-id/<id4>-<stem>/p<palette>-r<rotation>-f<frame>.png`

Examples:

- `HDAssets/ui/by-source-id/0003-intrface/p00-r00-f000.png`
- `HDAssets/ui/by-source-id/0137-lilgrnbut/p00-r00-f000.png`
- `HDAssets/ui/by-source-id/0354-dialoguewindow/p00-r00-f000.png`

Zero padding is fixed: four digits for source IDs, two for palette and rotation, three for frame. The stem is retained
for humans but source ID is authoritative. Preferred category views (`hud`, `inventory`, `dialogue`, and so on) may
be generated as manifests or curation indexes; they must not duplicate replacement PNGs or become runtime identity.
This deterministic by-source-ID scheme is used because one retail asset can serve several screens.

`HDAssets` is ignored, local-only content. Enhanced assets are never copied into the normal repository payload by the
resolver. Retail decoded references likewise remain outside Git.

## Runtime resolution contract (implemented by UI-A)

1. Receive a `UiAssetKey` and its source metadata.
2. Resolve/decode the Original ART frame through the existing mounted-data pipeline and cache it.
3. In Enhanced mode, form the exact canonical HD path.
4. If a replacement exists, validate width, height, readable PNG/RGBA data, and registration metadata.
5. Use it only when valid; otherwise record a diagnostic and fall back to that exact Original frame.
6. Return source logical size, pivot/hotspot, and slice metadata independently from the selected texture.

Fallback is per palette/rotation/frame. A valid normal button cannot conceal a missing pressed button. Failed or
missing Enhanced assets never change layout, hide a control, or block gameplay. Original mode never depends on
`HDAssets`.

Graphics mode is read only by the presentation resolver. An Original -> Enhanced -> Original switch rebuilds sprite
presentation and text material only; it cannot recreate controllers, clear screen/modal state, resend commands, or
mutate gameplay.

## AI/image-remaster asset contract

Default output is **exactly 4x native source width and 4x native source height**. Each deliverable must satisfy all of
the following:

- preserve composition, silhouette, orientation, functional meaning, and registration;
- preserve source pivot/hotspot and frame-to-frame alignment at a 4x scale;
- preserve transparent regions and use the upscaled source alpha footprint as authoritative where practical;
- preserve corners, borders, dividers, and family-wide geometry;
- contain no crop, added padding, canvas expansion, dimensional drift, or unrequested new ornament;
- contain no baked replacement label where Unity can render dynamic text;
- avoid hallucinated glyphs, symbols, controls, material affordances, or state changes;
- use conservative enhancement for icons and controls; ornamental backgrounds may be repainted more freely only
  while their geometry and interaction cues remain unchanged.

Recommended production sequence:

1. Export the exact retail RGB/alpha frame and record its manifest row/hash.
2. Upscale/redraw with image-to-image guidance at the required 4x canvas.
3. Force exact target dimensions without crop or padding.
4. Restore or validate a nearest-neighbor 4x source alpha mask.
5. Manually clean edges without moving the silhouette.
6. Compare against the source at 1x logical size and inspect the raw 4x texture.
7. Save at the canonical HD path.
8. Run automated dimension/alpha/identity checks and human state-family review.
9. Prove runtime fallback by temporarily omitting a related frame.

Generated art must not be accepted directly from a model without these checks.

## State families and animation

Normal, hover, pressed, disabled, active, inactive, selected, and unselected members of a family share an identical
canvas, pivot, control bounds, and edge registration. Difference images should show state treatment, not a moved
button. Animation frames and rotations retain source count, ordering, duration semantics, hotspots, and offsets.
Enhanced mode cannot insert or remove frames.

If one member fails validation, only that member falls back to Original; reviewers should normally reject the entire
family until it is visually coherent.

## Transparency requirements

Retail palette index 0 is exported transparent. For an Enhanced replacement, compare its alpha to the source alpha
expanded exactly 4x:

- fully transparent source blocks remain transparent unless edge antialiasing is explicitly approved;
- antialiasing may occupy only the immediate scaled boundary and must not create selection/hit ambiguity;
- holes, cutouts, and transparent world apertures remain intact;
- RGB in transparent pixels is normalized to avoid sampling fringes;
- the runtime still uses source hit rectangles/alpha-selection policy, not inferred Enhanced opacity.

## Typography and baked text

Prefer Unity-rendered dynamic text for menus, values, descriptions, quantities, dialogue, journal pages, save slots,
version text, and labels that can vary or be localized. Recreate source hierarchy using licensed fonts or project-owned
font assets, with audited size, color, tracking, alignment, outline/shadow, and line spacing.

Original bitmap font ART may be decoded locally for faithful Original mode but must not be redistributed. Enhanced
font generation is a typography task, not an image-model task. Baked words may remain only when inseparable from an
illustration/logo; document the exception and localization consequence in the manifest.

## Slicing and tiling metadata

Slicing is an explicit manifest decision:

- ID 354 `dialoguewindow`: provisional 8-pixel border on every side, 16 x 16 minimum; full 9-slice candidate.
- ID 822 `dialoguebox`: 16-pixel fixed end caps; horizontal extension only; 32 x 136 minimum.
- IDs 238/787/240: fixed-width 11-pixel scrollbar assembly with 5-pixel top, repeating 1-pixel middle, and 7-pixel
  bottom; minimum height 12.

Enhanced slice borders are exactly four times those source pixel values in the texture importer, while logical
borders retain the source size. Decorative backgrounds, book pages, paper dolls, and full HUD/menu art remain fixed
unless new source evidence explicitly proves a scalable center/edge.

## Import and validation metadata

The UI-A resolved-result boundary exposes runtime identity, source path, native dimensions, logical size,
pivot/hotspot, offset, transparency, skin/fallback result, and slice/tile policy. A later production-art curation
index should persist the complete review record:

- source key and source/Enhanced paths;
- source and replacement file hashes;
- native and Enhanced dimensions;
- logical size, pivot/hotspot, frame offset, and alpha bounds;
- slice/tile policy and borders;
- state-family and animation-family membership;
- validation version and result.

Reject an Enhanced file for wrong size, missing/invalid alpha where required, unreadable format, source-key mismatch,
or prohibited registration change. Warnings may flag excessive alpha drift, text-like artifacts, family misalignment,
and seam discontinuity; production acceptance requires human review.

## QA gates

### Automated

- exact 4x dimensions and stable source-key/path mapping;
- no missing/extra animation state compared with the accepted family;
- alpha-mask bounds, transparent aperture, and pivot/offset registration;
- slice/tile border dimensions and minimum-size behavior;
- per-frame fallback and corrupt-file fallback;
- identical logical rectangle/hit region in Original and Enhanced modes;
- deterministic resolver/cache behavior and no gameplay/controller references;
- Original -> Enhanced -> Original rebuild preserves active modal/focus/controller state.

### Visual

- source/Enhanced overlay at logical 1x;
- raw 4x inspection for edges, invented detail, glyph corruption, and palette/material consistency;
- family contact sheet for states/frames/rotations;
- nine-slice/tile tests at minimum, native, and representative expanded sizes;
- screenshots at 1920 x 1080, 2560 x 1440, and 3840 x 2160;
- Original and Enhanced side-by-side with identical UI-state fixture;
- accessibility/text-fit and cursor registration checks.

### Functional

The same focused screen tests run under both modes. They assert command routing, modal/input precedence, and hit
regions, not image color. Missing Enhanced art must be indistinguishable from Original for behavior. No remaster is
accepted if it changes gameplay state, screen order, focus, hover target, timing, or save data.

## Versioning and review

Treat each accepted replacement as a reviewable local asset with provenance, prompt/tool metadata when applicable,
manual edits, and validator result stored outside the retail reference. Do not overwrite the retail export. Regenerate
references only from mounted retail data. UI-A implements the canonical path and runtime validation boundary; a later
production-art workflow may add a persisted curation index without changing `UiAssetKey` or runtime authority.

## UI-A implementation and proof

UI-A completed on 2026-10-04. `RetailUiAssetResolver` reads the mounted retail VFS and ART decoder directly;
`EnhancedUiAssetResolver` validates the canonical local PNG per frame; and `UiSkinResolver` selects presentation,
falls back independently, caches bounded results, and invalidates bound components on graphics-mode changes. Common
image, button, cursor, tiled-scrollbar, typography, logical-root, and layer primitives are available to later screens.
No production Enhanced art was created and `UIReference/Original` remains reference-only.

Focused validation passed 23/23 and the complete EditMode suite passed 1114/1114. Physical Play Mode resolved retail
button/HUD/icon/scrollbar/cursor fixtures, loaded a temporary generated exact-4x checker, proved missing-frame fallback,
and rebound Original -> Enhanced -> Original without geometry, controller, screen, or gameplay mutation. See
[`ui-runtime-architecture.md`](ui-runtime-architecture.md) for the concrete component and migration contract.

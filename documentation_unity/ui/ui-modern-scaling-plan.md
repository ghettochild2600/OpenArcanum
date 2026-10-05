# Modern UI Scaling Plan

## Non-negotiable model

**Texture resolution is not logical UI size.** The authentic layout lives in an 800 x 600 logical coordinate space.
A retail 120 x 32 image and its Enhanced 480 x 128 replacement both occupy a 120 x 32 logical rectangle. Graphics
mode never selects a second coordinate system.

The UI-A runtime exposes three layers:

1. Source layout records in 800 x 600 UI units, including pivots and hit rectangles.
2. A responsive root that determines scale, safe region, and any world-only expansion.
3. A sprite resolver that supplies Original or Enhanced pixels without changing layer 1 or 2.

Full authored panels are not stretched. A 4:3 modal may be centered on a widescreen surface; gameplay may extend the
world viewport into the side area while fixed HUD groups remain registered to their authentic center/edge anchors.

## Source coordinate systems

- Base composition: 800 x 600.
- Gameplay frame: top 41, world strip 400, bottom 159.
- Management/modal strip: normally `(0,41,800,400)` or an authored 800 x 400 panel.
- Full screens such as Main Menu: 800 x 600.
- Mouse transforms in source Inventory, World Map, and Main Menu code explicitly pass through centered 800 x 600
  coordinates. The Unity presenter should make this transform visible/testable rather than spreading arithmetic
  through controls.

Separate local panel coordinates are permitted inside a source rectangle, but every control resolves back to the
same root logical surface. This is not Unity’s generic “scale every child to fit width” behavior.

## Root scale and safe surface

For a source-composed screen, define the uniform reference scale:

`referenceScale = min(screenWidth / 800, screenHeight / 600)`

This fills the target height on standard 16:9 displays while preserving 4:3 proportions. The remaining side width is
not used to stretch source art. It is handled by screen class:

- **Gameplay:** expose more world horizontally and anchor separable HUD groups to safe edges/center.
- **Full-screen authored menu/art:** center the 4:3 art; use neutral/derived pillar treatment or explicitly authored
  continuation, never a horizontal stretch.
- **Fixed 800 x 400 management/modal panel:** center within the safe height. Background/world may remain visible at
  sides, dimmed only where the source/modal design supports it.
- **Extensible source component:** expand only when manifest evidence proves tiling or slice-safe edges.

An accessibility/UI-scale preference may multiply the reference scale within tested bounds, but must preserve the
same logical geometry, keep required controls on screen, and use a safe-area solver rather than clipping.

## Target-resolution behavior

| Target | Uniform 800 x 600 fit | Remaining widescreen width | Required behavior |
|---|---:|---:|---|
| 1920 x 1080 | 1.8x = 1440 x 1080 | 480 physical px, 240 each side | Gameplay world expands to sides; 4:3 menus/panels remain centered; controls remain readable and un-stretched. |
| 2560 x 1440 | 2.4x = 1920 x 1440 | 640 physical px, 320 each side | Same logical composition, increased world exposure; dynamic text rasterizes at target scale. |
| 3840 x 2160 | 3.6x = 2880 x 2160 | 960 physical px, 480 each side | Same geometry; Enhanced 4x pixels are sampled near native density without changing logical size. |

These are reference fits, not separate layout authoring resolutions. Integer pixel snapping may be used selectively
for thin rules and bitmap-font Original mode, but it must not produce different hit rectangles or state.

## Major screen rules

### Gameplay and combat HUD

Retain the authentic 800-wide center composition and 41/400/159 vertical division. Where `intrface.art` is a single
800 x 600 image, reconstruction should separate only source-proven HUD pieces/masks so that the world aperture can
grow horizontally. Center-bound groups such as the lens/message/quick slots stay registered to the center; vial and
edge groups anchor to their corresponding safe edges. Additional width displays world, not duplicated buttons,
stretched ornament, or hidden pillars.

Combat reuses the same root. AP/readiness/called-shot/target state appears in its source regions. Entering combat or
switching graphics mode cannot rebuild authority or alter current turn/AP.

### Main Menu

Center the authentic 800 x 600 composition. Preserve button positions within it. Side areas use a deliberately
neutral treatment or future authored background continuation; they do not scale the 4:3 painting to fill. A single
uniform scale and safe-area offset converts hit rectangles.

### Character Creation and character management

Center the 800 x 400 editor strip within the 800 x 600 reference surface. At all required resolutions the primary
editor, identity controls, points, finalize/back, and fixed subpanels are simultaneously reachable with no horizontal
scrolling. Only a genuinely long source list may scroll vertically inside its source rectangle.

### Inventory, equipment, barter, and loot

Treat joined paper-doll/inventory art as a fixed 800 x 400 composition. Do not widen grids or stretch paper-doll art
just because extra width exists. Item icon pixels and dynamic stack labels resolve separately. All equipment hit
rectangles scale through the root transform.

### Journal, Magic, Technology, Crafting, Skills, Party, Dialogue

Keep book, tab, window, and fixed-panel art at source proportions. Dynamic text may reflow only inside evidenced text
rectangles. Dialogue ID 354 may use its proven slice margins; ID 822 may extend horizontally using preserved end caps.
Other ornamental panels stay fixed unless a future source proof changes the manifest.

### Local Map and World Map

World Map keeps its source 501 x 365 canvas and surrounding controls inside the 800 x 400 modal strip for the first
faithful implementation. Widescreen sides do not change route/destination coordinate math. Local Map is a separate
surface. A later optional larger canvas requires an explicit gameplay/presentation design decision and source proof,
not an incidental CanvasScaler effect.

### Save/Load, Options, Sleep, Fate, confirmations, tooltips

Center fixed source panels. Use proven tiling/slicing only for common windows. Confirmations remain above their owner
in one modal stack. Tooltips clamp to the safe surface while retaining their source offset and never capture gameplay
authority.

## Sprite import and Enhanced 4x sizing

The resolver reports both native pixel dimensions and source logical dimensions. For a valid 4x replacement:

- enhanced width must equal `nativeWidth * 4`;
- enhanced height must equal `nativeHeight * 4`;
- pivot/registration and alpha footprint scale exactly 4x;
- the RectTransform remains `nativeWidth x nativeHeight` logical units (or the explicit source logical size when it
  differs from frame pixels);
- pixels-per-unit/import metadata changes, not the authored geometry.

Frame, rotation, and palette identities are resolved independently, so one missing Enhanced frame falls back to that
Original frame instead of replacing or disabling the family.

## Safe areas, input, and text

Screen safe-area offsets are applied outside the 800 x 600 transform. Pointer input is inverted through the exact
same transform before hit testing. Keyboard shortcuts and modal precedence do not depend on screen resolution.

Dynamic text uses source rectangles, alignment, color, and hierarchy. Font size is expressed in logical units and
rasterized at target density. Wrapping is deterministic. Original bitmap glyph mode can pixel-snap; Enhanced/dynamic
mode may use licensed high-resolution font assets, but neither may resize a control to fit silently. Overflow is a
localization/accessibility defect that must be surfaced in validation.

## Validation matrix

Every reconstructed screen is captured at 1920 x 1080, 2560 x 1440, and 3840 x 2160 in Original and Enhanced modes.
Tests assert the same logical rectangles/hit targets across modes, no non-uniform sprite scale, correct safe-area
transform and input inversion, and per-frame fallback. Visual review checks proportions, fixed ornament, world-only
expansion, text fit, cursor registration, and absence of placeholder controls.

The UI-A harness now proves the 800 x 600 root, full-width gameplay world rectangle, and representative native/logical
dimensions directly through `UiLogicalMapping` and a transient `SourceUiPresentationRoot`. Focused tests and physical
Play Mode passed at 1920 x 1080, 2560 x 1440, and 3840 x 2160. The scales/origins are respectively 1.8/240,
2.4/320, and 3.6/480; a generated 4x texture retained the same 23 x 23 logical rectangle as its retail frame.
Development proof objects are transient and never part of production presentation.

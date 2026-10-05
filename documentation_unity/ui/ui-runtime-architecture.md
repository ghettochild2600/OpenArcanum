# Common Source UI Runtime Architecture

## Status and boundary

UI-A — Common UI Runtime / Skin Resolver completed on 2026-10-04. UI-B — Source-Faithful Main Menu Reconstruction
then became the first production consumer. The existing M12C `GameUiController`, view-model projections, command
routing, input precedence, modal state, and gameplay authority remain intact. UI-B replaces only the disposable Main
Menu view; the other placeholder views remain. Neither milestone contains production Enhanced artwork.

The migration rule for UI-B onward is:

```text
Placeholder View          Source-faithful View
       |                          |
       +---- same Controller / ViewModel ----+
```

Replacing pixels or a view hierarchy must never create another gameplay or screen-state authority.

## Asset identity and results

`UiAssetKey(sourceId, palette, rotation, frame)` is an immutable value. Equality, hashing, and diagnostics include all
four fields; the source ID remains authoritative and filenames are derived metadata. A resolved result carries the
requested key, source path, native logical size, frame offset, ART hotspot, pivot, transparency, selected skin,
fallback flag, runtime texture/sprite, and texture scale.

`RetailUiAssetResolver` mounts the loose module and retail module/DAT sources through the existing first-mount-wins
`DatVirtualFileSystem`, reads retail `art/interface/interface.mes`, and decodes the requested ART palette, rotation,
and frame through `ArtReader`/`ArtTextureFactory`. `UIReference/Original` is never consulted at runtime. Missing IDs,
paths, rotations, or frames fail safely with one bounded diagnostic.

`EnhancedUiAssetResolver` derives:

`HDAssets/ui/by-source-id/<id4>-<stem>/p<palette>-r<rotation>-f<frame>.png`

It first obtains the Original result, then accepts only a decodable image with exactly four times the native width
and height and compatible transparency. A missing, corrupt, wrong-size, or incompatible file is rejected for that
key only. `UiSkinResolver` returns the matching Original frame as a per-frame fallback, so another valid state or
animation frame remains Enhanced.

## Cache and lifetime

The retail resolver caches decoded ART sources and resolved frames by typed identity. The Enhanced resolver caches
accepted frames and a deduplicated rejected-path set. The skin resolver caches the final key/skin selection. A
graphics-mode change clears Enhanced/final presentation caches and notifies bound presentation components; Original
decoded source resources remain reusable. Explicit `Dispose` releases owned Unity textures/sprites, and the existing
HD loader now uses immediate destruction in EditMode so cache clearing is valid in both Editor and Play lifetimes.

This is a bounded session cache, not an asset-bundle or import database. `GameData` and `HDAssets` are read-only.

## Logical layout and widescreen mapping

`UiLogicalMapping` defines one top-left 800 x 600 source coordinate surface:

- top interface: 41 logical pixels;
- world/management region: 400 logical pixels;
- bottom HUD: 159 logical pixels.

The uniform reference scale is `min(width / 800, height / 600)`. At 1920 x 1080, 2560 x 1440, and 3840 x 2160 the
scales are 1.8, 2.4, and 3.6, with centered horizontal origins of 240, 320, and 480 physical pixels. Authored fixed art
uses that centered surface. The gameplay world rectangle may span the target width while retaining the source
41/400/159 vertical registration. A 4x Enhanced texture changes texture sampling density, not logical dimensions or
hit geometry.

`SourceUiPresentationRoot` creates separate World, HUD, Modal, Dialogue, Tooltip, and Cursor layers. It owns no
screen/modal state. Later views choose layers while their existing controllers remain authoritative.

## Common presentation components

- `SourceUiImage` binds one key, applies native logical sizing/aspect, and refreshes on skin invalidation.
- `SourceUiButton` maps normal, hover, pressed, disabled, and optional selected presentation keys onto one unchanged
  Unity `Button` interaction rectangle. It emits UI intent only.
- `SourceUiCursorPresenter` resolves retail/Enhanced pixels while retaining the ART hotspot as functional metadata.
- `SourceUiVerticalTileAssembly` composes fixed top/bottom caps and a repeating middle for the proven scrollbar
  family.
- `SourceUiScaleMetadataCatalog` defaults to Fixed and exposes only audited exceptions: ID 354 8-pixel 9-slice,
  ID 822 16-pixel horizontal caps, and IDs 238/787/240 scrollbar assembly.
- `SourceUiText` applies dynamic logical roles for size, color, alignment, style, and shadow. Labels remain dynamic;
  UI-A neither bundles retail font files nor claims final per-screen typography tuning.
- `SourceUiRuntime` creates the production resolvers lazily for later source-faithful views.

## Graphics-mode behavior

`OpenArcanumGraphicsSettings` exposes a presentation mode-change notification. `UiSkinResolver` responds by
invalidating selected presentation and bound components. It does not own input, controller, modal, screen, or gameplay
state and does not execute UI commands. Physical validation proved Original -> Enhanced -> Original rebinding of one
live `SourceUiImage` without geometry drift, controller recreation, duplicate presenters, screen changes, or gameplay
state mutation.

## Validation record

Focused UI-A EditMode validation passed **23/23**. It covers key value semantics; retail source/frame/native sizing and
safe missing lookup; valid exact-4x, missing, wrong-width, wrong-height, corrupt, and per-frame Enhanced paths; skin
selection/rebinding; cache reuse; 1080p/1440p/4K transforms; logical-size parity; button geometry; cursor hotspot;
fixed/slice/tile policies; layer separation; scrollbar assembly; and dynamic typography.

The generated Enhanced fixture is a temporary checker PNG under the test/Unity temporary cache. It contains no retail
art and is deleted after validation. No fixture or production replacement was added to `HDAssets`.

Physical Play Mode used the production M12C placeholder presenter and real mounted retail data. It resolved:

- ID 137 `lilgrnbut`, 23 x 23 button;
- ID 3 `intrface`, 800 x 600 HUD frame;
- ID 11, 24 x 24 icon and independent frames;
- ID 238, 11 x 5 scrollbar cap;
- ID 1, 16 x 22 cursor with source hotspot.

The proof retained the placeholder Main Menu, loaded the generated exact-4x button only in Enhanced mode, fell back
for a missing icon frame, returned to Original, retained one controller/presenter and unchanged gameplay/screen state,
and passed all three resolution mappings with no warnings or errors during the proof.

The complete EditMode suite passed **1114/1114**, with 0 failed, 0 skipped, and 0 inconclusive. Its 11 warnings were
the existing intentional fail-closed dialogue compatibility diagnostics. Unity compilation was clean.

## UI-B production consumer

`RetailMainMenuView` is attached once to `ProductionGameUiPresenter`. It resolves retail backgrounds 329/331,
bitmap-font 327, cursor 0, and `mes/mainmenu.mes` labels through the shared UI-A lifetime. The view projects one
presentation-only hierarchy over `GameUiController`; New Game, Load, Options, and quit still enter their existing
authorities. The presenter suppresses the legacy IMGUI Main Menu only while the source view is available and consumes
Main Menu input before gameplay shortcuts. One stale-invalidation guard lets parent rebuilds retire old glyphs safely.

UI-B passed 22/22 focused tests, UI-A 23/23, M12C 18/18, Player Startup 3/3, and the complete 1136/1136 EditMode
suite. Physical Play Mode proved the Original retail composition, generated exact-4x replacement and per-frame
fallback, state/geometry preservation across Original -> Enhanced -> Original, one presenter/EventSystem, production
New Game/Load/Options routes, bounded unsupported branches, retail cursor hotspot, and centered 1080p/1440p/4K output.
See [`ui-b-main-menu-reconstruction.md`](ui-b-main-menu-reconstruction.md).

## Exact UI-C boundary

UI-C should replace only the production Gameplay HUD presentation with the source-backed center composition and
source-proven widescreen anchors. It must reuse existing M12C/M8I projections, command routes, modal/input precedence,
and gameplay authority. It must not migrate Character Creation, Inventory, Save/Load, Options, other screens, or
generate production remastered artwork. UI-C has not started.

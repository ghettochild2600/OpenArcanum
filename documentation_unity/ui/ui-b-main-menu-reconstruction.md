# UI-B — Source-Faithful Main Menu Reconstruction

## Status and boundary

UI-B completed on 2026-10-04. The production launch path now replaces only the disposable M12C Main Menu
presentation with the retail Arcanum composition. `GameUiController`, M12B character creation, M6 save/load, the
Options screen, session ownership, and platform quit authority remain unchanged. HUD, Character Creation, Save/Load,
Options, Credits, and other screen reconstruction remain outside this milestone. No retail pixels or production
Enhanced artwork were added to Git.

## Retail sources

The view resolves all presentation through the mounted retail VFS and the UI-A skin runtime:

- interface ID **329**, `mainmenuback`: top-level 800 x 600 still life, including the lamp, central carved menu
  frame, blue device, skull, tabletop, and foreground props;
- interface ID **331**: 800 x 600 Single Player composition;
- interface ID **327**, `morph30font`: frame-addressed menu glyphs, where the source frame is `(byte)character - 31`;
- interface ID **0**: 24 x 27 retail pointer, rendered in the logical Canvas with its decoded ART hotspot;
- `mes/mainmenu.mes`: rows 460-463 for the available top-level labels, 50-54 for Single Player, 420-422 for the
  New Game choice, and 5100 for quit confirmation.

The mounted single-player distribution lacks the Multiplayer message row even though the supplied authentic retail
screenshot proves the item and its location. UI-B therefore retains the literal retail label in the proven five-row
layout while leaving multiplayer gameplay unsupported. This is the one documented text-table/source-package
discrepancy; it is not a visual approximation.

## Composition, typography, and controls

The Main Menu uses one fixed, centered **800 x 600** logical surface. Top-level labels are centered at x=410 with
source rows y=143, 193, 243, 293, and 343. The entire source painting is scaled uniformly; none of the lamp, skull,
globe, frame, tabletop, props, or lettering is stretched separately.

Retail lettering is not replaced by a generic Unity font. `SourceUiBitmapText` composes ID 327 glyph frames using
their decoded sizes and ART hotspot advances. Each menu label uses the source-shaped three-pass treatment: black at
(-1,-1), brown `(97,61,42)` at (+1,+1), and dark red `(100,0,0)` in front. Hover, pressed, and keyboard-selected
states change only the foreground to `(240,15,15)`. The transparent hit rectangle is derived once from the glyph
bounds and does not move between states. Arrow keys wrap the explicit button order; Enter activates the selected
entry; Escape returns submenus, notices, and confirmation to the top level.

The pointer uses retail ID 0 on the same logical surface. Its RectTransform pivot is derived from the decoded hotspot,
so the visible point and Unity hit testing agree. A Canvas cursor is used because runtime-decoded ART textures do not
meet every platform's native cursor import restrictions; this preserves source pixels and avoids native-cursor
warnings.

## Retail hierarchy and authority routing

The top-level order is exactly:

1. Single Player
2. Multiplayer
3. Options
4. Credits
5. Exit Game

Single Player switches to source background 331 and exposes `New Game`, `Load Game`, `Last Save`, `View Intro`, and
`Cancel`. New Game then exposes the source `Pick Character`, `New Character`, and `Cancel` choice. New Character calls
the existing `GameUiController.BeginNewGame` path and reaches the existing M12B/M12C Character Creation screen; it
does not own a second draft or session. Load Game and Last Save open the existing M6 load panel. Options opens the
existing Options controller before player creation. Exit Game shows source row 5100 and delegates the confirmed quit
to the already-bound platform callback.

Unsupported source branches fail closed inside the presentation model:

- Multiplayer remains visible and interactive, then reports that multiplayer is unavailable without creating a
  network/gameplay system.
- Credits reports that the retail Credits presentation is not yet available; it does not invent credits content.
- View Intro reports that retail movies are not yet mapped.
- Pick Character reports that pregenerated-character selection is not yet available.

Each bounded notice preserves the active session and returns to the Main Menu. Reconstructing Credits, intro movies,
pregenerated-character selection, and Multiplayer are separate future decisions, not hidden UI-B features.

## Production integration and lifecycle

`ProductionGameUiPresenter` owns one `RetailMainMenuView`, synchronizes it from the existing controller screen, and
suppresses the old IMGUI Main Menu only while the source view is available. The same presenter consumes Main Menu
input before generic gameplay shortcuts. The source view creates an EventSystem only when one does not already exist.
Physical validation found exactly one controller/presenter/view and one EventSystem, with no duplicate handlers or
stale placeholder underneath.

The UI-A invalidation path gained one lifecycle guard: if a parent rebuild has already destroyed a glyph while a
cached invalidation delegate is being dispatched, the retired Unity object is ignored. Retail message-table reads
were added to the existing retail resolver and share its mounted VFS, cache, diagnostics, and disposal lifetime.

## Original, Enhanced, and widescreen behavior

Original mode resolves IDs 329, 331, 327, and 0 from GameData at runtime. Physical comparison against the supplied
retail screenshot confirmed the recognizable lamp/frame/globe/skull/tabletop composition, five-item ordering,
positions, red gothic bitmap lettering, and pointer behavior. Unity filtering may differ from a retail framebuffer,
but no substantive composition deviation was observed.

Enhanced validation generated a temporary exact-4x 3200 x 2400 replacement for ID 329 outside the repository. The
live production view selected it in Enhanced mode while preserving the 800 x 600 logical rectangle, menu state, hit
regions, controller, and pointer hotspot. Missing glyph replacements fell back per frame to Original, and a 3199 x
2400 background was rejected in favor of Original. Returning to Original restored the retail background. The fixture
was deleted after validation and no file was added to `HDAssets`.

The same mapping passed at all required resolutions:

| Resolution | Uniform scale | Horizontal origin | Result |
|---|---:|---:|---|
| 1920 x 1080 | 1.8 | 240 px | centered 1440 x 1080 composition; controls usable |
| 2560 x 1440 | 2.4 | 320 px | centered 1920 x 1440 composition; unchanged logical geometry |
| 3840 x 2160 | 3.6 | 480 px | centered 2880 x 2160 composition; no clipping or distortion |

## Validation record

- Unity 6000.0.71f1 compilation: clean.
- UI-B focused EditMode: **22/22**, 0 failed/skipped/inconclusive.
- UI-A regression (shared resolver/layout touched): **23/23**.
- M12C menu/controller regression (production presenter/controller touched): **18/18**.
- Player Startup regression (launch integration touched): **3/3**.
- Complete EditMode suite: **1136/1136**, 0 failed/skipped/inconclusive.
- `git diff --check`: clean.

Physical Play Mode used `Assets/_Game/Scenes/OpenArcanum.unity` and the production `CombatStateService`-independent UI
path. It proved source backgrounds 329/331, five top-level entries, hover/pressed states without geometry drift,
Single Player and Escape, Main Menu -> Single Player -> New Game -> New Character -> existing Character Creation,
Main Menu -> Single Player -> Load Game -> existing load authority, Options, bounded Credits/Multiplayer behavior,
quit confirmation, generated Enhanced exact-4x selection, Original restoration, all three resolution mappings,
source cursor/hotspot, unchanged session authority, and zero duplicate views/controllers/EventSystems. The side-by-side
retail comparison passed. The proof Console ended at **0 warnings and 0 errors**.

The complete suite intentionally emits existing fail-closed dialogue diagnostics. Its result file is authoritative:
all 1,136 tests passed. A legacy M8A test-runner handoff emitted a post-result null reference after the suite had saved
its passing XML; a normal domain reload cleared that transient runner residue, and the final Unity Console was
rechecked independently.

## Remaining source ambiguity and next boundary

No source-specific Main Menu music/scheme mapping was proven through the current M12D tables. UI-B therefore does not
invent or duplicate audio behavior; mapping that context remains documented future audio/presentation work.

The exact recommended UI-C boundary is **Gameplay HUD presentation only**: replace the generic production HUD with the
source-backed center composition and source-proven widescreen anchors while reusing existing M12C/M8I projections and
commands. It must not absorb Character Creation, Inventory, Options, Save/Load, gameplay authority, or production
Enhanced artwork. UI-C has not started.

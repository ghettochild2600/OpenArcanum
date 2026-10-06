# UI-C — Source-Faithful Gameplay HUD Reconstruction

## Status

UI-C.2 completed on 2026-10-05 and supersedes the rejected UI-C.1 composition claim. The valid UI-C.1 cursor-hotspot
and physical pointer-mapping work remains. Production now follows the source-proven two-window gameplay HUD instead
of treating ID 3 `intrface` as a full-screen runtime composition.

No gameplay authority changed. `RetailGameplayHudView` remains a disposable projection over the existing
`GameUiController`, `CombatUiController`, quick-slot service, and session coordinator. No retail pixels or production
Enhanced assets were committed.

## Corrected source evidence

The correction was audited against retail `art/interface/interface.mes`, mounted retail ART, and the complete local
`arcanum-ce/src` tree. The decisive path is `src/ui/intgame.c`:

- `iso_interface_create` requests ID 185 at line 1221 and creates the top window with
  `TIG_WINDOW_HIDDEN | TIG_WINDOW_CENTERED | TIG_WINDOW_TOP` at line 1237.
- It requests ID 184 at line 1223 and creates the bottom window with
  `TIG_WINDOW_HIDDEN | TIG_WINDOW_CENTERED | TIG_WINDOW_BOTTOM` at line 1240.
- Each ART is blitted into its own window at line 1251.
- Child rectangles are converted from absolute 800 x 600 coordinates to the owning window's local coordinates at
  lines 1278–1299; quick-slot setup begins from the bottom window at line 1302.
- `src/ui/hotkey_ui.c:178` and later `intgame.c` call sites reuse ID 184; later `intgame.c` call sites reuse ID 185.
- `src/ui/hrp.c:26–49` translates center-horizontal windows by `(displayWidth - 800) / 2` and bottom windows by
  `displayHeight - 600`. It does not resize either ART.

No literal `tig_art_interface_id_create(3, ...)` call or `intrface` source-name reference exists in the audited
`arcanum-ce/src` tree. ID 3 is resolvable and visually resembles a legacy composite, but dimensions and decoded alpha
are not sufficient authority. Indirect or table-driven use remains possible but unproven. Production therefore does
not use ID 3 as the gameplay HUD.

## Production composition

| Window | Source ART | Native size | Physical placement |
|---|---:|---:|---|
| Top | ID 185 `inttop` | 800 x 41 | centered horizontally, anchored to top |
| Bottom | ID 184 `intbotom` | 800 x 159 | centered horizontally, anchored to bottom |

The gameplay camera covers the full physical display. At 800 x 600 the windows occupy source y=0–40 and y=441–599.
At higher resolutions they remain exactly 800 pixels wide and expose additional world both horizontally and
vertically. They are not scaled to an 800 x 600 reference surface.

The Canvas uses constant pixel size. Top controls remain local to ID 185. Bottom content retains its audited absolute
source coordinates and is converted with `localY = sourceY - 441` before parenting to ID 184. The full-screen cursor
layer preserves the valid UI-C.1 hotspot correction and maps one source cursor pixel to one physical pixel.

## Source-owned dynamic regions

- IDs 20/18/19 provide empty glass, health liquid, and fatigue liquid.
- Health liquid uses `(14,472,28,88)`; fatigue uses `(754,473,28,88)`.
- Counters use `(15,578,29,12)` and `(755,578,27,12)`.
- ID 354 supplies the message lens at `(196,492,410,107)`; dynamic text clips to `(211,503,383,82)`.
- ID 251 supplies the bounded supported ammunition icon.
- Ten quick slots use source x positions `198,237,276,315,354,418,456,495,534,573` at y=445 and source IDs
  `173,174,175,176,177,178,179,180,181,172`.
- IDs 169/187/193/186 provide Character, Logbook, Map, and Inventory at y=2 in the top window.
- IDs 470/472/473/471 provide Combat, Skills, Spells, and Schematics in the bottom window.

Health, fatigue, equipment, ammunition, AP, readiness, combat state, messages, and shortcuts remain projections of
the completed M3/M4/M8/M12C systems. Buttons and keys call existing controller routes. Unsupported Fate, Sleep,
party/status, broader ammunition, slot-drag/cooldown, and rare-audio branches remain fail-closed or deferred.

## Input and lifecycle

Only the physical ID 185 and ID 184 rectangles block pointer-to-world input. The world between and around them is
interactive. The cursor sits on a full-screen native layer. `ProductionGameUiPresenter` owns exactly one
`RetailGameplayHudView`; the persistent EventSystem stays outside the transient Main Menu canvas. Graphics rebuilds
replace only presentation resources and do not duplicate controllers, presenters, subscriptions, or EventSystems.

Dialogue and every other management modal remain separate M12C routes. Entering dialogue hides/replaces gameplay
presentation through the existing modal controller; the corrected HUD contributes no dialogue geometry or state.

## Resolution and skin behavior

| Resolution | Strip x | Top y | Bottom y | Strip sizes |
|---:|---:|---:|---:|---:|
| 800 x 600 | 0 | 0 | 441 | 800 x 41 / 800 x 159 |
| 1024 x 768 | 112 | 0 | 609 | 800 x 41 / 800 x 159 |
| 1920 x 1080 | 560 | 0 | 921 | 800 x 41 / 800 x 159 |
| 2560 x 1440 | 880 | 0 | 1281 | 800 x 41 / 800 x 159 |
| 3840 x 2160 | 1520 | 0 | 2001 | 800 x 41 / 800 x 159 |

Original mode resolves IDs 185 and 184 from mounted retail data. Generated temporary 3200 x 164 and 3200 x 636
fixtures prove exact-4x Enhanced texture selection while logical/physical window geometry remains 800 x 41 and
800 x 159. Missing replacements fall back independently. Original -> Enhanced -> Original preserves controller,
session, shortcut, combat, geometry, hit regions, cursor, and presenter/EventSystem identity.

## Physical validation and comparison corpus

The existing Unity Editor and production `Assets/_Game/Scenes/OpenArcanum.unity` were used. A fresh source-valid New
Game reached START_MAP 1 at the crash site. Direct review passed at 800 x 600, 1024 x 768, 1920 x 1080, 2560 x 1440,
and 3840 x 2160. At every target the two native strips remained centered, unclipped, and unscaled, with expanded
world visible around them. The real Inventory control opened the existing Inventory route. After leaving attack mode,
the world area accepted the gameplay pointer and displayed the correctly registered source cursor at the requested
world point. One HUD presenter, one controller presenter, and one EventSystem remained.

Local captures and the reference index stayed under ignored `Temp/UIC2Validation` and were not committed. Direct
visual comparison used unmodified 800 x 600 retail screenshots indexed by MobyGames, including the original 2001
Windows gallery and the later 1.0.7.4 full-screen-mode reference. The comparison was structural rather than a
pixel-diff because scenes differed: top/bottom silhouettes, management buttons, quick slots, vials, message lens,
and bottom control registration agreed; the prior oversized ID 3-derived composition was absent. Old-Games and ModDB
captures were retained only as uncertain/modded corroboration, never as authority.

No external screenshot or original retail ART was added to Git.

## Validation record

- UI-C focused EditMode: **42/42**.
- M12C controller/presenter regression: **18/18**.
- M12C production physical validator: **PASS**, including authentic Virgil dialogue entry and response selection
  through the separate modal route.
- Player Navigation/Input regression: **21/21**.
- Production Keyboard regression: **23/23**.
- UI-A, UI-B, and M8I were not rerun because UI-C.2 did not alter shared fixed-surface mapping, Main Menu runtime, or
  combat-HUD routing.
- Complete EditMode: **1178/1178**, 0 failed, 0 skipped, 0 inconclusive.
- Unity compilation: clean.
- Final cleared Console: 0 errors, 0 unexpected warnings.
- `git diff --check`: clean.

## Boundary

UI-C.2 corrects only source gameplay-HUD composition, ownership, native positioning, pointer blocking, audit data,
and their tests. It does not begin UI-D or change gameplay, modal, combat, inventory, navigation, dialogue, Save V1,
or Enhanced-art authority. Character Creation remains the next separately authorized presentation milestone.

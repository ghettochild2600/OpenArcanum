# UI-C — Source-Faithful Gameplay HUD Reconstruction

## Status

UI-C.3 completed on 2026-10-06 and closes the visual-fidelity follow-up that reopened UI-C after UI-C.2. The valid
UI-C.1 cursor-hotspot and physical pointer-mapping work and the UI-C.2 two-window correction remain. Production follows
the source-proven two-window gameplay HUD instead of treating ID 3 `intrface` as a full-screen runtime composition.
The last observed source defects are corrected: the counter apertures use the source bitmap font and formatting, the
maintained-spell apertures are filled with their source state art, and the HUD remains present behind ordinary
Inventory exactly as the source window model requires.

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
- Counters use `(15,578,29,12)` and `(755,578,27,12)`. Source font ID 171 supplies Cloister 18 digits; values are
  zero-padded to three digits, measured, centered, and rendered over the black counter aperture.
- Five maintained-spell slots use open IDs 188–192 at `(281 + 50n,3,32,32)` and plugged IDs 628–632 at
  `(280 + 50n,2,35,35)`. Open capacity is the source-bounded `Intelligence / 4`, capped at five.
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

Ordinary Inventory is the source exception to modal HUD hiding: it composes in the 800 x 400 middle window while the
native top and bottom strips remain visible. The Inventory button toggles that route closed when pressed again. A
selected inventory item can be assigned through an existing quick-slot button, but the shortcut service remains the
only binding authority. Dialogue and every other management modal remain separate M12C routes and hide/replace the
gameplay presentation through the existing modal controller.

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
world visible around them. The health/fatigue apertures displayed centered three-digit source digits; all five
maintained-spell apertures displayed source open/plugged art with no holes. The production Inventory button opened the
source 800 x 400 Inventory composition without hiding the HUD, and a source item was selected, bound to a quick slot,
equipped, and unequipped through existing authority. After leaving attack mode, the world area accepted the gameplay
pointer and displayed the correctly registered source cursor at the requested world point. One HUD presenter, one
Inventory presenter, one controller presenter, and one EventSystem remained.

Local captures and the reference index stayed under ignored `Temp/UIC2Validation` and were not committed. Direct
visual comparison used unmodified 800 x 600 retail screenshots indexed by MobyGames, including the original 2001
Windows gallery and the later 1.0.7.4 full-screen-mode reference. The comparison was structural rather than a
pixel-diff because scenes differed: top/bottom silhouettes, management buttons, quick slots, vials, message lens,
and bottom control registration agreed; the prior oversized ID 3-derived composition was absent. Old-Games and ModDB
captures were retained only as uncertain/modded corroboration, never as authority.

No external screenshot or original retail ART was added to Git.

## Validation record

- Combined UI-C/UI-E focused EditMode: **63/63**.
- M3C equipment: **13/13**; M3E inventory capacity: **21/21**.
- M12C controller/presenter: **18/18**; UI-A source runtime: **23/23**; UI-B Main Menu: **22/22**.
- M8I combat presentation: **17/17**; Player Navigation/Input: **21/21**; Production Keyboard: **23/23**;
  Player Startup: **3/3**.
- Production UI-C.3/UI-E.1 physical validator: **PASS**, including source counter/slot fidelity, ordinary Inventory
  coexistence, authentic starting-item presentation, select/bind/equip/unequip, graphics rebuild, and all target
  resolutions.
- Complete EditMode: **1199/1199**, 0 failed, 0 skipped, 0 inconclusive.
- Unity compilation: clean.
- Final cleared Console: 0 errors, 0 unexpected warnings.
- `git diff --check`: clean.

## Boundary

UI-C.3 changes only source gameplay-HUD aperture presentation, ordinary-Inventory coexistence, existing quick-slot
routing, audit data, and tests. It does not begin UI-D or change gameplay, combat, navigation, dialogue, Save V1, or
Enhanced-art authority. Character Creation remains a separately authorized presentation milestone.

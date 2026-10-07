# UI-C — Source-Faithful Gameplay HUD Reconstruction

## Status

UI-C.4 completed its bounded, source-proven implementation and validation on 2026-10-06. The valid UI-C.1 cursor,
UI-C.2 two-window composition, and UI-C.3 counter/maintained-slot/Inventory corrections remain intact. UI-C overall
is deliberately **OPEN**: the eleven deferred Fate effects have no exact resolution call sites in the current runtime,
and town waitability plus occupied-bed authority are not represented. Those branches fail before point, time, vitality,
spell, or presentation mutation instead of inventing behavior. UI-D has not started.

The supported source HUD now includes saved primary notifications and highlighted ART, the Fate counter and exact Full
Heal action, PC-maintained spell icons/cancellation, bounded wilderness sleep, the source clock, contextual counter,
two recent-action slots, and source XP gauge. `RetailGameplayHudView` remains a disposable projection; session state,
SourceTime, vitality, inventory, magic, progression, combat, and Save V1 remain the authority. No retail pixels or
production Enhanced assets were committed.

## Corrected source evidence

The correction was audited against retail `art/interface/interface.mes`, mounted retail ART, and the complete local
`arcanum-ce/src` tree. UI-C.4 additionally traced `intgame.c`, `hotkey_ui.c`, `spell_ui.c`, `sleep_ui.c`, `fate_ui.c`,
`fate.c`, `anim_ui.c`, `gameuilib.c`, and `hrp.c`, plus shipped interface/message data. The decisive window path is
`src/ui/intgame.c`:

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
- Primary normal IDs are Character 169, Logbook 187, Town Map 193, World Map 194, and Inventory 186. Persistent
  notification variants are 561, 560, 558, 195, and 559; opening the corresponding screen clears its saved flag.
- Fate uses panel ID 292, choice button ID 293, top button ID 137, and a two-digit counter at `(190,17,24,12)`.
  Full Heal immediately restores hit points and fatigue and removes poison in the source. OpenArcanum currently has no
  poison authority, so the representable HP/fatigue transaction is exact and the other eleven choices fail closed.
- Sleep uses ID 565 and source choices 1/2/4/8/24 hours, until 07:00, until 20:00, and until healed. Each hour advances
  SourceTime; wilderness sleep heals HP by Heal Rate and fatigue by three times Heal Rate, a bed doubles recovery,
  wait-only towns do not heal, and PC-maintained spells end before the interval. Only map 1 wilderness permission is
  currently authoritative; unsupported town and bed branches fail closed.
- Maintained slots are `(281 + 50n,3)` with open IDs 188–192 and plugged IDs 628–632. Active icons use each spell's
  shipped `spell_icon`; the supported catalog resolves Strength of Earth 93, Harm 105, Minor Healing 111, and Flash 82.
- The contextual counter is `(104,512,50,20)`, six digits, with gold ID 474, ammunition IDs 250–253, and mana-store ID
  469. Recent actions are `(69,548)` and `(114,548)` and initialize to skill icons 280 and 279. The XP gauge uses full
  segments 773–782 and partial strip 772. The clock uses time strip 207, moons 208–215, and pointer 216; although the
  source requests a 100 x 100 copy region for the pointer, the decoded 5 x 29 ART is blitted at native size without
  scaling, which is the production geometry OpenArcanum now preserves.

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
- IDs 561/560/558/195/559 replace those primary controls only while their authoritative notification is pending.
- Fate projects a saved two-digit point count. Full Heal consumes exactly one point after validation; unsupported
  deferred effects reject without mutation.
- The five maintained apertures project only PC-cast maintained M10A effects in stable identity order. Clicking an
  active icon cancels through M10A rather than mutating the presentation.
- Sleep/Wait is an authoritative transaction over SourceTime, M4 vitality, party membership, and M10A effects. The
  current bounded path supports source wilderness recovery on map 1 and rejects unsupported location authority.
- The source clock derives exclusively from `SourceTimeService`; no Unity wall clock participates.
- The bottom contextual counter selects Gold, supported ammunition, or mana-store facts from authoritative inventory.
- Two recent actions preserve source recency/deduplication semantics and activate through existing item/spell routes.
- XP segments are a read-only projection of M4C level progress; invalid or capped progress draws no invented state.
- IDs 470/472/473/471 provide Combat, Skills, Spells, and Schematics in the bottom window.

Health, fatigue, equipment, ammunition, AP, readiness, combat state, messages, and shortcuts remain projections of
the completed M3/M4/M8/M12C systems. C/L/W/I and pointer controls share the controller routes; F and S open the Fate
and Sleep panels, and A activates the first recent-action slot. Unsupported Fate resolution, town/bed sleep permission,
broader ammunition/mana-store content, slot drag/cooldown, and rare-audio branches remain fail-closed or deferred.

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

The existing Unity Editor and production `Assets/_Game/Scenes/OpenArcanum.unity` were used; no second Editor was
launched. A fresh source-valid New Game reached START_MAP 1 at the crash site. The run proved the initial starting-item
Inventory notification, normal/highlight ART and pointer plus C/L/W/I routes, exact notification clearing, maintained
Strength of Earth icon/cancellation, one-hour wilderness sleep with SourceTime/recovery/demaintain, Fate 00 -> 01 ->
Full Heal -> 00, all four clock periods, six-digit Gold context with ID 474, recent spell icon 93, XP projection,
turn-based combat authority, and Original -> Enhanced -> Original presentation rebuild. One HUD, Inventory presenter,
controller presenter, gameplay cursor, and EventSystem remained.

Geometry passed at 800 x 600, 1024 x 768, 1920 x 1080, 2560 x 1440, and 3840 x 2160. At every target the native strips
remained centered, unclipped, and unscaled, with expanded world around them. The production Inventory composition
coexisted with the HUD, and authoritative state survived its route and graphics rebuilds.

Local captures and the reference index stayed under ignored `Temp/UIC2Validation` and were not committed. Direct
visual comparison used unmodified 800 x 600 retail screenshots indexed by MobyGames, including the original 2001
Windows gallery and the later 1.0.7.4 full-screen-mode reference. The comparison was structural rather than a
pixel-diff because scenes differed: top/bottom silhouettes, management buttons, quick slots, vials, message lens,
and bottom control registration agreed; the prior oversized ID 3-derived composition was absent. Old-Games and ModDB
captures were retained only as uncertain/modded corroboration, never as authority.

No external screenshot or original retail ART was added to Git.

## Validation record

- UI-C.4 focused EditMode: **53/53**, 0 failed, 0 skipped, 0 inconclusive.
- Required affected regressions: **433/433**, covering UI-A/UI-E, M12B/M12C, M13A keyboard, startup/navigation,
  M3B–M3E, M4A–M4D, M6A–M6C, M7D/M7E, M8I, and M10A.
- Production UI-C.4 physical validator: **PASS** with the exact state/functionality and target-resolution proofs above.
- Complete EditMode: **1207/1207**, 0 failed, 0 skipped, 0 inconclusive (1199 plus eight justified UI-C.4 cases).
- Unity compilation: clean.
- Final cleared Console: 0 errors and 0 unexpected warnings. `git diff --check`: clean.

## Boundary

UI-C.4 changes only coordinator-owned HUD state, source-proven presentation, shared controller/input routes, optional
fields inside the compatible Save V1 envelope, and tests/audit data. It does not begin UI-D, add poison, invent Fate
resolvers, invent town/bed authority, or alter combat/navigation/dialogue/Enhanced-art ownership. UI-C remains open
until the eleven deferred Fate resolution call sites and source-backed town waitability/bed occupancy are supported.

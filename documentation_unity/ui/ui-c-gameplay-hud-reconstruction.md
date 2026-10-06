# UI-C — Source-Faithful Gameplay HUD Reconstruction

## Status

UI-C implementation, automated validation, and the UI-C.1 physical-fidelity closure completed on 2026-10-05. The
production source HUD was physically reviewed at 800×600, 1920×1080, 2560×1440, 3840×2160, 1920×1200,
3440×1440, tall/narrow, and live Free Aspect sizes without clipping, non-uniform stretch, or source-element drift.
The normal production gameplay view
uses one source-backed retail HUD over the existing `GameUiController`, `CombatUiController`, quick-slot service, and
session coordinator. The previous generic IMGUI button matrix is suppressed whenever the retail view is available.
No gameplay, inventory, combat, vitality, equipment, shortcut, screen, or modal authority moved into presentation.

No retail pixels or production Enhanced assets were committed. Original mode resolves retail ART from `GameData`
through the UI-A resolver. The only Enhanced pixels used for validation were generated in the system temporary
directory and deleted after the proof.

## Source evidence and composition

The implementation was audited against retail `art/interface/interface.mes`, the decoded reference under
`D:/OpenArcanum/UIReference/Original`, `arcanum-ce/src/ui/intgame.c`, and
`arcanum-ce/src/ui/hotkey_ui.c`. Runtime does not read `UIReference`.

The fixed logical composition is 800 x 600:

| Region | Source rectangle | Production behavior |
|---|---:|---|
| Top interface | `(0,0,800,41)` | Normal-gameplay portion of ID 3; four source management controls are interactive. |
| World / management | `(0,41,800,400)` | Transparent aperture in ID 3; the gameplay camera renders behind it. |
| Bottom interface | `(0,441,800,159)` | Integrated source composition, not a reconstructed generic panel. |

Retail ID 3 `intrface.art` supplies the full source composition and has real alpha in the world aperture. The camera
therefore remains full-screen behind it; opaque source pixels conceal the world and transparent pixels reveal it.
This matches the source image more faithfully than cropping the camera to a rectangular middle strip and also lets
the widescreen sides show additional world without horizontally stretching the HUD.

The source-backed elements used by the production view are:

- ID 3 `intrface`: complete 800 x 600 HUD composition.
- IDs 20/18/19 `clrvial`/`redvial`/`bluvial`: empty glass, health liquid, and fatigue liquid.
- ID 27 `morph15font`: dynamic counters, slot marks, and message text.
- ID 354 `dialoguewindow`: the source message/lens frame at `(196,492,410,107)`.
- ID 251 `ammo_icon_bullets`: the currently supported bounded ammunition icon at `(61,509,37,24)`.
- IDs 169/187/193/186: Character, Logbook, Map, and Inventory top controls.
- IDs 470/472/473/471: Combat, Skills, Spells, and Schematics bottom controls.
- IDs 173–181 followed by ID 172: the ten source quick-slot backgrounds.
- the shared UI-A source cursor and source button frame-state machinery.

ID 184 `intbotom` and ID 185 `inttop` corroborate the split source regions, but production uses the proven full
ID 3 alpha composition so the visible seams, decorative registration, and aperture remain exact.

## Exact dynamic regions

Health liquid is clipped to `(14,472,28,88)` and fatigue liquid to `(754,473,28,88)`. The source blitter retains an
eight-pixel overlap beneath the vial neck for any non-zero value; zero remains empty. The resulting visible height is
`8 + floor(80 * current / maximum)`, bounded to 0–88. Empty/full glass remains ID 20 at `(11,471,34,90)` and
`(751,472,34,90)`. Numeric counters occupy `(15,578,29,12)` and `(755,578,27,12)`. All values are read from the M4
projection; the view owns no vitality state and never issues a vitality mutation.

The message/lens text clips to `(211,503,383,82)`. Its first line projects existing interaction/controller feedback;
its second line projects the existing weapon/action state outside combat and the M8I combat mode, AP, and readiness
inside combat. The lens is therefore the normal player-facing surface for existing diagnostics without retaining the
placeholder debug grid.

The current bounded ammunition projection uses ID 251 and an adjacent dynamic counter. It is hidden when the
authoritative quantity is zero. Equipment, attack mode, ammunition, AP, combat mode, and readiness remain projections
from M3/M8/M8I; UI-C does not classify attacks or select equipment.

Ten source slots are placed at y=445 with x coordinates
`198,237,276,315,354,418,456,495,534,573`. Their source IDs are
`173,174,175,176,177,178,179,180,181,172`. Item quantities and spell/item bindings project from the existing
session-owned shortcut service. Clicking a slot and number-key activation both call the same existing controller
command. No second shortcut inventory, cooldown model, or drag/drop authority was added.

## Source controls and removed placeholder controls

The source controls and routes are:

| Control | Source ID | Position | Existing route |
|---|---:|---:|---|
| Character | 169 | `(4,2)` | Character |
| Logbook | 187 | `(41,2)` | Journal |
| Map | 193 | `(78,2)` | Map |
| Inventory | 186 | `(115,2)` | Inventory |
| Combat | 470 | `(86,457)` | existing combat toggle |
| Skills | 472 | `(693,456)` | Skills |
| Spells | 473 | `(649,494)` | Magic |
| Schematics | 471 | `(693,539)` | Crafting |

The retail Schematics control is the authentic route to the completed crafting system. The placeholder Party,
Barter, Save/Load, Technology, and duplicate Craft buttons were not reproduced. Their existing screens and keyboard
routes remain intact; UI-C removes only the non-source convenience surface. Fate and Sleep source regions were not
made interactive because their gameplay authorities are not implemented. No unsupported source branch was invented.

Each source button uses its ART normal/hover/pressed frame family through `SourceUiButton`. No UI-C-specific audio
mapping was proven, so UI-C relies on the existing shared M12D/UI-A behavior and does not synthesize a new sound.

## Input, cursor, and lifecycle

The persistent EventSystem is parented outside the transient Main Menu canvas so the same one EventSystem remains
active after New Game. Only actual top and bottom source interface bands block pointer-to-world input. Transparent
world aperture and widescreen side regions do not steal click-to-move or alpha-tested world interaction. Pointer
coordinates continue through the shared centered 800 x 600 mapping.

`ProductionGameUiPresenter` owns exactly one `RetailGameplayHudView`. It synchronizes the source view from the existing
controller and suppresses only the generic gameplay HUD drawing while the source view is available. Rebinding after a
test fixture or graphics rebuild replaces/disposes presentation resources safely and restores the production-owned
resolver; it does not duplicate controllers, presenters, subscriptions, or EventSystems.

The source cursor remains presentation-only. Production attack/talk selection and keyboard input remain owned by the
existing input stack.

## Modern resolutions and skins

At 800 x 600 the source composition maps exactly to the screen. At 1920 x 1080, 2560 x 1440, and 3840 x 2160 the
shared uniform scales are 1.8, 2.4, and 3.6, producing centered 1440 x 1080, 1920 x 1440, and 2880 x 2160 fixed-art
surfaces. Extra horizontal area exposes the full-screen world behind the source alpha; the 800-wide ornament is never
stretched. Controls, vials, slots, hit rectangles, and logical text regions remain unchanged.

Original mode resolves every production HUD pixel from the mounted retail data. A generated 3200 x 2400 ID 3 fixture
proved the exact-4x Enhanced path while retaining the same 800 x 600 logical rectangle and controller state. Missing
Enhanced button/slot frames fell back independently to Original. Original -> Enhanced -> Original preserved
geometry, hit regions, session state, shortcut binding, combat authority, and the single presenter/EventSystem.

## Validation record

Focused UI-C EditMode validation passed **33/33**. It covers source resolution, the three source regions, vial clipping
at zero/partial/nearly-full/full values, all ten slot IDs/positions, authoritative HP/fatigue/equipment/ammunition/AP
and slot projections, source screen/control routing, absence of non-source convenience controls, input bands and world
aperture, 800 x 600/1080p/1440p/4K mapping, full camera backing, exact-4x selection, per-frame fallback, skin rebuild,
fixture-to-production resolver rebinding, and unique view/controller lifecycle.

Targeted regressions were limited to production code touched by UI-C:

- UI-A source runtime: **23/23**.
- M12C controller/presenter: **18/18**.
- M8I combat UI: **17/17**.
- Player navigation/input: **21/21**.
- production keyboard input: **23/23**.
- UI-B Main Menu/EventSystem lifecycle: **22/22**.

The complete EditMode suite passed **1169/1169**, with 0 failed, 0 skipped, and 0 inconclusive. Unity compilation was
clean. Final Console was cleared and rechecked at 0 logs, 0 warnings, and 0 errors. `git diff --check` was clean.

The recorded physical Play Mode pass used the existing Unity Editor and `Assets/_Game/Scenes/OpenArcanum.unity`. A
fresh source-valid New Game reached START_MAP 1 at the crash site. It proved the recognizable retail ID 3 HUD, absence of the generic
button grid, authoritative health/fatigue/equipment/message state, the transparent world aperture, one presenter,
one controller presenter, and one active EventSystem. The real Inventory source button opened the existing Inventory
screen and the real `I` shortcut returned to the same HUD. A real world click moved the PC across a sector boundary
without the HUD stealing input. A learned Strength of Earth binding appeared in slot 1 and the real `1` key activated
it through existing magic/fatigue authority. An authentic loaded crash-site NPC drove the existing M8 turn-based
combat route and the source lens reflected live combat mode/AP/readiness; M8I and keyboard regressions cover End Turn
and real-time command routing without adding a non-source control. Original -> generated exact-4x Enhanced -> Original
and 800 x 600/1080p/1440p/4K validator checks all passed. Those programmatic mappings and the earlier run do not close
the newly reopened visual-fidelity gate: the current rendered HUD still requires direct human review at every target
resolution for clipping and alignment. The validator itself emitted 0 warnings and 0 errors.

No pickup was required for the HUD proof because UI-C does not own inventory mutation and the relevant equipment,
ammunition, slot, and Inventory routes were proved directly. Campaign progression did not continue beyond the bounded
crash-site/adjacent-sector interaction proof.

## Explicit ambiguities and deferrals

- Retail source behavior for every ammunition icon family is broader than the currently authoritative bounded combat
  projection; UI-C presents the proven supported quantity and does not infer unsupported weapon/ammo categories.
- Fate, Sleep, follower/party portrait regions, selected-actor indicators, clock/moon strips, and broader status icons
  require their own source/gameplay authority audit before becoming interactive.
- Quick-slot drag/drop artwork and unavailable/cooldown states were not proven by current authority and remain absent;
  assignment and activation remain available through completed controller routes.
- Exact rare HUD sounds remain a minor M12D presentation follow-up where no mapping is proven.
- Character Creation, Inventory/Paper Doll, Character management, Magic/Technology screens, Journal, maps, merchant,
  Save/Load, and other modal reconstructions remain outside UI-C.

The exact recommended UI-D boundary is one source-faithful **Character Creation presentation** over the completed
M12B/M12C authority. It must not redesign creation rules or absorb Inventory, Character management, or another screen.

## UI-C.1 — Physical Fidelity / Aspect-Safe Closure

The direct rendered review confirmed that the existing UI-A root contract was already correct: the fixed source
surface uses `scale = min(displayWidth / 800, displayHeight / 600)`, stays centered, and never stretches on one axis.
At 4:3 it fills the display. At wider 16:9, 16:10, and ultrawide targets it remains centered while the full-screen
world camera is visible laterally through areas not occupied by opaque source art. At tall/narrow or very small Free
Aspect sizes it scales down uniformly and letterboxes rather than clipping primary controls. The authentic logical
world aperture remains `(0,41,800,400)` within ID 3 while the camera itself continues to render the complete physical
target. Live Game-view resizing rebuilt the mapping without child drift, duplicate canvases, duplicate HUD
presenters, duplicate controller presenters, or duplicate EventSystems.

The proven defect was narrower than the reopened concern: `Retail Gameplay Cursor` was parented to the full unscaled
canvas and bound to that canvas for pointer conversion, while ID 3 and every other source component lived below the
centered, uniformly scaled 800×600 reference surface. At non-800×600 sizes the cursor could therefore drift from the
source HUD and interaction point. The cursor is now parented to the shared cursor layer and bound to the same
`SourceReference800x600` transform as the HUD. No root scaling, camera, controller, raycast, or gameplay authority was
changed. Earlier tests covered root/HUD rectangles but did not assert the gameplay cursor's parent, reference surface,
or physical scale; the focused suite now protects those relationships and round-trips arbitrary physical/logical
coordinates inside and outside the fixed composition.

Local-only PNG captures were taken from the existing Editor after waiting for the Game view and `LateUpdate` mapping
to settle. They covered 800×600, 1920×1080, 2560×1440, 3840×2160, 1920×1200, and 3440×1440; retail pixels stayed
under ignored `Temp/UICValidation` and were not committed. Direct inspection found the complete top and bottom bands,
all ten slots, assigned slot 1, both vials, counters, controls, message lens/text, and cursor source-registered at every
target. A second 800×600 capture set exercised full, half, 1-point, and zero health/fatigue through M4B authority and
confirmed upward fill, the bounded neck overlap, glass clipping, no bleed, and identical registration. An initially
bad 800×600 capture was traced to taking the screenshot before the selected Game-view size had propagated; after
three settled frames its runtime and mapping both reported 800×600 at scale 1 and the rendered composition matched
the golden geometry.

The production Play Mode proof again created a source-valid PC, entered START_MAP 1 at the crash site, opened and
closed Inventory through the authentic source control, assigned and activated Strength of Earth through slot 1,
entered and ended combat against an authentic loaded NPC, and switched Original -> generated exact-4× Enhanced ->
Original. Presentation rebuilds preserved the controller/session, authoritative vitals, equipment, shortcut, and
combat state. The full camera backing, transparent aperture, widened world, pointer conversion, source-band blocking,
and existing world interaction routes remained intact; Player Navigation/Input regression coverage protects
click-to-move and world-object/corpse/NPC/portal dispatch, while the new arbitrary-aspect round trips protect left and
right extended-world coordinate conversion. Missing Enhanced frames continued to fall back independently to
Original with identical logical geometry and hit regions.

Final validation results were:

- UI-C focused: **42/42**.
- UI-A: **23/23**; M12C: **18/18**; Player Navigation/Input: **21/21**; Production Keyboard: **23/23**; UI-B:
  **22/22**.
- M8I was not rerun because combat-HUD routing and authority did not change.
- Complete EditMode: **1178/1178**, with 0 failed, 0 skipped, and 0 inconclusive.
- Unity compilation: clean. Final cleared Console: 0 warnings and 0 errors. `git diff --check`: clean.

No remaining UI-C clipping, registration, scale, cursor, or aspect defect was observed. Broader ammunition families,
Fate/Sleep, party/status regions, cooldown/drag art, and rare HUD audio remain the already documented source/authority
ambiguities; they are not UI-C.1 defects. UI-C is therefore complete. UI-D remains unstarted.

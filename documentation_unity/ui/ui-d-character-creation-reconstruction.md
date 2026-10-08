# UI-D Character Creation Reconstruction

Status: UI-D.1 player-facing correction complete (2026-10-07)

UI-D replaces the production Character Creation placeholder and its whole-screen `ScrollRect` with the two
fixed, source-backed compositions used by retail Arcanum. It changes presentation only: M12B remains the sole
owner of the draft, legality, point costs, validation, and atomic New Game finalization.

## Source runtime path

The reconstruction follows runtime creation calls rather than classifying ART by filename or dimensions.

| Stage | Source runtime evidence | Source composition | OpenArcanum route |
|---|---|---|---|
| Identity | `mainmenu_ui_new_char_create`, `mainmenu_ui_new_char_refresh`, and `mmUISharedCharRefreshFunc` in `arcanum-ce/src/ui/mainmenu_ui.c` | Interface ID 765, `createcharacterbase.art`, 800 x 400 | `GameUiController.BeginNewGame()` opens one `RetailCharacterCreationView` in the Identity stage |
| Editor | `mainmenu_ui_new_char_execute` enters `charedit_open(..., CHAREDIT_MODE_CREATE)`; `charedit_open` in `arcanum-ce/src/ui/charedit_ui.c` creates the editor and its child windows | Interface ID 22, `char_maint.art`, 800 x 400, plus the active ID 29/30/31 subpanel | Identity Continue advances the same view to the Editor stage without creating or finalizing a PC |
| Finalization | `mainmenu_ui_charedit_buttons[0]` is bottom-bar Continue ID 321; its release calls `sub_5480C0(2)` | Existing M12B transaction | Editor Continue submits `FinalizeNewGame`; success creates exactly one PC and enters source `START_MAP` 1 |

The identity screen and character editor are therefore not one large form and not one ART-derived screen. They
are two sequential source compositions with different ownership and navigation. The production presenter suppresses
the old generic Character Creation drawing only while this source view is available.

## Identity stage

ID 765 owns the fixed 800 x 400 background in `mainmenu_ui_window_partial_rect` at logical `(0,41)`. Source-proven
dynamic regions and controls are:

| Element | Logical source geometry / center | Evidence and behavior |
|---|---:|---|
| Portrait | origin `(95,28)`, draw size `128 x 128` | `mainmenu_ui_shared_char_portrait_rect` is the 64 x 64 invalidation/fill rectangle, but `mmUISharedCharRefreshFunc` then calls `portrait_draw_128x128` at that origin. Production preserves the 128 x 128 draw instead of fitting it into the invalidation rectangle; it resolves `portrait/<name>_b.bmp` with the retail ordinary-BMP fallback. |
| Name | `(46,183,228,18)` | Source text-edit buffer; the Unity `InputField` consumes text focus so gameplay shortcuts do not receive name entry |
| Gender | `(66,243,186,19)` | Source text and ID 757/758 controls at screen top-left `(42,281)` and `(255,282)` |
| Race | `(66,293,186,19)` | Source text and ID 757/758 controls at screen top-left `(42,331)` and `(255,332)` |
| Background | `(66,343,186,19)` | Data-driven M12B identity and ID 757/758 controls at screen top-left `(42,381)` and `(255,382)` |
| Portrait arrows | screen top-left `(43,118)` / `(244,118)` | IDs 283/284; only portraits legal for the current M12B race/gender may be selected |
| Back / Continue | screen top-left `(69,496)` / `(675,496)` | IDs 323/321 are hosted by the separate bottom-bar cover windows (`source y 55 + 441`); Back returns to the New Character choice and Continue validates identity before opening the editor |
| Statistics title | `(339,23,409,19)` | Read-only source-style projection of the draft |
| Description | `(350,155,395,212)` | Contextual race, gender, background, and validation text |

All eight supported M12B races remain available: Human, Dwarf, Elf, Half-Elf, Gnome, Halfling, Half-Orc, and
Half-Ogre. Source allows Female for Human, Elf, Half-Elf, and Half-Orc; Dwarf, Gnome, Halfling, and Half-Ogre are
male-only. Female race cycling skips unsupported races, selecting a male-only race restores Male, and every
race/gender change clears and re-evaluates Background before selecting the first legal portrait, matching the
original set-gender/set-race path. These rules remain M12B authority rather than a presenter-only legality table.
Malformed retail Background 2 remains rejected by the existing data parser; UI-D does not invent replacement data.

## Character editor stage

`charedit_open` creates interface ID 22 in the shared 800 x 400 big window at logical y 41, then creates its
attribute buttons, category controls, auto-level-scheme control, and child windows. Character-creation navigation
remains owned by the separate Main Menu bottom bar. UI-D retains that hierarchy.

- Attribute increments use ID 24 and decrements use ID 23 at the source coordinates for Strength, Dexterity,
  Constitution, Beauty, Intelligence, Perception, Willpower, and Charisma.
- Skills, Technology, and Magic category controls use IDs 34, 35, and 36 at local `(527,11)`, `(595,11)`, and
  `(663,11)`.
- ID 568 at local `(754,21)` selects the auto-level-scheme child window in source. OpenArcanum has no M12B
  auto-level-scheme authority, so this exact route reports unavailable and fails closed before draft, Character
  Point, player, or navigation mutation.
- Editor Back/Continue use Main Menu IDs 323/321 at absolute source top-left `(69,496)` / `(675,496)`; Continue is
  the finalization route. There is no invented transparent editor Back region.
- The active child panel begins at local `(503,4)` and is replaced, rather than accumulated or globally scrolled.
- Skills use ID 29, group controls 302–305, and row controls 624/625. Rank changes submit only
  `AdjustCreationSkill` intent. ID 623 `skilgauge.art` is restored from the panel at zero and filled left-to-right at
  `clamp(source skill level, 0, 20) / 20`; its 120 x 27 rectangles use source y positions 87/153/219/285.
- Technology uses ID 30, discipline controls 306–313, and rank controls 647/648. Rank changes submit only
  `AdjustCreationTechnology` intent.
- Magic uses ID 31, the source college icon mapping
  `11,44,38,92,50,86,56,98,62,68,74,104,110,80,116,122`, and rank controls 647/648. Rank changes submit only
  `AdjustCreationSpell` intent.
- Source bitmap-font roles 26, 27, and 28 provide the small labels, values, and descriptions.

Disabled state is prospective M12B validation over a cloned specification. The view never duplicates costs,
prerequisites, attribute limits, rank limits, or remaining-point calculations. Character Points and all values are
projections of `GameUiController.CreationDraft` and `CreationValidation`.

Dynamic text is redrawn from immutable source rectangles. The reported selector drift came from reusing an already
centered `RectTransform.anchoredPosition` as the next centering origin, so every refresh accumulated the previous
offset. The corrected path keeps the authored rectangle separately, measures bitmap glyphs, and derives each new x
coordinate from that unchanged rectangle. The same measured-centering path covers one- and two-digit attribute
values, Character Points, identity labels, and skill values without moving plus/minus geometry.

## Navigation and authority

There is one production `RetailCharacterCreationView`, one `ProductionGameUiPresenter`, one controller/session, and
one `EventSystem`. The fixed source composition captures pointer input, but it does not create gameplay authority.

- Escape/Back from Editor returns to Identity and preserves the authoritative draft.
- Escape/Back from Identity returns through the existing Main Menu route.
- Identity Continue validates before changing stage and cannot create a partial PC.
- Editor Continue calls the existing M12B finalization transaction. Invalid creation remains open with no player
  mutation; valid creation creates exactly one PC, enters the authentic start map, closes creation, and reveals the
  existing source HUD.
- Pick Character opens source ID 766 and browses the twelve retail entries in source order. Their basic PC
  prototypes 16067–16078 are engine-owned `proto.c` entries rather than required loose `data/proto/*.pro` files.
  OpenArcanum reconstructs only those source-defined basic prototypes, then applies the authentic prototype identity,
  auto-level scheme, background, portrait, inventory source, selection global, and natural auto-equip path. It does
  not translate them into custom-character presets.
- Pregenerated Start creates exactly one source-prototype PC through the existing atomic New Game/campaign path.
  Save V1 retains optional `PregeneratedSourceIndex` metadata while committed character, inventory/equipment,
  campaign, world, and HUD authority remain in their existing V1 sections. Older V1 documents omit the field as zero.
- Intro playback and retail post-creation shopping remain deferred.

## Resolution and graphics behavior

Both stages remain fixed 800 x 400 logical compositions at source `(0,41)` within the centered 800 x 600 source
surface, with navigation in the source bottom bar. They do not
stretch or become a whole-screen scrolling form at larger resolutions.

| Display | Fixed composition result |
|---:|---|
| 800 x 600 | fully visible at source scale |
| 1024 x 768 | centered, bottom-bar finalization control accessible |
| 1920 x 1080 | centered, no clipping or global horizontal scrolling |
| 2560 x 1440 | centered, source geometry unchanged |
| 3840 x 2160 | centered, source geometry unchanged |

Original mode resolves mounted retail ART and portrait BMPs. Physical validation generated temporary local exact-4x
replacements for IDs 765 and 22, switched Original -> Enhanced -> Original, and proved that stage, active Technology
panel, draft values, Character Points, logical geometry, presenter identity, and M12B authority were unchanged.
Missing Enhanced children continue to fall back independently to Original. No retail or generated validation asset
is committed.

## UI-D.1 / UI-B.1 correction validation record

- Unity compilation: clean in the existing Unity 6000.0.71f1 Editor; no second Editor was launched.
- Focused gates: UI-D.1 **28/28**, UI-B.1 **22/22**, UI-C.5 **69/69**, UI-E.2 **20/20**, M12B **15/15**, and
  production keyboard/input **23/23**; 0 failed, skipped, or inconclusive.
- Targeted regressions: **418/418** across UI-A/B/C/D/E.1/E.2, M3A, M3C, M4A–M4D, M6A, M8F, M10, M12B,
  M12C, production keyboard/input, and Player Startup.
- Complete EditMode: **1280/1280**, 0 failed, 0 skipped, 0 inconclusive (1269 baseline plus 11 justified cases).
- Production Play Mode: **PASS**. The existing Editor proof exercised the 128 x 128 portrait draw; all source gender
  restrictions; 100 selector cycles without drift; one- and two-digit centering; ID 623 zero/partial/full/refund
  behavior; all twelve ID 766 pregenerated entries; two distinct prototype-backed campaign starts; authentic
  inventory/equipment; Save V1 round trip; HUD authority; Original -> Enhanced -> Original; and exact 800 x 600,
  1024 x 768, 1920 x 1080, 2560 x 1440, and 3840 x 2160 placement.
- Production uniqueness: one Character Creation view, presenter/controller, and EventSystem.
- Final cleared Unity Console: 0 logs, 0 warnings, 0 errors. `git diff --check`: clean.

## Boundary

UI-D owns source-faithful presentation, fixed composition, portrait pixel resolution, and intent forwarding only.
M12B continues to own character legality and finalization; M4, M10A, and M10B continue to own the projected game
facts. Runtime Character/Skills presentation belongs to UI-E.2. Pregenerated browsing and prototype-backed New Game
are now closed. Intro playback, shopping, auto-level-scheme authority, new character rules, and any gameplay redesign
remain outside UI-D.

## Player-Facing Correctness Pass II

The final player-facing correction preserves the two-stage UI-D architecture and closes the defects found during
physical review. Identity Back/Continue remain source IDs 323/321 at absolute `(69,496)` / `(675,496)`. The portrait
is now created before the overlapping arrow widgets, matching the source paint order, while the arrows retain their
source positions. Race or gender changes select the first legal portrait in ascending retail ID order and clear the
old background before re-evaluating legality.

Direct inspection of mounted retail `portrait/gameport.mes` corrected a reversed parser assumption: `HA` is
Halfling and `HE` is Half-Elf. The retail entries include `HAM1` for a male Halfling and `HEM1`/`HEF1` for male and
female Half-Elves. A fresh Female Elf -> Half-Elf change now immediately selects a Female Half-Elf portrait and
continues into the editor without mismatch feedback.

The editor now projects the selected background's effective attributes and skills without stacking that effect into
the draft. Source IDs 23/24 at the audited HP and Fatigue positions buy or refund the corresponding M4B vitality
points transactionally: each increase spends one Character Point, and only a purchase made during the current editor
session may be refunded. Attribute, skill, HP, Fatigue, and category controls project the corresponding retail
`mes/charedit.mes` help through the lower source information panel; hover presentation never mutates the draft.

Production Play Mode passed at 800 x 600, 1024 x 768, 1920 x 1080, 2560 x 1440, and 3840 x 2160 with the fixed
source composition centered and unclipped. The physical proof covered Female Half-Elf portrait correction,
background-adjusted projection, HP and Fatigue spend/refund with Character Point changes, attribute hover help, and
Back/Continue geometry. The final complete EditMode suite passed **1301/1301**, with 0 failed, 0 skipped, and 0
inconclusive. Compilation is clean; the final cleared Console has 0 logs, 0 warnings, and 0 errors; and
`git diff --check` is clean.

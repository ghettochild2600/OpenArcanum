# UI-D Character Creation Reconstruction

Status: complete (2026-10-07)

UI-D replaces the production Character Creation placeholder and its whole-screen `ScrollRect` with the two
fixed, source-backed compositions used by retail Arcanum. It changes presentation only: M12B remains the sole
owner of the draft, legality, point costs, validation, and atomic New Game finalization.

## Source runtime path

The reconstruction follows runtime creation calls rather than classifying ART by filename or dimensions.

| Stage | Source runtime evidence | Source composition | OpenArcanum route |
|---|---|---|---|
| Identity | `mainmenu_ui_new_char_create`, `mainmenu_ui_new_char_refresh`, and `mmUISharedCharRefreshFunc` in `arcanum-ce/src/ui/mainmenu_ui.c` | Interface ID 765, `createcharacterbase.art`, 800 x 400 | `GameUiController.BeginNewGame()` opens one `RetailCharacterCreationView` in the Identity stage |
| Editor | `mainmenu_ui_new_char_execute` enters `charedit_open(..., CHAREDIT_MODE_CREATE)`; `charedit_open` in `arcanum-ce/src/ui/charedit_ui.c` creates the editor and its child windows | Interface ID 22, `char_maint.art`, 800 x 400, plus the active ID 29/30/31 subpanel | Identity Continue advances the same view to the Editor stage without creating or finalizing a PC |
| Finalization | The source Accept route closes creation only after character-edit validation | Existing M12B transaction | ID 568 submits `FinalizeNewGame`; success creates exactly one PC and enters source `START_MAP` 1 |

The identity screen and character editor are therefore not one large form and not one ART-derived screen. They
are two sequential source compositions with different ownership and navigation. The production presenter suppresses
the old generic Character Creation drawing only while this source view is available.

## Identity stage

ID 765 owns the fixed 800 x 400 background. Source-proven dynamic regions and controls are:

| Element | Logical source geometry / center | Evidence and behavior |
|---|---:|---|
| Portrait | `(95,28,64,64)` | `mmUISharedCharRefreshFunc` calls `portrait_draw_128x128` into the source portrait region; the presentation resolves `portrait/<name>_b.bmp` with the retail ordinary-BMP fallback |
| Name | `(46,183,228,18)` | Source text-edit buffer; the Unity `InputField` consumes text focus so gameplay shortcuts do not receive name entry |
| Gender | `(66,243,186,19)` | Source text and ID 757/758 cycle controls at centers `(42,281)` and `(255,282)` |
| Race | `(66,293,186,19)` | Source text and ID 757/758 cycle controls at centers `(42,331)` and `(255,332)` |
| Background | `(66,343,186,19)` | Data-driven M12B background identity and ID 757/758 controls at centers `(42,381)` and `(255,382)` |
| Portrait arrows | centers `(43,118)` / `(244,118)` | IDs 283/284; only portraits legal for the current M12B race/gender may be selected |
| Back / Continue | centers `(69,55)` / `(675,55)` | IDs 323/321; Back returns to the New Character choice, Continue validates identity before opening the editor |
| Statistics title | `(339,23,409,19)` | Read-only source-style projection of the draft |
| Description | `(350,155,395,212)` | Contextual race, gender, background, and validation text |

All eight supported M12B races remain available: Human, Dwarf, Elf, Half-Elf, Gnome, Halfling, Half-Orc, and
Half-Ogre. Existing body/gender restrictions are enforced by M12B and the mounted portrait catalog. A race or gender
change repairs portrait selection through that authority rather than retaining an illegal presentation value.
Malformed retail Background 2 remains rejected by the existing data parser; UI-D does not invent replacement data.

## Character editor stage

`charedit_open` creates interface ID 22, then creates its attribute buttons, category controls, Accept control, and
child windows. UI-D retains that hierarchy.

- Attribute increments use ID 24 and decrements use ID 23 at the source coordinates for Strength, Dexterity,
  Constitution, Beauty, Intelligence, Perception, Willpower, and Charisma.
- Skills, Technology, and Magic category controls use IDs 34, 35, and 36 at local `(527,11)`, `(595,11)`, and
  `(663,11)`.
- Accept uses ID 568 at local `(754,21)`.
- The active child panel begins at local `(503,4)` and is replaced, rather than accumulated or globally scrolled.
- Skills use ID 29, group controls 302–305, and row controls 624/625. Rank changes submit only
  `AdjustCreationSkill` intent.
- Technology uses ID 30, discipline controls 306–313, and rank controls 647/648. Rank changes submit only
  `AdjustCreationTechnology` intent.
- Magic uses ID 31, the source college icon mapping
  `11,44,38,92,50,86,56,98,62,68,74,104,110,80,116,122`, and rank controls 647/648. Rank changes submit only
  `AdjustCreationSpell` intent.
- Source bitmap-font roles 26, 27, and 28 provide the small labels, values, and descriptions.

Disabled state is prospective M12B validation over a cloned specification. The view never duplicates costs,
prerequisites, attribute limits, rank limits, or remaining-point calculations. Character Points and all values are
projections of `GameUiController.CreationDraft` and `CreationValidation`.

## Navigation and authority

There is one production `RetailCharacterCreationView`, one `ProductionGameUiPresenter`, one controller/session, and
one `EventSystem`. The fixed source composition captures pointer input, but it does not create gameplay authority.

- Escape/Back from Editor returns to Identity and preserves the authoritative draft.
- Escape/Back from Identity returns through the existing Main Menu route.
- Identity Continue validates before changing stage and cannot create a partial PC.
- Editor Accept calls the existing M12B finalization transaction. Invalid creation remains open with no player
  mutation; valid creation creates exactly one PC, enters the authentic start map, closes creation, and reveals the
  existing source HUD.
- Pick Character/pregenerated-character selection, intro playback, and retail post-creation shopping remain the
  already documented fail-closed/deferred routes. UI-D does not add those subsystems.

## Resolution and graphics behavior

Both stages remain fixed 800 x 400 logical compositions centered within the 800 x 600 source surface. They do not
stretch or become a whole-screen scrolling form at larger resolutions.

| Display | Fixed composition result |
|---:|---|
| 800 x 600 | fully visible at source scale |
| 1024 x 768 | centered, finalization control accessible |
| 1920 x 1080 | centered, no clipping or global horizontal scrolling |
| 2560 x 1440 | centered, source geometry unchanged |
| 3840 x 2160 | centered, source geometry unchanged |

Original mode resolves mounted retail ART and portrait BMPs. Physical validation generated temporary local exact-4x
replacements for IDs 765 and 22, switched Original -> Enhanced -> Original, and proved that stage, active Technology
panel, draft values, Character Points, logical geometry, presenter identity, and M12B authority were unchanged.
Missing Enhanced children continue to fall back independently to Original. No retail or generated validation asset
is committed.

## Validation record

- Unity compilation: clean in the existing Unity 6000.0.71f1 Editor; no second Editor was launched.
- Focused UI-D EditMode: **19/19**, 0 failed, 0 skipped, 0 inconclusive.
- Directly affected regressions: **217/217**, comprising UI-A 23, UI-B 22, M12B 13, M4A 13, M4B 20,
  M4C 29, M4D 25, M10A 34, M10B 12, Player Startup 3, and production keyboard/input 23.
- Production Play Mode: **PASS**. The proof exercised invalid-name rejection without mutation, mounted portrait,
  gender/race/background cycling, source IDs 765 -> 22/29, all four point-spend categories, Back/Continue draft
  preservation, Original/Enhanced/Original rebuilding, all five target resolution mappings, and atomic finalization
  to `maps/arcanum1-024-fixed` with the source HUD visible.
- Production uniqueness: one Character Creation view, presenter/controller, and EventSystem.
- Final cleared Unity Console: 0 logs, 0 warnings, 0 errors.
- `git diff --check`: clean.

The complete EditMode suite is intentionally deferred to the UI-E.2 gate, as directed by the bounded overnight plan;
UI-D required no core-authority change.

## Boundary

UI-D owns source-faithful presentation, fixed composition, portrait pixel resolution, and intent forwarding only.
M12B continues to own character legality and finalization; M4, M10A, and M10B continue to own the projected game
facts. Runtime Character/Skills presentation belongs to UI-E.2. Pregenerated characters, intro playback, shopping,
new character rules, and any gameplay redesign are outside UI-D.

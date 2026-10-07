# UI-E.2 Character / Skills Reconstruction

Status: complete (2026-10-07)

UI-E.2 replaces the generic runtime Character and Skills overlays with the source-backed active-character editor
and Skills HUD route used by retail Arcanum. Presentation remains disposable: character attributes, derived values,
vitality, progression, skill ranks, training, and Character Points remain owned by the established M4 services.

## Source runtime composition

The implementation follows the active-character call path in `arcanum-ce/src/ui/charedit_ui.c`, not the filename or
dimensions of `char_maint.art` in isolation.

| Route | Source evidence | Production composition |
|---|---|---|
| Character HUD control / `C` | `charedit_open(..., CHAREDIT_MODE_ACTIVE)` | Shared big window ID 22 (`char_maint.art`), fixed 800 x 400 at source y 41, while the source HUD remains present |
| Skills child panel | Active `charedit_open` creates/replaces the current child window | ID 29 (`skills_window.art`) at local `(503,4)`, with group controls 302–305 and row controls 624/625 |
| Skills HUD control / `K` | HUD control ID 472 toggles the rotating Skills window | ID 274 at source `(196,492)` with source action controls 279, 280, 282, and 278; it is not a second full-screen modal |
| Character categories | Active editor controls 34/35/36 select Skills/Technology/Magic | Skills is implemented in this boundary; Technology and Magic retain their established routes and do not create parallel advancement authority |
| Auto-level schemes | ID 568 opens the source auto-level-scheme route | OpenArcanum has no runtime auto-level-scheme authority, so the control reports unavailable without mutation |

The active sheet therefore shares source presentation infrastructure with UI-D but does not reuse UI-D's creation
draft. It projects the authoritative finalized player and submits bounded runtime adjustment intent.

## Character projection

`GameUiController.ProjectCharacter()` now exposes the facts required by the source active sheet:

- finalized identity, race, gender, background, portrait, and age;
- level, current XP, XP remaining to the next level, and unspent Character Points;
- current and maximum HP and Fatigue from M4B;
- all eight base/effective attributes from M4A;
- armor class, carried weight/capacity, alignment, magick/technological aptitude, and the source-derived statistic set
  from M4D and inventory-capacity authority;
- all five resistances from M4D; and
- all sixteen skills, purchased points, effective ranks, and training/mastery state from M4C.

No value is recalculated in `RetailCharacterSheetView`. The view resolves source ART and bitmap-font roles, displays
the projection, and forwards button intent to `GameUiController`.

## Runtime point spending

Retail active mode permits legal Character Point spending and same-open-session refunds. The implementation adds a
bounded mutation path to the existing M4 authority rather than storing provisional values in the presenter.

- Attribute `+` spends one Character Point through `CharacterProgressionService` and `CharacterStatService`.
- Attribute `-` refunds only an increase made after this Character-sheet instance was opened. It cannot reduce the
  accepted base value.
- Skill `+` uses the existing M4C cost, maximum-rank, and governing-attribute rules.
- Skill `-` refunds only same-open-session purchased points and cannot cross the accepted baseline.
- Attribute reductions prospectively validate every dependent skill. A reduction that would make an existing skill
  illegal is rejected before attribute or Character Point mutation.
- Closing and reopening the Character sheet accepts the current authoritative values as the new floor.

The transactions validate first, then mutate the attribute or skill and Character Point balance. Invalid or
read-only operations fail closed without partial state.

## Skills and management-family routing

The active ID 29 child panel displays four source skill groups, four rows per group, purchased points, effective
rank, and Apprentice/Expert/Master training. Group selection is presentation-only and survives graphics invalidation.
Training remains the existing M4C authority; UI-E.2 adds no trainer or parallel mastery system.

Inventory and Character both occupy the fixed 800 x 400 middle management region while retaining the source HUD.
Switching `I`/Inventory to `C`/Character replaces the active middle presentation after the normal presenter update;
there is one controller, one modal owner, one Inventory view, and one Character view. Close returns to gameplay with
committed character state intact.

`K` now follows the source HUD route: it toggles `GameUiHudPanel.Skills`, leaves `GameUiScreen.None`, and mounts ID
274 above the lower HUD. The four source controls map to Prowling, Pick Pocket, Disarm Traps, and Repair. Their target
and action authorities are not present in the current core runtime, so each exact control reports its missing target
boundary and fails closed before Character Points, skill ranks, inventory, world, combat, or turn state can mutate.

## Resolution and graphics behavior

The active editor remains a native 800 x 400 logical composition positioned at
`((width - 800) / 2, 41 + (height - 600) / 2)`. It does not stretch or introduce a global `ScrollRect`.

| Display | Result |
|---:|---|
| 800 x 600 | full source window and HUD controls remain accessible |
| 1024 x 768 | centered source composition, no clipping |
| 1920 x 1080 | fixed source geometry, world remains visible around the management window |
| 2560 x 1440 | fixed source geometry, no modal duplication |
| 3840 x 2160 | fixed source geometry, controls remain within the display |

Physical validation switched Original -> Enhanced -> Original and invalidated presentation. The Character screen,
selected skill group, purchased Melee point, training state, controller/session identity, and authoritative values
survived. Enhanced mode continues to resolve each replacement independently and falls back to Original pixels.

## Validation record

- Unity compilation: clean in the existing Unity 6000.0.71f1 Editor; no second Editor was launched.
- Focused UI-E.2 EditMode: **20/20**, 0 failed, 0 skipped, 0 inconclusive.
- Targeted regressions: **169/169**: UI-D 20, UI-E.1 18, M4A 13, M4B 20, M4C 29, M4D 25, M12C 18,
  production keyboard/input 23, and Player Startup 3.
- Complete EditMode: **1269/1269**, 0 failed, 0 skipped, 0 inconclusive. The first complete run exposed two stale
  UI-C expectations for the pre-audit Character/Skills routes; those assertions were corrected and the complete
  suite reran cleanly.
- Production Play Mode: **PASS** through authentic New Game finalization and Player Startup at the crash site. It
  exercised source IDs 22/29, authoritative projection, attribute spend/refund, skill spend/training, source `K`
  routing and four fail-closed actions, Inventory -> Character switching, Original/Enhanced/Original rebuilding,
  all five target resolutions, and unique production view ownership.
- Final cleared Unity Console: 0 logs, 0 warnings, 0 errors. `git diff --check`: clean.

## Boundary

UI-E.2 is complete for the Character sheet, Skills advancement panel, and source Skills HUD presentation. It does
not implement Prowling/Pick Pocket/Disarm Traps/Repair target selection, trainers, auto-level schemes, Magic or
Technology advancement presentation, paper-doll compositing, follower management, or any new gameplay rule. Those
systems require their own source-backed runtime boundaries; the exposed unsupported controls remain fail closed.

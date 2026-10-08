# Retail Runtime UI Certification

Status: framework active; startup and the keyboard-accessible portion of Phase 2 were observed on 2026-10-07.
Click-only retail controls are blocked by the current native automation boundary, so Phase 2 and dependent phases
are not certified complete.

## Method and status vocabulary

Retail Arcanum 1.0.7.4 runs from the disposable
`D:\OpenArcanum\UIReference\RuntimeComparison\RetailSandbox` at native 800 x 600. OpenArcanum uses the already-open
Unity 6000.0.71f1 Editor; no second Editor is launched. Retail source/data and `arcanum-ce` are supporting evidence,
not substitutes for a physical observation. No retail screenshots are committed.

Rows use: **PASS**, **VISUAL MISMATCH**, **BEHAVIOR MISMATCH**, **MISSING**, **INTENTIONALLY DIFFERENT**,
**SOURCE AMBIGUITY**, or **BLOCKED**.

## Certification rows

| Screen | State | Control / region | Retail result | OpenArcanum result | Source evidence | Status | Root cause | Fix / deferral |
|---|---|---|---|---|---|---|---|---|
| Startup | cold launch | audio initialization | No-argument sandbox launch faults in retail `mss32.dll`; `-nosound` reaches the sequence | Startup media is not implemented | `main.c` `-nosound`; Windows application error 1000 | BLOCKED | host retail audio DLL access violation | keep audio certification open; do not model the crash |
| Startup | successful `-nosound` run | logo/intro order | Sierra -> Troika -> movie ID 8 -> Main Menu | launches directly to reconstructed Main Menu | `gamelib_logo`, `gamelib_splash`, `main.c`, `gmovie.c`, retail `movies.mes` | MISSING | startup media milestone is deferred | exact future boundary is in `ui-startup-sequence-audit.md` |
| Startup | later run | Escape | Escape skips the active logo; missing sandbox movie ID 8 then permits rapid Main Menu arrival | no startup media to skip | movie call has no keyboard-ignore flag | PASS | n/a for current menu handoff | retain as future startup acceptance criterion |
| Main Menu | top level | composition | fixed 800 x 600 still life with five centered entries | source ID 329 fixed 800 x 600 composition and five-entry order | `mainmenu_ui.c`; `mes/mainmenu.mes`; UI-B tests | PASS | none observed | none |
| Main Menu | Single Player | keyboard accelerator | `S` opens Single Player | UI-B models the same route | `mainmenu_ui` mnemonic dispatch | PASS | none observed | none |
| Single Player | normal | visible entries | New Game, Load Game, Last Save, View Intro, Cancel | New Game and Load are functional; unavailable routes remain bounded notices or absent per UI-B boundary | IDs 331 and source rows 50-54 | BLOCKED | all visible retail entries were observed, but click/transition exhaustion was not physically possible | re-run with a backend capable of retail DirectInput pointer control |
| Single Player | New Game highlighted | `N` | opens Pick Character / New Character / Cancel | existing UI-B New Game choice models these routes | source row group 420 and window transition | PASS | none observed | none |
| New Game choice | normal | `N` New Character | opens ID 765 identity composition | opens production `RetailCharacterCreationView` identity stage | `mainmenu_ui_new_char_create`; interface ID 765 | PASS | none observed | none |
| New Game choice | normal | `P` Pick Character | opens ID 766 on first pregenerated character | opens production ID 766 selector | `mainmenu_ui_pregen_char_refresh`; UI-B.1 tests | PASS | none observed | none |
| New Character | initial Human Male | full composition | top 336, center 765, bottom 335; portrait and center arrows are separated from bottom Back/Continue | current UI-D.2 uses the same three-region ownership and geometry | button flags and window handles in `mainmenu_ui.c`; UI-D.2 tests | PASS | prior ownership defect is already corrected | none |
| New Character | initial Human Male | initial stats | ST/CN/DX/BE/IN/WP/PE/CH all 8; Carry 4000; Damage -1; AC -2; Speed 8; Heal 3; Poison 8; Reaction -7 | projected from authoritative creation draft | retail physical observation; M12B projection | PASS | none visible in initial state | retain automated projection checks |
| New Character | identity navigation | Back / Escape | Escape returns to Pick/New choice | UI-D HandleEscape returns to Main Menu flow | source keyboard handler and ID 323 | PASS | none observed | none |
| New Character | identity permutations | race/gender/portrait/background arrows | click-only in retail | automated UI-D.1 and M12B coverage exists, but retail physical permutations were not driven this session | IDs 283/284/757/758; `background_clear`, `background_set`, effect authority | BLOCKED | native automation moves the Windows pointer, not the retail gold DirectInput cursor | do not infer; resume with compatible native pointer control |
| Character Editor | all pages | attributes, HP/Fatigue, Skills/Magic/Technology and hover help | click-only path not reached in retail session | existing production view and focused validators cover the bounded authority | `charedit_ui.c`; UI-D/UI-D.1 validation | BLOCKED | same DirectInput boundary | dependent Phase 2 closure withheld |
| Pick Character | index 0 | selected candidate | Merwin Tumblebrook, Male Halfling; ST 5, CN 8, DX 10, BE 8, IN 8, WP 8, PE 8, CH 8; Carry 2500; Damage -2; AC 0; Speed 10; Heal 3; Poison 8; Reaction -7 | source prototype-backed candidate projection exists | retail physical observation; prototypes 16067-16078; UI-B.1 tests | PASS | none observed at first candidate | none |
| Pick Character | cycle matrix | Next/Previous and all 12 candidates | click-only in retail | automated source order, varying projection, restore, start, and Save V1 checks exist | `mainmenu_ui_pregen_char_refresh`; UI-D.1 validator | BLOCKED | retail DirectInput cursor cannot be driven by the current backend | do not claim the 12-entry physical matrix complete |

## Automation boundary proof

The native backend captures the retail 800 x 600 surface and sends keyboard input successfully. It also moves a
separate Windows pointer, visible as a white pointer, while the gold retail cursor remains at its DirectInput
position. Attempts to click portrait/pregenerated arrows therefore changed only the Windows pointer and did not
advance retail state. The failure is observable and repeatable. Under the requested stop policy, no result from a
click-only control is marked PASS unless independently observed.

## Current gate

Source-backed review and existing automated tests may continue, but Phase 2 cannot be closed and Phases 3-6 cannot
be started as dependent implementation milestones until the retail click-only matrix is physically exercisable.
The last complete-suite OpenArcanum baseline remains HEAD `83a6ff0f227764d8e234f51b9cab41d47d0b5d86`, complete EditMode
1304/1304, with a clean worktree before these documentation additions. The complete suite was not repeated after the
retail DirectInput stop gate because the requested batch requires that run only after the dependent phases complete.

## OpenArcanum-side revalidation

The existing Unity 6000.0.71f1 Editor remained responsive and compiled the bounded editor-validation correction
cleanly. The focused gates were rerun after that correction:

- UI-D.1 Character Creation: 44/44;
- UI-B.1 Main Menu / Pick Character: 23/23;
- UI-C.5 gameplay HUD: 70/70;
- UI-E.2 Character / Skills: 20/20;
- M12B Character Creation: 16/16;
- production keyboard/input: 23/23.

All six gates reported zero failures, skips, and inconclusive results. The run emitted the two established original
asset diagnostics for UI source IDs 302 and 304 requesting palette 2 where the source resource exposes only palette
0; no Unity error was emitted.

The UI-D.1 production Play Mode validator also passed. It proved the 128 x 128 portrait path, 100 selector cycles
without drift, source race/gender restrictions, one- and two-digit attribute centering, source gauge ID 623,
prototype-backed 12-entry pregenerated selection and Save V1 round-trip, Skills/Spells/Schematics routing, current
combat-readiness authority, fail-closed unsupported authorities, five target resolutions, one presenter, and one
EventSystem. The validator previously expected obsolete `Inactive` feedback even though the current M12C combat
authority correctly enters combat and reports `Combat ready`; only that editor assertion was updated. No production
runtime code changed.

After the validation logs were reviewed, the final Unity Console was cleared and physically verified at 0 logs,
0 warnings, and 0 errors.

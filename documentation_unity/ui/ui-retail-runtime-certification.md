# Retail Runtime UI Certification

Status: M0 DirectInput tooling, M1 Character Creation / Pick Character, and M2 Inventory certification complete
(2026-10-08). M3 Movement / Combat / Sleep is in progress: movement and combat are physically certified; the retail
Sleep/Wait option menu is physically observed, but duration execution remains open after the disposable sandbox
process exited with its interrupted helper session.

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
| Single Player | normal | visible entries | New Game, Load Game, Last Save, View Intro, Cancel; pointer hover/click routes respond | New Game and Load are functional; unavailable routes remain bounded notices or absent per UI-B boundary | IDs 331 and source rows 50-54 | PASS | none for the certified New Game path | keep unavailable routes bounded |
| Single Player | New Game highlighted | `N` | opens Pick Character / New Character / Cancel | existing UI-B New Game choice models these routes | source row group 420 and window transition | PASS | none observed | none |
| New Game choice | normal | `N` New Character | opens ID 765 identity composition | opens production `RetailCharacterCreationView` identity stage | `mainmenu_ui_new_char_create`; interface ID 765 | PASS | none observed | none |
| New Game choice | normal | `P` Pick Character | opens ID 766 on first pregenerated character | opens production ID 766 selector | `mainmenu_ui_pregen_char_refresh`; UI-B.1 tests | PASS | none observed | none |
| New Character | initial Human Male | full composition | top 336, center 765, bottom 335; portrait and center arrows are separated from bottom Back/Continue | current UI-D.2 uses the same three-region ownership and geometry | button flags and window handles in `mainmenu_ui.c`; UI-D.2 tests | PASS | prior ownership defect is already corrected | none |
| New Character | initial Human Male | initial stats | ST/CN/DX/BE/IN/WP/PE/CH all 8; Carry 4000; Damage -1; AC -2; Speed 8; Heal 3; Poison 8; Reaction -7 | projected from authoritative creation draft | retail physical observation; M12B projection | PASS | none visible in initial state | retain automated projection checks |
| New Character | identity navigation | Back / Escape | Escape returns to Pick/New choice | UI-D HandleEscape returns to Main Menu flow | source keyboard handler and ID 323 | PASS | none observed | none |
| New Character | identity permutations | race/gender/portrait/background arrows and name field | every visible control was physically exercised; selection, hover/pressed state, legality, background projection, and name editing respond | UI-D.1 delegates legality and projection to M12B and preserves the source three-window composition | IDs 283/284/757/758; `background_clear`, `background_set`, effect authority | PASS | none remaining in the bounded identity flow | none |
| Character Editor | all pages | attributes, HP/Fatigue, Skills/Magic/Technology, hover help, Back and Continue | all displayed attribute and vitality +/- controls, skill categories and +/- controls, Magic, Technology, Back, and Continue were physically exercised | production view submits intent to the existing M4/M10/M12B authorities and fails unsupported actions closed | `charedit_ui.c`; UI-D/UI-D.1 validation | PASS | no bounded M1 discrepancy remains | post-creation Shopkeeper is intentionally deferred to the shopping/inventory boundary |
| Pick Character | index 0 | selected candidate | Merwin Tumblebrook, Male Halfling; ST 5, CN 8, DX 10, BE 8, IN 8, WP 8, PE 8, CH 8; Carry 2500; Damage -2; AC 0; Speed 10; Heal 3; Poison 8; Reaction -7 | source prototype-backed candidate projection exists | retail physical observation; prototypes 16067-16078; UI-B.1 tests | PASS | none observed at first candidate | none |
| Pick Character | cycle matrix | Next/Previous and all 12 candidates | all twelve retail candidates were physically cycled and recorded; Back and Continue respond | all twelve are prototype-backed and now display their effective initial and derived stats in retail order | `mainmenu_ui_pregen_char_refresh`; prototypes 16067-16078; UI-D.1 validator | PASS | prior UI projected raw prototype defaults, used the wrong order, and omitted derived values | corrected through shared character-derived-stat rules and M12B projection; no presenter-owned hardcoded stat table |
| Inventory | ordinary PC inventory | composition and transactions | fixed paper doll plus inventory grid; live PC lens; footprints/stacks; inspection; drag/drop; Arrange; equip/unequip; Defense 8 -> 6 -> 8; quick-slot assignment; Gold/ammo/weight/encumbrance/speed/attack/defense counters | production UI-E projects and submits to existing inventory/equipment/shortcut authorities; matching actions were already physically validated in Unity | `inven_ui.c`; UI-E, M3C, M3E, M12C tests | PASS | no bounded M2 mismatch observed | none |
| Gameplay | crash-site movement | click, Ctrl-click, NumLock | ordinary click walks; Ctrl-click runs; NumLock enables persistent Always Run; sustained Ctrl while Always Run stays running | production input now treats Ctrl and Always Run as independent run requests | `intgame.c` calls `turn_on_running` in the Ctrl branch before consulting Always Run; `anim.c::get_always_run` inversion is bypassed by this pointer-click path | PASS | old OpenArcanum XOR interpretation contradicted retail and the controlling source path | corrected at the production input boundary; no movement clock or animation authority changed |
| Gameplay | combat readiness | `R` and HUD control | both enter and leave combat when legal; leaving is rejected while Virgil remains hostile | both routes delegate to coordinator-owned combat readiness | `intgame.c`; M8/UI-C authority | PASS | none observed | none |
| Sleep/Wait | crash site | source menu | `S` opens seven wait rows in order: Advance Time One Hour, Two Hours, Four Hours, Eight Hours, One Day, to Morning, to Evening; no Until Healed row; ID 293 hover frame observed | production classifies the authentic crash-site sector as TownWait and exposes the same seven choices | `sleep_ui.c`; `TownMapInfo`; `townmap.mes [w:1]`; UI-C.5 tests/validator | BLOCKED | duration execution was interrupted; subsequent RetailSandbox restarts currently exit before creating a window | retain M3 as open; do not infer clock/recovery behavior from menu visibility alone |
| Starting equipment | after Character Editor Continue | Shopkeeper transition | retail enters the shopping map before START_MAP | OpenArcanum currently finalizes directly into START_MAP | `gameinit.c` selects `MAP_TYPE_SHOPPING_MAP`; `mainmenu_ui.c` refreshes the first NPC and opens shared barter inventory | MISSING | required intermediate lifecycle and authored shopping actor/state do not yet exist | do not substitute a generic merchant; implement a bounded creation-to-shopping-to-start transaction after the retail process is available again |
| Dialogue | Virgil at crash site | ordinary conversation | NPC speech is over the live world and PC responses occupy a lower live-world strip; pointer selection advances; number-key selection did not respond in the observed run | UI-F.1 now presents the authentic Virgil M5 transaction as a world bubble plus five-row 400 x 100 source-font strip above the persistent HUD; pointer selection physically advances and dialogue leaves world movement open | `dialog_ui.c`; `tc.c`; `intgame.c`; UI-F.1 tests and production validator | PASS | prior generic nine-choice blocking modal contradicted retail/source | corrected without moving dialogue authority into the view; physical retail audio parity remains unclaimed under `-nosound` |

## M0 DirectInput bridge

The original blocker was confirmed: ordinary native pointer movement moved the Windows cursor but not retail
Arcanum's independent gold DirectInput cursor. A local-only helper was built outside Git under
`UIReference/RuntimeComparison/Tools/DirectInputMouse`. It uses standard `user32.dll` `SendInput` relative mouse
packets and button packets through a named-pipe command surface. Before every input it resolves the expected
RetailSandbox process, validates PID/HWND/title/class, brings that exact window to the foreground, and refuses to
send if foreground ownership does not match.

Calibration at the native 800 x 600 retail surface established an effective 1:1 relative-delta scale in both axes.
Positive, negative, short, and long corrections were visually closed-looped against the gold cursor. Hover, click,
Back, Continue, previous/next portrait, and previous/next pregenerated-character controls all produced the expected
retail transitions. More than twenty deliberate movements/clicks completed without a desktop or Unity misclick.
No Windows mouse setting, retail executable, DirectInput DLL, or clean reference file was changed. The helper and
all comparison captures remain local-only and uncommitted.

## M1 pregenerated retail matrix

The retail order is zero-based below; OpenArcanum's persisted source index is one-based. Portrait and background
IDs are the source bindings used by the matching engine-owned prototype. Attribute order is retail
`ST/CN/DX/BE/IN/WP/PE/CH`; derived order is Carry, Damage, AC, Speed, Heal, Poison, Reaction.

| Order | Name | Sex / race | Portrait / background | Attributes | Derived |
|---:|---|---|---|---|---|
| 0 | Merwin Tumblebrook | Male Halfling | 1017 / 39 | 5/8/10/8/8/8/8/8 | 2500/-2/0/10/3/8/-7 |
| 1 | Godfrey Castleburger | Male Gnome | 1002 / 40 | 8/6/8/8/8/10/8/8 | 4000/-1/-2/8/2/6/-7 |
| 2 | Horace McGinley | Male Half-Elf | 1032 / 41 | 8/7/9/9/8/8/8/8 | 4000/-1/-1/9/2/7/-3 |
| 3 | Muck Evil Eye | Male Half-Ogre | 1027 / 42 | 12/8/8/7/4/8/8/8 | 6000/2/-2/8/3/8/-12 |
| 4 | D'ren L'elor | Male Elf | 1033 / 43 | 7/6/9/9/8/9/8/8 | 3500/-1/-1/9/2/6/-3 |
| 5 | Solomon Doone | Male Human | 1005 / 44 | 8/8/8/8/8/8/8/8 | 4000/-1/-2/8/3/8/-7 |
| 6 | Merik Luggerton | Male Half-Orc | 1030 / 45 | 9/9/8/6/8/8/8/6 | 4500/0/-2/8/3/9/-18 |
| 7 | Lugard Bloodstone | Male Dwarf | 1020 / 46 | 9/9/7/8/8/8/8/7 | 4500/0/-3/7/3/9/-7 |
| 8 | Victoria Warrington | Female Human | 1025 / 47 | 7/9/8/8/8/8/8/8 | 3500/-1/-2/8/3/9/-7 |
| 9 | B'elan T'Seraa | Female Elf | 1024 / 48 | 6/7/9/9/8/9/8/8 | 3000/-2/-1/9/2/7/-3 |
| 10 | Clarisse Vorak | Female Half-Orc | 1031 / 49 | 8/10/8/6/8/8/8/6 | 4000/-1/-2/8/3/10/-18 |
| 11 | Ker'ee Melange | Female Half-Elf | 1029 / 50 | 7/8/9/9/8/8/8/8 | 3500/-1/-1/9/3/8/-3 |

## Current gate

M0, M1, and M2 are closed. M3 movement and combat are physically certified. M3 Sleep/Wait remains open only on
executing the displayed retail duration rows and recording the resulting clock transitions; OpenArcanum's existing
town-wait and wilderness-sleep transactions remain green. M4 is a confirmed gap: retail enters the authored
Shopkeeper shopping map while OpenArcanum currently proceeds directly to START_MAP. Its implementation stays blocked
until the exact retail transaction can be observed again and the intermediate lifecycle is bounded; a generic
merchant substitute is rejected. M6 UI-F.1 ordinary dialogue presentation is implemented and green from the prior
physical retail Virgil observation, controlling source, focused tests, and production Unity proof. The complete
EditMode suite remains deferred to the single final batch gate after the authorized sequential phases.

## OpenArcanum-side revalidation

The existing Unity 6000.0.71f1 Editor remained responsive and compiled the bounded runtime/test correction cleanly.
The M1-focused and directly affected gates passed:

- UI-D.1 Character Creation: 44/44;
- UI-B.1 Main Menu / Pick Character: 24/24;
- UI-C.5 gameplay HUD: 70/70;
- UI-E.2 Character / Skills: 20/20;
- M12B Character Creation: 17/17;
- UI-D.2 partial-window ownership: 8/8;
- M4D derived statistics: 25/25;
- production keyboard/input: 23/23.

Every gate reported zero failures, skips, and inconclusive results. The run emitted the two established original
asset diagnostics for UI source IDs 302 and 304 requesting palette 2 where the source resource exposes only palette
0; no Unity error was emitted.

The UI-D.1 production Play Mode validator also passed. It proved the exact retail raw and derived values for all
twelve entries on the rendered ID 766 screen, 128 x 128 portraits, 100 selector cycles without drift, source
race/gender restrictions, one- and two-digit attribute centering, source gauge ID 623, two prototype-backed campaign
starts and Save V1 round-trip, Skills/Spells/Schematics routing, current combat-readiness authority, fail-closed
unsupported authorities, five target resolutions, one presenter, and one EventSystem. Original-mode 800 x 600
inspection showed the two stat columns aligned and unclipped.

The bounded correction centralizes already-established carry/damage/AC/speed/heal/poison/reaction rules, projects
effective pregenerated/background stats through M12B, and leaves the UI as a pure consumer. It does not add a
presenter-owned retail table or change character authority.

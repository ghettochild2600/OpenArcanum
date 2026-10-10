# UI-D Character Creation Source-Runtime Certification

Status: all seven player-visible Character Creation defects and the post-editor starting-equipment lifecycle are
closed (2026-10-09).

## Evidence classes

- `C:\Arcanum - Modded` is the **WINDOWED-RETAIL-DERIVED-PHYSICAL-REFERENCE**. The seven user screenshots from this
  runtime are the player-visible acceptance control.
- `arcanum-ce` is the **SOURCE-RUNTIME-AUTOMATION-REFERENCE**. It was run from
  `D:\OpenArcanum\UIReference\RuntimeComparison\ArcanumCE-800x600\arcanum-ce.exe -window -geometry=800x600`
  against legitimate local retail data. Its observations are **SOURCE-RUNTIME-PHYSICALLY-CERTIFIED**, never
  retail-physically-certified.
- `D:\OpenArcanum\Source\Steam-Clean` is the **PRISTINE-RETAIL-CONTROL** and remained read-only.
- `D:\OpenArcanum\Research\Repositories\arcanum-ce` supplied controlling source evidence and remained clean.

Sky native-window automation reached the 800 x 600 arcanum-ce Main Menu, New Character, Character Editor,
Human Shopkeeper, and campaign surfaces. It moved the visible source-runtime cursor, hovered source controls, and
clicked through the complete path. OpenArcanum was exercised in the existing Unity 6000.0.71f1 Editor only.

## Seven screenshot corrections

| # | Player-visible issue | Source-runtime observation and source finding | Prior OpenArcanum behavior | Correction and proof |
|---:|---|---|---|---|
| 1 | Next / Previous contextual help | Hovering IDs 321/323 shows source icon 444, white title `Action:`, then `Next Screen` / `Previous Screen`; leaving the control clears the region. The strings are main-menu messages 6999, 7003, and 7002. | Bottom controls did not use the shared source contextual region. | Both controls project the same source message path into the bottom window and clear on exit. Focused tests cover both directions; production hover passed. |
| 2 | Empty-name validation | Continue with an empty name stays on New Character and shows centered white `You must choose a name.` without a modal or help icon. The text is main-menu message 506. | Validation did not reproduce the source information-region response. | Continue is rejected transactionally, the stage and draft remain unchanged, and the exact message is rendered in the bottom region. Focused and physical validation passed. |
| 3 | Gender information | Hovering Male/Female values or arrows displays the corresponding main-menu gender text (742 plus gender index) in the bounded identity description region, without a separate title. | Gender controls did not project the source description consistently. | Values and arrows share the source-backed bounded description path; wrapping and title/body separation are preserved. |
| 4 | Race information and effects | Race uses title message 745 and body `770 + 2 * race + gender`. All eight legal races were cycled physically; race effects changed effective raw/derived values and Human -> Dwarf -> Human -> Dwarf did not accumulate. | Help and complete effective-stat refresh were incomplete. | Race selection reprojects through creation/race authority, resets to the first legal portrait, and derives every displayed value from the effective candidate. Tests cover help, refresh, and repeat cycling. |
| 5 | Complete Character Editor | The source runtime shows identity, vitality, derived statistics, resistances, attributes, skill ranks/gauges, category navigation, and aptitude in the fixed ID 22 composition with child IDs 29/30/31. | Several source regions and numeric projections were missing or incomplete. | The entire source composition is now projected from M4/M10/M12B facts. The UI owns no gameplay formulas. The five-resolution physical walkthrough passed. |
| 6 | Skill contextual help | Bow shows icon 444, title `Bow`, source description, and red `Minimum DX to raise: 6`. The requirement changes with the governing attribute and next rank. | Requirement text was absent/incomplete and risked becoming a fixed example. | `CharacterSkillRules.MinimumAttributeForRank` computes the next-rank requirement; all skill families use the shared source help region. Representative family tests and physical Bow hover passed. |
| 7 | Human Shopkeeper | Custom creation continues to `HUMAN SHOPKEEPER`: Gold 400, source barter/player panels, zero ammunition counters, real stock, quotes/help, purchase, then START_MAP. Shaft costs 7; purchase leaves Gold 393; reverse projection quotes 2. Pregenerated characters bypass shopping because their authored inventories already exist. | Custom creation finalized directly into START_MAP. | The same authoritative PC enters SHOPPING_MAP once, trades through existing M11A/M3 authority, auto-equips source-compatible best items on Continue, and transfers to START_MAP. Back cancels the pending creation session; Save V1 preserves the post-shopping PC without replaying stock or transactions. |

## Character Editor difference matrix

| Region | Source-runtime result | Retail-derived screenshot control | Corrected OpenArcanum result | Authority / evidence | Status |
|---|---|---|---|---|---|
| Identity | `A`, LEVEL 1, POINTS 5, XP NEXT 2100, HUMAN, MALE, AGE 20 | Same field family and fixed placement | Live draft/player values at source coordinates and alignment | `charedit_ui.c`; M12B draft | OPENARCANUM-PHYSICALLY-VALIDATED |
| Attributes | ST/CN/DX/BE/IN/WP/PE/CH, source +/- states and costs | Same eight-cell grid | Effective background/race values, transactional controls, centered one/two-digit text | M4A/M12B | OPENARCANUM-PHYSICALLY-VALIDATED |
| Health | 30 centered in the source circle with +/- | Mis-centering visible in supplied comparison | Current/max authoritative HP projection with corrected centering and hit regions | M4B | OPENARCANUM-PHYSICALLY-VALIDATED |
| Fatigue | 30 centered in the source circle with +/- | Mis-centering visible in supplied comparison | Current/max authoritative Fatigue projection; never relabeled Mana | M4B | OPENARCANUM-PHYSICALLY-VALIDATED |
| Derived Stats | Carry 4000, Damage -1, AC -2, Speed 8, Heal 3, Poison Recovery 8, Reaction -7, Max Followers 2 | Block present in retail-derived editor | Exact ordered labels and shared derived values | M4D and party capacity | OPENARCANUM-PHYSICALLY-VALIDATED |
| Resistance | Damage 0, Magick 0, Fire 0, Poison 20, Electrical 0 | Block present in retail-derived editor | Exact ordered values from authoritative resistance state | character prototype/race state | OPENARCANUM-PHYSICALLY-VALIDATED |
| Skills | Source numeric rank, ID 623 gauge, training state, +/- across Combat/Thieving/Social/Technological | Numeric rows visible in supplied editor | All families project source units and legal mutation state | M4C skill authority | OPENARCANUM-PHYSICALLY-VALIDATED |
| Category controls | Skills, Technological Disciplines, and Spell Colleges route to IDs 29/30/31 | Missing controls visible by comparison | Source ART, hit regions, selected states, and fail-closed unsupported actions | `charedit_ui.c` | OPENARCANUM-PHYSICALLY-VALIDATED |
| Aptitude/right side | Magick 0 and Technology 0 with source markers and surrounding blocks | Previously sparse/incomplete | Live M10-derived aptitude plus derived/resistance presentation | M10A/M10B | OPENARCANUM-PHYSICALLY-VALIDATED |
| Context help | Icon 444, separate title/body, source color/wrapping; dynamic governing requirement | Bow example supplied | Shared bounded help architecture for attributes, vitality, skills, categories, magic, and technology | `mes/charedit.mes`; rule services | OPENARCANUM-PHYSICALLY-VALIDATED |

No bounded mismatch remains in the seven supplied Character Creation comparisons. This does not certify unrelated
startup media, audio, later Logbook data, follower HUD, Schematics UI, maps, or world-renderer fidelity.

## Starting-equipment lifecycle

Controlling source routes custom creation through `gameinit.c`'s `MAP_TYPE_SHOPPING_MAP`, creates the player once,
then `mainmenu_ui_shop_refresh` restocks the first merchant and opens barter. Completion auto-equips the best
compatible weapon/armor before relocating that same player to START_MAP. The reconstructed presentation uses source
IDs 235/221, paper dolls 337/341, toggles 338/339, Arrange 340, bottom navigation 321/323, and help icon 444. Gold is
shown only in its counter, not as a draggable item.

The production proof bought the Shaft from the Human Shopkeeper, observed Gold and inventory mutation, completed
shopping, reached the crash site, and round-tripped identity, Gold, the purchased item, equipment, and sector through
Save V1. A fresh custom session restocks once; Back and later load do not duplicate transactions. Pregenerated PCs
continue directly to START_MAP with authored equipment, matching the source-runtime branch.

## Certification

| Feature | SOURCE-CERTIFIED | SOURCE-RUNTIME-PHYSICALLY-CERTIFIED | WINDOWED-RETAIL-REFERENCE-PHYSICALLY-OBSERVED | OPENARCANUM-PHYSICALLY-VALIDATED |
|---|:---:|:---:|:---:|:---:|
| Navigation help | yes | yes | yes | yes |
| Empty-name validation | yes | yes | yes | yes |
| Gender help | yes | yes | yes | yes |
| Race help/effects | yes | yes | yes | yes |
| Complete Character Editor | yes | yes | yes | yes |
| Dynamic skill help | yes | yes | yes | yes |
| Human Shopkeeper lifecycle | yes | yes | yes | yes |

The windowed retail-derived column means the supplied screenshots were physically observed. It does not elevate the
arcanum-ce results to `RETAIL-PHYSICALLY-CERTIFIED`.

## Validation record

- UI-D focused EditMode: **64/64**.
- Directly affected regressions: **231/231** across UI-A, UI-B, M12B, M4A-M4D, M10A/M10B, Player Startup, and
  production keyboard/input.
- Production Play Mode: **PASS** twice after the final art-state correction. The complete flow reached identity,
  editor, SHOPPING_MAP/Human Shopkeeper, a real purchase, START_MAP, and Save V1 restoration. It also preserved one
  presenter and one EventSystem through Original -> exact-4x Enhanced -> Original rebuilds and 800 x 600, 1024 x
  768, 1920 x 1080, 2560 x 1440, and 3840 x 2160 checks.
- Complete EditMode: recorded in `PROJECT_STATUS.md` after the one final batch gate.


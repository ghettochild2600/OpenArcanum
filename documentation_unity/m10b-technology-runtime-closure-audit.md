# M10B Technology Runtime Closure Audit

Date: 2026-09-27
Branch: `feature/session-save-load`
Starting HEAD: `b51768ddf29bf44ef540dc1bd8ad3a820754388d`

## Decision

**DONE M10B — Technology Runtime.**

No M10B Phase 2 is required. The implemented runtime owns the technology-specific authorities needed before later
content and crafting work: discipline ranks, effective rank and built-in-schematic eligibility, shared M4
Magick/Technology aptitude integration, source item-effectiveness and malfunction arithmetic, typed transactional
item use, composition with M3/M4/M8/M8H, and optional Save V1 persistence. Healing Salve and Power Axe prove two
different authentic production paths without introducing parallel inventory, vitality, combat, scheduling, or save
authorities.

The remaining source behavior is either content over that kernel, M11C crafting/schematic work, or a consumer of a
broader item/effect/world authority that does not belong uniquely to technology. Keeping M10B open for those systems
would incorrectly make the technology runtime own general-purpose mechanics.

## Closure standard

M10B is a core-runtime milestone, not a promise to author every technological item or schematic. It is closed when
technology identity and progression are authoritative; source aptitude and item calculations are available at the
correct runtime boundary; supported uses validate before mutation and commit through existing domain owners; combat
malfunction composes the existing critical-failure transaction; and committed technology state survives Save V1
while transient use/combat work normalizes.

Those conditions are met. The Phase 1 validation baseline remains **12/12 focused**, **206/206 affected
regressions**, and **932/932 complete EditMode**, with clean Unity compilation, **0 failed, 0 skipped, 0
inconclusive**, a final cleared Console of **0 logs, 0 warnings, 0 errors**, and clean `git diff --check`.

## Source/runtime closure matrix

Classification numbers correspond to the requested closure categories.

| Source area | Classification | Closure finding |
|---|---:|---|
| Eight disciplines, stored ranks, INT gates, effective levels, and technological-point mutation | 7 — already implemented | `TechnologyStateService` owns the source ranks and effective-rank projection; M4 remains the sole aptitude authority. |
| Built-in schematic identity and eligibility | 7 — already implemented | The exact `1990 + 200 * discipline + 10 * degree` identity is exposed without pretending that eligibility is manufacture. |
| Found/written schematic learning | 3 — defer to M11C | The source stores found schematic IDs separately and consumes the written schematic. This belongs with the complete crafting knowledge transaction. |
| Recipe prerequisites and effective-expertise checks | 3 — defer to M11C | Readiness depends on known schematic, effective technology level, and complete component availability. |
| Ingredient aliases, atomic consumption, product alternatives, quantity, creation, and transfer | 3 — defer to M11C | These are the source manufacture transaction. Produced objects become ordinary M3 items; M10B needs no crafted-item subclass. |
| Technology UI and schematic browser | 5 — UI/AI/campaign | Presentation consumes authoritative projections after M11C; it is not a core runtime owner. |
| Exact item effective-power arithmetic | 7 — already implemented | The signed-complexity calculation and technological clamp are available through the technology service. |
| Effective-power ratio and downstream armor/weapon/skill/light/weight modifier consumers | 4 — M11A/M11B/later shared systems | The source adjusts shared item properties through general item/effect consumers. Unity does not yet expose all of those mutable/property channels, and they are not technology-only authority. |
| Technological weapon malfunction probability and supported self-hit consequence | 7 — already implemented | The exact positive-Magick-aptitude probability is evaluated at the audited M8 call site and reuses the M8F critical-failure transaction. |
| Owner/target aptitude cross-interaction found in broader combat/item logic | 4 — M11A/M11B/later shared systems | Any future integration belongs at the shared combat/item-effect boundary, not in a second technology attack system. |
| Healing Salve typed use, eligibility, range, AP, healing, depletion, and scheduling | 7 — already implemented | The authentic one-use item proves fail-before-mutation validation and commits through M3, M4, M8, and M8H. |
| Additional simple therapeutics, herbology items, and compatible beneficial uses | 2 — content on existing runtime | Items with the same supported target/effect shape can be added as immutable definitions without new authority. |
| Poison, status, resistance, or script-heavy chemistry/therapeutics | 4 — M11A/M11B/later shared systems | Their missing dependency is a general status/effect/script contract, not a missing technology rank or use coordinator. |
| One-use source charge/depletion | 7 — already implemented | Healing Salve proves final-use removal through M3 rather than duplicate M10B quantity state. |
| Reusable mutable charges, charge cells, recharging, and negative/infinite stores | 4 — M11A/M11B/later shared systems | Source `SpellMana`/mana-store behavior needs persistent general item-instance properties and shared magic/technology charge handling. |
| Additional technological melee weapons | 2 — content on existing runtime | Existing M8 melee plus the completed malfunction hook already supplies the core path; item records are content. |
| Firearms | 7 / 2 / 4 | M8 already owns ranged attacks and M10B supplies the shared technology malfunction calculation (7). More guns are content (2). General source-adjusted weapon properties belong to shared item-property integration (4). |
| Explosives and grenades | 4 / 6 | Authentic radius enumeration, mixed damage, knockback, effect objects, and friendly-fire ordering need general AoE/world/effect authority (4); unclear reconstructed edge ordering remains source/dependency-not-ready (6). |
| Placed traps and trap devices | 4 / 6 | Source behavior creates world trap objects, attaches scripts, schedules events, enumerates radius targets, and manages ownership/disarm state (4). The exact usable-product and object-use relationships remain ambiguous (6). |
| Mechanical/electrical devices | 2 / 4 / 6 | A definition that fits existing typed use is content (2). Persistent world/device/status/script behavior needs later shared authority (4), and unsupported source end semantics remain dependency-not-ready (6). |
| Temporary or maintained technological effects | 4 — M11A/M11B/later shared systems | They should use the existing shared source-time axis, but require the general active-effect/item-trigger lifecycle. M10B must not create another clock. |
| Item wear/unwear, pickup/drop, hit, damage, unconscious/dying, and random item-effect triggers | 4 — M11A/M11B/later shared systems | `mt_item.c` describes a broad magic/technology item-trigger engine. It is general equipment/effect infrastructure, not a technology-specific prerequisite. |
| World-object and portal/container device interaction | 4 / 6 | Authentic mutation belongs to the owning world/object/script system (4). Trap Springer lacks a proven bounded End/Use result with current authorities (6). |
| Magic/technology interaction | 7 / 4 | Shared M4 aptitude, exact power arithmetic, and malfunction are implemented (7). General magic/technology item adjustment, charges, and effect triggers wait for their shared consumers (4). |
| Technology persistence and transient normalization | 7 — already implemented | Save V1 optionally stores validated ranks; M4 aptitude, M3 item consequences, and M4 vitality remain in their owners; pending use/combat/scheduler work does not serialize. |
| Future mutable charges and active effects in saves | 4 — M11A/M11B/later shared systems | Persistence must be added by the future item-instance/effect owner rather than predeclared as duplicate M10B state. |
| NPC/follower item selection, merchant/economy flows, authored grants, and campaign use | 5 — UI/AI/campaign | These systems consume the completed kernel and do not define technology runtime correctness. |

## Required M10B work

Category 1 is empty. No technology-specific authority or prerequisite is missing from the bounded core runtime, so a
new implementation phase would expand M10B rather than close a defect.

## Exact M11C boundary

M11C owns the complete schematic/manufacture workflow:

- built-in and found schematic knowledge as a unified eligibility projection;
- written-schematic acquisition and its source item consumption;
- effective-discipline prerequisite checks;
- source recipe records, all ingredient aliases, and product alternatives;
- atomic component selection and consumption, including ammunition/stack quantities;
- source output prototype selection and output quantity;
- item creation and transfer through the existing M3 authority;
- persistence of found schematic knowledge and any committed crafting consequences;
- a presentation-independent crafting transaction that later UI can invoke.

M11C does not own technology ranks, aptitude, combat, vitality, item placement, or generic world/effect systems. It
must consume those existing owners and preserve the source recipe records rather than silently correcting them.

## Preserved ambiguities and rejected inference

- Retail schematic 2000 is named Healing Salve but identifies product prototype 10059, while the directly usable
  retail Healing Salve vertical slice is prototype 10079.
- Mechanical Trap Springer recipe/product and usable-effect prototype relationships do not resolve to one proven
  runtime identity through the currently bounded path.
- Reconstructed explosive, trap, status, and device behavior depends on general script, object, AoE, active-effect,
  or event ordering that the current runtime cannot faithfully represent.
- Decompiled names, apparent dead paths, and incomplete source comments are evidence to preserve, not permission to
  normalize behavior or invent a convenient rule.

These findings are category 6 until the owning dependency and authentic transaction can be proved. They do not
constitute a missing M10B Phase 2.

## Validation and change policy

This closure assessment changes documentation only. It does not modify production runtime, tests, assets, saves, or
Unity project settings. The already accepted M10B compile/test/physical-validation baseline therefore remains the
authoritative gate and was not rerun. `git diff --check` is the only required new validation for this documentation
commit.

This closure does not authorize M11, M11C, UI, AI, campaign, crafting, or later shared item/effect/world work.

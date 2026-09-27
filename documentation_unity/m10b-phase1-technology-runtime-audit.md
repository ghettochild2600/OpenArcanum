# M10B Phase 1 Technology Runtime Audit

Date: 2026-09-27
Branch: `feature/session-save-load`
Starting HEAD: `235ed4437c2b22395390deaa18b6e85faed544a7`

## Result

M10B is complete as a bounded core runtime. `WorldMapSessionCoordinator` owns one `TechnologyStateService` for
source discipline ranks, built-in-schematic eligibility, technological item effectiveness, typed non-attack item
use, and the bridge into existing M3/M4/M8 authority. The authentic Phase 1 slice is retail Healing Salve prototype
10079 and the technological-weapon malfunction rule proved with retail Power Axe prototype 6088. No technology
state is owned by Unity presentation, an item view, the combat UI, or a second aptitude/attack/scheduler system.

The implementation deliberately does not build crafting. It records the source schematic identity boundary that
future M11C can consume, but it does not add ingredient discovery, recipe acquisition, manufacture, crafting UI,
economy, bulk production, or synthetic item recipes.

## Source archaeology

The executable behavior was audited in the local `arcanum-ce` reconstruction and checked against the mounted retail
DAT/loose prototype records. The retained Unity audit parsed 1,423 retail prototypes and identified 281 technology,
usable-effect, healing-item, or grenade candidates. Primary implementation references were:

- `tech.h` and `tech.c`: the eight disciplines, eight stored degrees, intelligence table, effective levels,
  sequential degree changes, technological-point mutation, and built-in schematic formula;
- `item.c`: `item_effective_power`, `item_effective_power_ratio`, and
  `item_aptitude_crit_failure_chance`;
- `combat.c`: the aptitude critical-failure roll for equipped non-throwing weapons;
- `magictech.c`, `mt_item.c`, and retail `rules/spelllist.mes`: technological item target/effect execution;
- `anim.c`: item-use animation/SAP_USE boundary;
- retail `mes/tech.mes`, `rules/schematic.mes`, `mes/schematic_text.mes`, `mes/spell.mes`,
  `rules/spelllist.mes`, and `data/proto/*.pro`.

### Disciplines and degrees

The stored discipline indices are, in order: Herbology, Chemistry, Electrical, Explosives, Gun Smithy, Mechanical,
Smithy, and Therapeutics. Each source character uses `OBJ_F_CRITTER_SPELL_TECH_IDX` slots 17-24.

The stored degree values are source enums, not an invented unlearned sentinel:

| Value | Degree | Minimum INT | Effective level | Point cost to obtain |
|---:|---|---:|---:|---:|
| 0 | Layman | 0 | 0 | 0 |
| 1 | Novice | 5 | 10 | 1 |
| 2 | Assistant | 8 | 20 | 1 |
| 3 | Associate | 11 | 35 | 1 |
| 4 | Technician | 13 | 50 | 1 |
| 5 | Engineer | 15 | 65 | 1 |
| 6 | Professor | 17 | 80 | 1 |
| 7 | Doctorate | 19 | 100 | 1 |

Ranks are sequential. The source has no separate skill, training, or aptitude prerequisite for buying the next
degree: the gates are next-rank INT and one available character point. A temporary INT reduction does not erase the
stored degree; effective degree/level walks downward until its INT requirement is met. Phase 1 exposes the degree
projection needed by runtime and M11C without inventing a separate technology mastery track.

Each purchased degree adds one source technological point. M4 remains the only authority for the resulting shared
Magick/Technology aptitude; M10B records only the added technological-point adjustment and asks M4 for the effective
aptitude. There is no duplicate technology-aptitude field.

### Schematics and crafted items

Built-in schematic identity is exactly `1990 + 200 * discipline + 10 * degree` for degrees 1-7. Therefore Herbology
Novice is schematic 2000. A Layman has no built-in schematic. Found/written schematic acquisition is a separate
source PC array and is deferred to M11C.

Source manufacture creates the recipe's ordinary product prototype. The runtime does not expose a separate
"crafted item" behavior class: once created, inventory identity, placement, equipment, quantity, ammunition, and
use remain the existing M3 item authority. Phase 1 consequently records eligibility and schematic IDs but does not
pretend that possession or use requires having crafted the item personally.

One retail ambiguity is preserved rather than normalized: schematic 2000 is named Healing Salve but its product
field is prototype 10059, while the directly usable retail item named Healing Salve is prototype 10079. Likewise,
the Mechanical Trap Springer schematic and usable effect item resolve to different product/prototype numbers. M11C
must resolve those recipe/product aliases from the complete source workflow; M10B does not rewrite them.

### Technological item identity and aptitude

Negative `OBJ_F_ITEM_MAGIC_TECH_COMPLEXITY` marks technological complexity; positive values are magical and zero is
neutral. Discipline is stored separately on the item prototype. Usable targeted retail items carry the source
CanUse/NeedsTarget flags (`0x80`/`0x100`); charges or consumable behavior remain prototype/item state, not a parallel
M10B quantity.

For a critter owner, source effective item power is `(complexity + MagickTechAptitude) / 2`, with integer arithmetic.
A technological result is clamped between its negative complexity and zero. A highly magical owner therefore moves
a technological item toward zero effectiveness; a technological or neutral owner does not incur the weapon
malfunction penalty. The exact technological critical-failure chance is:

`-complexity * positiveMagickAptitude / 100`

and zero for non-technological items, invalid/non-critter owners, or aptitude at/below zero. Phase 1 exposes both
source calculations but applies the critical-failure rule only at the audited combat call site.

## Selected authentic vertical slice

| Item/rule | Source identity | Discipline/rank relationship | Target and cost | Depletion/effect | Why selected |
|---|---|---|---|---|---|
| Healing Salve | prototype 10079, effect 150 | Herbology item; use does not require knowing its recipe | damaged living non-mechanical critter, range 2, ordinary item-use cost 4 AP | one source charge, consumed; exact 20 HP heal | proves typed beneficial use, target eligibility, M3 consumption, M4 vitality, M8 AP, and M8H timing without treating technology as magic |
| Power Axe malfunction | prototype 6088, complexity -40, Smithy | weapon use does not require the Smithy recipe | existing M8 melee target/range and the weapon's ordinary attack AP | no extra consumption; qualifying aptitude roll reclassifies through supported M8 critical failure/self-hit | proves a genuinely technological combat rule while keeping hit, AP, damage, vitality, death, and turn authority in M8 |

The Healing Salve definition is immutable and source-derived: effect 150, range 2, magnitude 20, one charge, final-use
depletion, and organic damaged-living target. Retail `spelllist.mes` marks it friendly, No_Stack, item-triggered, and
excludes dead and mechanical critters. The effect has no persistent timer and no resistance roll. Technological
item effects are distinct from M10A spells even when they ultimately call the same M4 vitality owner.

Power Axe remains an ordinary M8 attack. M10B contributes only the source technological malfunction probability.
The qualifying roll occurs before dodge and reuses the already supported M8F critical-failure table/transaction;
the target receives no damage, the attacker receives the supported self-hit, and normal weapon AP/turn progression
remain unchanged. Weapons with zero technological failure chance draw no extra RNG sample and preserve their prior
behavior.

## Structured use and authority boundaries

`TechnologyUseRequest` carries only actor, item, and target ObjectIDs. Execution re-resolves current character,
vitality, item prototype, item placement/ownership, target state, mechanical flag, range/traversal, combat turn/AP,
and real-time scheduling state. The immutable `TechnologyItemDefinition` and typed result/failure values are separate
from request intent.

Every invalid actor, unavailable actor, missing/unsupported/unowned item, invalid/unavailable/full-health/mechanical
target, out-of-range traversal, wrong combat turn, insufficient AP, or unscheduled real-time attempt fails before AP,
item removal, vitality, turn, or scheduler mutation. A successful Healing Salve use asks M3 to remove the singular
item and record its tombstone, M8 to commit four AP when turn-based, and M4 to restore/cap HP. M10B stores no duplicate
quantity, charge, HP, AP, or turn state.

M8H owns real-time WHEN. Technology adds one typed scheduler action carrying the request; the production ART timing
source provides effect/recovery boundaries. Only the effect boundary invokes the same authoritative use transaction,
and READY/BUSY/exactly-once behavior remains M8H state. Presentation chooses the existing unarmed item-use animation;
no new clock or coroutine owns gameplay state.

M9 was not modified. The API is ObjectID-based and can support a future NPC/follower chooser, but Phase 1 does not
teach AI to select technology. No final technology/crafting UI was added.

## Device, explosive, firearm, and active-effect audit

Trap Springer prototype 15122/effect 184 was the candidate object-oriented family. The source contract targets a
locked portal/container at range 2, destroys the source at Begin, waits the configured duration, then invokes Use on
the target. The current bounded object authority does not yet represent the full locked-default-use/container result
required to prove that End action faithfully. It is therefore dependency-not-ready, not replaced with a synthetic
unlock or generic flag flip.

Explosives/grenades require authentic radius enumeration, mixed normal/fire damage, knockback, summoned fire or
other effect objects, and friendly-fire ordering. The current combat kernel has no general AoE transaction, so an
explosive was rejected for Phase 1 rather than reduced to single-target damage. Firearms already use M8 ranged
attacks; technological complexity/malfunction applies through the shared weapon rule without a second gun attack
system. Electrical, chemical, therapeutic, trap, and mechanical-device entries that fit the completed interfaces are
future content; entries needing AoE, status, world-device, poison, or script semantics wait for those authorities.

No selected Phase 1 technology creates a maintained or finite active effect. M10A's shared source-time service was
not changed, and Magic production code was not modified. A later technological timed effect may use that one clock,
but its identity and validation must remain technological.

## Save V1

Save format remains V1. The new optional `technology` domain stores only eight validated rank values for each known
character. The M4 derived record stores the learned-degree technological-point adjustment so shared aptitude restores
exactly. Existing item identities/tombstones and committed HP remain their M3/M4 save domains.

Older V1 documents without technology remain valid and reconstruct immutable source ranks when source objects load.
Restore validates identities, character ownership, uniqueness, array length, and rank range before swapping service
roots. It does not persist a use request, target preview, RNG, scheduler action, combat transaction, ART timing,
diagnostic, or presentation state. A pending real-time Healing Salve use therefore reloads as an owned, unconsumed
item with committed rank/aptitude/vitality intact and combat inactive.

## Validation

Focused M10B EditMode: **12/12**.

The focused set proves exact catalog/schematic identity; source/effective ranks; sequential INT/point gates; M4
aptitude mutation; exact Healing Salve healing/depletion; invalid actor, full-health, mechanical, and out-of-range
transactionality; exact four-AP turn use; M8H READY/BUSY/exactly-once resolution; effective-power/malfunction
arithmetic; the Power Axe M8 critical-failure transaction; and V1 rank/consequence restore with pending-use
normalization.

Directly affected older-system regressions: **206/206**.

- M3A inventory state **8/8**
- M3C equipment **13/13**
- M4B vitality **20/20**
- M4C progression **29/29**
- M4D derived stats **25/25**
- M6A session save/load **25/25**
- M6B migration/slots **32/32**
- M8B turn-based combat **23/23**
- M8F critical resolution **9/9**
- M8H real-time combat **22/22**

Those groups are the exact older owners changed or materially reused. M9 and M10A production code did not change and
were intentionally not rerun separately. The one complete suite remains the global regression gate.

Complete EditMode: **932/932** (prior **920** plus 12 focused tests), with **0 failed, 0 skipped, and 0
inconclusive**.

Computer Use drove the single already-open Unity 6000.0.71f1 Editor. The accepted production Play Mode proof used
the production PC, authentic Polar Bear Cub, retail prototypes 10079/6088, production navigation/inventory/
character/vitality/combat/save services, production ART timing, and the existing graphics rebuild. It proved:

1. Layman -> Novice Herbology eligibility, one point spent, M4 aptitude change, and schematic eligibility;
2. a full-health invalid use left the salve and vitality unchanged;
3. Healing Salve healed exactly 20, cost exactly four turn AP, reported one-to-zero charge depletion, and removed the
   item through M3;
4. real-time use entered BUSY, rejected duplicate scheduling, mutated only at the production effect boundary,
   resolved once, and became READY at recovery;
5. a deterministic positive-Magick-aptitude Power Axe malfunction used the M8F self-hit path, ordinary attack AP,
   normal turn ownership, and no bear damage;
6. Original -> Enhanced -> Original rebuild preserved rank, item, combat, and pending-action authority;
7. V1 load restored rank, aptitude, item, committed vitality, and presentation while normalizing combat and the
   pending technology action.

Accepted Play Mode result: **0 warnings, 0 errors**. Unity compilation was clean after the final runtime and harness
changes.

## Closure assessment

1. **Required for M10B closure:** none. The core now owns ranks/eligibility, aptitude queries, typed item use,
   transactional validation, M3/M4/M8/M8H composition, and optional V1 persistence, with two distinct authentic
   runtime families physically proved.
2. **Content using the completed runtime:** additional simple therapeutic/beneficial items and technological weapons
   whose semantics fit existing M3/M4/M8 boundaries.
3. **Deferred to M11C crafting/schematics:** found schematic acquisition, ingredient matching/consumption, product
   alias resolution, recipe browser, crafting transactions/economy, bulk production, and crafting presentation.
4. **Deferred to M12 UI:** final discipline, technology-use, targeting, item-status, and crafting HUD/UI.
5. **Deferred to AI/campaign integration:** NPC/follower item selection, authored campaign grants, merchant/economy
   flows, and follower technology policy.
6. **Source ambiguous/dependency not ready:** Healing Salve/Trap Springer recipe-product discrepancies; Trap Springer
   locked-object End/Use semantics; explosive AoE/friendly-fire/knockback/fire; poison/status/device/script-heavy
   effects; technology-specific eye-candy/audio; and any timed device whose end semantics lack an authority owner.

M10B should not remain open merely because every retail item has not been hand-authored. Those are content or named
dependency work over the completed kernel. This closure does not authorize M11.

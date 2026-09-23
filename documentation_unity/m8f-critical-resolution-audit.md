# M8F Bounded Critical Resolution Audit

## Scope

M8F adds authoritative critical classification to the existing M8B/M8C attack pipeline and implements one bounded critical-success family and one bounded critical-failure case. It does not add the broad injury, durability, weapon-breakage, alternate-target, AI, spell, technology, real-time, or presentation systems represented by the remaining original tables.

The recovered source was audited in `D:/OpenArcanum/Research/Repositories/arcanum-ce`, primarily:

- `src/game/skill.c`
- `src/game/combat.c`
- `src/game/combat.h`
- `src/game/criticals.h`
- `src/game/effect.c`
- `src/game/item.c`

## A. Critical classification

`skill_invocation_run` performs an ordinary 1-100 difficulty roll, followed unconditionally by a second 1-100 critical roll. A successful ordinary attack can become a critical success; a failed ordinary attack can become a critical failure. The combat skill formulas are:

- critical-success chance: `effectiveness / 20`
- critical-failure chance: `max(2, (100 - effectiveness) / 7)`

The original engine adds aimed-location, weapon magic, backstab, item-condition, and effect modifiers. The M8F fixtures have none of those modifiers, so the bounded implementation uses the exact zero-modifier formulas. Master melee suppresses critical failure, as in the source. Technological aptitude failure is a separate pre-skill roll and remains deferred.

The source classifies the attack before dodge. A successful dodge clears hit and critical state. Critical-dodge reclassification is outside this bounded slice, so a successful existing M8B/M8C dodge projects as an ordinary miss.

## B. Selected critical-success effect

The critical-hit path selects tables by weapon damage type and target body type. Before injury and equipment flags, the source makes independent secondary rolls for damage bonuses:

1. `roll <= chance + 10`: `CDF_BONUS_DAM_200` (3x post-resistance normal damage)
2. otherwise `roll <= chance + 30`: `CDF_BONUS_DAM_100` (2x)
3. otherwise `roll <= chance + 60`: `CDF_BONUS_DAM_50` (1.5x, integer truncation)

If the table produces no damage flag, the source falls back to `CDF_BONUS_DAM_50`. The bounded M8F implementation models this damage-only family with the fixture's zero modifier. It does not evaluate subsequent injury or equipment flags.

GREEN fixture: the production PC makes an unarmed melee attack against the authentic Polar Bear Cub. Ordinary roll `1`, critical roll `1`, base normal/fatigue damage rolls, then damage-table rolls `100, 100, 1` select `CDF_BONUS_DAM_50`. Resistance is applied before the critical multiplier, matching `combat_dmg`. AP and vitality continue through the existing authoritative services. The source excludes the damage-bonus prelude when an NPC attacks the PC; because the remaining injury table is deferred, that critical-success context is rejected transactionally instead of projecting the wrong bounded effect.

## C. Selected critical-failure effect

`combat_process_crit_miss` clears aim/critical, marks a critical miss, sets hit, and retargets the attack to the attacker. A secondary 1-100 roll of 50 or less re-enters critical-hit processing on the attacker and may also damage/destroy equipment or ammunition. A roll above 50 performs an ordinary self-hit.

GREEN fixture: the authentic unarmed Polar Bear Cub attacks the production PC. Ordinary roll `100`, critical roll at or below the source failure chance, then critical-failure effect roll `100` selects the ordinary self-hit. The PC is unchanged; damage/resistance and M4B vitality mutation apply to the bear exactly once. The attack consumes ordinary AP and advances the turn when AP reaches zero. There is no ammunition for this unarmed fixture.

The `<= 50` critical-miss branch is deliberately rejected transactionally as an unsupported critical effect. No AP, ammo, vitality, or turn state is changed when that deferred downstream effect cannot be represented.

## D-G. Deferred domains

- D, injury: stun, unconsciousness/knockout, blindness, scars, cripples, knockdown, and aimed-location consequences.
- E, durability: weapon/armor damage, drops, destruction, and ammunition explosions.
- F, special targeting: ally/nearby target selection and critical-dodge reclassification.
- G, presentation: combat log/script hooks, animation, sound, floating messages, and final combat UI.

## Authoritative RNG order

For the supported paths, `ICombatRandom` is the only RNG source. Every attack begins with the ordinary attack roll, critical classification roll, and (for an ordinary hit with a dodge chance) the existing dodge roll. A surviving hit then rolls normal damage, fatigue damage, and the critical-success damage-table rolls. A critical failure instead rolls its effect first, followed by the supported self-hit normal and fatigue damage rolls.

The critical-success table rolls occur after base damage has been calculated and before vitality mutation. The critical-failure effect roll occurs before its self-hit damage rolls, matching the source control flow.

## Transaction and lifecycle boundaries

M8F retains the existing preflight boundary. An unsupported bounded effect or unresolved lethal death script returns before AP, ammunition, vitality, death consequences, or turn advancement mutate. Supported critical successes and failures consume the same AP and ammunition as their ordinary attacks. M4B remains authoritative for HP/fatigue; M8D/M8E defeat and death consequences are reused and are not duplicated.

## Validation and completion (2026-09-22)

The final physical Play Mode proof used the production PC unarmed against the authentic Polar Bear Cub with injected deterministic `ICombatRandom` sequences. It verified:

- `CriticalSuccess` classification and source damage-only critical handling.
- resistance before the critical multiplier, including the selected +50% result and the inclusive +100% and +200% thresholds.
- the normal M8B unarmed attack AP cost, correct turn progression, and M4B-only vitality mutation.
- `CriticalFailure` from an ordinary bear miss with a qualifying critical roll and secondary effect roll `51`: the PC received zero damage, ordinary damage retargeted to the bear, normal AP was spent, and the turn advanced once.
- source-derived Master Melee suppression under otherwise qualifying deterministic failure rolls.
- transactional fail-closed behavior for injury/equipment, the unsupported `<= 50` critical-failure branch, ranged critical failure, and unsupported NPC-to-PC critical-success tables, with no AP, ammunition, vitality, or turn mutation.
- active-combat Original -> Enhanced -> Original presentation rebuilds without changes to combat authority, round, AP, turn, or vitality.
- a lethal unarmed critical against the authentic Greater Skeleton through M8D/M8E: exactly one death transition, same-identity corpse state, one 88-XP reward, one processed consequence marker, and no replay on a later processing attempt.
- combat end/restart after committed damage/death, followed by a Save V1 round trip that retained committed bear vitality, skeleton death/corpse state, XP, and the consequence marker while restoring no attack transaction, critical transaction, participants, turn, or AP.

The physical run completed with 0 warnings and 0 errors. Unity compilation was clean. The final automated results were:

- focused M8F: **9 passed / 0 failed / 0 skipped / 0 inconclusive**.
- required combat regressions: **53/53** — M8B 23, M8C 12, M8D 11, M8E 7.
- complete EditMode suite: **741 passed / 0 failed / 0 skipped / 0 inconclusive**.

The complete suite emitted five known intentional fail-closed dialogue-compatibility warnings and no errors. After clearing those expected test diagnostics, the final Unity Console was 0 warnings and 0 errors. `git diff --check` was clean. Save format remains V1, M8F is complete, and no subsequent milestone was begun.

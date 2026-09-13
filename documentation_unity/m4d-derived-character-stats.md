# M4D — Derived character statistics and alignment/reaction inputs

## Source audit (recorded before production changes)

The audit uses the read-only retail data mounted through `GameData` and the recovered engine sources in
`arcanum-ce/src/game/stat.h`, `stat.c`, `object.c`, `reaction.c`, `effect.c`, `resistance.h`, and `obj.h`. Source IDs
0–27 form the critter stat array; only IDs 8–16 are derived by `stat_base_get`. `stat_level_get` then applies special
environment/status logic, the general effect pipeline, and the final source clamp.

Classification: **A** can be calculated now from M4A/M4C inputs; **B** needs equipment/effects; **C** needs another
gameplay domain; **D** is stored authoritative source state; **E** needs deeper investigation.

| Source identity | ID / field | Stored or exact base formula | Source bounds / ordering | Class | M4D decision |
|---|---:|---|---|---|---|
| Carry Weight | stat 8 | `500 * effective Strength` | 300..10,000 | A | Already authoritative in M3E `InventoryCapacityService`; do not duplicate. |
| Melee Damage Bonus | stat 9 | `Strength - 10`; negative result uses integer division by 2; double at extraordinary Strength (effective >=20) | -50..50 after effects | A/B | Implement innate value with explicit neutral future-effect stage. |
| AC Adjustment | stat 10 | `effective Dexterity - 10` | -9..95 after effects | A/B | Implement typed adjustment. |
| Armor Class | common `OBJ_F_AC` field 26 | stored base AC + equipped armor + effects + AC Adjustment | final 0..95 | A/B/D | Implement stored base + AC Adjustment; equipment/effects remain neutral stages. |
| Speed | stat 11 | effective Dexterity; +5 at extraordinary Dexterity | 1..100 after Tempus Fugit, crippled legs, and effects | A/B/C | Implement innate value only; do not change navigation. |
| Heal Rate | stat 12 | `(effective Constitution + 1) / 3` | 0..6 after effects | A/B | Implement numeric query; do not schedule regeneration. |
| Poison Recovery | stat 13 | effective Constitution | 1..20 after effects | A/B/C | Implement numeric query; poison state/timers remain deferred. |
| Beauty Reaction Modifier | stat 14 | lookup `[-65,-52,-42,-33,-25,-18,-12,-7,-3,0,3,7,12,18,25,33,42,52,65,75]`; extraordinary Beauty uses `2 * (5 * Beauty - 50)` | -65..200 after effects | A/B | Implement the player-side Beauty input, not final NPC reaction. |
| Maximum Followers | stat 15 | effective Charisma / 4; +1 for Expert/Master Persuasion | 1..7 after effects | A/B/C | Implement typed input; no follower AI/party system. |
| Magick/Tech Aptitude | stat 16 | `(50 * effective Magick Points - 55 * effective Tech Points) / 10` + sector adjustment | -100..100 after effects | A/B/C/D | Retain stored point inputs and implement neutral-environment base aptitude; expose missing environment/effect stages explicitly. |
| Level / XP | stats 17/18 | stored | 0..51 / 0..2,000,000,000 | D | Already owned by M4C; do not duplicate. |
| Alignment | stat 19 | stored; negative is evil, zero neutral, positive good | -1000..1000 after effects | B/C/D | Implement authoritative stored value plus clamped controlled set/adjust; no event-driven consequences. |
| Fate Points | stat 20 | stored | 0..100 | C/D | Defer to fate gameplay. |
| Unspent Character Points | stat 21 | stored | 0..56 | D | Already owned by M4C. |
| Magick Points / Tech Points | stats 22/23 | stored aptitude inputs | 0..210 | B/C/D | Retain as immutable source inputs for neutral base aptitude; acquisition/effects remain deferred. |
| Poison Level | stat 24 | stored mutable status | 0..1000 | C/D | Defer poison application, immunity, damage, recovery, and timers. |
| Age | stat 25 | stored | 20..1000 | C/D | Defer until a consumer requires it. |
| Gender / Race | stats 26/27 | stored | 0..1 / 0..10 | D | Already owned by M4A. |
| Damage/Fire/Electrical/Poison/Magic Resistance | common field 31, indices 0..4 | stored base; equipment; effects; Poison adds `max(0, 5 * (Constitution - 4))` | normally 0..95; monstrous NPCs bypass final resistance clamp | A/B/D | Implement stored innate values, Constitution poison term, audited innate race effects, and neutral equipment/effect stages. |
| NPC Reaction Base | NPC field 295 | stored, default 50; instance overrides prototype | intentionally not clamped in `reaction_get` | B/C/D | Retain authoritative source input. |
| Pairwise initial reaction inputs | `reaction.c` | NPC base + PC Beauty modifier + NPC-race/PC-race table + reputation + effects/memory | final system has no simple clamp | A/B/C/D | Expose the currently authoritative components and subtotal only; defer reputation, memory, shitlist, faction, mind control, and social consequences. |
| Initiative / action points | consumers of Speed | combat reads Speed | combat/animation domain | C | Speed only; no initiative or turn loop. |
| Critical hit/failure modifiers | effect/weapon/combat fields | not a single derived critter stat | equipment/effect/combat domain | B/C | Defer. |
| HP/Fatigue maxima and damage | object/vitality fields | M4B formulas/state | existing M4B bounds | A/D | Already owned by M4B; do not duplicate. |

### Ordering and boundaries

For admitted derived values, M4D preserves the source order: stored/attribute-derived base, then audited innate race
contribution where applicable, then explicit neutral future equipment/environment/effect stages, then the source final
clamp. This slice does not pretend that the neutral stages are implemented gameplay.

Alignment is source-stored history state, not derived from a kill/quest ledger. Source mutation clamps it and then
triggers follower/UI consequences; M4D admits only the stored value and clamp because those consumers do not exist.

`reaction_get` is pairwise. The bounded input subtotal is NPC reaction base plus the PC Beauty reaction modifier and
the exact NPC-race/PC-race bias. Alignment is not directly part of this recovered formula. Reputation, per-PC NPC
memory, effect modifiers, shitlists, mind control, faction/AI policy, and translation into social behavior remain later
domains.

### Smallest proposed change set

1. Surface common base AC field 26 alongside the already parsed resistance array and NPC reaction base.
2. Add typed derived-stat/resistance identities, immutable source inputs, persistent alignment state, and a session-owned
   deterministic query service keyed by `ArcanumObjectId`.
3. Initialize production PC source defaults explicitly and real NPCs from whole-field instance-over-prototype values.
4. Reuse M4A effective attributes and M4C level/skills/training; keep M3E carry weight and M4B vitality as their existing
   authorities.
5. Add focused tests, retail source audit tooling, and a Play Mode lifecycle validator without changing navigation,
   equipment behavior, combat, dialogue, magic/technology gameplay, or presentation ownership.

## Implemented runtime contract

`WorldMapSessionCoordinator.DerivedStats` owns one `CharacterDerivedStatService`. The service stores exactly one
`PersistentCharacterDerivedState` per persistent PC/NPC ObjectID. That state retains immutable, resolved source inputs
and the one admitted mutable value, Alignment. Unity `GameObject`, `WorldObject`, sprite-owner, navigation, and demo
components neither own nor cache any M4D value.

The typed `CharacterDerivedStat` enum preserves source IDs 8–16. `CarryWeight` delegates through
`ICharacterCarryWeightProvider` to the existing `InventoryCapacityService`; M4D does not duplicate its formula or
authority. `CharacterResistance` preserves the source order Normal, Fire, Electrical, Poison, Magic (0–4).

Real critters initialize in this order:

```text
ObjectInstanceReader / ObjectProtoReader
  -> whole-field instance-over-prototype source resolution
  -> CharacterStatService + CharacterProgressionService
  -> CharacterDerivedSource
  -> WorldMapSessionCoordinator.DerivedStats
  -> deterministic queries

Unity presentation -> reads session identity/state only; owns no gameplay value
```

Common `OBJ_F_AC` field 26 is now surfaced as `BaseArmorClass`; the existing resistance array, reaction base, NPC
flags, critter flags, and stat array provide the other source inputs. Missing AC/resistance values default to zero and
missing NPC reaction base defaults to 50. Instance arrays replace prototype arrays as whole fields, matching the
existing character/progression precedence contract. A reload with changed source metadata fails explicitly instead of
silently replacing persistent state.

The production Human Male PC has an explicit development source: base AC/resistances/alignment/magick points/tech
points zero and reaction base 50. Its normal session identity remains separate from these gameplay values and from its
presentation ART identity.

### Implemented formulas and clamps

All formulas use M4A effective attributes. The service calculates on demand, so Race/Gender and M4C training changes
cannot leave stale derived values.

| Query | M4D formula | Final source clamp |
|---|---|---:|
| Melee Damage Bonus | `STR - 10`; negative result `/ 2` with C integer truncation; double when effective STR >= 20 | -50..50 |
| AC Adjustment | `DEX - 10` | -9..95 |
| Armor Class | stored base AC + AC Adjustment; equipment/effects are currently neutral | 0..95 |
| Speed | effective DEX, plus 5 when effective DEX >= 20; environment/status/effects neutral | 1..100 |
| Heal Rate | `(CON + 1) / 3` | 0..6 |
| Poison Recovery | effective CON | 1..20 |
| Beauty Reaction Modifier | exact 1–19 lookup; `2 * (5 * Beauty - 50)` at extraordinary Beauty >= 20 | -65..200 |
| Maximum Followers | `CHA / 4`, plus 1 at Expert or Master Persuasion | 1..7 |
| Magick/Tech Aptitude | `(50 * effectiveMagickPoints - 55 * effectiveTechPoints) / 10`; sector/effects neutral | -100..100 |

Audited innate aptitude effects are Dwarf +3 Tech, Elf +3 Magick, Half-Elf +1 Magick, and Dark Elf +3 Magick.
Audited innate resistance effects are Half-Orc +10 Poison, Half-Ogre +10 Normal, and Orc +20 Poison. Poison resistance
also adds `max(0, 5 * (effective CON - 4))`. Normal PCs/NPCs clamp each final resistance to 0..95; monstrous NPCs retain
the recovered engine's no-final-clamp exception. Equipment, spell, temporary-effect, Tempus Fugit, crippled-leg, and
sector-aptitude stages remain explicitly neutral and deferred.

## Alignment, aptitude, and reaction boundaries

Alignment is stored session state initialized from stat slot 19 (instance stat array before prototype), with neutral
zero, evil negative, good positive, and range -1000..1000. `SetAlignment` and `AdjustAlignment` clamp atomically and
emit `AlignmentChanged` only for an actual change. Quest, kill, follower, and UI consequences are not implemented.

Magick and Tech Points remain immutable resolved inputs from stat slots 22 and 23, each clamped to 0..210 before the
audited innate race effect. M4D exposes stored/effective point queries and the neutral-environment aptitude result; it
does not add spells, technology disciplines, crafting, item aptitude effects, or sector aptitude data.

`GetReactionInputs(npcId, pcId)` is deliberately not a complete reaction system. It returns the NPC source base,
the PC Beauty modifier, the exact 11-by-8 NPC-race/PC-race matrix contribution, whether character modifiers apply, and
their subtotal. Aloof or monstrous NPCs suppress the Beauty/race inputs exactly as recovered. Alignment is not directly
part of `reaction_get`; reputation, faction policy, memory, temporary reaction, shitlists, mind control, and dialogue
outcomes remain deferred.

## Golden fixtures

The production PC and authentic retail NPC were validated with these exact results:

| Value | Human Male production PC, Level 2 after controlled XP | `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, proto 17101 |
|---|---:|---:|
| Carry Weight | 4000 | 4500 |
| Melee Damage Bonus | -1 | 0 |
| AC Adjustment / final AC | -2 / 0 | -1 / 0 |
| Speed | 8 | 9 |
| Heal Rate / Poison Recovery | 3 / 8 | 5 / 16 |
| Beauty Reaction Modifier | -7 | 0 |
| Maximum Followers | 2 | 2 |
| Aptitude | 0 | -5 |
| Alignment | 0 | 100 |
| Normal/Fire/Electrical/Poison/Magic resistance | 0/0/0/20/0 | 0/0/0/60/0 |

The NPC's initial bounded reaction toward the Human production PC is `50 + (-7) + 0 = 43`.

## Validation completed

- Source audit tool: retail effect rows 64–74 and 330 plus nine notable critters; fixture source fields matched.
- Focused M4D EditMode: **25/25 passed**, 0 failed/skipped/inconclusive.
- Required regression matrix: **227/227 passed** across M4C, M4B, M4A, M3E–M3A, M2B–M2A,
  PlayerNavigation, M1A, M1B, WorldSessionState, and PortalArtResolver.
- Complete EditMode: **423/423 passed**, 0 failed/skipped/inconclusive.
- Unity compilation: **0 errors**.
- Computer Use Play Mode: production PC and real fixture rendered and resolved; controlled Race/Gender mutation updated
  only dependent queries and restored exactly; controlled XP established Level 2 without changing the admitted
  non-Level-dependent M4D subset; authentic inventory transfer/equip/unequip remained neutral; state and reference
  identity survived Original→Enhanced→Original, NPC reload, PC A→B→A, and presentation recreation. All four character
  registries held the same 14 unique identities, with one production PC/presentation/navigation binding and one sprite
  owner per persistent identity. **0 new warnings, 0 errors**.

## Deferred work and next milestone

Deferred candidates remain: equipment/background/spell/status modifiers, sector aptitude, poison application and
timers, regeneration scheduling, encumbrance consequences, initiative/action points, critical modifiers, follower AI,
complete reaction/social policy, dialogue, combat, character UI, and save serialization.

The exact recommended next task is **M5A — production script-host state foundation and one minimal real
focus-object→dialogue→quest-transition vertical slice**. Begin with a corpus/source audit of required focus-object,
global/local-state, dialogue, quest, and journal operations, then admit only the smallest strict-telemetry path needed
for one start-to-finish quest. Do not begin combat, broad opcode support, or save serialization in that slice.

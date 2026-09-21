# M8B Turn-Based Combat Source Audit

M8B is a bounded implementation of turn-based AP transactions, local combat
movement, one ordinary unarmed melee attack, hit/miss resolution, and damage
through the existing M4B vitality owner. Original data under
`D:/OpenArcanum/Source/Steam-Clean` is read only.

## Source baseline

The audit uses `arcanum-ce` commit
`a7ff41b300ef712f0e7d088183a3d08957110cdd`, principally
`src/game/combat.c`, `anim.c`, `skill.c`, `item.c`, `stat.c`, `object.c`,
`critter.c`, `damage_type.h`, and `resistance.h`.

### A. AP transaction and turn lifecycle

- `combat_turn_based_whos_turn_set` resets AP to effective Speed, minimum 5.
- `combat_consume_action_points` subtracts only for the current participant.
  Ordinary sufficient spends never underflow. A local PC with positive AP and
  more than one current fatigue can overdraw one action: it takes 2 fatigue
  damage and AP becomes zero. Otherwise an insufficient spend fails and the
  source advances the subturn.
- `combat_turn_based_end_critter_turn` sets AP to zero and advances immediately.
  Exact AP exhaustion advances after the current animation completes. The
  synchronous M8B command boundary performs that deferred advancement after
  committing the admitted action.
- The participant list advances in source order. Exhausting the final
  participant begins a new round and resets the first eligible actor's AP.

### B. Combat movement

- `combat_move_cost` is the source path length multiplied by 2. A local PC with
  the global always-run preference enabled uses multiplier 1. Cardinal and
  diagonal route entries are each one path step; diagonal geometry does not add
  a second AP unit.
- The animation goal consumes AP as steps execute. A non-PC route must pass the
  full-path affordability check. A PC overdraw admitted by the fatigue rule can
  stop partway when AP reaches zero.
- The same source pathfinding, terrain, object, wall, portal, and diagonal
  corner restrictions apply. M8B reuses `SectorNavigationMap` and
  `DeterministicTilePathfinder`; it does not introduce NavMesh movement.
- M8B exposes a synchronous authoritative move result. Presentation shows the
  final step facing with WALK followed by STAND, while the session owns the
  committed tile.

### C. Attack eligibility

- The source rejects inactive, dead, unconscious, stunned, paralyzed, or stoned
  critters before action execution. M8B uses the M8A eligibility subset that is
  actually represented: participant presence, loaded/off state, M4B HP and
  fatigue, and stunned/paralyzed flags.
- The attacker must be the current participant and the target must be a distinct
  active participant. The selected unarmed melee mode has source range 1.
- `combat_attack_cost` reads effective weapon speed. No weapon returns speed 10,
  so the formula `8 - speed / 3` gives **5 AP**. Weapon speeds over 20/24 use the
  special 2/1 AP tiers. M8B audits those tiers but implements only the selected
  unarmed path.

### D. Hit resolution

- Unarmed combat uses `SKILL_MELEE`, governed by Dexterity. Effective rank is
  the existing M4C value; monstrous melee begins at 20 and is still capped by
  the Dexterity rank table.
- Normal-difficulty melee effectiveness is `5 * effectiveMelee + 25`.
- For an ordinary non-called melee attack, Armor Class contributes
  `effectiveness * (AC / 2) / 100` source difficulty using integer arithmetic.
  A `1..100` roll hits when `difficulty + roll <= effectiveness`. The effective
  hit chance is therefore that threshold clamped to the roll domain.
- A successful attack check is followed by a separate Dodge invocation. Dodge
  effectiveness is `5 * effectiveDodge`, clamped to 95 by the source skill
  flags; a successful Dodge converts the attack to a miss.
- Weapon to-hit, minimum-Strength penalty, called shots, lighting, unaware-target
  modifiers, backstab, cover/range, aptitude failure, Fate, and critical tables
  are outside the selected fixture and remain deferred. The admitted M8B query
  reports melee effectiveness, AC difficulty, attack chance, Dodge chance, and
  combined final chance explicitly.

### E. Damage resolution

- `item_weapon_damage` uses `OBJ_F_NPC_DAMAGE_IDX` for an unarmed monstrous
  critter. Non-monstrous bare hands use source normal/fatigue range 1..5.
- Effective Strength supplies `STAT_DAMAGE_BONUS`: `Strength - 10`, with a
  negative value divided by 2 using truncation toward zero. It adjusts both
  normal and fatigue melee ranges, retaining a minimum positive result when a
  negative bonus would cross zero.
- Each admitted damage type rolls inclusively between its adjusted bounds.
  Normal resistance reduces normal damage by `resistance * damage / 100`.
  Fatigue first uses three quarters of normal resistance, then the same
  percentage reduction.
- Normal damage mutates only M4B HP damage; fatigue damage mutates only M4B
  fatigue damage. `CombatStateService` owns neither value. A miss spends the
  admitted attack AP but deals no damage.

### F-I. Explicitly deferred

Critical success/failure tables, Fate, weapon wear, scripts, death/corpse/loot
presentation, unconscious presentation, AI, perception-driven participant
collection, real-time scheduling, broad combat animation sequencing, and combat
UI remain outside M8B. Reaching HP or fatigue zero is retained by M4B and makes
the participant ineligible; later milestones own the broader consequence.

## GREEN fixture

The attack proof reuses the real hostile Polar Bear Cub from M8A and attacks the
production development PC:

| Field | Value |
| --- | --- |
| Attacker | Polar Bear Cub |
| Attacker ObjectID | `G_9B807B01_A142_4949_80CE_5A085F3BEEB1` |
| Attacker prototype | `28422` |
| Sector | `maps/arcanum1-024-fixed/47781512457.sec` |
| Defender | production development PC |
| Weapon state | unarmed |
| Governing skill | Melee |
| Bear attributes | Strength 7, Dexterity 4 |
| Effective bear Melee | 3 (monstrous 20 capped by Dexterity 4) |
| PC Armor Class | 0 |
| PC Dodge | 0 |
| Expected attack chance | 40% before and after Dodge |
| Expected attack range | 1 tile |
| Expected attack AP | 5 |
| Bear turn AP | 5 |
| Damage source | prototype `OBJ_F_NPC_DAMAGE_IDX` normal 3..6, all other types 0..0 |
| Adjusted normal damage | 2..5 after Strength bonus -1 |
| Defender normal resistance | 0 |

The production Formats parser retains the authentic ten-value source array
`3,6,0,0,0,0,0,0,0,0`; the focused fixture uses those values directly.

## Transaction boundary

`CombatStateService` remains authoritative and accepts stable ObjectIDs only:

- `MoveInCombat(actor, destination)` validates the active/current actor, source
  route, budget, and presentation binding before committing tile/AP/facing.
- `Attack(actor, target, BasicMelee)` validates every actor/target/range/AP
  condition before spending AP. An admitted attack then spends AP, rolls through
  an injected session-owned random source, and applies M4B damage.
- `EndCurrentTurn(actor)` remains the explicit end-turn command.

Validation failures preserve AP, vitality, positions, participant order, round,
and current actor. The default random source does not use `UnityEngine.Random`;
focused tests replace it with a deterministic sequence.

Combat movement also treats every other registered combat actor as occupied,
including the controlled PC whose tile is deliberately omitted from ordinary
static navigation blockers. The route finder retains its existing public
non-combat behavior and accepts this additional occupancy predicate only at the
combat command boundary.

## Physical production validation

Computer Use drove the existing Unity 6000.0.71f1 editor and production
`TestTerrain` composition root. The validation harness used the authentic Polar
Bear Cub sector and production PC; it did not mutate vitality directly.

- Combat admitted the stable order bear -> PC. The bear began with 5 AP.
- A one-step bear move cost 2 AP, ended in the correct facing with WALK then
  STAND presentation, and an over-budget NPC route failed atomically.
- Injected rolls `[1,5]` produced the expected 40% hit, raw/mitigated normal
  damage 5/5, PC HP 30 -> 25, one M4B mutation, bear AP exhaustion, and advance
  to the PC. Injected roll `[100]` produced a miss that spent 5 AP, caused zero
  HP/Fatigue damage, and advanced the turn.
- PC walking cost 2 AP per step, always-run cost 1 AP per step, and the admitted
  final overdraw step applied exactly 2 Fatigue before advancing the turn.
- Repeated bear -> PC -> next-round bear transitions reached round 4 with exact
  AP reset, stable order, and no duplicate round increment.
- Inactive, out-of-turn, insufficient-AP, out-of-range, invalid-target,
  unsupported-mode, occupied/blocked movement, over-budget movement, and
  dead/unconscious actor or target failures preserved AP, HP, Fatigue, position,
  current actor, and round.
- Original -> Enhanced -> Original rebuild while combat was active preserved
  participants, current actor, round, AP, vitality, identity, and unique
  gameplay/presentation owners; combat continued afterward.
- `EndCombat` retained M4B damage and restored ordinary navigation and
  interaction. Restarting combat retained that same authoritative damage.
- Saving during combat retained Save V1 and excluded transient combat. Load,
  sector unload, and cross-map transition normalized combat to Inactive while
  preserving stable identity and persistent vitality.

The physical run passed with **0 warnings and 0 errors**. The required regression
matrix passed **347/347**: M8A 22, M7E 15, M7D 16, M7C 14, M7B 27, M7A 24,
M6C 26, M6B 32, M6A 25, M4B 20, M4C 29, M4D 25, PlayerNavigation 21,
M2A 16, M2B 8, and WorldSessionState 27. Focused M8B passed **23/23** after
adding the combat-actor occupancy regression. The complete EditMode suite passed
**702/702**, with 0 failed, 0 skipped, and 0 inconclusive. Its final console had
6 known compatibility warnings and 0 errors; no warning occurred in the
physical M8B proof. Final compilation was clean.

## Remaining M8 limitations and recommended M8C

M8B intentionally has no equipped-weapon attacks, ranged line of sight,
ammunition transaction, called shots, critical success/failure, Fate, weapon
wear, death/corpse/loot/XP consequences, combat AI, broad animation sequencing,
real-time scheduling, or player-facing combat UI.

The exact next separately authorized milestone is **M8C: one bounded equipped
ranged-weapon attack and ammunition transaction path**. It should audit and
implement source range/line-of-sight, weapon attack speed and to-hit modifiers,
one authentic projectile/ammunition pairing, atomic ammunition consumption,
and the existing M4B damage handoff, with seeded hit/miss and rollback proof.
Critical tables, death/loot/XP, AI, spells, and real-time combat remain later
work unless separately authorized.

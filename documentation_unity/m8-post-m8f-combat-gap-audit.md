# Post-M8F Turn-Based Combat Gap Audit

Date: 2026-09-23
OpenArcanum baseline: `5324f9121bd8e46187911aaa4dc8e6503c8b65cd`
Original-source mirror: `D:\OpenArcanum\Research\Repositories\arcanum-ce` at
`a7ff41b300ef712f0e7d088183a3d08957110cdd`

## Decision

**M8G is required before real-time combat.** The appropriate milestone is a bounded **Turn-Based Combat Kernel
Closure**, not a broad combat expansion. M8A-M8F form a coherent vertical slice, but four source-backed seams still
shape combat authority strongly enough that adding a real-time scheduler first would duplicate or freeze the wrong
contracts:

1. source-shaped roster growth, engagement, and round-boundary authority;
2. a structured attack request and modifier breakdown, including called locations;
3. hard line-of-fire versus numeric cover;
4. supported Bow and Dodge training behavior that can produce more than one authoritative resolution.

M8G should close only those seams. It must not add AI decisions, followers, equipped melee weapons, firearms,
throwing, explosives, magic, technology, combat UI, projectile presentation, critical injuries/equipment damage, or
real-time scheduling. Save format remains V1 and active combat remains transient.

## Method and scope

This was a read-only comparison of the committed M8A-M8F implementation and audits against the original-source
mirror. The source paths cited below are relative to `src/game/` in that mirror. The OpenArcanum side was inspected in
`Assets/_OpenArcanum`, including the production combat coordinator/services, navigation, vitality/death consequence,
Save V1 normalization, and the M8A-M8F validation fixtures.

The classifications mean:

- **REQUIRED BEFORE REAL-TIME**: an authoritative contract that both turn-based and real-time combat must share, or
  a defect in an already-supported M8A-M8F path.
- **SAFE TO DEFER**: authentic behavior, but outside the supported unarmed/Bow slice or implementable later without
  replacing the shared combat kernel.
- **NOT SUPPORTED / NOT PRESENT**: no matching general mechanic was found in the audited source; it must not be
  invented as a compatibility requirement.

This audit does not start M8G and makes no production-code or test changes.

## Required before real-time combat

### 1. Dynamic combat roster and engagement authority

**Source behavior and evidence.** `combat.c:3106` (`combat_turn_based_start`) derives a combat-perception range of at
least 10 tiles from the PC's Perception, collects nearby PC/NPC critters, sorts them, and begins the first turn.
`combat.c:3183` (`combat_turn_based_begin_turn`) repeats collection at each round boundary and adds newly eligible
critters to the existing list. Inactive critters are skipped. `combat.c:1173` (`combat_can_exit_combat_mode`) gates
exit from a critter's active combat focus, not merely its static KOS flag.

**Current OpenArcanum.** `CombatStateService.StartCombat` registers the initiating PC and one hostile target.
Additional actors require explicit `RegisterParticipant`; removal is explicit or death-derived. `EndCombat` is blocked
by an eligible source-hostile NPC participant, using static source hostility as the bounded proxy.

**Mismatch.** A fight does not discover a nearby combatant at start or on a later round, and static KOS is not the
same thing as runtime engagement/focus. A real-time scheduler built now would need a second population and exit model.

**Minimum M8G boundary.** Add coordinator-owned transient roster discovery at combat start and each turn-based round
boundary, using the source PC-perception square and loaded PC/NPC objects. New eligible actors may join; actors are not
removed merely for leaving the discovery radius. Continue skipping inactive actors and retain deterministic ordering
with the PC tail. Introduce a transient engagement/focus relation with explicit later-facing APIs for engage,
disengage/surrender, and removal. Do not add AI decisions, party/follower behavior, perception stealth, or autonomous
movement.

**Dependencies.** Loaded object registry, runtime positions, critter flags/status, PC Perception, stable ObjectID, and
the existing combat coordinator.

**Save change.** None. Roster and engagement remain transient under Save V1 normalization.

**Authority versus presentation.** Discovery membership, engagement, eligibility, and ordering are authority.
Selection rings, threat indicators, and combat UI are presentation and remain deferred.

### 2. Round boundary and original game-time hook

**Source behavior and evidence.** `combat.c:3322` (`combat_turn_based_end_turn`) advances the game clock by exactly
1,000 milliseconds when a round finishes, then begins the next round. Poison and fatigue recovery elsewhere use the
same game-time event system.

**Current OpenArcanum.** Turn advancement and AP refresh are authoritative, but there is no typed combat-round time
advance owned by the coordinator.

**Mismatch.** Real-time and turn-based modes would otherwise acquire separate time side-effect contracts. Adding
poison or recovery first would also have nowhere authoritative to subscribe.

**Minimum M8G boundary.** Publish one authoritative, exactly-once round-completed event or injected clock operation
carrying a fixed 1,000 ms delta. Test wraparound, inactive participants, and exact-once emission. Do not implement a
new world clock, day/night, poison, healing, fatigue recovery, or scheduled effects in M8G.

**Dependencies.** Combat turn coordinator and, if available, the future/shared game-time owner.

**Save change.** None for combat. Any already-authoritative world clock remains independently owned.

**Authority versus presentation.** The boundary and delta are authority. Animation pacing is presentation.

### 3. Structured attack request, called location, and modifier ledger

**Source behavior and evidence.** `combat.c:2321` defines hit-location penalties: torso 0, head -50, arm -30, leg
-30. The combat setup path marks an explicit non-torso aim; an ordinary torso/default attack resolves a weighted
location (70% torso, 15% leg, 10% arm, 5% head). `skill.c:2210` (`skill_invocation_difficulty`) applies the location,
awareness, light, distance, and cover terms. Location also changes critical-success chance: head +10 percentage
points and arm/leg +6 through the source penalty-to-critical conversion.

**Current OpenArcanum.** `Attack(actor, target, CombatAttackMode)` carries only actor, object target, and the two
bounded modes `BasicMelee`/`BasicRanged`. Hit chance is computed directly from skill, AC, strength/range, and Dodge.
There is no hit location or inspectable modifier ledger.

**Mismatch.** Called shots are a fundamental property of the attack command, not merely future UI. Adding real-time
command scheduling before the request shape is stable would force replacement of queued attacks and replay/debug
contracts. The current scalar calculation also has no safe source-shaped seam for deferred awareness, lighting,
backstab, Fate, scripts, magic, or technology.

**Minimum M8G boundary.** Replace or wrap the bounded call with an immutable structured attack request containing the
object target, attack family, and optional location. Implement torso/default plus head/arm/leg penalties, default
weighted location resolution, and the source critical-chance adjustments. Return a typed, ordered modifier ledger
whose empty/provider slots can later host lighting, awareness/backstab, magic/tech, Fate, and scripts. Do not
implement those deferred domains. Unsupported critical injury/equipment results must retain M8F's transactionally
fail-closed behavior before AP, ammunition, vitality, death consequence, or turn mutation.

**Dependencies.** Deterministic combat RNG, current M8F roll ordering, skill/training snapshots, and combat result
types.

**Save change.** None. A pending request/transaction must not be serialized in Save V1.

**Authority versus presentation.** Requested/resolved location and every numeric modifier are authority. Aiming UI,
body-part overlays, floating text, and hit animation are presentation.

### 4. Numeric cover distinct from hard line-of-fire

**Source behavior and evidence.** `skill.c:2379` asks `ai.c`'s `sub_4ADE00` for a straight path. A returned blocking
object makes the attack impossible; otherwise positive traversal cost is added as numeric difficulty. Critters are
ignored by this preflight. Later projectile travel can interact with an intervening object, but that is a separate
impact/presentation path.

**Current OpenArcanum.** `SectorNavigationMap.HasProjectileLineOfFire` provides a binary clear/blocked answer and
M8C correctly handles doors and hard blockers. It does not return a traversal/cover penalty.

**Mismatch.** A supported Bow attack through traversable cover currently receives no source penalty. A real-time
mode must share the same authoritative attack difficulty rather than infer it from projectile presentation.

**Minimum M8G boundary.** Add a single navigation query returning hard block plus accumulated numeric cover cost,
and feed it into the attack modifier ledger. Preserve current hard-block behavior. Cover cost must be deterministic
and derived only from available authoritative navigation/object data; where original traversal metadata has no
OpenArcanum equivalent, fail closed or document the bounded mapping rather than inventing visual heuristics.

**Dependencies.** Navigation cells/portals, object traversal metadata mapping, and the structured attack request.

**Save change.** None.

**Authority versus presentation.** Block/cover cost is authority. Projectile arcs, occlusion effects, and cover UI
are presentation.

### 5. Supported Bow mastery and multi-resolution transaction

**Source behavior and evidence.** `skill.c:2369` exempts a Master of Bow from the long-range Perception penalty.
`combat.c:1057` (`combat_process_ranged_attack`) creates two arrows for Bow Expert or Master (`num_arrows = 2` at
lines 1107-1113) while the attack consumes one ammunition unit. `combat.c:548` reconstructs and resolves combat at a
projectile impact, so the two projectiles can produce two damage resolutions. The source offsets their trajectories;
the exact collision/presentation details are separate from the authoritative fact that one attack can resolve twice.

**Current OpenArcanum.** M8C applies its long-range penalty regardless of Bow mastery and produces one
`CombatAttackResult` with at most one damage mutation per attack. AP and one arrow are consumed transactionally.

**Mismatch.** These are defects in the already-supported Bow family. More importantly, the single-result assumption
cannot represent source multi-hit behavior or guarantee exact-once M8D/M8E consequences when one attack has multiple
impacts.

**Minimum M8G boundary.** Make Bow Master ignore the source long-range Perception penalty. For Expert/Master, resolve
two ordered authoritative impacts under one attack transaction, one normal attack AP cost, and one source ammunition
consumption. Return an aggregate result with ordered subresults. Stop or safely no-op subsequent vitality mutation
after death as appropriate, while guaranteeing exactly one death transition, corpse projection, XP award, and
processed marker. Preserve rollback before any mutation when preflight fails. Defer projectile objects, offsets,
alternate-object collision, visuals, sounds, and animation timing.

**Dependencies.** Structured request/result, deterministic RNG sequence, M4B vitality, M8D defeat, M8E death
consequence, and M8F critical resolution.

**Save change.** None. Neither the aggregate attack nor either impact is persisted.

**Authority versus presentation.** Number/order of impacts, AP/ammo cost, rolls, and damage are authority. Two visible
arrows and their flight/impact timing are presentation.

### 6. Critical Dodge reclassification

**Source behavior and evidence.** In `skill.c`, a successful Dodge clears the attack hit/critical result. A critical
Dodge can then reclassify the attack as a critical miss according to training thresholds 0/10/50/100. This is not a
generic dodge animation effect; it feeds critical-failure resolution.

**Current OpenArcanum.** A successful Dodge always produces an ordinary miss. Dodge skill and training already exist
in the supported attack calculation.

**Mismatch.** This is a source-backed defect in current unarmed/Bow resolution and connects directly to M8F's
critical-failure transaction.

**Minimum M8G boundary.** Preserve source roll order and implement critical-Dodge reclassification with the
0/10/50/100 training table. Route only M8F-supported critical-failure results; unsupported injury, equipment, or
NPC-to-PC table branches must fail closed before all mutation.

**Dependencies.** Structured attack resolution, Dodge training, deterministic RNG, and M8F.

**Save change.** None.

**Authority versus presentation.** Reclassification and downstream effect are authority. Dodge/hit reactions are
presentation.

## Safe to defer

| Gap | Source behavior / evidence | Current mismatch | Why it can wait and minimum later boundary | Save / ownership |
|---|---|---|---|---|
| Equipped melee weapons and weapon families | Source AP cost uses effective weapon speed: >24 costs 1, >20 costs 2, otherwise `max(1, 8 - speed/3)` (`combat.c:3506`). Firearms, throwing, grenades, and explosives have family-specific paths. | Only unarmed melee and Bow are supported; the weapon data already exposes the authentic speed cost formula. | Add by family with equipment, ammo/projectile, skill, and damage contracts. Do not genericize from Bow. | Equipment/ammo remain domain authority; pending attacks remain transient. |
| Throwing, firearms, grenades, explosives, and area effects | Thrown misses can scatter; boomerangs return; explosions can affect several/friendly targets. | No corresponding attack modes. | Later equipment/technology milestones can use M8G's structured request and multi-resolution result. | May need committed ammo/item placement changes, not combat transaction persistence. |
| Elemental, poison, and broader damage types | `combat_dmg` covers normal, electrical, fire, poison, and fatigue with resistances and scheduled effects. | M8A-F intentionally support normal/fatigue in the bounded slice. | Add with source resistances and a real game-time/event owner; do not encode them in presentation. | Persistent vitality/status belongs to character/world domains. |
| Critical injuries, equipment damage, knockout, knockdown, stun, scars, blindness, and crippling | Source critical tables apply location/training-specific status and equipment effects. | M8F supports damage-only success and ordinary self-hit failure, and otherwise fails closed. | Later status/equipment milestones may fill typed critical effects. M8G must retain atomic fail-closed preflight. | Durable injury/equipment state may require a future save-version decision. |
| Backstab, facing, awareness, light, stealth/prowling | Source uses facing, target awareness, light, and Backstab training; Expert expands weapon families and Master changes armor/critical behavior. Sleeping/unconscious/paralyzed and prone/stunned targets have source difficulty modifiers. | No facing/awareness/light/backstab authority in current combat. | Add after those world/skill/equipment authorities exist; reserve typed modifier providers in M8G only. | Likely world/actor state, not transient attack state. |
| Combined move-then-melee command | Source `combat_check_attack` includes movement cost when the melee target is not adjacent. | OpenArcanum currently supports authoritative movement and attack as separate commands. | This is action composition/UI scheduling, not a missing damage rule. Compose later without changing the kernel. | No save change. |
| Object use, pickup, spell, and skill AP actions | Source checks 2 AP for use-object, 1 for pickup, and 4 for spell/skill, plus movement where applicable. | Current combat exposes movement and attacks only. | Add with the owning item/spell/skill milestones. Do not add hollow generic actions to M8G. | Their committed domain effects own persistence. |
| Projectile interception and alternate impact object | Source projectile travel can sometimes strike an intervening critter or blocker after attack preflight. | M8C uses authoritative binary LOS and immediate resolution. | Defer until projectile/world collision presentation exists; M8G's ordered subresults must leave room for a later resolved target distinct from requested target. | Impact result transient; resulting vitality/world state persistent. |
| AI flee/surrender and combat behavior | Source AI owns combat focus plus fleeing/surrender flags and deactivates combat mode accordingly. | No combat AI. | M8G supplies engagement APIs only. AI chooses when to use them later. | AI intent transient; lasting world consequences separately owned. |
| Followers and party ordering | Source includes nearby PCs/NPCs and has party-tail ordering behavior; decompiled ordering details are not fully clear. | Current slice has one PC and hostile NPCs. | Add with follower/party authority. Preserve deterministic source-order approximation now. | Party membership is not combat save state. |
| Active-combat save/load compatibility | Original `combat_save/load` serializes active turn-based state, round, AP, current/focus object IDs and resumes combat. | OpenArcanum Save V1 intentionally normalizes combat inactive while preserving committed world consequences. | Keep the deliberate V1 policy through M8G and real-time groundwork. Revisit only in a separately authorized save-version milestone. | Would require explicit format/version policy. |
| Damage-proportional combat XP | Original combat distributes XP from a damage-related pool; M8E uses the bounded 20% death reward. | Broader XP behavior is not implemented. | Defer to a campaign/progression compatibility milestone; it does not alter scheduling architecture. | XP remains M4C authority. |
| Sector/map transitions during combat | Current coordinator blocks ordinary world interaction and normalizes combat on transition/load. | Original uses global and per-critter combat modes but no requirement was found for cross-map active combat. | Existing bounded transition policy is safe. Revisit only with source evidence and world-streaming scope. | Save V1 unchanged. |

## Not supported / not present as a general source mechanic

### Attack-of-opportunity or disengagement strikes

No general opportunity-attack, free-attack-on-departure, disengage, or withdrawal-strike path was found in the
audited combat/movement source. Movement spends AP and does not trigger an adjacent enemy attack merely because the
actor leaves adjacency. M8G must not invent this mechanic. The word *disengage* in the proposed transient engagement
API means ending combat focus/surrender/removal, not making a D&D-style combat action.

### Separate initiative or surprise round

The source does not roll initiative or run a separate surprise round. It collects and sorts the combat list, moves
PC/party actors toward the tail, preserves list/source ordering for the rest, and skips inactive critters. An unaware
target changes attack difficulty; it does not create a distinct initiative subsystem. OpenArcanum should not add one
as pre-real-time compatibility work.

### Generic combat stances, reload action, or burst-mode command

No general stance system or universal reload/burst command was found in the audited core path. Combat mode itself and
called hit location are real, but they are not stances. Source firearms draw from their ammo/equipment rules. Any
future item-specific mode requires direct evidence and belongs with that weapon/technology family.

### Separate nonlethal attack mode

The source has fatigue damage and fatigue-based unconsciousness, not a general command that converts arbitrary damage
to nonlethal. OpenArcanum already treats fatigue through M4B/M8D. Do not add a generic nonlethal toggle without new
source evidence.

### Generic ordinary-attack knockback

Knockdown/stun can arise from critical effects, and magic/technology can play displacement animations, but no generic
knockback rule for every ordinary melee hit was found. It is not part of M8G.

## Recommended M8G acceptance boundary

M8G should be complete only when all of the following are proved through production paths and deterministic focused
tests:

1. Combat start and round refresh discover the source-bounded nearby roster, add newcomers, retain prior members who
   leave the discovery radius, skip inactive actors, and preserve deterministic PC-tail turn order.
2. Runtime engagement, not static KOS alone, controls combat-exit eligibility; later AI can drive engagement without
   owning combat state.
3. Each completed round emits exactly one authoritative +1,000 ms hook and no other clock/status feature is added.
4. One immutable attack request supports object target, bounded family, and torso/head/arm/leg intent; default location,
   penalties, and critical-chance modifiers match source roll order.
5. The modifier ledger distinguishes hard block from numeric cover and exposes typed empty seams without implementing
   light, awareness/backstab, magic/tech, Fate, or scripts.
6. Bow Master ignores the source long-range Perception penalty. Bow Expert/Master resolve two ordered impacts for one
   AP cost and one ammunition unit, with exact-once vitality/death/corpse/XP consequences.
7. Critical Dodge uses the 0/10/50/100 source thresholds and unsupported resulting critical effects still fail closed
   before every mutation.
8. Save V1 continues to restore committed vitality, death, inventory/ammo, XP, and world consequences while restoring
   no roster, engagement, AP, turn, request, roll, critical, or impact transaction.
9. Original -> Enhanced -> Original rebuild during active combat changes presentation only.
10. M8A-M8F focused regressions and the complete EditMode suite remain green, with a clean Unity compile and no new
    console errors or unexpected warnings.

## Architectural risks to control

- Do not let a future real-time scheduler own rosters, targeting, modifiers, damage, engagement, or consequences; it
  should schedule the same authoritative commands used by turn-based combat.
- Replace the single-damage-result assumption deliberately. Multi-impact attacks must have one transaction envelope
  and ordered subresults without duplicating M4B/M8D/M8E side effects.
- Keep immutable source facts separate from runtime combat status. `CombatActorSource.CritterFlags` must not become a
  mutable engagement/status bag.
- Guard synchronous vitality notifications against re-entrant multi-impact processing and repeat consequence awards.
- Do not infer cover from rendered pixels or projectile animation. Navigation/object data owns it.
- Do not broaden Save V1 by accident. No active attack, roster, engagement, AP, turn, RNG, or scheduler state is
  serialized.

## Source ambiguities and bounded decisions

- The exact original traversal-cost contribution depends on object metadata that may not yet have a direct
  OpenArcanum navigation representation. M8G must document and test a narrow mapping or fail closed.
- The source party/follower tail-order condition is not fully reliable in the decompiled mirror. Keep the existing
  stable `SourceOrder` approximation until the follower milestone has stronger evidence.
- The +1,000 ms round delta is clear; the eventual owner of the complete campaign clock is not. M8G should publish or
  inject the boundary rather than create a parallel clock.
- Two Bow projectiles and per-impact resolution are clear. Exact visual offsets, intervening-object collision, and
  alternate-hit presentation can wait for projectile presentation.
- Source object-list order is engine-derived. OpenArcanum's explicit stable source order is the deterministic
  compatibility approximation.
- Real-time scheduling details were intentionally not audited here. They should be designed only after M8G freezes
  the shared kernel contracts.

## Final recommendation

Authorize **M8G — Turn-Based Combat Kernel Closure** with exactly the acceptance boundary above. After it is complete,
audited, physically validated, and committed, a separately authorized real-time milestone can add scheduling and
presentation over the same combat authority. Until then, real-time combat would be premature.

## Audit-tail validation

The existing Unity 6.0.71f1 Editor compiled cleanly and the complete EditMode suite passed **741/741**, with 0 failed,
0 skipped, and 0 inconclusive. The run emitted the same five expected intentional fail-closed
dialogue-compatibility warnings and 0 errors. After those expected diagnostics were cleared, the final Unity Console
was 0 warnings and 0 errors. `git diff --check` was clean. No production code, tests, save format, original game data,
`GameData`, `HDAssets`, graphics configuration, or build-settings content was changed by this audit.

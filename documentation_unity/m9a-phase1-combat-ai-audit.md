# M9A Phase 1 NPC Combat AI Audit

Date: 2026-09-24
Branch: `feature/session-save-load`
Pre-M9A HEAD: `aa232631f870fabc90a6ecae8d4fb090555f2f96`

## Boundary

This bounded M9A Phase 1 vertical slice makes hostile non-player actors autonomously participate in the existing
turn-based and real-time combat systems. It does not create a second combat authority. `CombatAiController` selects a
target and submits an attack, movement, or yield intent; M8A-M8I continue to decide whether that intent is legal and
to own roster membership, turns, AP, real-time readiness, navigation, line-of-fire, cover, modifier ledgers,
ammunition, timing, damage, vitality, defeat, death consequences, and combat termination.

Phase 1 supports hostile unarmed melee and Bow intents. It uses ordinary/no-called-location attacks and a small,
deterministic target/approach policy. Wielded non-Bow melee weapons, weapon and ammunition switching, grenades,
spells, technology, fleeing, surrender, party/follower behavior, social/threat memory, schedules, dialogue AI, and
general NPC simulation are outside this phase.

## Original-source archaeology

The local `arcanum-ce` implementation was audited before implementation, principally `src/game/ai.c` and the combat
animation/action-goal path in `src/game/anim.c`.

- A valid `OBJ_F_NPC_COMBAT_FOCUS` is retained. Otherwise the source gathers perceived kill-on-sight candidates,
  excludes invalid/off/dead/party actors, and considers candidates within 20 tiles.
- The ordinary target chooser favors distance. A reacting branch can compare either negative distance or
  level-minus-distance, selected randomly. The current OpenArcanum runtime does not yet own the complete source
  reaction/threat/party inputs, so Phase 1 uses the smallest deterministic subset: nearest eligible PC, then combat
  source order, then stable ObjectID.
- `ai_action_perform_combat` normally submits `anim_goal_attack_ex`. The attack goal approaches when the attack is
  illegal because of range or obstruction, and source AI is reconsidered while action points remain.
- Turn-based source actors may therefore perform multiple legal actions before yielding; ending after every attack
  would not be faithful. Phase 1 reevaluates until authority advances the turn, no legal action remains, or a 32-step
  deterministic safety bound is reached.
- Source ranged behavior does not shoot through a hard obstruction. The attack goal moves near the target and
  reevaluates. Numeric cover remains a legal shot and stays entirely inside the M8G modifier ledger.
- The source contains weapon/ammunition selection, rare backoff, grenade, and other tactical branches that are not
  required to prove this basic loop and depend on authorities not yet present in OpenArcanum. They are not
  approximated here.
- Source real-time AI uses time-event/action-goal scheduling. OpenArcanum already established M8H as the authoritative
  source-time scheduler, so Phase 1 adds no second cooldown or frame-time authority.
- Source nonlethal target filtering rejects unconscious actors, while lethal source paths can continue attacking an
  unconscious target when no other target exists. Existing M8D semantics make unconscious participants ineligible
  to act or be selected. Phase 1 deliberately preserves that established combat contract and records the source
  difference rather than silently changing M8D.

These findings support the Phase 1 target/attack/approach loop but do not justify claiming all source NPC combat AI is
complete.

## Architecture and target selection

`CombatAiController` is a deterministic, non-MonoBehaviour decision layer over
`WorldMapSessionCoordinator.Combat`. Its only per-actor state is the currently retained target. The target is an
authoritative ObjectID, never a sprite, collider, GameObject, or UI object.

For each hostile NPC, the controller:

1. retains the current eligible PC target while it remains within the source 20-tile focus bound;
2. otherwise chooses the nearest eligible PC;
3. breaks equal distances by combat source order and then stable ObjectID;
4. clears/reconsiders the target after death, ineligibility, removal, or distance invalidation;
5. submits no command when no eligible opposition remains and asks combat authority to terminate the encounter.

M8G remains the roster and dynamic-engagement owner. AI state is created lazily once per stable actor identity and
removed when the actor leaves the authoritative hostile roster. Runtime enrollment, presentation rebuilds, and
polling cannot duplicate combat participants or controllers.

`ProductionCombatAiDriver` is an idempotently composed Unity polling adapter. `Update()` only asks the controller to
poll; it owns no decision timing or legality. The production loader creates exactly one driver, and all command
effects still pass through `CombatStateService`.

## Decision and command behavior

### Turn-based

Only the authoritative current hostile NPC may act. An unarmed actor requests `BasicMelee`; a Bow-equipped actor
requests `BasicRanged`. A successful attack is submitted through the existing structured request with
`CombatCalledLocation.None`. The controller then reevaluates while the actor still owns the turn and has a legal
action. Movement and attacks spend the existing M8 AP costs; exhaustion and existing turn logic advance ownership.

If no action can be submitted, the controller yields through `EndCurrentTurn`. A 32-decision bound prevents a bad
fixture or future authority regression from producing an infinite decision loop; reaching it explicitly ends the
owned turn.

### Real-time

Each poll visits hostile NPCs in authoritative roster order. A READY actor may submit one attack or movement command.
A BUSY or recovering actor submits nothing. The M8H pending-action record, source clock, effect time, readiness time,
and stable resolution order remain authoritative; AI adds no cooldown and never executes effects from `Update()`.

### Movement and blocked shots

AI first calls the same non-mutating `PreviewAttack` used by production combat UI. A legal preview is executed through
the normal attack path. `OutOfRange` or `LineOfFireBlocked` asks the combat service for an authoritative approach
preview. That preview uses the existing navigation map and pathfinder, selects the shortest reachable adjacent tile
in deterministic source direction order, and returns only the first step. The actual move is then submitted through
the existing turn-based or M8H real-time movement command.

For Bow approach candidates, the destination must have clear projectile traversal to the target. Hard obstruction
therefore causes movement/reevaluation rather than a shot. The blocked attempt spends no attack AP or ammunition and
causes no vitality mutation. Numeric cover does not block the shot and is applied once by the existing M8G ledger.

An equipped Bow reuses all established range, cover, training, multi-impact, critical, ammo, damage, and defeat
behavior. No ammunition produces the existing transactional failure and a yield; Phase 1 does not invent a weapon
switch. An equipped unsupported weapon similarly yields instead of bypassing current combat rules.

## Defeat, termination, and save/load

Dead or unconscious AI actors fail eligibility before any decision, movement, or attack. A dead/invalid target is
discarded and another eligible PC is selected deterministically. If either eligible player opposition or eligible
hostile opposition disappears, `CombatStateService` performs the existing transient combat teardown. AI
reevaluation does not process M8E consequences and cannot replay XP, corpse, or death markers.

AI target, pending decision, movement intent, attack intent, cached preview, and driver polling state are transient.
Save V1 is unchanged. An active-combat save/load continues to normalize combat, M8H pending actions, UI state, and
AI state rather than restoring a stale command.

## Focused automated validation

The new `M9APhase1CombatAI` category contains 15 tests. They prove:

- nearest deterministic target selection, source-order/ObjectID tie breaking, and valid-target retention;
- no off-turn action and multi-action turn-based melee/AP/turn behavior;
- authoritative one-step approach, AP spending, and the deterministic safety bound;
- legal Bow attack reuse of ammo and the modifier ledger;
- hard-blocked Bow movement with no attack-ammo or vitality mutation;
- no-ammunition transactional failure;
- one real-time READY submission, BUSY rejection, and reevaluation after recovery;
- dead/unconscious actor suppression and combat termination;
- target-death invalidation and selection of another eligible PC;
- dynamic enrollment exactly once in deterministic order;
- Save V1 transient normalization; and
- exactly one production driver bound to the session.

Accepted result: **15/15 passed**, 0 failed, 0 skipped, 0 inconclusive.

## Physical Play Mode validation

Computer Use drove the already-open Unity 6000.0.71f1 Editor and the production `TestTerrain` composition. No second
Editor was launched. The accepted harness used the production PC, authentic Polar Bear Cub, authentic Bow and arrow
objects, production loader/navigation, and production `CombatStateService`.

Representative production proofs passed:

- the hostile Polar Bear Cub autonomously took its turn and issued an ordinary unarmed melee attack;
- the attack used the normal five-AP M8B cost and authority advanced turn ownership;
- an out-of-range hostile submitted authoritative one-step movement, reevaluated, and stopped at its AP boundary;
- unrelated source hostiles were removed only from the bounded validation roster so they could not consume the proof
  turn; dynamic-enrollment behavior remains covered by production authority and focused tests;
- a hard-blocked Bow preview produced a movement decision with no arrow or vitality mutation;
- a clear Bow shot scheduled through the real-time combat path and consumed exactly one arrow;
- a READY actor scheduled exactly one action, BUSY prevented another, and advancing the source clock to the exact
  `ReadyAtMilliseconds` value enabled reevaluation;
- unconscious and dead actors stopped acting, target death invalidated retained target state, and no eligible
  opposition terminated combat;
- active-combat Save V1 reload restored no AI target, pending decision, or M8H pending action.

The authentic Polar Bear Cub exposes two fixture limits. It has only eight turn AP, below the authentic Bow attack
cost, so a turn-based Bow shot correctly fails with `InsufficientActionPoints`. It also has no authored Bear-with-Bow
ART timing profile, so production timing correctly reports `TimingUnavailable`. The proportional clear-Bow proof
therefore used a deterministic M8H timing provider (50 ms effect, 100 ms ready) while retaining the authentic Bow,
arrow stack, production combat service, scheduler, attack transaction, and vitality authority. The harness restored
the production loader timing provider before the authored unarmed/movement READY/BUSY proof. This is a missing
presentation fixture, not alternate AI or combat authority.

The accepted physical run recorded **0 warnings and 0 errors**.

## Regression and complete-suite validation

- M8I combat UI: **17/17**.
- M8H real-time combat: **22/22**.
- M8G Phase 4 Bow/Critical Dodge: **17/17**.
- M8G Phase 3 cover/Bow Master: **12/12**.
- M8G Phase 2 structured attacks: **17/17**.
- M8G Phase 1 combat loop: **16/16**.
- M8F critical resolution: **9/9**.
- M8E death consequences: **7/7**.
- M8D defeat state: **11/11**.
- M8C ranged combat: **12/12**.
- M8B turn-based combat: **23/23**.
- M8A core combat state: **22/22**.

The combined M8A-M8I regression result was **168/168**. The complete EditMode result was **857/857**, the prior
842-test baseline plus 15 justified M9A tests, with 0 failed, 0 skipped, and 0 inconclusive. The complete suite emitted
the same five intentional fail-closed dialogue warnings and 0 errors. After clearing expected test output, the final
Unity Console was **0 logs, 0 warnings, 0 errors**. Unity compilation was clean.

`git diff --check` was clean.

## Status, ambiguity, and next bounded phase

M9A Phase 1 is complete. It proves the basic autonomous hostile combat loop in both combat modes without weakening
M8 authority. It does not complete the entire M9A NPC-AI milestone.

A separately authorized M9A Phase 2 should audit and implement the next source-required combat subset: supported
melee-weapon use, weapon/ammunition selection and fallback, and the source combat-focus/target-scoring inputs that can
be represented by existing OpenArcanum authorities. Source-random reactive scoring must remain deterministic under
injected combat randomness. Backoff, grenades, fleeing/surrender, spells, technology, followers/party, and general
simulation should remain deferred until their own dependencies and milestone boundaries exist.

No M9A Phase 2 or M9B work was started.

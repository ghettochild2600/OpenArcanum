# M8G Phase 1 — Dynamic Combat Loop and Round Boundary

Validated 2026-09-23 on `feature/session-save-load` with Unity 6000.0.71f1.

## Scope and status

M8G Phase 1 closes only the combat-loop authority gaps identified by the post-M8F audit:

- source-bounded initial roster discovery;
- dynamic discovery and explicit runtime engagement;
- deterministic, exactly-once participant enrollment;
- an exactly-once authoritative 1,000 ms completed-round boundary;
- participant death/removal, unconscious skipping, termination, presentation rebuild, and Save V1 interactions.

Phase 2 has not started. Structured attack requests, called locations, modifier ledgers, numeric cover, Bow mastery
changes, two-impact Bow attacks, and Critical-Dodge reclassification remain outside this phase. AI, followers,
real-time scheduling, combat UI, spells, technology, and the other systems deferred by the post-M8F audit remain
deferred.

## Authority and discovery contract

`WorldMapSessionCoordinator` continues to own one transient `CombatStateService`. No presentation object owns or
duplicates the roster, engagement set, turn, AP, round, or elapsed source-combat time.

Combat start admits the requested actor and target, then discovers loaded source-hostile NPCs in the PC-centered
source perception square. The bounded radius is `max(10, Perception / 2 + 5)` independently on X and Y. Discovery
requires a registered NPC source, source hostility (`ONF_KOS`, no `ONF_NO_ATTACK`, and no Will-KOS script), a loaded
combat position, and current eligibility. Neutral, scripted-hostility, out-of-range, dead, newly unconscious, and
otherwise inactive actors are not admitted.

The service records engagement separately from immutable source facts. `EngageParticipant(ObjectID)` is the narrow
future-AI seam: it accepts only a loaded, eligible, source-hostile NPC while combat is active, inserts it once, and
returns `AlreadyRegistered` on a repeat. Enrollment never resets the current participant or current AP.

Participants are sorted with non-PC actors first by stable `SourceOrder`, then stable ObjectID; the PC remains at the
tail. Discovery and explicit engagement both use that same authority. An enrolled actor that later leaves the
discovery square remains in the encounter. Death/removal deletes the roster and engagement entry. An existing
participant that becomes unconscious remains enrolled but is skipped under the existing M8D rules; an actor already
dead or unconscious is not newly discovered.

Runtime engagement, rather than a fresh static KOS scan, controls the supported combat-exit check. An eligible
engaged hostile blocks `EndCombat`; once hostile participants are removed or otherwise ineligible, explicit combat
termination succeeds and clears all transient roster, engagement, turn, AP, round, and elapsed-boundary state.

## Completed-round boundary

A completed round is the point after the last currently eligible participant at or after the current source-order
position finishes, or is removed, and before the first eligible participant of the next round begins. Skipped
ineligible participants do not manufacture turns. At that single point the service:

1. captures the completed round number;
2. adds exactly `CombatStateService.RoundBoundaryMilliseconds` (1,000 ms) to transient elapsed combat time;
3. emits one `CombatRoundBoundary(completedRound, 1000, cumulativeTotal)`;
4. increments the round;
5. discovers newly relevant hostiles and re-sorts the roster;
6. begins the first eligible participant in the new order.

Starting combat, ending only part of a round, enrolling a participant, killing/removing an actor before the end,
skipping an unconscious actor, rebuilding graphics, terminating combat, normalizing through save/load, and starting
a new combat do not independently emit the hook. Exactly N completed rounds emit N hooks with cumulative totals
`1000, 2000, ... N*1000`. The hook is deterministic source-combat time; it does not wait for one real-world second or
depend on Unity frames. No poison, healing, fatigue recovery, scheduled effect, or general campaign-clock behavior
was added.

## Presentation and persistence

Original -> Enhanced -> Original calls only `WorldObjectSectorLoader.RebuildVisuals()`. During active combat the
physical proof retained the identical roster, current participant, current AP, round number, elapsed combat time,
engagement membership, and boundary count. Presentation rebuild performed no discovery and emitted no boundary.

Save V1 remains unchanged. Loading replaces the coordinator-owned combat service and restores no active roster,
engagement set, current participant, AP, round, elapsed combat time, or pending boundary. A later combat begins at
round 1 with zero elapsed time and cannot replay an earlier hook. Already committed vitality, death, inventory,
campaign, and world consequences continue to restore through their existing authoritative domains.

## Physical Play Mode evidence

The validation-only editor harness uses the real `CombatStateService`, production TestTerrain composition, the
production PC, and the authentic Polar Bear Cub sector
`maps/arcanum1-024-fixed/47781512457.sec`. It introduces only two deterministic loaded NPC fixtures for controlling
out-of-range-to-relevant discovery and explicit engagement; all roster, turn, boundary, vitality, death/removal,
termination, graphics, and save/load changes are submitted to production authorities.

The authentic initial encounter produced a six-participant roster, including the Polar Bear Cub and PC, with unique
engagement and deterministic PC-tail ordering. A loaded hostile began out of range, moved into the PC perception
square during active combat, remained unenrolled until the production round refresh, then joined exactly once without
stealing the current turn. Repeated refresh did not duplicate it. A second hostile enrolled through
`EngageParticipant` exactly once; the rejected repeat did not change turn or AP. Both validation actors received
their source-ordered turns without duplicate or skipped turns.

Three completed rounds emitted exactly three boundaries with deltas/totals `1000/1000`, `1000/2000`, and
`1000/3000`. Ending one actor turn emitted none. Runtime enrollment, non-current death/removal, unconscious retention
and skipping, leaving the discovery square, graphics rebuild, combat termination, restart, and save/load emitted no
extra hook. The dead explicit actor could not be re-enrolled. Removing the grown hostile roster allowed explicit
termination; the next combat began with clean round state.

The same run proved Original -> Enhanced -> Original authority preservation and Save V1 normalization. A committed
one-point vitality change survived the active-combat save/load while the old combat service, roster, engagement,
current turn, round, and elapsed boundary state did not. The pre-validation authoritative baseline was restored.
The harness reported zero warnings and zero errors.

## Automated validation

- Unity production/test/editor compilation: clean.
- M8G Phase 1 focused EditMode: **16/16** passed; 0 failed, 0 skipped, 0 inconclusive.
- Corrected M8A-M8F combat regressions: **84/84** passed; 0 failed, 0 skipped, 0 inconclusive.
  - M8F Critical Resolution: 9/9
  - M8E Death Consequences: 7/7
  - M8D Defeat State: 11/11
  - M8C Ranged Combat: 12/12
  - M8B Turn-Based Combat: 23/23
  - M8A Core Combat State: 22/22
- Complete EditMode suite: **757/757** passed; 0 failed, 0 skipped, 0 inconclusive.
- Complete-suite diagnostics: five expected intentional fail-closed dialogue warnings; 0 errors.
- Final cleared Unity Console: 0 warnings, 0 errors.
- `git diff --check`: clean.

## M8E test correction

The M8E death-consequence test previously assumed its scripted target immediately owned the first combat turn.
Phase 1 correctly discovers another eligible hostile with an earlier source order in that authentic fixture. The
test now advances through `CombatStateService.CurrentParticipant` and `EndCurrentTurn` until the PC owns the turn,
with a roster-count guard and an explicit PC-ownership assertion. It does not reorder production actors, bypass
combat authority, weaken death-consequence assertions, or change production behavior; it removes only the invalid
two-participant/immediate-turn assumption.

## Remaining ambiguities and deferred work

The post-M8F audit ambiguities remain unchanged: original object-list order is approximated by explicit stable source
order, follower/party-tail details await the follower milestone, and the eventual complete campaign-clock owner is
still undecided. Phase 1 publishes deterministic completed-round deltas without inventing that clock.

M8G remains incomplete overall. Its next separately authorized phase must address only the remaining audited kernel
items: structured attack requests/called locations/modifier ledger, numeric cover, Bow Master range exemption and
Expert/Master two-impact attacks, and Critical-Dodge reclassification. Real-time combat must not begin before those
contracts are complete.

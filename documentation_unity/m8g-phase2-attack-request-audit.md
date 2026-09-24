# M8G Phase 2 — Structured Attack Requests, Called Locations, and Modifier Ledger

Validated 2026-09-23 on `feature/session-save-load` with Unity 6000.0.71f1.

## Scope and status

M8G Phase 2 closes only the attack-input and inspectability gaps identified by the post-M8F audit:

- immutable structured melee/ranged attack requests;
- source hit-location identifiers and called-location effectiveness penalties;
- source called-location critical-success bonuses;
- a deterministic, inspectable attack-modifier ledger that is also the single hit-value authority;
- fail-closed validation before gameplay mutation;
- transient request/ledger diagnostics that do not change Save V1.

This phase does not add numeric cover, Bow Master range exemption, Expert/Master two-impact Bow attacks,
Critical-Dodge reclassification, equipped melee weapons, firearms, throwing, projectile presentation, combat UI, AI,
followers, magic, technology, or real-time scheduling. M8G remains incomplete until the remaining audited kernel
items are separately implemented and validated.

## Source contract

The bounded source comparison used the `arcanum-ce` mirror's combat/skill implementation. Source hit-location IDs
are preserved exactly:

| Location | ID | Hit-value modifier | Critical-success chance bonus |
| --- | ---: | ---: | ---: |
| Torso | 0 | 0 | 0 |
| Head | 1 | -50 | +10 percentage points |
| Arm | 2 | -30 | +6 percentage points |
| Leg | 3 | -30 | +6 percentage points |

The location modifier applies to both Melee and Bow. The critical bonus affects the critical-success chance only after
an ordinary hit; it does not alter the existing critical-failure formula. Location-specific injury, blindness,
crippling, knockdown, armor, helmet, and weapon outcomes remain unsupported. Those branches continue to fail closed
through the existing M8F critical-effect boundary rather than silently inventing partial effects.

The original ordinary-attack path can choose a weighted resolved body location (70% torso, 15% leg, 10% arm,
5% head). Integrating that roll now would change the established M8B-M8F deterministic roll order while the
location-specific effect tables remain unsupported. `CombatCalledLocation.None` therefore preserves the validated
ordinary attack path without a new roll. Explicit `Torso` is distinct request intent with a zero modifier. The
weighted ordinary location remains a recorded source ambiguity for the later location-effect milestone.

## Request and transaction authority

`CombatAttackRequest` contains stable attacker and target ObjectIDs, the existing bounded attack mode, and an explicit
called location. The legacy `Attack(actor, target, mode)` entry point constructs a `None` request, so all M8B-M8F
callers retain their prior behavior and random-call order.

The request is intent, not a snapshot. At execution, `CombatStateService` re-reads the current participant, roster,
eligibility, positions, equipped weapon, ammunition, AP, fatigue, target defenses, skills, attributes, range, and hard
line-of-fire state. A request created before movement or presentation rebuild therefore cannot carry stale authority.
Invalid modes, invalid location values, unavailable actors/targets, range/line-of-fire failures, unsupported weapon or
damage profiles, unsupported critical branches, and unresolved death scripts still return before AP, ammunition,
vitality, death-consequence, or turn mutation.

`CombatAttackResult` returns the original request, requested location, immutable modifier ledger, and final effective
attack value. `LastAttackResult` is a coordinator-owned transient diagnostic only. Combat reset, world change, and
Save V1 load clear it with the rest of transient combat state.

## Modifier ledger

Melee and ranged attacks build one ordered ledger before rolling. Its stages are:

1. base skill effectiveness (`5 * effective rank + 25`);
2. Intelligence 20 bonus;
3. target Armor Class difficulty;
4. minimum-Strength penalty when applicable;
5. Perception/range penalty when applicable;
6. weapon to-hit bonus when applicable;
7. called-location modifier.

Each immutable entry records stage, reason, signed value, whether it applies, whether it is suppressed, and the source
value used to derive it. Non-applicable stages remain visible with zero value. The ledger sums applied,
non-suppressed entries once, exposes the unclamped total, and clamps the final effective attack value to 0..100. That
final value is the value used by attack resolution; there is no second hidden calculation. Existing
`CombatHitChance` fields remain available to old callers.

Numeric cover is intentionally absent. Existing hard source-grid line-of-fire remains a binary precondition from M8C
and does not appear as a fabricated percentage modifier.

## Critical and lifecycle integration

Called attacks reuse the existing M8F resolution paths. A called Arm Bow hit on the production PC fixture adds six
critical-success percentage points, produces the existing damage-only critical table, applies resistance before the
bounded damage multiplier, and consumes ordinary Bow AP/ammunition exactly once. A called Head melee miss can enter
the existing self-hit critical-failure transaction. Unsupported NPC-to-PC critical-success effects and unsupported
critical-failure equipment/injury branches retain M8F atomic rollback.

Structured attacks use the current M8G Phase 1 turn/AP/round authority. They do not enroll actors, reorder the roster,
emit a boundary independently, or duplicate the completed-round hook. A final attack that exhausts the final actor's
AP advances normally and emits exactly one boundary. Original -> Enhanced -> Original presentation rebuilds cannot
change a pending request, ledger inputs, current actor, AP, round, or boundary count.

Save format remains V1. Active-combat serialization contains no attack request, modifier ledger, critical transaction,
roster, turn, AP, or round-boundary payload. Loading replaces the transient combat service and clears
`LastAttackResult`; already committed ammunition, vitality, death, inventory, campaign, and world consequences remain
owned and restored by their existing domains.

## Physical Play Mode evidence

The validation-only editor harness uses the production TestTerrain composition, production PC, authentic Bow
`G_1575DBCA_4990_C243_8184_524D51F7D533`, authentic 70-arrow stack
`G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`, and authentic Polar Bear Cub
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in
`maps/arcanum1-024-fixed/47781512457.sec`.

The physical run proved ordinary structured melee and ranged requests, execution-time authority, the four exact
location IDs/modifiers, ordered ledger composition and single-application math, malformed melee/ranged rollback,
called Arm +50% ranged critical success, called Head melee self-hit critical failure, normal AP/ammunition/turn
progression, and M4B-owned vitality mutation. It also proved a request created before Original -> Enhanced -> Original
rebuild resolves with unchanged intent, exactly one completed-round hook, and no presentation-owned combat state.

An active-combat V1 round trip dropped the request, result, ledger, participants, AP, and round state, emitted no extra
round boundary, and retained committed bear vitality. The harness reported zero warnings and zero errors.

## Automated validation

- Unity production/test/editor compilation: clean.
- M8G Phase 2 focused EditMode: **17/17** passed; 0 failed, 0 skipped, 0 inconclusive.
- M8A-M8G Phase 1 combat regressions: **100/100** passed; 0 failed, 0 skipped, 0 inconclusive.
  - M8G Phase 1 combat loop: 16/16
  - M8F Critical Resolution: 9/9
  - M8E Death Consequences: 7/7
  - M8D Defeat State: 11/11
  - M8C Ranged Combat: 12/12
  - M8B Turn-Based Combat: 23/23
  - M8A Core Combat State: 22/22
- Complete EditMode suite: **774/774** passed; 0 failed, 0 skipped, 0 inconclusive.
- Complete-suite diagnostics: five expected intentional fail-closed dialogue warnings; 0 errors.
- Final cleared Unity Console: 0 warnings, 0 errors.
- `git diff --check`: clean.

## Remaining M8G work

Phase 2 is complete, but M8G is not. The remaining post-M8F kernel scope is numeric cover distinct from hard
line-of-fire, Bow Master range exemption, Expert/Master two-impact Bow attacks, and Critical-Dodge reclassification.
Those mechanics require separate source-bounded implementation and validation. Real-time combat must not begin before
the audited kernel closure is complete.

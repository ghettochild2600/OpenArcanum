# M8H Real-Time Combat Audit

Date: 2026-09-24
Branch: `feature/session-save-load`
Pre-M8H HEAD: `716c49726992041c4cd786f13ebc18da24a22e0b`

## Boundary

This phase establishes one authoritative real-time scheduler and a bounded production vertical slice. It does not
create a second combat rules engine. `CombatStateService` still owns the M8A-M8G participant, request, modifier,
cover, critical, damage, vitality, death, and consequence transaction. The new scheduler decides only when a command
may begin, when its effect reaches the existing kernel, and when that actor becomes ready again.

Combat UI, autonomous NPC policy, followers, magic, technology, real-time auto-attack, and later milestones are not
part of this phase.

## Original-source archaeology

The local `arcanum-ce` source was audited before implementation:

- `src/game/combat.c:3339-3345` and `3539-3546`: both AP checking and AP consumption immediately succeed when
  turn-based combat is inactive. Real-time combat therefore does **not** directly convert or spend the turn-based AP
  pool.
- `src/game/combat.c:3506-3523`: the turn-based attack cost is separately derived from effective weapon speed
  (`>24 -> 1`, `>20 -> 2`, otherwise `max(1, 8 - speed / 3)`). This value remains a diagnostic property of the reused
  attack result; it is not a real-time cooldown formula.
- `src/game/anim.c:12763-12795`: an attack begins through an animation goal. Its base frame interval comes from ART,
  is adjusted by the actor's Speed, then subtracts `10 * (effective weapon speed - 10)` and clamps to 30..800 ms.
  A null/unarmed weapon has effective speed 10 (`src/game/item.c:3279-3287`). Apprentice-or-better weapon training
  adds 5 to effective weapon speed (`item.c:3289-3304`); the current bounded runtime has the source Bow speed factor
  and training level but no separate magic-speed adjustment field.
- `src/game/anim.c:12867-12921`: the combat effect is tied to the ART action frame and the actor remains in the
  animation goal through its remaining frames. Bow and firearm loops have additional real-time source behavior;
  automatic repeated attacks are not part of this bounded phase.
- `src/game/anim.c:15343-15413,15487-15499`: source Speed changes animation fps through exact tables and integer
  interpolation. Walk is `17/6/30`, run `20/8/30`, unarmed/sword attack `15/7/23`, and dagger/Bow attack `10/6/14`
  (normal/low/high). Dwarf and halfling walk/run endpoints add 4. Speeds below 8 interpolate from low to normal;
  speeds from 9 through 29 use the source expression `normal + speed * (high - normal) / 30`; 30+ uses high.
- `first_party/tig/include/tig/art.h:123-139` and `src/game/name.c:152-167`: the critter ART weapon field is the
  source `TigArtWeaponType` enum. Unarmed is value 1 and Bow is value 8. Physical validation exposed and corrected
  an initial use of the wrong field values before this phase was accepted.
- `src/game/timeevent.c:801-840`: source real-time and animation clocks are integer milliseconds. A ping ignores
  deltas below 5 ms, caps one wall-clock delta at 250 ms, and advances game/animation time by `8 * delta` outside
  turn-based combat. The M8H API consumes already-converted authoritative source milliseconds. A future production
  wall-clock driver must reproduce the source conversion without making Unity frame rate authoritative.
- `src/game/combat.c:3322-3329`: turn-based end-turn advances game time by exactly 1,000 ms. The original real-time
  engine has no separate modern cooldown-round abstraction; M8H reuses M8G's existing exactly-once 1,000 ms hook as
  an OpenArcanum integration boundary over the same authoritative combat clock.
- `src/game/combat.c:2857-2889`: changing the setting during an active encounter explicitly ends the turn-based
  subsystem when switching to real time and starts it when switching back. Exact pending-animation transfer and UI
  interaction semantics are not sufficiently bounded for this phase, so mid-encounter mode switching is deferred.

The source model is animation-goal scheduling, not a modern queue of AP-priced cooldown jobs. An actor has one active
goal; effect timing occurs at the source action frame and readiness follows the remaining source frames. The bounded
OpenArcanum representation mirrors those observable rules without making a Unity animator or callback authoritative.

## Authoritative scheduler

`CombatRealTimeScheduler.cs` is a partial of `CombatStateService`, so it shares the established roster and kernel
rather than coordinating a parallel rules domain. Its inspectable state contains:

- authoritative elapsed combat milliseconds;
- one ready-at time per enrolled eligible actor;
- at most one pending move/melee/Bow action per actor;
- action start, effect, and ready times;
- the immutable structured attack request or movement destination;
- the most recent real-time resolution as transient diagnostics.

`ScheduleRealTimeAttack` and `ScheduleRealTimeMove` validate mode, actor readiness, target/route, and timing
availability before installing pending state. Direct calls into `Attack` or `MoveInCombat` are rejected in real-time
mode unless the scheduler is resolving that actor's effect. A failed kernel transaction clears the pending action and
returns the actor to immediate readiness, avoiding an invalid permanent cooldown.

`AdvanceRealTime(int)` is deterministic and independent of machine speed. It processes all events at or before the
requested target time, uses stable participant/source order for simultaneous effects, resolves every effect exactly
once, and releases readiness only at the source recovery time. Large advances cannot duplicate an effect.

## AP and source-time relationship

Real-time combat has no current-turn actor and exposes zero current/maximum turn AP. The existing attack kernel still
calculates and reports the authentic turn-based `ActionPointCost`, but `ActionPointsSpent` is zero and no turn-based
overdraw/fatigue or turn advancement occurs. This is deliberate source parity: the original AP check/consume calls are
no-ops outside the active turn-based subsystem.

Timing instead comes from original ART metadata plus the audited Speed and effective weapon-speed formulas. The
production `WorldObjectSectorLoader` implements `ICombatRealTimeTimingSource`, reads ART frame count/action frame
through the VFS, and supplies durations without consulting `SpriteFrameAnimator`, `Update()`, or presentation
callbacks. It rebinds the timing source whenever a sector presentation creates a replacement combat service after
reset or Save V1 restore.

The bounded profiles are PC/NPC walk, run, unarmed attack, and Bow attack. Other weapon profiles and their source
animation variants remain unsupported by this phase rather than silently using invented values.

## Movement

The scheduler preflights a route through the existing `SectorNavigationMap` and source pathfinder. Walk/run duration is
the source ART frame interval multiplied by frames-per-rotation and route steps. The bounded slice commits the existing
authoritative movement transaction atomically at completion. It does not use animation playback speed as authority.

Per-frame/intermediate tile occupation, mid-route replanning, and source interruption at an intermediate step require
a later bounded M8H phase. Until then, a failed completion leaves no permanent readiness corruption.

## Melee and ranged execution

At effect time the scheduler invokes the same `CombatAttackRequest` path used by turn-based combat:

- melee continues through the existing unarmed hit/Dodge, critical, resistance, M4B vitality, M8D defeat, and M8E
  consequence path;
- Bow continues through M8C ammo authority and M8G called-location, range, cover/line-of-fire, Bow Master,
  Expert/Master two-impact, Critical-Dodge, and critical-resolution rules;
- AP cost is retained in diagnostics but not consumed in real time;
- ammo, vitality, corpse state, XP, and processed-death markers retain their existing owners and transactional rules.

## Roster, defeat, termination, and boundaries

Real-time start reuses M8G's deterministic production roster. Runtime engagement and boundary discovery synchronize
one scheduler record for each newly enrolled eligible actor without creating a turn owner. Death removes the actor and
any pending future effect. Unconsciousness preserves the roster's existing semantics but cancels/suspends scheduling.

Every crossed multiple of 1,000 authoritative milliseconds emits the existing `RoundCompleted` event once. A large
advance emits all crossed boundaries; scheduling or effect execution emits none by itself. Presentation rebuilds do
not own or advance this state. Explicit combat end retains M8A's hostile-participant guard and clears all real-time
clock, cooldown, pending-action, and diagnostic state before returning to ordinary movement.

## Save V1

Save format remains V1 and contains no combat payload. Loading replaces `CombatStateService`, so pending actions,
cooldowns, clock, attack diagnostics, critical diagnostics, and impacts cannot survive. Already committed ammo,
vitality, death/corpse, XP, and world consequences remain in their existing V1 domains. The sector presentation
rebinds both navigation and production timing to the replacement combat service.

## Focused coverage

The M8H category contains 22 deterministic EditMode tests covering:

- exact source fps tables, integer interpolation, weapon-speed adjustment, and 30..800 ms clamp;
- real-time start, ready state, no turn owner/AP spending, effect time, recovery, and exactly-once execution;
- stable simultaneous resolution and large time advances;
- exact single/multiple 1,000 ms boundaries with no action-manufactured boundary;
- source-timed movement completion;
- structured melee critical/damage reuse;
- Bow ammo, ledger, called location, range/cover, Bow Master, and Expert/Master two-impact reuse;
- dynamic discovery and explicit engagement exactly once;
- death removal, unconscious suspension, lethal M8D/M8E consequences exactly once;
- rejected/direct-bypass transaction safety;
- combat termination and Save V1 transient normalization.

## Validation results

Live Unity compilation is clean. Focused M8H validation is **22/22**. The M8A-M8G combat regression matrix is
**146/146** (M8A 22, M8B 23, M8C 12, M8D 11, M8E 7, M8F 9, M8G Phase 1 16, Phase 2 17, Phase 3 12, Phase 4 17).
The complete EditMode suite is **825/825**. Every automated run has 0 failed, 0 skipped, and 0 inconclusive tests.

Computer Use physical Play Mode validation used the production PC, authentic Bow/arrow objects, authentic Polar Bear
Cub, production `WorldObjectSectorLoader`, and production `CombatStateService`. It proved source-ART-timed running,
unarmed melee and Bow effect/recovery timing, zero real-time AP spend, existing M8C/M8G ammo/ledger behavior,
readiness rejection during recovery, deterministic runtime engagement, exact and multi-boundary hitch behavior,
Original -> Enhanced -> Original presentation rebuild independence, lethal M8D/M8E death/consequence processing once,
clean termination, and Save V1 transient normalization with committed death/XP preserved. The accepted physical run
recorded 0 warnings and 0 errors.

The complete suite emitted the same five intentional fail-closed dialogue-compatibility warnings and 0 errors. After
clearing those expected diagnostics and performing one final refresh, the final Unity Console was 0 logs, 0 warnings,
and 0 errors. `git diff --check` was clean. Save format remains V1.

## Source ambiguities and deferred M8H work

This vertical slice does not close the complete real-time-combat domain. A next bounded M8H phase is required for:

- a production wall-clock input adapter implementing the audited 8x source-time conversion and 250 ms ping cap;
- progressive route occupation and source interruption/cancellation semantics rather than atomic end-of-route commit;
- source-safe command replacement/auto-attack behavior;
- exact mid-encounter real-time <-> turn-based transfer of pending animation/readiness state;
- broader weapon timing profiles and any missing effective magic-speed adjustments;
- presentation observation of authoritative action start/effect/recovery without owning outcomes;
- deterministic NPC command policy beyond the manual validation hook.

The exact insertion ordering of independent original time events with identical timestamps is not named by the
decompiled source. OpenArcanum therefore uses stable authoritative roster order, with ObjectID as a final tie-breaker,
and records that bounded choice explicitly.

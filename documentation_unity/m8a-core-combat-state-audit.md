# M8A Core Combat State Audit

M8A is a bounded combat-state foundation. It does not implement attacks, damage,
critical effects, combat AI, followers, spells, technology, loot, combat UI, or
real-time scheduling. Original data under `D:/OpenArcanum/Source/Steam-Clean`
is read only.

## Source baseline

The source audit used `arcanum-ce` commit
`a7ff41b300ef712f0e7d088183a3d08957110cdd`, principally
`src/game/combat.c`, `combat.h`, `critter.c`, `ai.c`, `obj_flags.h`, and
`src/ui/intgame.c`.

Original Arcanum separates two concepts:

- Each critter has `OCF_COMBAT_MODE_ACTIVE` (`0x00040000`). The local PC's UI
  toggle activates/deactivates this flag, subject to active-critter and
  under-attack checks.
- Turn-based combat is a distinct global session. `combat_turn_based_start`
  interrupts ordinary animation goals, builds the nearby PC/NPC list, sorts
  it, activates the session, and begins turn 1. The real-time/turn-based user
  preference is separate from whether a turn-based session is active.

M8A represents that boundary explicitly with a mode plus a session lifecycle;
it implements only the turn-based state path. Real-time scheduling is deferred.

## Lifecycle and eligibility

The bounded lifecycle is `Inactive -> Starting -> Active -> Ending -> Inactive`.
Failed starts are transactional and return to the exact inactive state.
Re-entrant starts fail without mutating the active session.

`critter_is_active` requires a present PC/NPC which is not destroyed/off, dead,
unconscious, stunned, paralyzed, or stoned. M8A can prove the corresponding
currently modeled subset from session presence, `PersistentObjectState.Off`,
source critter flags, and the existing M4B vitality owner:

- current HP must be positive;
- current fatigue must be positive unless `OCF_UNDEAD` (`0x00000004`) is set;
- `OCF_STUNNED` (`0x00000020`) and `OCF_PARALYZED` (`0x00000040`) are rejected;
- an NPC must be a live loaded session object, while the actor must be the
  session's production PC.

Stoned is not yet represented in the runtime source profile, so M8A does not
invent it. The service owns no duplicate HP or fatigue values.

## Hostility boundary

`ai_check_kos` first permits the target's `SAP_WILL_KOS` script to veto normal
processing, then admits source hostility from `ONF_KOS` (`0x00000100`), reaction
at or below the AI packet threshold, alignment separation, decoys, and guard
protection. Most of those consumers require later script/AI/faction work.

M8A admits only the directly provable subset: an NPC with effective
`ONF_KOS`, no unresolved `SAP_WILL_KOS` veto, and no `ONF_NO_ATTACK`
(`0x20000000`). It does not guess the result of scripts or broaden this into AI.

## Participants, order, and action points

`combat_turn_based_start` collects nearby PCs and NPCs in source object-list
order. `sort_combat_list` moves PCs to the tail and otherwise preserves order;
there is no initiative roll and Speed is not an initiative roll. M8A therefore
uses stable source registration order for non-PC participants, followed by PCs,
with ObjectID as the deterministic final tie-breaker. Starting this slice
registers the production PC and requested hostile NPC; later perception/party
work may supply the complete nearby list without changing the ordering rule.

`combat_turn_based_whos_turn_set` resets the current actor's AP to effective
`STAT_SPEED`, with a minimum of 5. M8A consumes the existing M4D Speed query and
stores one current/max AP pair for the current turn. It does not add movement,
attack, skill, spell, or item-use costs. The audited future movement boundary is
`combat_move_cost`: two AP per path step, or one for the local PC when always-run
is enabled. Ordinary navigation is blocked while M8A combat is active so it
cannot bypass that future AP-aware route.

## Exit and transient policy

`combat_can_exit_combat_mode` allows exit when combat mode is already inactive
or the actor is inactive, but blocks an active actor while an active fighting
NPC focuses the PC or party. M8A models that bounded case: an eligible hostile
NPC participant prevents an ordinary end. Removing or making that participant
ineligible permits the PC to end. End clears participants, current actor,
turn/AP, and combat focus while preserving character, vitality, inventory, and
campaign owners.

Dialogue is cancelled on successful combat start. Sector unload, map travel,
session reset, and save restore clear transient combat state. Save format V1 is
unchanged and deliberately does not serialize an active M8A combat session.
Ordinary world interactions are rejected by the session while combat is active,
and presentation cancels any pending approach interaction, so a pre-combat
command cannot execute after the lifecycle boundary.

## Authentic fixture

The clean retail module and loose prototypes were read through the production
Formats parsers. The selected fixture is intentionally simple, proves the AP
minimum, and has no `SAP_WILL_KOS` ambiguity:

| Field | Value |
| --- | --- |
| Description | Polar Bear Cub |
| Sector | `maps/arcanum1-024-fixed/47781512457.sec` |
| Global tile | `(82527, 45623)` |
| ObjectID | `G_9B807B01_A142_4949_80CE_5A085F3BEEB1` |
| Prototype | `28422` |
| Effective NPC flags | `0x00001102` (`ONF_KOS` present) |
| `SAP_WILL_KOS` | `0` |
| AI packet | `0` |
| Effective base attributes | `7, 4, 5, 17, 4, 5, 5, 14` |
| Level / alignment | `5 / 0` |
| M4D Speed | `4` |
| M8A turn AP | `5` (source minimum) |
| M4B HP / fatigue | `48 / 29` |

The production development PC supplies the other participant. The authentic
two-participant order is therefore Polar Bear Cub first and PC last.

## Bounded implementation decision

`CombatStateService` is coordinator-owned and keyed only by stable
`ArcanumObjectId`. The object loader registers immutable source profiles; Unity
objects remain disposable presentation. The public start/end/register/remove
operations validate first and commit atomically. Source profile registration is
rebuilt on sector load and is not a new save domain.

The loader keeps NPC source profiles immutable. The production PC is registered
after `SectorSelected` relocates it, and its otherwise-identical inactive source
profile may rebase only its sector during direct coordinator traversal. This
preserves strict source facts while keeping the stable PC identity aligned with
the coordinator-owned map lifecycle.

## Physical Play Mode validation

Computer Use drove Unity 6000.0.71f1 against the real sector and real Polar Bear
Cub. The production PC and bear were the only combat participants, in exact
`bear -> PC` order. The bear's M4D Speed 4 produced the source-minimum 5 AP.
Invalid actor/target, dead and unconscious target, re-entrant start, duplicate
participant, premature end, and inactive end paths all failed without partial
state.

The run also proved the presentation boundary. A normal production pickup
approach was pending before combat; successful start cancelled that command and
its stale route. Navigation, interaction, and dialogue remained blocked while
active. Removing the active hostile admitted production end, and two complete
start/end cycles left no duplicate participants or presentation clones.

Original -> Enhanced -> Original graphics rebuild preserved lifecycle,
participant order, current actor, round, AP, identities, and authoritative
state. Sector unload and cross-map selection normalized combat to Inactive;
returning to the fixture retained the same stable PC and bear identities. The
save UI admitted a normal V1 save during active combat, but combat remained
deliberately absent from V1. Loading that slot restored the exact saved
non-combat state with combat Inactive, rebuilt presentation without duplicates,
and restored navigation and interaction. The temporary validation slot was
deleted.

The fixture sector contains no suitable authored ordinary interaction target,
so the lockout check created a temporary prototype-10078 Food item through the
production session item/placement path and normal loader presentation. It was
not a combat participant and did not substitute for the authentic PC/bear
fixture.

## Validation results

- Unity production, test, and editor assemblies compiled cleanly.
- Focused M8A EditMode: **22 passed**, 0 failed, 0 skipped, 0 inconclusive.
- Required regression matrix: **296 passed**, 0 failed, 0 skipped, 0
  inconclusive (M7E 15, M7D 16, M7C 14, M7B 27, M7A 24, M6C 26, M6B 32,
  M6A 25, M4B 20, M4D 25, Player Navigation 21, M2A 16, M2B 8, World Session
  State 27).
- Complete EditMode: **679 passed**, 0 failed, 0 skipped, 0 inconclusive. Six
  pre-existing compatibility-warning paths emitted their expected warnings.
- Final physical Play Mode validation: passed with **0 warnings and 0 errors**.

## Remaining boundary and next milestone

M8A owns only lifecycle, eligibility, hostility admission, participants,
deterministic order, current turn, and AP initialization. It does not implement
combat movement/AP spending, attacks, damage resolution, AI, perception-based
participant gathering, spells, technology, followers, loot, real-time combat,
or combat persistence. Save format remains V1.

The recommended next separately authorized slice is **M8B: one bounded basic
attack and damage-resolution path**, sourced from the original combat formulas
and routed through the existing M4B vitality and M8A participant/AP owners. AI,
spells, technology, followers, loot, and broader combat scheduling should remain
deferred unless explicitly included in that milestone.

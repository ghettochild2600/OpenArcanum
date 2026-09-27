# M10A Phase 2 Magic Time and Termination Closure Audit

Date: 2026-09-27
Branch: `feature/session-save-load`
Starting HEAD: `3c992c135ece33643a4ce05088ebaf5f75379f13`

## Result

M10A is formally closed. Phase 2 adds one coordinator-owned source game-time axis, binds both noncombat and combat
progression to it, represents finite spell deadlines explicitly, adds source-bounded cancellation/dispel termination,
and proves one authentic finite-duration spell: Flash (ID 66). No Unity object, frame counter, animation, graphics
mode, or UI component owns gameplay time or active magic state.

The implementation remains deliberately narrow. It does not add a calendar/day-night simulation, travel duration,
encounters, spell UI, generalized status-effect content, technology, AI spell selection, summons, resurrection, or a
second finite spell.

## Primary-source findings

The source clock and spell lifetime were resolved from the `arcanum-ce` reconstruction and independently checked
against the locally mounted retail DAT records:

- `timeevent.c` classifies `TIMEEVENT_TYPE_MAGICTECH` as saveable game time. Outside turn-based combat, monotonic
  timer deltas below 5 ms are ignored, each processed delta is capped at 250 ms, and unpaused game time advances at
  eight times real time.
- `combat.c` advances game and animation time by exactly 1,000 ms when a turn-based round completes. Normal polling
  does not advance game time while turn-based combat is active.
- `magictech.c` converts retail duration seconds to milliseconds and then multiplies by eight before scheduling the
  magic time event. It runs the spell End action for natural duration, interruption, maintenance failure, and dispel.
- `magictech.c` applies non-damaging finite-spell magic resistance as a saving-throw bonus, then evaluates the
  configured stat check. Flash uses `Constitution @ -5`.
- `obj_flags.h` defines `OCF_BLINDED` as `0x00000080`; `skill.c` consumes that same flag for sight-dependent rules.
- The retail finite-duration source audit found only Flesh to Stone (ID 48) and Flash (ID 66) in the 0-79 player
  spell table. Flash was selected because its Begin/End contract is one reversible critter flag. Flesh to Stone also
  changes critter activity and portal/stone targeting and would widen this closure phase.

Primary references:

- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/timeevent.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/combat.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/magictech.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/obj_flags.h>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/skill.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/critter.c>

## Authoritative source time

`WorldMapSessionCoordinator` now owns `SourceTimeService`. The service uses monotonic `Stopwatch` input rather than
Unity frame time, exposes an explicit pause predicate, applies the source 5 ms minimum and 250 ms cap, and advances
noncombat game time at 8x. The coordinator polls it only while combat is inactive.

M8 owns combat integration. A completed turn-based round adds exactly 1,000 source milliseconds before publishing
the existing round boundary. M8H advances the same clock by eight source milliseconds for each real-time scheduler
millisecond, split at each action/boundary event so a crossed magic deadline resolves in deterministic order. It
does not also advance from presentation frames, so combat cannot double-count time.

`MagicStateService` subscribes to source-time advances and no longer owns an elapsed-time counter. Its compatibility
`ElapsedMilliseconds` projection and `AdvanceTime` entry point delegate to the shared service. Strength of Earth's
retail 10-second upkeep interval is therefore represented as 80,000 source-game milliseconds; Phase 2 corrects the
Phase 1 placeholder that had treated the source seconds-to-game-time conversion as 10,000 ms.

The existing M7E travel operation is instantaneous and intentionally has no travel-duration model. Its processing
frames use the same noncombat clock, but Phase 2 does not invent route time. Exact menu/world-map pause predicates
beyond the existing explicit session pause boundary remain deferred until those presentation states have an
authoritative lifecycle owner.

## Authentic finite spell: Flash

Flash is spell ID 66, Phantasm rank 2, cost 10 fatigue, four turn-based AP, range 99, `No_Stack:1`, aggressive,
non-mechanical living-critter target, `Constitution @ -5` save, and retail `Duration: (10 @ -1)`. The runtime stores
an absolute 80,000-source-ms expiration deadline. Its effect is the exact `OCF_BLINDED` flag (`0x80`) exposed through
the existing combat critter-flag query; no presentation component mutates the source actor record.

Cast validation remains transactional. A mechanical, dead, missing, invalid, out-of-range, hard-LOS, duplicate, or
combat-rejected target changes no fatigue, AP, vitality, active effect, source time, or turn ownership. A successful
Constitution save still consumes the valid cast cost but creates no active effect, matching the source Begin path.
The flag remains present until the absolute deadline, then End removes it once with `NaturalExpiration`.

## Cancellation, dispel, and invalidation

Active-effect termination is typed and records effect ID, spell ID, source timestamp, and reason:

- `NaturalExpiration` for a finite deadline;
- `CasterCancellation` for the validated caster ending a maintained spell;
- `Dispelled` for explicit magic-effect removal sourced by or targeting the subject;
- `UpkeepFailure` when maintained fatigue cannot be paid;
- `InvalidParticipant` when the caster becomes unavailable or the target is no longer alive.

Every path invokes the same effect-specific reversal and removes the record before future upkeep/deadline processing.
Repeated cancellation/dispel therefore fails closed and cannot replay an End mutation. The dispel operation is the
source-bounded core removal contract only; Phase 2 does not invent the Disperse Magic cast, targeting UI, visuals, or
AI use. No technology effect is included in the magic set.

## Save V1

Save format remains V1. The existing optional `magic.elapsedMilliseconds` field now snapshots the shared source
clock for backward compatibility. Finite effects add an absolute `expiresAtMilliseconds` value. Restore validates
the spell family, identity, target uniqueness, start/deadline relationship, and source-time bounds before swapping
roots. It installs the source clock before rebuilding active magic effects.

An unexpired Flash restores with the same absolute deadline and remaining duration. An already expired record is
not re-applied. Combat, pending attack/cast actions, previews, RNG, and other transient transactions remain
normalized and cannot resurrect a timer or effect. Older V1 saves without magic still restore with source time zero;
older maintained-effect records legitimately have an expiration value of zero.

## Validation

Focused M10A Phase 2 EditMode: **10/10**.

The tests prove source scaling/cap/pause and turn-boundary increments; the exact Flash definition; exact deadline;
Constitution save; blinded overlay; No_Stack and mechanical rejection before mutation; typed cancellation and
dispel; no future upkeep after termination; turn-based/real-time continuity; finite save/load remaining duration;
and dead/unconscious participant cleanup.

Directly affected older-system regressions: **178/178**.

- M4A attributes **13/13**
- M4B vitality **20/20**
- M4D derived stats **25/25**
- M6A session save/load **25/25**
- M6B migration/slots **32/32**
- M8B turn-based combat **23/23**
- M8D defeat **11/11**
- M8E death consequences **7/7**
- M8H real-time combat **22/22**

Complete EditMode: **920/920**, with **0 failed, 0 skipped, 0 inconclusive**. This is the prior 910-test baseline plus
10 Phase 2 tests. Unity compilation was clean.

Computer Use drove the single already-open Unity 6000.0.71f1 Editor. The accepted production Play Mode proof used
the production PC, authentic Polar Bear Cub, retail ART, production navigation/combat/vitality/save services, and
the existing graphics rebuild path. It proved:

1. Flash applied the production blinded flag and remained active one source millisecond before 80,000;
2. noncombat advance, one completed turn-based round (+1,000), and real-time combat (+8x) used one continuous clock;
3. Flash ended exactly once at the deadline;
4. V1 load preserved the absolute deadline and remaining duration without restoring transient combat;
5. maintained cancellation and finite dispel each ended once and a repeated request failed closed;
6. Phase 1 transactional casting, M8H timing, Original -> Enhanced -> Original rebuild, V1 normalization, and lethal
   M8D/M8E consequence proofs remained intact.

Accepted Play Mode result: **0 warnings, 0 errors**.

## Closure assessment

M10A core closure criteria are satisfied: coordinator-owned authority, one source clock spanning noncombat and both
combat modes, instantaneous/maintained/finite effect families, source resistance/saving throws for the selected
content, typed end semantics, optional V1 persistence, transactionality, production Play Mode proof, and full-suite
regression coverage.

Additional compatible spell definitions are content expansion, not M10A core work. The following remain explicitly
deferred:

- M10B technology, disciplines, schematics, crafting, and technology UI;
- final spell-selection/targeting UI and spell eye-candy/audio;
- spellcasting AI and follower spell policy;
- summons, resurrection, charm/control/social effects, and campaign-specific scripts;
- arbitrary-location teleportation until M7 owns that destination boundary;
- a calendar/day-night simulation, authored travel duration, encounters, and menu-specific pause policy.

Flesh to Stone remains rejected for this phase because its inactive/stone/portal semantics require a broader status
and world-object contract. No unsupported duration-stat scaling, callback triggers, area effects, item magic,
anti-magic, or generalized dispel spell was inferred.

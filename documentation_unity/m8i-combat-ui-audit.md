# M8I Combat UI Audit

Date: 2026-09-24
Branch: `feature/session-save-load`
Pre-M8I HEAD: `ccf11483cd2e37e2e73740e13f9b86d74af05082`

## Boundary

This bounded M8I vertical slice adds a usable production combat presentation over the existing M8A-M8H authority.
It does not create a UI-owned combat model. `CombatStateService` remains the sole owner of combat legality, actor and
roster state, turn/AP progression, real-time readiness, modifier composition, random resolution, ammo, damage,
vitality, defeat, death consequences, and committed world state.

The UI owns only transient presentation state: selected target, selected attack mode, called location, current
preview, and user-facing feedback. It submits structured requests to the existing turn-based attack kernel or M8H
real-time scheduler. Save format remains V1, and combat/UI transients are intentionally normalized on load.

Autonomous combat AI, followers, magic, technology, richer combat animation, source-art panel replacement, cursor
art, broader input binding, and later milestones remain outside this slice.

## Original-source archaeology

The local `arcanum-ce` source was audited before implementation:

- `src/ui/combat_ui.c`: the source combat bar projects available versus required AP with green/orange/red states;
  during an NPC turn it presents a moving green indicator. The End Turn button and `E` advance the local PC's
  turn rather than mutating the combat state directly in the widget.
- `src/ui/intgame.c`: combat target selection filters world objects to valid living combat targets and sends the
  selected stable object into the combat command path. The UI does not independently resolve attacks.
- `src/ui/intgame.c`: comma, period, and slash select Head, Leg, and Arm called shots, while the ordinary attack uses
  the default torso location. The source goal carries location identifiers 1/2/3; OpenArcanum continues using the
  M8G typed called-location request rather than duplicating those integers in presentation.
- Source UI feedback is message/result-driven. M8I therefore projects the authoritative result and modifier ledger,
  including a distinct Critical-Dodge presentation, instead of predicting or rerolling outcomes in the UI.

These findings bound M8I to command selection and read-only projection. They do not justify a parallel hit-chance,
damage, AP, ammo, timing, or turn implementation.

## Architecture

`CombatUiController` is a non-MonoBehaviour transient adapter over `WorldMapSessionCoordinator.Combat`. It exposes:

- current combat mode;
- turn-based current actor and AP;
- real-time READY/BUSY/recovering state from the M8H scheduler;
- stable-ID target selection;
- melee/Bow and Torso/Head/Arm/Leg choices;
- authoritative preview, ledger, final effectiveness, and final hit chance;
- authoritative ammo quantity and resolved outcome feedback;
- End Turn through the existing turn authority.

`CombatStateService.PreviewAttack` is a non-mutating projection of the same structural/resource preflight and the
same M8G modifier builders used by execution. It consumes no RNG and changes no AP, ammo, vitality, turn, roster, or
scheduler state. Failed previews remain fail-closed.

`ProductionCombatPresenter` supplies the bounded IMGUI surface and the source-shaped `E`, comma, period, and slash
inputs. `PlayerClickMoveInput` hands ordinary world clicks to this presenter only while combat is active, preventing
movement or dialogue commands from leaking through a combat target click. `WorldObjectTargetSelector` converts the
clicked production PC/NPC presentation to its stable identity. The production sector loader adds the presenter
idempotently.

The presenter is disposable. Disabling it, ending combat, or loading Save V1 clears target, mode, location, preview,
result, and feedback transients without changing committed authority.

## Focused automated validation

The new `M8ICombatUI` category contains 17 tests. They prove:

- preview uses the authoritative ledger and is transactionally read-only;
- invalid/nonparticipant selection is rejected and valid stable-ID selection succeeds;
- Torso, Head, Arm, and Leg project the exact existing called-location modifiers;
- turn-based current actor/AP and real-time READY/BUSY state come from combat authority;
- melee, Bow, End Turn, AP, ammo, Miss, Hit, Critical Success, Critical Failure, and Critical Dodge are projected
  from existing results;
- a second real-time command is rejected by the existing busy guard;
- invalid commands fail before AP, ammo, vitality, or turn mutation;
- repeated presentation refreshes do not mutate combat or lose transient selection;
- combat end and Save V1 load normalize all UI transients;
- production composition adds exactly one presenter without starting or owning combat.

Accepted result: **17/17 passed**, 0 failed, 0 skipped, 0 inconclusive.

## Physical Play Mode validation

Computer Use drove the already-open Unity Editor and the production `TestTerrain` composition. No second Editor was
launched. The accepted harness used the production PC, authentic Polar Bear Cub, authentic Bow, authentic arrows,
production loader/navigation, and production `CombatStateService`.

Representative production proofs passed:

- turn-based UI projected the authoritative current actor and AP;
- the Polar Bear Cub was selected by stable identity;
- unarmed melee submitted through authority, returned Hit, and spent the normal M8B AP cost;
- End Turn advanced the authoritative participant;
- the authentic Bow and Arm called location projected the M8G ledger, exact -30 called-location entry, final
  effectiveness, and final chance;
- the Bow command returned Miss and consumed exactly one authoritative arrow;
- Original -> Enhanced -> Original rebuilt presentation while preserving target/mode/location and submitting no
  combat action;
- combat teardown cleared transient UI state;
- real-time UI projected READY, then BUSY; a second command was rejected by authority; advancing the authoritative
  source clock produced the resolved Miss and exactly one more ammo consumption;
- Save V1 load during active combat restored no stale combat command, target, preview, or result;
- the pre-validation authoritative baseline was restored and the authentic target remained alive.

The accepted physical run recorded **0 warnings and 0 errors**.

## Regression and complete-suite validation

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

The combined M8A-M8H regression result was **168/168**. The complete EditMode result was **842/842**, the prior
825-test baseline plus 17 justified M8I tests, with 0 failed, 0 skipped, and 0 inconclusive. The complete suite emitted
the same five intentional fail-closed dialogue warnings and 0 errors. After clearing expected test logs and forcing a
final Unity refresh, compilation was clean and the final Console was **0 logs, 0 warnings, 0 errors**.

`git diff --check` was clean.

## Status and follow-up boundary

The bounded M8I combat-UI vertical slice is complete. It provides a functional production command/projection surface
without weakening M8A-M8H authority or expanding Save V1.

If further M8I work is separately authorized, the next narrow presentation phase should replace the temporary IMGUI
layout with source-art-backed combat-bar/cursor presentation and improve visual AP/target feedback while retaining
this controller and authority boundary. M9A and all other later systems remain unstarted.

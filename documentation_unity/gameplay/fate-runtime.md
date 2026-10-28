# Fate Runtime Authority

## Source boundary

This pass traces `arcanum-ce/src/game/fate.c`, `arcanum-ce/src/ui/fate_ui.c`, and every represented
`fate_resolve` call site. The retail table has one immediate choice and eleven deferred choices. Each costs one Fate
point. Activation spends the point; an armed deferred choice may be deactivated for a refund; resolution clears the
saved flag. OpenArcanum keeps that lifecycle in session-owned `GameplayHudStateService`; UI only submits a choice.

Invalid choices, missing/dead players, insufficient points, duplicate activation, and unsupported resolution kinds
fail before point or flag mutation. A deferred fate remains armed when its action fails before the authoritative
transaction commits. Save V1's optional gameplay-HUD domain preserves points and armed flags.

## Complete retail table

| Choice | Target and timing | Source effect | OpenArcanum boundary |
|---|---|---|---|
| Full Heal | PC; immediate | Restore HP and fatigue fully | Supported through M4 vitality; spends on successful activation |
| Force Good Reaction | PC's next qualifying dialogue start | Raise the NPC's reaction to 100 | Supported through M11B social authority; consumed only in the dialogue transaction and restored on rollback |
| Critical Hit | PC's next committed attack | Force critical success | Supported through M8; preflight remains transactional and the flag clears only immediately before mutation |
| Critical Miss | Next committed attack against the PC | Force critical failure against the opponent | Supported through M8; unsupported critical branches still fail closed before AP/ammo/vitality/turn or Fate mutation |
| Save Against Magick | Next qualifying hostile resisted spell targeting the PC | Force a successful save | Supported through M10A after successful cast preview |
| Spell At Maximum | PC's next valid spell cast | Use the spell's source maximum magnitude | Supported through M10A after successful cast preview |
| Critical Success: Gambling | PC's next Gambling check | Force critical success | Blocked: no authoritative Gambling action-check transaction exists |
| Critical Success: Heal | PC's next Heal check | Force critical success | Blocked: no authoritative Heal action-check transaction exists |
| Critical Success: Pick Pocket | PC's next Pick Pocket check | Force critical success | Blocked: no authoritative Pick Pocket action-check transaction exists |
| Critical Success: Repair | PC's next Repair check | Force critical success | Blocked: no authoritative Repair action-check transaction exists |
| Critical Success: Pick Locks | PC's next Pick Locks check | Force critical success | Blocked: no authoritative Pick Locks action-check transaction exists |
| Critical Success: Disarm Traps | PC's next Disarm Traps check | Force critical success | Blocked: no authoritative Disarm Traps action-check transaction exists |

The six blocked skill choices are still enumerated and displayed, but activation reports the exact unsupported-
deferred-effect result without spending a point. Implementing them safely requires each skill's real action authority,
not a Fate-specific substitute.

## Validation

Focused coverage enumerates all 12 choices, their one-point cost, supported/unsupported classification, insufficient-
point and duplicate-arm rejection, refund, Save V1 reconstruction, rollback, valid resolution, and failed-transaction
retention. Integration cases exercise M8 Critical Hit/Critical Miss, M10A Save Against Magick/Spell At Maximum, and
M11B Force Good Reaction. The complete EditMode suite passed **1229/1229**, with no failed, skipped, or inconclusive
tests. Production UI validation proved the source panel, Fate counter, F route, and immediate Full Heal path.

## Follow-up boundary

Add no further Fate behavior until a source-faithful action transaction exists for one of the six named skills. That
transaction must own target eligibility, roll classification, effect application, rollback, and the exact Fate
resolution point. Fate can then bind to that commit boundary without changing the HUD contract.

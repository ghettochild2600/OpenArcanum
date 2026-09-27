# M9B Phase 2 Dialogue / Party Closure Audit

Date: 2026-09-27
Branch: `feature/session-save-load`

## Result

M9B is complete. The short post-Phase-1 closure assessment found one bounded core gap: the dialogue formats already
decoded the source follower test/effects `fo`, `jo`, and `lv`, but `ProductionDialogueSession` could not evaluate or
commit them through the new coordinator-owned `PartyStateService`. Phase 2 closes only that gap.

Production dialogue now:

- evaluates `fo 0` / `fo 1` against authoritative party membership;
- preflights `jo` and `lv` through non-mutating party previews;
- commits join/remove through the same Phase-1 party authority;
- includes membership in the dialogue transaction snapshot, so a later response or target-node failure restores the
  original party exactly;
- routes retail SAP_DIALOG `OBJ_FOLLOWING_PC` through party membership; and
- treats the currently unrepresented retail `OBJ_JILTED` branch as false through the script host, rather than failing
  strict execution or inventing jilted state.

UI and dialogue presentation remain observers. Save format remains V1; party persistence and transient normalization
are unchanged from Phase 1.

## Source Evidence

The immutable retail fixture remains Virgil:

- ObjectID: `G_A09DCD63_7A15_D411_8F1D_00E02920220C`
- prototype: `17102`
- dialogue/script number: `1324`
- sector: `maps/arcanum1-024-fixed/86570436012.sec`
- dialogue resource: `dlg/01324virgil.dlg` (409 parsed lines)

The live source audit resolved these exact rows:

- line 72: `jo 0 74`
- line 514: `lv`
- line 524: `lf31 1`

The current format parser's bounded semantics for `jo` are follower recruitment; its extra retail operands are not
interpreted as a new jump or party policy. Phase 2 therefore did not invent meaning for `0 74`. The authored response
target and normal dialogue lifecycle continue to own node progression.

## Failure and Transaction Contract

Join and leave are rejected before mutation when the party operation would fail. Covered failures include capacity,
duplicate recruitment, and removal of a nonmember. A later missing target rolls a successful join back through the
same dialogue transaction snapshot. The failure boundary preserves local flags, membership ordering, inventory,
campaign state, character state, and presentation ownership.

`PreviewJoin` and `PreviewRemove` share the validation path used by their committing counterparts. Restore replaces
the membership snapshot in order and emits the existing membership-change notification; it does not synthesize
presentation or combat state.

## Physical Production Proof

The TestTerrain Play Mode proof used the authentic Virgil world object and retail dialogue 1324 source rows. A
harness-only neutral entry node selected the exact retail rows directly because Virgil's complete first-meeting
conversation also depends on broader dialogue vocabulary outside this bounded milestone.

The production `ProductionDialogueSession` proof passed:

- retail line 72 executed `jo 0 74` and enrolled Virgil exactly once;
- retail line 514 executed `lv` through the authoritative remove transaction;
- retail result line 524 committed local flag 31;
- the same Virgil ObjectID and exactly one world presentation remained after disband; and
- the accepted run reported **0 warnings and 0 errors**.

The harness did not replace party authority, call `PartyStateService` directly for the result under test, or substitute
a synthetic follower identity. It only provided a bounded safe entry to the authentic response rows.

## Automated Validation

- focused M9B Phase 2: **4/4**
- M9B Phase 1: **16/16**
- M5C trainer dialogue: **21/21**
- M5B quest/journal: **18/18**
- M5A dialogue/quest: **16/16**
- M9A Phase 2 / Phase 1: **12/12**, **15/15**
- M8A-M8I: **185/185**
- combined M9B Phase 1 + M5A-M5C + M9A + M8 regression matrix: **283/283**
- complete EditMode: **889/889**
- failed / skipped / inconclusive: **0 / 0 / 0**
- Unity 6000.0.71f1 compilation: clean
- final cleared Console: **0 logs, 0 warnings, 0 errors**
- `git diff --check`: clean

Expected compatibility-warning fixtures remain asserted inside automated tests and do not represent accepted-run or
final-Console warnings.

## Deferred, Rejected, and Ambiguous Mechanics

The closure assessment does not justify another M9B core phase.

- Deferred to later progression work: follower leveling and any catch-up level policy.
- Deferred to later social/campaign systems: loyalty, reaction/alignment departures, jilted state, scripted forced
  departures, resurrection, and follower-specific quest consequences.
- Deferred to later presentation/control work: portraits, HUD, follower inventory/equipment UI, formations, orders,
  stances, waiting controls, and party management UI.
- Deferred to later gameplay systems: magic, technology, healing, barter/economy, and campaign-wide follower scripts.
- Optional rather than required for the bounded kernel: the source distance-30 catch-up teleport. Existing ordinary
  following and every authoritative transition already preserve stable membership and identity.
- Rejected as unsupported invention: a more specific party-tail combat ordering, leader-target preference, loyalty
  thresholds, or interpretation of unrepresented `jo` operands.
- Source ambiguity retained: the available source material does not prove exact formation/catch-up behavior or how
  every campaign-specific follower exception composes with the generic party kernel.

Virgil's complete 409-line conversation exercises additional already-known or future dialogue commands. Phase 2 does
not broaden M5 dialogue support merely to make that entire campaign conversation executable. Those commands retain
their owning milestone boundaries and fail closed when unsupported.

## Closure

M9B now owns the bounded authoritative follower/party kernel across membership, following, travel, combat, defeat,
save/load, presentation rebuild, and dialogue recruitment/removal. M10A has not started.

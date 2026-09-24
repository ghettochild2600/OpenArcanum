# M8G Phase 4 — Bow Multi-Impact and Critical-Dodge Resolution

Validated 2026-09-24 on `feature/session-save-load` with Unity 6000.0.71f1.

## Scope and status

M8G Phase 4 closes the final two bounded gaps from the post-M8F turn-based combat audit:

- Expert and Master Bow attacks resolve two ordered impacts inside one attack transaction;
- a defender's qualifying critical Dodge reclassifies the cleared hit through the existing attack critical-failure
  path.

The phase does not add projectile presentation or alternate collision targets, critical injury/equipment tables,
equipped melee weapons, Throwing, Firearms, AI, followers, magic, technology, combat UI, or real-time scheduling. Save
format remains V1.

## Source archaeology — Bow Expert and Master

The bounded source comparison used recovered `arcanum-ce/src/game/combat.c`, especially `sub_4B3170`,
`combat_process_ranged_attack`, projectile creation, and projectile-impact damage processing.

The source resolves the attack command once, consumes the weapon's ammunition quantity once, and classifies hit,
critical state, Dodge, called location, and target before projectile creation. `combat_process_ranged_attack` selects
one projectile normally and exactly two when the attack skill is Bow and Bow training is at least Expert. Expert and
Master therefore receive the same two-projectile rule. This is one attack transaction, not two attack commands:

- one normal ranged AP cost;
- one weapon `AmmoConsumption` cost, which is one arrow for the authentic Bow;
- one shared attack roll, critical roll, requested location, requested target, and modifier calculation;
- two ordered projectile impacts, each inheriting the shared classification and request state;
- independently rolled damage and independently applied resistance at each impact;
- for a shared critical success, independently resolved damage-only critical effect at each impact.

Both projectiles are created against the same target. No source rule was found that retargets the second projectile
after the first impact. The intended-target impact path does not reject an already dead target, so a lethal first
impact does not cancel the second. OpenArcanum therefore applies the second impact to the same stable ObjectID while
retaining the M8D/M8E positive-to-nonpositive guard: only the first crossing can create the death/corpse transition
and process XP/consequences.

The source does not reroll attack accuracy or critical classification per projectile. The ordered OpenArcanum impact
records repeat the shared attack/critical/ledger diagnostics and expose each impact's independent damage, mitigation,
critical damage effect, target, and resulting vitality.

## Transaction and result model

`CombatAttackRequest` remains the only authoritative attack request. Ranged preflight still validates roster/turn,
weapon, range, projectile traversal, AP, and ammunition before mutation. A hard block or other rejected request has
zero impacts and consumes no AP, ammunition, vitality, or turn authority.

`CombatAttackResult` remains the transaction envelope. It now contains an immutable ordered `Impacts` collection and
`ImpactCount`. `CombatAttackImpactResult` carries the stable target, shared roll/chance/ledger/classification fields,
per-impact critical-effect diagnostics, independent raw/mitigated normal and fatigue damage, and resulting vitality.
For backward compatibility, successful single-impact melee and ranged attacks expose one impact automatically.
Transaction-level damage values are the sums of all impacts; the legacy scalar critical-effect fields project the
first impact, while the impact list is authoritative for multi-impact diagnostics.

All impact plans are resolved before commitment. This preserves fail-closed behavior for unsupported critical or
death-script branches. The transaction then spends AP and ammunition once and applies ordered impact mutations only
through M4B vitality. M8D participant/death behavior and M8E consequences remain downstream owners.

Focused validation proves that one arrow is sufficient for an Expert two-impact attack and is consumed exactly once.
The authentic Bow continues to cost one normal M8C/M8B ranged action. Below Expert, including Apprentice, remains one
impact.

## Cover, range, called location, and death integration

Phase 4 does not duplicate Phase 2/3 calculations. A multi-impact request builds one modifier ledger and shares it
across both impact records. Called location, numeric cover, hard line-of-fire, Perception range, and Bow Master range
suppression are therefore calculated once per command. Bow Master still suppresses only the range entry; cover and
called-location entries remain applicable.

The lethal focused proof starts the authentic two-impact target at one hit point. The first impact crosses into death,
the second continues against the same target and lowers vitality further, and the same-identity corpse, XP award, and
processed consequence marker occur exactly once. A later consequence-processing request returns `AlreadyProcessed`
and does not replay XP.

## Source archaeology — Critical Dodge

The source first resolves the attack's ordinary hit and critical state. Only an ordinary hit invokes the defender's
Dodge skill. Dodge is a defender result driven by the defender's effective Dodge rank and Dodge training, and it is
available to both melee and ranged attacks.

A successful Dodge clears both attack hit and attack critical flags. If that Dodge invocation is itself a critical
success, the source rolls a secondary value from 1 through 100 and compares it with the defender's training table:

| Dodge training | Qualifying threshold |
| --- | ---: |
| None | 0 |
| Apprentice | 10 |
| Expert | 50 |
| Master | 100 |

When the secondary roll qualifies, the source restores the attack critical flag while leaving hit clear. The normal
attack flow then enters `combat_process_crit_miss`. Critical Dodge is therefore not a separate final source outcome;
OpenArcanum retains `CombatAttackOutcome.CriticalFailure` and exposes `CriticalDodge`, the Dodge critical roll, the
secondary threshold roll, and the selected threshold as diagnostics.

The bounded supported critical-miss branch remains self-hit when the existing M8F secondary effect roll is above 50.
It consumes the normal attack AP/ammunition and follows ordinary remaining-AP/turn progression. A nonqualifying Dodge
remains `Miss`. Unsupported injury/equipment branches still return `UnsupportedCriticalEffect` before AP,
ammunition, vitality, or turn mutation. Natural non-Critical-Dodge ranged critical failure retains its existing
fail-closed behavior.

## Save V1 and presentation boundary

Requests, impact plans/results, Dodge rolls, Critical-Dodge diagnostics, RNG state, roster, AP, and turn state are
transient. Save V1 serializes none of them. The focused save/load proof commits an Expert two-impact attack, saves and
loads V1, verifies that combat and the last attack/impact diagnostics normalize away, and verifies that committed
target vitality and arrow quantity remain authoritative.

No graphics or presentation owner participates in multi-impact or Critical-Dodge resolution. Existing Phase 1/2/3
physical and automated coverage continues to prove Original -> Enhanced -> Original rebuild independence.

## Physical Play Mode evidence

The proportional production proof used the existing TestTerrain composition, production `CombatStateService`, the
production PC, authentic Bow `G_1575DBCA_4990_C243_8184_524D51F7D533`, authentic 70-arrow stack
`G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`, and authentic Polar Bear Cub
`G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in `maps/arcanum1-024-fixed/47781512457.sec`.

- the below-Expert production PC produced exactly one Bow impact for one normal AP/ammunition transaction;
- a source-derived Expert using the authentic Bow produced two ordered same-target impact results, one AP cost, and
  one-arrow consumption;
- both Expert impact records shared the same production range and called-Arm ledger, while their damage rolls were
  independent;
- a high-Dexterity source-derived Master Dodge defender converted the PC's ordinary ranged hit into the existing
  `CriticalFailure` self-hit path at threshold 100; AP and one arrow committed once and remaining-AP turn ownership
  stayed with the attacker.

The run reported zero warnings and zero errors. The lethal first-impact/corpse/XP permutation was not repeated in the
physical matrix because the deterministic focused test already proves it through the same production service and
M4B/M8D/M8E authorities; no discrepancy required a duplicate physical permutation.

## Automated validation

- Unity production/test/editor compilation: clean.
- M8G Phase 4 focused EditMode: **17/17** passed; 0 failed, 0 skipped, 0 inconclusive.
- M8G Phase 3 focused EditMode: **12/12** passed; 0 failed, 0 skipped, 0 inconclusive.
- M8G Phase 2 focused EditMode: **17/17** passed; 0 failed, 0 skipped, 0 inconclusive.
- M8G Phase 1 focused EditMode: **16/16** passed; 0 failed, 0 skipped, 0 inconclusive.
- M8A-M8F combat regressions: **84/84** passed; 0 failed, 0 skipped, 0 inconclusive.
  - M8F Critical Resolution: 9/9
  - M8E Death Consequences: 7/7
  - M8D Defeat State: 11/11
  - M8C Ranged Combat: 12/12
  - M8B Turn-Based Combat: 23/23
  - M8A Core Combat State: 22/22
- Combined M8G Phase 1-3 plus M8A-M8F regression run: **129/129** passed.
- Complete EditMode suite: **803/803** passed; 0 failed, 0 skipped, 0 inconclusive.
- The complete suite emitted the same five intentional fail-closed dialogue warnings and zero errors. After clearing
  expected test diagnostics, the final Unity Console was 0 logs, 0 warnings, 0 errors.
- `git diff --check`: clean.

## Remaining source ambiguities and M8G closure

Projectile visual offsets, flight timing, animation/sound synchronization, and alternate intervening-object impact
remain deferred to a projectile-presentation/collision milestone. The source's object-list ordering and sub-tile
projectile path are not reproduced by Unity rendering; Phase 3's deterministic authoritative grid remains the bounded
geometry contract. Weighted ordinary hit location and unsupported critical injury/equipment/status effects remain
fail-closed/deferred.

The post-M8F audit named six required M8G areas: dynamic roster/engagement, exactly-once round boundary, structured
requests/called locations/ledger, hard block plus numeric cover, Bow mastery/multi-impact, and Critical Dodge. Phases
1-4 now cover all six. No required post-M8F audit item remains before M8G can close. Real-time scheduling, combat UI,
AI, followers, magic, technology, and other later systems require separate authorization and were not started.

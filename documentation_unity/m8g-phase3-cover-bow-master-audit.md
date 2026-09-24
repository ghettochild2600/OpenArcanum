# M8G Phase 3 — Numeric Cover, Hard Line-of-Fire, and Bow Master Range Exemption

Validated 2026-09-23 on `feature/session-save-load` with Unity 6000.0.71f1.

## Scope and status

M8G Phase 3 closes the bounded cover and Bow Master range gaps identified by the post-M8F combat audit:

- one authoritative projectile traversal result that distinguishes an impossible shot from numeric cover;
- source-flag-derived cover cost integrated once into the Phase 2 modifier ledger;
- source-authentic Bow Master exemption from the long-range Perception penalty;
- transactional hard-block rejection before AP, ammunition, vitality, or turn mutation;
- source-grid wall and portal edge cover, including open-portal and wall-passage exclusions.

This phase does not implement Expert/Master two-impact Bow attacks or Critical-Dodge reclassification, so M8G as a
whole is not complete. It also does not add Throwing, Firearms, projectile objects/presentation, combat UI, AI,
followers, magic, technology, or real-time combat. Save format remains V1.

## Source contract

The bounded source comparison uses the recovered `arcanum-ce` paths in `skill.c`, `ai.c`, `object.c`, and
`obj_flags.h`.

`skill_invocation_difficulty` asks `sub_4ADE00` for a straight projectile traversal. That helper uses
`OBJ_TRAVERSAL_PROJECTILE | OBJ_TRAVERSAL_IGNORE_CRITTERS`, skips objects on the target tile, and returns two distinct
facts: a blocking object and a numeric traversal cost. A blocking object makes the shot impossible. When no hard
block exists, positive traversal cost is ordinary attack difficulty.

The admitted object-flag mapping is exact:

| Relevant source flags | Result |
| --- | ---: |
| no `OF_SHOOT_THROUGH` | hard block |
| `OF_SHOOT_THROUGH`, without `OF_SEE_THROUGH` | +50 cover difficulty |
| `OF_SHOOT_THROUGH | OF_SEE_THROUGH | OF_PROVIDES_COVER` | +20 cover difficulty |
| `OF_SHOOT_THROUGH | OF_SEE_THROUGH`, without `OF_PROVIDES_COVER` | 0 cover difficulty |

The source adds each traversal contribution; it does not impose a separate cover cap. Phase 3 therefore sums distinct
encountered contributions and exposes their total as one signed ledger entry. The existing Phase 2 final-effectiveness
clamp to 0..100 remains the only cap. The focused matrix proves +20 and +50 individually and a stacked +70 result.

For range, the source safe distance is half effective Perception. Each additional tile contributes five difficulty
points. Masters of Bow, Throwing, and Firearms are exempt in the original game. Phase 3 implements only the already
supported Bow family: an ordinary Bow user receives `5 * max(0, distance - Perception / 2)`, while a character whose
Bow training is exactly Master suppresses that contribution. Throwing and Firearms remain unsupported attack
families and are not changed by Bow training.

## Authoritative geometry and traversal result

`SectorNavigationMap.GetProjectileTraversal` owns both `IsBlocked` and `CoverPenalty`. It uses the existing
authoritative 64x64 sector grid: source terrain/block masks, placed ordinary-object tiles, wall/portal ART rotation,
portal open state, and the existing odd-edge decomposition used for diagonal crossings. It does not inspect Unity
colliders, rendered sprites, alpha, bounds, or camera-visible geometry. Intervening critters remain ignored, and
ordinary objects on the target tile remain excluded.

Hard terrain, non-shoot-through ordinary obstacles, and non-shoot-through edge obstacles return `IsBlocked=true`.
Ranged attack preflight maps that result to `LineOfFireBlocked` before any gameplay mutation. A clear traversal
returns `IsBlocked=false` and the accumulated source cover cost, including zero.

Wall and portal objects remain directional edge objects rather than tile occupants. Phase 3 applies the same source
flag cover mapping only after the edge's authored ART rotation matches the crossed boundary:

- a qualifying shoot-through wall edge contributes numeric cover;
- a qualifying closed shoot-through portal edge contributes numeric cover;
- an open portal contributes neither a block nor cover;
- an existing source wall-passage ART piece contributes neither a block nor cover;
- a qualifying edge object is counted once even when diagonal decomposition inspects it through more than one edge
  probe.

Physical validation exposed that the first implementation applied numeric cover only to ordinary tile objects and
therefore omitted wall/portal edges. The final correction moved edge processing onto the same hard-block/cover result
and flag table. A focused shoot-through wall-edge regression protects the corrected path.

## Modifier ledger and Bow Master behavior

Ranged resolution queries projectile traversal once during preflight. A successful traversal passes its cover total
into `BuildRangedHitChance`, which inserts exactly one `Cover` ledger entry between Perception range and weapon to-hit.
The entry's `SourceValue` is the positive accumulated cost and its signed value is the corresponding negative attack
modifier. Clear shots retain a visible zero, non-applied cover entry.

The Perception-range entry always records the raw source-derived modifier. For Bow Master it remains visible and
`Applied`, but is marked `Suppressed`; the ledger sum excludes suppressed entries. This preserves diagnostics without
silently rewriting the source input. Bow mastery suppresses only that range entry. Cover, Armor Class, minimum
Strength, weapon to-hit, and called-location entries remain independently applicable. Focused coverage proves that
cover and the called Arm -30 modifier both remain active for a Bow Master, and that non-Bow range calculation is not
affected.

The ledger's final clamped effectiveness is the value compared with the ordinary attack roll. M8F then consumes that
ordinary hit/miss classification and retains its established critical roll order and bounded damage-only table.
Cover and range do not directly replace M8F's critical-percentage formula: critical-success/failure chance continues
to use base skill effectiveness plus the Phase 2 called-location bonus where applicable. They can prevent an ordinary
hit—and therefore the critical-success path—because the ordinary hit decision uses the ledger's final effectiveness.
The focused cover miss proves that this final value reaches the M8F classification path.

## Transaction and compatibility boundaries

A hard block returns before ammunition selection/consumption and before AP, vitality, death consequences, or turn
progression can change. Numeric cover is not a failure; it participates in the same deterministic ranged roll,
ammunition, AP, vitality, critical, and turn transaction already established by M8C-M8F. Failed unsupported critical
branches remain atomic under the existing M8F preflight.

Legacy melee behavior is unchanged and has no cover ledger entry. Clear ranged attacks retain their former outcome
with a zero cover stage, and existing callers of `HasProjectileLineOfFire` continue to receive the binary projection
of the new traversal result. Phase 1 roster/round authority and Phase 2 request/called-location behavior are not
reordered or duplicated. Requests, traversal diagnostics, results, and ledgers remain transient; Save V1 is unchanged.

## Physical Play Mode evidence

The proportional production proof used the existing TestTerrain composition and production `CombatStateService`:

- the production PC, authentic Bow `G_1575DBCA_4990_C243_8184_524D51F7D533`, and authentic 70-arrow stack
  `G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`;
- the authentic Polar Bear Cub `G_9B807B01_A142_4949_80CE_5A085F3BEEB1` in
  `maps/arcanum1-024-fixed/47781512457.sec` for clear and hard-blocked shots;
- authentic retail cover geometry in `maps/arcanum1-024-fixed/101535712980.sec` with a source-derived validation
  hostile placed at the selected production-grid endpoint;
- a source-derived Bow Master using the authentic Bow/ammunition against the authentic bear.

The clear Bow shot was legal and recorded cover zero. The hard-blocked shot returned `LineOfFireBlocked` with AP,
ammunition, target vitality, and current-turn identity unchanged. The authentic cover route accumulated 40 difficulty
and produced exactly one active `-40` cover ledger entry whose final effectiveness matched the attack chance. The Bow
Master proof retained a negative raw range entry but suppressed it, while the called Arm `-30` entry remained active.
The physical run reported zero warnings and zero errors.

The physical matrix intentionally used one representative proof per requested behavior. Automated tests own the
permutation matrix; no discrepancy required physically repeating every flag, range, cover, or called-location
combination.

## Automated validation

- Unity production/test/editor compilation: clean.
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
- Combined M8G Phase 1-2 plus M8A-M8F regression run: **117/117** passed.
- Complete EditMode suite: **786/786** passed; 0 failed, 0 skipped, 0 inconclusive.
- Final cleared Unity Console: 0 logs, 0 warnings, 0 errors.
- `git diff --check`: clean.

## Remaining source ambiguities and M8G work

The original straight-path helper samples sub-tile offsets before invoking object traversal. OpenArcanum's bounded
equivalent uses its existing authoritative tile/edge Bresenham traversal; it does not claim pixel-offset projectile
collision parity. The implementation admits only object types and edge semantics already represented by the current
navigation map. Projectile impact with an alternate intervening object remains separate and deferred, as do visual
arcs, offsets, sounds, and animation timing.

M8G still requires the source Expert/Master two-impact Bow transaction and Critical-Dodge reclassification. Those
must preserve one attack's AP/ammunition transaction, ordered impact results, exact-once M8D/M8E consequences, and
the established M8F roll ordering. They were not started in Phase 3. Real-time combat, combat UI, AI, followers,
magic, and technology remain later milestones.

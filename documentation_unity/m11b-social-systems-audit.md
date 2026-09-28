# M11B Social Systems Audit

Date: 2026-09-28
Branch: `feature/session-save-load`
Starting HEAD: `e734c2045d14878912c1a0f5528a37aada800322`

## Result

M11B is complete as the bounded, source-derived social authority. The implementation adds reaction composition,
typed reputation records, faction/party alliances, remembered directional hostility, source AI social thresholds,
the representable detected-theft boundary, dialogue integration, economy/combat consumers, and Save V1 persistence.
It does not create a second character-stat, party, combat, inventory, economy, or time authority.

## Source Findings

- Effective NPC-to-PC reaction is the unbounded checked sum of source base reaction, the existing M4 Beauty term,
  the existing M4 race matrix term, stored pairwise reaction adjustment, and every matching active reputation
  effect. There is no general Charisma reaction term in this path.
- Source disposition thresholds are exact: `<= 0` Hatred, `<= 20` Dislike, `<= 40` Suspicious, `<= 60` Neutral,
  `<= 80` Courteous, `<= 100` Amiable, and `> 100` Love. The final value is not clamped.
- `rules/gamerep.mes` defines reputation IDs 1000-1999. A reputation is an acquired/not-acquired record with an
  acquisition timestamp. Each row has three faction-grant slots and at most five `(reaction, origin, faction)`
  filters; all matching effects from all active reputations stack.
- Authentic examples used to constrain the parser include reputation 1000's global `+10`, 1001's faction-11
  `+15`, 1002's faction-12 `+10` and faction-13 `-100`, 1024's faction-7 grant and faction-7 `+100`, 1070's
  origin-16/faction-53 split, and 1075's faction-55 `-150`.
- Party membership is the existing M9B override. Outside the party, NPCs with the same nonzero faction are allies;
  a PC gains a faction only through an active reputation grant. Alliance suppresses remembered/KOS hostility.
- Retail AI packet fields 10 and 11 provide reaction-attack and alignment-difference thresholds. Social state
  supplies those answers to the existing M9A combat AI; it never starts combat or chooses an action itself.
- Source theft consequences depend on awareness/ownership context. The current engine has no authoritative general
  stealth, perception, LOS, or `notify_npc` service, so M11B accepts a caller-supplied detected result and validates
  offender, victim, direct item ownership, and a conscious nonparty NPC witness before recording hostility.
- Dialogue `re`, `rp`, and `co` were already parsed and implemented but omitted from the admitted M5A/M5B/M7C
  operation sets. M11B admits those existing operations and includes social state in dialogue rollback snapshots.
- M4 remains the authority for social skill ranks and derived reaction inputs. M11A remains the sole price formula
  authority and now consumes M11B's final reaction. M9B remains the party authority.

## Runtime Boundary

`SocialStateService` is coordinator-owned and binds immutable reputation and social-AI catalogs loaded from the
authentic module. It exposes typed add/remove/query reputation operations, exact reaction breakdowns and disposition,
faction/alliance queries, directional hostility, AI-threshold queries, and detected-theft reporting.

Persistent NPC state now retains source `AiData`, `Origin`, and `Faction`, with instance values taking precedence over
prototype fallback. Combat opponent checks use M11B alliances, remembered hostility, and source reaction/alignment
thresholds. Dialogue conditions/effects and M11A price previews read the same social authority.

Save format remains V1. The optional social domain stores ordered PC reputation acquisitions and directional
hostilities; ordinary object records store the three source social inputs. Load validates the complete document
before mutation, restores committed social/world state, supports earlier V1 documents without the optional domain,
and never restores a pending dialogue, theft, price, or combat transaction.

## Transaction and Failure Semantics

- Invalid or duplicate reputation operations do not partially mutate records.
- Unsupported dialogue tails roll back reaction, reputation, inventory, quest, party, and other transaction state
  together.
- Theft fails closed for invalid offender, victim, item, witness, legal/direct-ownership mismatch, and undetected
  input. It does not fabricate detection or mutate AP, inventory, vitality, or combat turn state.
- Remembered hostility is directional and idempotent. Social state informs combat eligibility but does not issue a
  combat action or activate combat.
- Invalid social save data is rejected before any live session mutation.

## Automated Validation

Focused M11B EditMode: **31/31 passed**.

The focused matrix covers authentic `gamerep.mes` parsing; exact unbounded reaction components and all disposition
thresholds; dynamic M4 recomputation; typed/idempotent reputation records; origin/faction filters and stacking;
faction and party alliances; reaction/alignment AI thresholds; remembered hostility; fail-closed and successful
theft; dialogue reaction/reputation/cooperation transaction behavior and rollback; M11A price consumption; Save V1
round trip, older-V1 compatibility, object-input restoration, and invalid-save atomicity.

Directly affected regressions: **143/143 passed**.

- M4D derived social inputs: 25/25
- M5A dialogue operations: 16/16
- M6A save/load: 25/25
- M8A combat enrollment: 22/22
- M9A combat AI hostility: 15/15
- M9B party foundation: 16/16
- M9B dialogue/party: 4/4
- M11A economy pricing: 20/20

Complete EditMode: **983/983 passed**, with **0 failed, 0 skipped, and 0 inconclusive**. The baseline increased from
952 by the 31 focused M11B cases.

## Physical Production Validation

Computer Use drove the existing Unity Editor and TestTerrain Play Mode; no second Editor was launched. The accepted
run used authentic Tarant inventor `G_C626B82F_5190_2C40_995A_00BCB987F7A5` in
`maps/arcanum1-024-fixed/68853695432.sec`, with its retail multiplier 200 and inventory source 7.

The production proof established:

- authentic component reaction `43`, followed by one production social/dialogue adjustment `+5`;
- authentic global reputation 1000 adds exactly `+10`, producing committed reaction `58`;
- the M11A quote changed from `16` to `15` while consuming that final reaction;
- authentic reputation 1024 grants source faction 7, then removes cleanly after the bounded proof;
- remembered hostility makes the merchant a combat opponent without starting combat;
- a detected theft of an authentic merchandise prototype, directly owned by the merchant, records the authentic
  merchant witness and the same directional hostility without inventing detection;
- Original -> Enhanced -> Original rebuilds preserve reaction, reputation, hostility, and merchant price authority;
- Save V1 restores reaction, reputation, hostility, `AiData`, origin, and faction, while combat remains transient.

The accepted run reported **0 warnings and 0 errors**. Unity compilation was clean. The complete suite's expected
fail-closed dialogue diagnostics were cleared; the final Console showed **0 logs, 0 warnings, and 0 errors**.
`git diff --check` was clean.

## Closure Assessment

M11B core is closed. No Phase 2 is required for reaction, reputation, faction alliance, remembered hostility, or the
currently representable theft boundary.

Deferred to their owning systems:

- stealth, perception, line of sight, awareness propagation, witnesses beyond the explicit caller boundary, and
  `notify_npc` behavior;
- campaign/script-specific reputation awards, removals, faction diplomacy, crime consequences, guard escalation,
  fines, jail, and city reactions;
- assault and murder consequence orchestration beyond existing M8/M9 death/combat/script authority;
- follower loyalty, reaction/alignment departures, charm/control, resurrection, and party UI;
- final dialogue/social/reputation UI, character-sheet presentation, feedback, and localization.

Unsupported or rejected mechanics remain fail-closed. M11B does not invent a generic crime ledger, faction-wide
hostility propagation, reputation decay, hidden reputation ranks, inferred theft detection, a universal Charisma
reaction bonus, or undocumented meanings for unused reputation fields. Those behaviors require direct source proof
and the missing owning systems rather than expansion of this milestone.

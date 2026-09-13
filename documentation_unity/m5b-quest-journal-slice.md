# M5B authentic quest completion and journal projection

## Pre-implementation source audit (2026-09-12)

This audit was completed before production changes. It inspected the shipped quest metadata, journal descriptions,
dialogue resources, script resources, placed mobile records, and the recovered `arcanum-ce` implementations of
`quest.c`, `dialog.c`, `item.c`, and the logbook UI.

### Quest-state and journal source contract

- Per-PC states are `Unknown(0)`, `Mentioned(1)`, `Accepted(2)`, `Achieved(3)`, `Completed(4)`,
  `OtherCompleted(5)`, and exposed `Botched(6)`. A botch is stored as bit `0x100` over the prior raw state.
- Normal state assignment is monotonic. Completed, other-completed, and botched are terminal. The separate
  `quest_unbotch` operation can reopen a botched quest, but that explicit recovery operation is outside M5B.
- Every accepted mutation replaces the quest's two-part game-time timestamp (`days`, `milliseconds`). The logbook
  includes every non-unknown quest and sorts the current entries by that timestamp, earliest first.
- The logbook does not select state-specific quest prose. It prefixes the source state label and then displays the
  one quest description from `gamequestlog.mes`, using `gamequestlogdumb.mes` instead for Intelligence <= 4.
- Completion first awards the quest's source XP, then commits the terminal state. The internal commit timestamps the
  quest, applies the quest metadata alignment adjustment, and adds 10 reaction to the source NPC when supplied.
  Duplicate terminal requests do none of those things.

### Candidate classification

- **RED — quest 1130 (M5A).** Leonid Anderson can complete it, but his dialogue never exposes a terminal-specific
  follow-up: its completion condition still passes after completion. The botch route is Grak's death script and
  therefore requires combat/death semantics.
- **RED — quest 1136.** Ryan Sanders has a clean Completed follow-up, but the completion response is available only
  from Achieved; reaching Achieved depends on the quest's missing combat/death chain. It also awards gold.
- **RED — quest 1137.** David Witt has Accepted and Completed entry conditions, but Accepted cannot advance through
  his dialogue. The Achieved transition is in a kill/transform script.
- **RED — quest 1119.** Completion depends on another quest, an external global, and journal-item transfer.
- **RED — quest 1040.** Completion is restricted to the low-Intelligence path and coupled to quest 1013 and multiple
  globals.
- **YELLOW — quest 1081.** Its dialogue can complete directly from Accepted, but the selected branch also requires
  met-before/invisibility script support, a global prerequisite, explicit alignment and fate-point effects, and a
  local flag that changes later interactions to a float line.
- **GREEN — quest 1005.** The Black Root mayor's SAP_DIALOG is the already-supported two-line
  `True -> Dialog(1) -> Return` form. From an authentic Accepted setup with the mayor's dagger (object name ID 2002,
  prototype 6071),
  source response 4 enters node 100 and response 102 performs `qu 1005 4, in 2002`: complete the quest, then transfer
  the dagger from PC to mayor. Re-engagement exposes the exact Completed branch at source response 5 and excludes the
  pre-terminal dagger branch. The route uses the existing M3 inventory state; the optional authored payment is the
  straightforward source gold transfer already representable as the M3 Gold stack (prototype 9056).

Selected placed NPC:

- ObjectID: `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`
- Prototype: `17088`
- Dialogue/script: `1009`
- Sector: `maps/arcanum1-024-fixed/96636765255.sec`
- Quest: `1005`, XP `800`, completion alignment `+50`

No source dialogue or journal prose is copied into this document; production continues to read it from the user's
locally mounted original data.

### Smallest implementation change set

1. Extend `CampaignStateService` with source-shaped quest timestamps and transition preflight, while keeping it the
   sole quest/global/local state owner.
2. Add a read-only `JournalProjectionService` over `CampaignStateService` plus the existing `QuestLog` parser, and
   bind the four shipped quest resources when the sector loader mounts source data.
3. Extend the production dialogue transaction only for quest 1005's audited vocabulary: `qb`, `in`, `re`, dagger
   transfer, source completion XP/alignment/reaction, and the optional positive gold award.
4. Add rollback snapshots for the exact authoritative domains touched by one response. Do not let the dialogue or
   journal presenters own any of those values.
5. Add a minimal read-only journal panel, focused M5B EditMode tests, and a physical validation command using the
   stable mayor ObjectID.

## Implemented ownership and call graph

```text
WorldMapSessionCoordinator
  +- CampaignStateService        authoritative globals, local script state, quest state/timestamps
  +- CharacterProgressionService authoritative XP/level/character points
  +- CharacterDerivedStatService authoritative alignment and pairwise reaction adjustment
  +- persistent object state     authoritative dagger/gold identity, placement, quantity
  +- ProductionDialogueSession   transient stable-ID conversation + per-response transaction
  |    +- strict ScriptVm SAP_DIALOG entry
  |    +- DialogScriptEvaluator strict tests/effects and complete-effect preflight
  |    +- existing inventory transfer/capacity authority
  |    `- source generated-dialog text resolver
  `- JournalProjectionService    read-only CampaignStateService + QuestLog projection

WorldObjectSectorLoader          mounts source resources and binds resolvers only
ProductionDialoguePresenter     observes dialogue state only
ProductionJournalPresenter      observes journal projection only
Unity WorldObject/sprite owners present stable identities only
```

No presenter, `WorldObject`, loader, or scene object owns quest, reward, inventory, progression, alignment, reaction,
or journal state. Dialogue transactions snapshot and restore campaign, progression, derived-stat, inventory placement,
quantity, tombstone, and dynamic-identity state before an admitted response changes anything.

## Authentic quest 1005 contract

- Mayor: `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`, prototype 17088, SAP_DIALOG script/dialogue 1009, sector
  `maps/arcanum1-024-fixed/96636765255.sec`. The placed source mobile is
  `maps/arcanum1-024-fixed/g_abd47a78_6190_4e2b_a691_f582800b2bb3.mob`.
- Dagger: `G_52E2AC87_1A3B_6842_8C2E_5247C9571D11`, weapon prototype 6071, instance-level `OBJ_F_NAME` 2002.
  The semantic ObjectID is read from the mobile record; its VFS filename uses source byte ordering. The item is authored
  beneath NPC `G_209BECF5_6598_1541_B19C_EFBA71B268E3` in
  `maps/arcanum1-024-fixed/96435438664.sec`. Dialog `in 2002`/`ni 2002` calls source `item_find_by_name`; 2002 is not a
  prototype number and no NPC is selected, cloned, or substituted.
- Acceptance: Mentioned(1), response 3, effect `qu 1005 2`, Accepted(2). Source behavior raises the mayor's reaction to
  at least 41; it does not reduce a higher source-derived initial reaction.
- Completion: Accepted response 4 (`qb 1005 3, in 2002`) enters node 100. Response 102 executes
  `qu 1005 4, in 2002` and enters node 430. Complete-effect preflight first verifies the legal terminal transition and
  exact name-ID-owned dagger transfer. Execution then preserves recovered source order: award quest XP, commit and
  timestamp Completed(4), apply quest alignment and completion reaction, and transfer the dagger from PC to mayor.
- Rewards: quest metadata supplies XP 800 and alignment +50; completion adds +10 to the mayor's reaction. Response 431
  then enters node 440, whose source NPC effect `$$100` awards 100 gold through the existing M3 Gold stack/capacity
  authority. Gold is source built-in prototype 9056 (source world AID `0x60000003`, inventory AID `0x60001003`), which
  `proto.c` constructs even though there is no required loose 009056 `.pro` file. The gold node is a second authored
  response transaction, not silently folded into response 102.
- Follow-up: on the next conversation, Completed and reaction >=41 expose response 5 and exclude response 4. Response
  5 is source token `t:`. Its visible label is resolved from the original generated-dialog 500-599 range. Interactive
  training is outside M5B, so selecting this token emits dialogue/script/line telemetry and fails closed without
  changing quest, timestamp, XP, gold, dagger, alignment, or reaction.

## Journal and lifecycle contract

`CampaignStateService` stores the raw source-shaped state plus `{days,milliseconds}` on every accepted mutation. M5B
uses a deterministic in-session clock until M6 supplies persisted game time. `JournalProjectionService` includes every
non-Unknown source quest and sorts by current timestamp, then quest number. Retail data has no separate quest title or
state-specific prose: the projection exposes the source state label (`Accepted`, `Completed`, and supported terminal
labels) plus the one `gamequestlog.mes` description, choosing `gamequestlogdumb.mes` only for Intelligence <=4. It has
no mutation API. The minimal IMGUI panel is explicitly presentation-only.

The exact dagger state, Gold stack, quest state/timestamp, XP, level/points, vitality, alignment/reaction, and journal
projection survive Original -> Enhanced -> Original rebuild, the dagger-sector -> mayor-sector traversal in both
directions, mayor-sector unload/reload, and presentation recreation. Retained foreign-sector dagger placement suppresses
its authored source record, so it does not resurrect. One coordinator, object owner/root, navigation controller,
interaction controller, dialogue presenter, journal presenter, PC presentation, mayor presentation, and sprite owner
remain after every lifecycle operation.

## Validation (2026-09-13)

- Focused M5B EditMode: **18/18 passed**, 0 failed, 0 skipped, 0 inconclusive.
- Required regression suites: M5A 16/16, M4D 25/25, M4C 29/29, M4B 20/20, M4A 13/13, M3E 21/21,
  M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8, M2A 16/16, PlayerNavigation 21/21,
  M1A 7/7, M1B 11/11, WorldSessionState 27/27, and PortalArtResolver 2/2. Every suite had zero failures,
  skips, or inconclusive tests.
- Complete EditMode: **457/457 passed**, 0 failed, 0 skipped, 0 inconclusive. Final Unity compilation was clean.
- Computer Use Play Mode: loaded the authored dagger sector, retained the exact dagger through the inventory authority,
  loaded the real mayor sector, established Accepted through response 3, and literally clicked the visible mayor from
  out of range. The production PC approached and opened dialogue 1009. Physical selections 4 -> 102 -> 431 -> 443
  transferred the dagger, produced Completed journal state, awarded 100 Gold and 800 XP, applied alignment +50 and
  reaction +10 (real source-derived 43 -> 53), and ended normally. A second literal mayor click showed source response
  5 and no response 4; no reward repeated. Graphics rebuild, A -> B -> A traversal, foreign-source suppression,
  sector reload, stable references, and all uniqueness checks passed. Final Play Mode Console: **0 warnings, 0 errors**.

## Deliberately deferred

Interactive `t:` training UI/transactions, other special dialogue tokens, broad dialogue/script opcode families,
additional quest families and alternate branches, reputation/faction/social-memory rules, combat/death, followers,
travel, economy UI, final journal UI, and save serialization remain out of M5B.

The exact recommended next task is **M5C — one bounded production trainer-dialogue slice**: implement the source `t:`
special-handler transaction against the existing M4C skill/training authority, including cost/precondition/UI
preflight, strict telemetry, cancellation, idempotency, and lifecycle validation, without broadening to barter or other
special tokens. M6 remains the subsequent versioned save/load foundation.

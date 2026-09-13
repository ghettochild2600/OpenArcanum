# M5A production dialogue and quest-state slice

## Pre-implementation audit (2026-09-12)

The source/content audit was completed before production changes. It inspected the shipped dialogue and quest data, the object script attachments, `ScriptVm`, `ScriptDatabase`, `ScriptGlobals`, `DialogConversation`, `DialogScriptEvaluator`, the gallery harness, the world interaction kernel, and the recovered `arcanum-ce` implementations of `anim.c`, `ai.c`, `dialog.c`, `quest.c`, and `script.c`.

The local corpus contains 2,399 parsed scripts with zero parse failures. The candidate scan read 12,723 mobile records, found 3,792 NPC records, 789 persistent NPCs with resolvable dialogue, and 533 structurally closed state loops. The user's earlier 2,396 figure was therefore treated as a historical count; 2,399 is the reproducible count for the currently mounted clean source data.

### Existing responsibility classification

- **A — parser/data model:** `DlgReader`, `DialogScript`, `DialogLine`, `DialogLocator`, `ScriptDatabase`, the script model/reader, and `QuestLog` already parse the relevant source resources.
- **B — demo/gallery:** `DialogScriptGallery` can explore source dialogue, but its synthetic host and mutable local harness are not production state or a safe gameplay entry point.
- **C — reusable production logic:** `ScriptVm.ExecuteStrict`, stable `ArcanumObjectId`, the M1–M4 session/character services, `DialogConversation`, and the existing interaction approach/cancellation path are reusable.
- **D — missing authoritative runtime state:** source-sized global variables/flags, source quest transition rules, and per-object/per-SAP flags and counters were not owned by persistent session state. Dialogue had no production session lifecycle.
- **E — unsupported production semantics:** the selected slice does not require broad VM work. Its SAP_DIALOG uses only `LocalFlag`, `True`, `Dialog`, `Goto`, `DoNothing`, and the two return actions. The selected dialogue path needs only `lf` and `qu`; unrelated response tests/effects remain fail-closed with diagnostic telemetry.

`dialog.c` establishes two ordering rules used by this slice: an NPC line's effect executes when that line is entered, before it is presented, and a selected player response executes its effect once before following its target. `quest.c` establishes the per-PC states `Unknown(0)`, `Mentioned(1)`, `Accepted(2)`, `Achieved(3)`, `Completed(4)`, `OtherCompleted(5)`, and `Botched(6)`, with monotonic transitions and terminal completed/botched states. Global quest state is a separate reduced value and defaults to accepted.

Source talking range is also explicit: a PC can begin dialogue when `location_dist < ai_max_dialog_distance`, and `ai_max_dialog_distance(PC)` is 5. In integer Chebyshev tiles this means a start range of 4; an out-of-range talk action approaches to the source move-near range of 1.

## Candidate audit

- **GREEN — Thomgrak, dialogue 1760.** The SAP_DIALOG is entirely within the bounded strict VM surface. The authentic first path can set dialog-local flag 1 and enter a node that advances quest 1130 from unknown to mentioned. On the next interaction the local flag makes the script open line 140, whose authentic response set is quest-state conditioned.
- **YELLOW — Renee, dialogue 1520.** Compact closed loop, but the entry script reads and mutates reaction, requiring a new authoritative reaction-delta domain.
- **YELLOW — Pillar of Truth, dialogue 1168.** Compact quest mutation, but requires story-state preconditions plus rumor effects and does not offer as small a natural initial setup.
- **YELLOW — Pelojian, dialogue 1921.** Closed loop, but the relevant path reveals a map area and its entry script also branches on another object's death.
- **YELLOW — Captain Roseborough guard, dialogue 2762.** Requires reaction, charisma, money, alignment, and quest-botch behavior.
- **YELLOW — Bates factory guard / Brinda, dialogues 2074 and 2291.** Their post-mutation re-interactions resolve to float-line behavior rather than a second player-choice branch.
- **RED for M5A — Riddler 2, dialogue 1035.** Quest branching is authentic, but wrong paths invoke combat.

## Selected authentic content

- NPC ObjectID: `G_DF753C8F_B655_D411_8F1D_00A0CC6511C6`
- Prototype: `17232`
- Sector: `maps/arcanum1-024-fixed/68786586569.sec`
- Mobile: `maps/arcanum1-024-fixed/g_8f3c75df_55b6_11d4_8f1d_00a0cc6511c6.mob`
- Dialogue/script resource: `dlg/01760thomgrak.dlg` / script `1760`
- Persistent state: SAP_DIALOG local flag 1 and PC quest 1130
- Initial branch: SAP_DIALOG opens authored node 1. The selected unconditional normal-INT response (line 11) sets local flag 1 and enters node 60; entering node 60 advances quest 1130 to Mentioned.
- Second-conversation branch: local flag 1 makes SAP_DIALOG open authored node 140. Its available responses are conditioned by quest 1130 and therefore differ from the initial branch.

No dialogue prose is reproduced here; the runtime reads it from the user's locally mounted source data.

## Smallest production change set

1. Replace the session's permissive script-global store with a source-bounded campaign-state service that also owns per-object/per-SAP flags/counters and source-faithful quest transitions.
2. Retain each NPC's effective dialogue number in persistent object state, not in its Unity presentation.
3. Add a stable-ID production dialogue host/session that uses `ScriptVm.ExecuteStrict`, the existing parsers, and a narrow strict dialogue evaluator/context.
4. Add `Talk` to the existing interaction command, hit-test, approach, navigation-cancellation, and session execution path using start range 4 / approach range 1.
5. Add one minimal presentation-only dialogue panel and bind it to the session; cancel deterministically on target loss or sector unload.
6. Add focused edit-mode tests and an editor validation command for the exact authentic slice. Do not add combat, followers, inventory dialogue actions, final journal UI, save serialization, or broad corpus compatibility.

## Final implementation and validation

Completed on 2026-09-12 with Unity 6000.0.71f1 on `feature/inventory-commands`.

### Ownership and call graph

```text
literal Game-view click
  -> PlayerClickMoveInput (hit test only)
  -> PlayerInteractionController.TryTalk (transient approach/cancellation)
  -> existing PlayerNavigationController/source grid
  -> WorldMapSessionCoordinator.ExecuteInteraction(Talk)
  -> ProductionDialogueSession.Start(stable PC ObjectID, stable NPC ObjectID)
  -> strict SAP_DIALOG execution through ScriptVm
  -> source DialogScript + strict DialogScriptEvaluator
  -> CampaignStateService / existing M4 character services

ProductionDialoguePresenter
  <- observes ProductionDialogueSession
  -> submits only the selected response index or cancellation
```

`WorldMapSessionCoordinator` owns the one `CampaignStateService` and lazily owns the one authoritative
`ProductionDialogueSession`. `PersistentObjectState` retains the effective dialogue number resolved from the source
instance/prototype. `WorldObject`, `WorldObjectSpriteOwner`, the sector loader, the NPC GameObject, and the IMGUI panel
remain disposable projections and do not own campaign or conversation decisions.

### Campaign and quest state contract

- Global variables and PC variables are source-sized signed Int32 arrays of 2,000 entries. Global and PC flags are
  separate 3,200-bit stores. Invalid indices fail explicitly.
- Per-object script state is keyed by stable `ArcanumObjectId` plus attachment point. Its 32 local flags and four
  byte-sized counters reproduce the state carried by the source script attachment rather than storing state on an NPC
  component.
- PC quest IDs 1000–1999 use `Unknown(0)`, `Mentioned(1)`, `Accepted(2)`, `Achieved(3)`, `Completed(4)`,
  `OtherCompleted(5)`, and `Botched(6)`. Transitions are monotonic; completed/other-completed/botched states are
  terminal. Global quest state is separate and defaults to Accepted, matching the recovered source initialization.
- A response transaction snapshots campaign state. Unsupported or failed response/node execution restores the full
  snapshot, so no partial global, quest, PC, or NPC-local mutation survives.
- This state is in-memory and session-owned. Disk save serialization remains M6 work.

### Production dialogue contract

The session has explicit `Idle`, `Starting`, `Active`, `AwaitingPlayerChoice`, `ExecutingResponse`, `Completed`, and
`Cancelled` phases. It stores stable participant ObjectIDs, the source dialogue number/resource, current source line,
and deterministic response records. No Unity instance ID or presentation reference enters authoritative dialogue
state.

The selected SAP_DIALOG is preflighted before execution. M5A admits only script conditions `True`/`LocalFlag` and
actions `Dialog`, `Goto`, `DoNothing`, `ReturnAndSkipDefault`, and `ReturnAndRunDefault`. The dialogue evaluator admits
only response tests `gf`, `qu`, and `ra`, and effects `lf`, `qu`, and `fl`. Race, intelligence, alignment, reaction, and
skill values are queried through the existing authoritative M4 services; only race and intelligence are exercised by
this target. Any condition/effect outside the admitted set fails closed and emits one deduplicated diagnostic with
dialogue number, line, NPC identity, failure class, and detail. Normal successful execution emits no compatibility
warning.

NPC node effects execute on node entry before presentation. A player response effect executes once before following
its target. An active session rejects duplicate starts. Missing resources, invalid participants, unsupported script
operations, target loss, and sector unload are explicit failures/cancellations. A graphics-only rebuild does not restart
or duplicate the session.

### Authentic closed loop

Thomgrak's first SAP_DIALOG evaluation opens source node 1. For the default Human Male production PC, the exact
available source response lines are 2, 11, 12, and the generated goodbye at 19. Physical validation selected authored
line 11. It set SAP_DIALOG local flag 1, entered node 60 (whose NPC-entry effect changed quest 1130 to Mentioned), then
selected authored line 61, which advanced quest 1130 to Accepted and followed `fl 70`. The final node closed normally.

The second literal click ran the real SAP_DIALOG again. Local flag 1 selected source node 140 instead of node 1.
Accepted quest state exposed authored response 143 and excluded the Mentioned-only response 145, proving the closed
dialogue -> campaign state -> dialogue loop without validator-injected branching.

### Presentation and lifecycle

The M5A UI is intentionally utilitarian: it displays the current NPC text, up to five source-filtered responses, number
keys/click selection, and cancel. It projects the session only. It does not resolve conditions, execute effects, retain
identities, or store campaign state.

Computer Use physically clicked the rendered Thomgrak sprite from a reachable out-of-range tile. The exact stable
ObjectID was selected; the production PC moved through the existing grid navigation to source talk range, and one
authoritative Talk command opened the dialogue. The same run proved:

- start range 4 and out-of-range approach range 1;
- normal response selection and conversation completion;
- local flag 1 and quest 1130 Accepted committed once;
- a second physical NPC click selected changed source node 140;
- repeated Talk returned dialogue-busy without restarting the active session;
- Original -> Enhanced -> Original rebuild retained the same active line, ObjectIDs, and campaign state;
- active sector reload cancelled dialogue deterministically, retained the authoritative NPC/state objects, and
  restored one NPC projection;
- the changed branch still opened after reload;
- replacement navigation cancelled a pending Talk approach;
- removing the NPC presentation before arrival produced target-not-found without opening dialogue;
- one coordinator, loader, navigation controller, interaction controller, dialogue presenter, `WorldObjects` root,
  production-PC projection, and Thomgrak projection remained, with no orphan/duplicate sprite owners.

The Play Mode harness recorded **0 new warnings and 0 errors**.

### Automated validation

- M5A focused: **16/16**.
- M4D: **25/25**; M4C: **29/29**; M4B: **20/20**; M4A: **13/13**.
- M3E: **21/21**; M3D: **18/18**; M3C: **13/13**; M3B: **13/13**; M3A: **8/8**.
- M2B: **8/8**; M2A: **16/16**.
- PlayerNavigation: **21/21**; M1A: **7/7**; M1B: **11/11**.
- WorldSessionState: **27/27**; PortalArtResolver: **2/2**.
- Complete EditMode suite: **439/439**, with zero failures, skips, or inconclusive tests.
- Final Unity compilation: clean.

### Deliberate M5A limits and next slice

M5A does not implement the entire dialogue corpus, broad script opcodes, journal UI, quest rewards/completion, reaction
mutation, reputation/factions, barter/training, followers, combat, travel, clocks, save/load, or final dialogue UI.
Unsupported content stays fail-closed and observable.

The exact recommended next task is **M5B — one authentic quest-completion and journal-projection slice**: continue one
audited real quest from Accepted through its source completion/botch boundary, add only the journal metadata and strict
script/effect vocabulary that case requires, and preserve the M5A stable-ID/campaign/dialogue ownership model. Do not
broaden into combat, followers, travel, economy, or save serialization.

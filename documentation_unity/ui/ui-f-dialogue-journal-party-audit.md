# UI-F Dialogue / Journal / Party Source Audit

## Status and boundary

This is a read-only source-archaeology result. It changes no production runtime, test, scene, prefab, or retail data.
The audit traces the actual `arcanum-ce` creation and event paths before treating an ART name or decoded bitmap as
screen evidence. OpenArcanum gameplay authority remains unchanged.

The principal result is that UI-F is not one safe presentation-only milestone:

- Dialogue has strong conversation authority, but its current full modal is not the source composition or input
  ownership.
- The Logbook has a completely bounded visual shell, but only the Quests tab has a complete source-text projection.
- Party membership, vitality, movement, combat allegiance, and the six global F-key broadcasts are strong, but the
  source follower HUD needs portrait/poison projection and individual-follower command authority that do not yet
  exist.

UI-F should therefore be implemented in three separately gated slices. It must not be represented as a grouped skin
swap over the current generic Dialogue, Journal, and Party panels.

## Evidence hierarchy

Controlling evidence:

1. `arcanum-ce/src/ui/dialog_ui.c`, `src/game/tc.c`, and the dialogue portions of `src/ui/intgame.c`;
2. `arcanum-ce/src/ui/logbook_ui.c`;
3. `arcanum-ce/src/ui/follower_ui.c`, `src/game/broadcast.c`, and the follower portions of `src/ui/intgame.c`;
4. OpenArcanum production services, controllers, presenters, and retained source data;
5. `art/interface/interface.mes` identities and local decoded references.

Asset names and decoded dimensions remain corroboration. In particular, ID 822 `dialoguebox.art` has no traced
runtime call site and is not evidence for the ordinary retail conversation path.

## Executive findings

| Family | Source runtime composition | OpenArcanum today | Safe conclusion |
|---|---|---|---|
| Dialogue | NPC speech is a world text bubble. Up to five PC responses are drawn by `tc` over a darkened live-world strip above the HUD. Dialogue mode still permits world scrolling and click movement. | Authoritative dialogue is strong, but the production UI is one generic blocking modal that draws NPC and PC text together and accepts up to nine response buttons. | Reconstruct the source bubble/response presentation over the world. Do not build an ornate portrait dialogue screen or use ID 822 without a proven call path. |
| Journal / Logbook | One locked 800 x 400 big window at source y=41: left sidebar, two-page book, seven tab buttons, page arrows, headers/footers, PC lens, source text flow. | Quest projection is authoritative; the current presentation is a generic quest-only list. Several non-quest domains have partial state but no complete logbook projection. | The book shell and Quests tab are presentation-safe. Other tabs require bounded data-authority/projection closure first. |
| Party / Followers | Persistent left-edge HUD stack of up to six 40 x 49 portrait slots, HP/fatigue/poison bars, toggle/scrollers, and an eight-command per-follower context menu. | Party state, movement, combat integration, save/load, and global F1-F6 broadcasts are strong. The visible `P` screen is a generic modal list with Dismiss only. | Replace the modal concept with a HUD-owned follower strip only after portrait/poison and individual-follower command boundaries are explicit. |

## Dialogue

### Actual runtime path and ownership

`dialog_ui_start_dialog` rejects a dead/unconscious PC, rejects an unavailable speaker, exits combat only when the
source permits it, loads and initializes the dialogue, calls `intgame_dialog_begin`, rotates the participants, removes
concealment, and then calls `dialog_ui_display` (`dialog_ui.c:118-207`). `intgame_dialog_begin` calls `tc_show()` and
switches to `INTGAME_MODE_DIALOG` (`intgame.c:5889-5896`). It does not create a separate 800 x 400 screen.

`dialog_ui_display` sends the NPC line to `dialog_ui_npc_say` as a white, non-expiring text bubble and then clears and
sets the local response options (`dialog_ui.c:410-427`). `dialog_ui_npc_say` removes the prior bubble, calls `tb_add`,
sets expiry, and starts speech audio (`dialog_ui.c:431-444`). The selected local-PC response is not drawn as a local PC
bubble: `dialog_ui_pc_say` only adds a PC bubble when the speaker is not the local PC (`dialog_ui.c:448-460`).

The response compositor is `src/game/tc.c`, not ID 822:

- exactly five option lines (`TEXT_CONVERSATION_LINES = 5`);
- source font ID 229 `flare12font.art`, white with shadow and yellow on hover; a red variant exists but its controlling
  flag is never set (`tc.c:196-215`, `471-480`);
- default content rectangle 400 x 100, horizontally centered;
- normal y is `iso height - 100 - 159`, immediately above the ordinary bottom HUD; compact y is
  `iso height - 100 - 37` (`tc.c:385-422`);
- 6 px horizontal padding, 3 px vertical spacing, and five equal hit rows;
- the width expands to the widest response while remaining centered (`tc.c:445-491`);
- no response wrapping or scrolling: dialogue loading uses `tc_check_size`, which explicitly measures overflow
  because the conversation strip does not wrap (`tc.c:582-609`);
- the backdrop is the live iso window copied, darkened by `(18,18,18)` subtraction, and copied back before transparent
  response text is composited (`tc.c:619-683`);
- `tc_handle_message` selects only on mouse-down and returns the selected row (`tc.c:515-579`).

The source dialogue mode is not a conventional blocking modal. `INTGAME_MODE_DIALOG` retains scrolling and handles
world clicks as PC movement (`intgame.c:724-744`, `2970-3003`). It does own response-event precedence through
`dialog_ui_message_filter`, which asks `intgame_dialog_get_option` first and delegates other messages back to intgame
(`dialog_ui.c:357-376`).

### Frame and portrait evidence

| Evidence | Exact source use | Audit result |
|---|---|---|
| ID 229 `flare12font.art` | Response text in `tc_init` | Proven ordinary dialogue asset. |
| ID 354 `dialoguewindow.art`, 410 x 107 | Registered at `(196,492)` for both `ROTWIN_TYPE_MSG` and `ROTWIN_TYPE_DIALOGUE`; dialogue text rect is `(211,507,383,84)` and five invisible dialogue controls are created (`intgame.c:286-298`, `450-457`, `3919-3925`). | Source-associated lower-HUD rotating-window component, not a complete dialogue screen. The audited start/display path still puts NPC speech in a world bubble and replies in `tc`; physical retail confirmation is required before assigning additional dialogue content to this lens. |
| ID 822 `dialoguebox.art`, 325 x 136 | No literal or table-driven call site found in the ordinary dialogue path. | Rejected as ordinary-dialogue authority. Do not implement or 9-slice it from its filename/appearance alone. |
| Speaker portrait/name panel | No portrait fetch or portrait draw occurs in `dialog_ui_start_dialog`, `dialog_ui_display`, or `tc`. | The previously documented portrait-modal assumption is rejected. |

### Special transitions

The source dialogue result switch can suspend or end the conversation for barter, passive NPC Character Sheet,
World Map, Schematics, Identify, and Repair (`dialog_ui.c:300-351`). OpenArcanum's
`ProductionDialogueSession` has a strong transactional lifecycle, source conditions/effects, training subflow, voice
integration, and `CharacterExamination`, but `DialogueSpecialView` currently exposes only Character Examination.
Other source special transitions must not be inferred merely because separate merchant/map/schematic services exist.

### Current OpenArcanum gap

`ProductionGameUiPresenter.DrawDialogue` draws NPC text plus every available response in a generic framed modal;
the keyboard route accepts as many as nine numeric responses. `GameUiController.IsModalOpen` treats every non-None
screen, including Dialogue, as input-blocking. This differs from the source in composition, five-response limit,
world anchoring, and dialogue-mode movement/scroll ownership. There is no world text-bubble presenter.

The authority to start, advance, reject, roll back, complete, cancel, train, play dialogue voice, and route the already
supported Character Examination remains reusable and must not move into a view.

### Safe Dialogue follow-up boundary

`UI-F.1 — Source Dialogue Presentation` may:

1. add one disposable world-anchored NPC text-bubble presenter using `ProductionDialogueSession.NpcText` and the
   current NPC stable identity;
2. replace the generic response modal with one `tc`-shaped five-line live-world overlay using ID 229, exact geometry,
   colors, hover, and source input precedence;
3. correct input gating so the source-permitted dialogue world scroll/move path remains available without allowing
   unrelated modal shortcuts;
4. preserve existing transactional dialogue, training, audio, and Character Examination authority;
5. physically resolve whether the registered ID 354 dialogue rotating window displays content in ordinary retail
   conversations before reproducing more than its already proven HUD ownership.

It must not add portraits, use ID 822, invent response scrolling/pagination, accept more than five simultaneously
presented source options, or synthesize unsupported barter/map/schematic/identify/repair transitions. If a real
dialogue produces more than five eligible responses, that is a source/data-authority defect to investigate, not a UI
scrolling request.

## Journal / Logbook

### Window, art, and exact geometry

`logbook_ui_open` toggles an existing logbook closed, rejects null/dead-PC requests, enters
`INTGAME_MODE_LOGBOOK`, and creates the UI (`logbook_ui.c:510-536`). Creation acquires the source big-window lock, so
the Logbook is a true modal fixed composition (`logbook_ui.c:567-583`). The source window is `(0,41,800,400)` in the
800 x 600 interface model.

| Element | ART | Source rectangle / destination | Runtime evidence |
|---|---:|---|---|
| Left sidebar | 264 `logbooks_side.art` | blit `(0,0)` in the 800 x 400 window | `logbook_ui.c:594-610` |
| Blank two-page spread | 260 `book_main.art` | blit at `(165,0)` | `logbook_ui.c:785-800`, `907-922` |
| Book border | 261 `book_quests.art` | blit at `(172,23)` | indirect parameter to `logbook_ui_draw_panel`; `763-806` |
| PC lens cover | 198 `pcwincvr.art` | `(25,24,89,89)` | `logbook_ui.c:85`, `637-641` |
| Left/right page turn | 272 / 273 | `(213,77)` / `(675,77)` | `logbook_ui.c:92-95` |
| Rumors & Notes | 265 | `(696,83)` | `logbook_ui.c:102-104` |
| Quests | 267 | `(696,129)` | `logbook_ui.c:104` |
| Reputations | 271 | `(696,175)` | `logbook_ui.c:105` |
| Blessings & Curses | 266 | `(695,221)` | `logbook_ui.c:106` |
| Kills & Injuries | 270 | `(696,267)` | `logbook_ui.c:107` |
| Background | 268 | `(696,313)` | `logbook_ui.c:108` |
| Keys | 269 | `(696,359)` | `logbook_ui.c:109` |
| Left/right content | dynamic text | `(222,72,215,270)` / `(468,72,215,270)` | `logbook_ui.c:117-120` |
| Left/right header | dynamic text | `(236,43,187,19)` / `(482,43,187,19)` | `logbook_ui.c:128-131` |
| Left/right footer | dynamic text | `(222,346,215,15)` / `(468,346,215,15)` | `logbook_ui.c:139-142` |

The `(150,11,501,365)` secondary background rectangle is declared but does not replace the proven ID 260/261
composition. It must not be promoted to a separate full-screen asset on dimensions alone.

### Text, tabs, and pagination

Body and page-number text use ID 229. Headers use ID 27. The source creates black body text, centered black page
numbers, black strikethrough for quelled/healed entries, green strikethrough for completed quests, red strikethrough
for failed/botched quests, centered black headers, blue active quests/blessings, and red active curses
(`logbook_ui.c:377-434`).

The source has seven ordinary tabs. Holding Left Ctrl + Left Alt while opening switches Rumors & Notes to the hidden
`Bloopers & Quotes` mode (`logbook_ui.c:585-592`). A tab press resets to page 1; reopening normally preserves the
in-memory selected tab/page. Page turns move in two-page increments and a 100-spread start-index cache permits back
navigation (`logbook_ui.c:828-886`). Entries flow left page then right page, wrap within 215 px, and only truncate an
entry when the source explicitly allows it (`logbook_ui.c:978-1145`). The PC lens closes the book; tab hover text is
routed to the HUD message window (`logbook_ui.c:683-746`). Book open, close, switch, and page-turn sounds are explicit.

### Authority matrix

| Tab | OpenArcanum current authority | Missing before authentic projection |
|---|---|---|
| Quests | Complete bounded `CampaignStateService` quest state/timestamps plus `QuestLog` descriptions; `JournalProjectionService.ProjectAll` is read-only and source-driven. | Source book pagination/sorting/style only. This is the one presentation-safe tab. |
| Rumors & Notes | Campaign state retains known rumor IDs and includes them in dialogue transaction snapshots. | Source rumor prose, timestamps, quelled state, ordering, logbook projection, and Save V1 persistence. Current dialog contract explicitly does not drive rumor-quell state. |
| Reputations | `SocialStateService` retains reputation IDs and acquisition milliseconds; source effect definitions are bound. | Source display-name/description projection and logbook formatting. Do not expose effect-table internals as player prose. |
| Blessings & Curses | Script opcodes are catalogued but no complete authoritative blessing/curse state service exists. | Ownership, timestamps, names, add/remove behavior, save/load, and projection. |
| Kills & Injuries | Combat/death consequences exist for bounded gameplay, but no source logbook kill-stat aggregate or persistent injury ledger exists. | The nine source kill statistics, most/least descriptors, injury/effect state, healing/strikethrough behavior, and persistence. |
| Background | Finalized Player Creation metadata retains background identity/text identity. | Source body-text lookup/formatting and long-body page splitting at word boundaries. |
| Keys | Inventory authority exists, but no source `item_get_keys` equivalent or key-description projection is present. | Exact key eligibility, ordering, descriptions, and save-compatible projection. |

The current generic Journal draws only quest rows. It does not own book art, tabs, two-page layout, remembered page,
or source typography. `ProductionJournalPresenter` is legacy/minimal and is disabled by the unified production
presenter; it is not a second authority to preserve.

### Safe Logbook follow-up boundary

`UI-F.2a — Source Logbook Shell + Quests` may implement the exact fixed book composition, Quests tab, two-page
pagination, source fonts/colors, PC lens close, hover feedback, and audio over the existing quest projection. The six
non-quest tabs must be visibly unavailable/fail closed rather than presenting an empty list as authoritative absence.

`UI-F.2b — Remaining Logbook Data Authority` is a gameplay/data milestone, not a view task. It should add one domain
at a time only after tracing the corresponding original `rumor_*`, `reputation_*`, `bless_*`, `curse_*`,
`logbook_get_kills`/injury, background-text, or `item_get_keys` source path and Save V1 implications. Only then should
that tab become interactive. The hidden quotes mode is presentation/data polish after ordinary Rumors & Notes exists.

## Party / Followers

### Persistent HUD composition

`follower_ui.c` owns a persistent HUD-side stack, not an 800 x 400 modal. At most six followers are visible at once.
Portrait-slot windows use left/top HRP anchoring; the special controls move between normal and compact/widescreen
positions. Extra width of at least 134 px forces the compact positions (`follower_ui.c:39-82`, `369-405`).

| Element | ART | Exact source geometry / behavior |
|---|---:|---|
| Slots 0-5 | 503 `followerframe.art` | `(11,50/112/174/236/298/360,40,49)` |
| Portrait | portrait source or interface fallback | local `(4,4,32,32)`; interface fallback is drawn at 50% |
| HP bar | 505 `followerframe_hp.art` | local `(3,39)`, horizontal crop to HP percent |
| Poison HP bar | 616 `followerframe_po.art` | same rect when source poison level is greater than 20 |
| Critical-health tint | dynamic red | portrait tint when HP percent is below 20 |
| Fatigue bar | 504 `followerframe_ft.art` | local `(3,44)`, horizontal crop to fatigue percent |
| Toggle open/closed | 501 / 502 | normal `(0,428,67,13)`; compact/wide `(0,587,67,13)` |
| Scroll up | 506 active / 507 disabled | normal `(0,415,33,13)`; compact/wide `(0,574,33,13)` |
| Scroll down | 508 active / 509 disabled | normal `(33,415,34,13)`; compact/wide `(33,574,34,13)` |
| Context menu | 826 `follwercomandmenu.art` | immediately right of the selected slot; entry rect `(10,3,118,15)`, 18 px stride |

The panel rebuilds from the current follower list, clamps scroll position, hides when no followers exist, and shows
scroll controls only when more than six followers exist (`follower_ui.c:1169-1289`). Visibility and top scroll index
are serialized by the original follower UI (`follower_ui.c:614-634`). Left-click selects/targets the follower;
hover writes examination text to the message window; right-click opens the per-follower context menu
(`follower_ui.c:742-832`).

### Commands and input ownership

The context menu contains exactly eight commands, with labels from `mes/follower_ui.mes`:

1. Walk
2. Attack
3. Stay Close
4. Spread Out
5. Back Off
6. Wait
7. Inventory
8. Character Sheet

Walk enters tile targeting. Attack normally accepts living non-party critters; Alt enables the broader non-self,
non-wall object target mask (`follower_ui.c:897-928`). Inventory opens barter inventory only when permitted. Character
Sheet opens passive charedit. Wait is disabled for a mind-controlled follower. Stay Close, Spread Out, Back Off, and
Wait are issued to the selected subordinate as broadcasts (`follower_ui.c:679-717`, `1297-1338`).

This per-follower menu is distinct from the global F1-F6 broadcast bank. The source F-key table is Walk, Attack, Stay
Close, Spread Out, Back Off, Follow, loaded from broadcast message rows 500-505 and issued at the pointer location
(`broadcast.c:485-507`; `intgame.c:2935-2946`). OpenArcanum's existing F1-F6 mapping and `PartyOrder.Follow` therefore
remain correct; the menu's sixth `Wait` must not replace F6 Follow.

### Current OpenArcanum authority and gaps

| Concern | Current state | UI-F implication |
|---|---|---|
| Ordered membership/capacity | Strong M9B session-owned stable IDs, forced-member distinction, validation, Save V1. | Reuse directly; HUD never owns membership. |
| HP/fatigue/dead/unconscious | Strong M4B projection through `GameUiPartyMemberView`. | Bars and status styling are presentation-safe. |
| Following/formation/global orders | Strong M9B movement authority for Walk, Attack, Stay Close, Spread Out, Back Off, Follow; F1-F6 source order is already validated. | Keep the global command path. Do not route per-follower menu commands through a whole-party mutation. |
| Combat/world transitions | Strong M8/M9 ally behavior, accompaniment, death/unconscious eligibility, and transactional map travel. | Presentation observes only. |
| Portrait | `GameUiPartyMemberView` has no portrait identity, and persistent object state does not retain the source NPC portrait field. | Add a bounded source-retention/projection path before drawing portraits; do not guess from critter ART. |
| Poison bar | OpenArcanum has poison damage/resistance concepts but no authoritative current poison-level state corresponding to `STAT_POISON_LEVEL`. | Show ordinary HP art until exact poison state exists; do not infer poison from damage type/history. |
| Individual Walk/Attack/formation/Wait | `PartyStateService.IssueOrder` represents a global party order, not the selected subordinate broadcast. | Requires selected-follower command authority, source validation, AI/movement consumption, and save policy. |
| Follower Inventory / passive Character Sheet | Inventory and Character controllers exist but the generic Party panel exposes only Dismiss. | Add target-specific read/presentation routes only after controller preflight proves the follower target and source restrictions. |
| Selection/hover/context menu/scroll/toggle | Not represented by the current modal Party screen. | Disposable HUD presentation state; source saves visibility/top index, so decide explicitly whether Save V1 gains optional fields or normalizes them. |
| Dismiss | OpenArcanum exposes Dismiss in the generic Party panel; the source follower context menu does not. Membership departure is normally dialogue/script-owned. | Remove Dismiss from the reconstructed HUD unless a separate source-backed route explicitly requires it. Do not lose existing underlying `PartyStateService.Remove` authority. |

### Safe Party follow-up boundary

`UI-F.3a — Follower Command/Projection Closure` should first add only the missing source facts and commands:
source portrait identity retention/projection, current poison level if source-bounded, selected-follower order requests,
Wait semantics, target-specific Inventory/passive Character Sheet preflight, and an explicit Save V1 policy for
visibility/top index. Every invalid command must reject before party, AI, movement, combat, inventory, or character
mutation.

`UI-F.3b — Source Follower HUD` may then replace the `P` modal with the six-slot left HUD stack, exact ART/geometry,
portrait and bars, source toggle/scrollers, selection/hover, and the eight-command context menu. It must preserve the
existing global F1-F6 bank, membership ordering, current-turn ownership, death/unconscious semantics, world travel,
and presentation-rebuild independence. The non-retail `P` convenience may be removed or limited to toggling the
source strip; it must not reopen a modal follower list.

## Recommended implementation order and gates

1. **UI-F.1 — Source Dialogue Presentation.** Authority is strongest and the required change is primarily
   presentation/input ownership. Gate on a physical retail check of ID 354's ordinary-dialogue role, a five-response
   real-data fixture, world movement/scroll without dialogue loss, voice/bubble lifetime, special-view fail-closed
   behavior, and Original/Enhanced rebuild.
2. **UI-F.2a — Source Logbook Shell + Quests.** Use only current quest authority. Gate on exact 800 x 400 geometry,
   page fitting, colors, page turns, tab fail-closed behavior, lens close, audio, and rebuild/page preservation.
3. **UI-F.2b — Non-quest Logbook Authority.** Separate gameplay/data milestones; no presentation completion claim
   until each supported tab has source text/state/timestamp/save behavior.
4. **UI-F.3a — Follower Command/Projection Closure.** Close portrait, poison, selected-follower order, Wait,
   Inventory, passive Character Sheet, and UI-save policy boundaries without changing the HUD yet.
5. **UI-F.3b — Source Follower HUD.** Only then replace the current modal list.

Do not combine these gates into one large commit. Dialogue must not wait on Journal/Party authority, and a safe quest
book shell must not be used to claim the seven-tab Logbook complete.

## Required validation for later implementation

Each slice should retain the existing UI-A Original/Enhanced exact-geometry contract, one presenter/controller,
stable authoritative state through presentation rebuild, and the standard 800 x 600, 1024 x 768, 1920 x 1080,
2560 x 1440, and 3840 x 2160 physical checks.

Dialogue tests must prove five-line geometry, source colors/hit rows, no unproven portrait/ID 822 path, world input
ownership, response transactionality, training, voice, and supported/unsupported special transitions. Logbook tests
must prove exact art IDs/rectangles, deterministic two-page flow, state colors, page history, and tab-authority gates.
Party tests must prove six-slot clipping/scrolling, portrait/status projection, global versus individual command
separation, invalid-command transactionality, selection/order targeting, death/unconscious changes, Save V1 policy,
and graphics/presentation rebuild independence.

## Audit conclusion

The read-only UI-F archaeology is complete. The exact source compositions are now bounded, two prior documentation
assumptions are rejected (ornate portrait Dialogue and modal Party), and the next implementation may begin only with
the separately scoped UI-F.1 Dialogue presentation slice above. No production code or retail asset was changed.

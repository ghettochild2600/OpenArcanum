# M13A Opening Crash-Site Compatibility Audit

Date: 2026-09-29
Branch: `feature/session-save-load`
Starting HEAD: `13c989afda2d809c530d70df5b42453106c9b7c8`

## Scope

This is the first bounded compatibility pass driven by defects found during the user's authentic retail New Game play.
Validation stops in the opening crash-site sector and does not attempt a broader campaign playthrough.

Authentic checkpoint:

- scene: `Assets/_Game/Scenes/OpenArcanum.unity`
- retail sector: `maps/arcanum1-024-fixed/86570436012.sec`
- production PC start: local tile `(30,32)`

## Defect register

| Defect | Authentic subject | User-observed behavior | Source-expected behavior | Root cause / owning subsystem | Bounded fix | Regression / physical validation | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Virgil dialogue blocker | Virgil `G_A09DCD63_7A15_D411_8F1D_00E02920220C`; dialogue/script 1324 | Starting dialogue failed as `UnsupportedScriptOpcode` at line 0. | Reachable SAP_DIALOG entry logic executes, the first retail line and choices appear, and a chosen response advances normally. | Dialogue startup eagerly rejected every opcode anywhere in the retail script. Script entry 16 contains a dormant `Sat.FloatLine`; it was never reached by the opening path, but the whole-file policy scan rejected the conversation before line 0. The reachable path also needs generic `gf`, `in`, and `ru` effect preflight plus campaign-owned rumor state. Owner: M5 production dialogue/campaign authority. | Removed the eager whole-file opcode scan. The strict VM now rejects only an opcode actually reached, restores the transaction, and maps that result to the existing unsupported-script diagnostic. Added source-shaped global-flag, inventory-transfer, and rumor operations; rumor state participates in the existing dialogue snapshot rollback. No Virgil special case and no retail-data patch. | Focused real-data regression proves entry 16 remains dormant, dialogue starts at line 1, flag 2004, quest 1010, rumor 2013, and authentic item 2804 update through normal authority. A second test proves a reached unsupported opcode still fails closed. From fresh New Game, Virgil's first authentic line opened, choices appeared, and one authentic response advanced without an unexpected warning/error. | Fixed |
| Pre-existing corpses stand/animate | Retail opening corpses `G_DD753C8F_B655_D411_8F1D_00A0CC6511C6`, `G_39EBE998_F113_D411_8F1D_00E02920220C`, and `G_B82C1DC3_080B_D411_8F1D_00E02920220C` | Bodies that were already dead in source data visibly stood or replayed the fall animation. | Source HP/damage initializes the actors as dead, preserves ObjectID/inventory, projects action 7, and freezes the final corpse frame without processing a new combat death. | The loader already built authoritative M4B vitality from instance/prototype HP fields and projected dead actors to action 7. Presentation nevertheless initialized/looped that multi-frame ART like a living animation. Owner: world-object sprite presentation; authoritative state was already correct. | A source-dead actor now freezes the resolved action-7 sprite on its final frame. The static animator records the selected frame index. Loading does not invoke M8D/M8E death consequences, award XP, change ObjectID, or alter corpse inventory. | Real-data regression checks all three bodies: authoritative dead state, action 7, final frame, stable identity, unchanged source inventory counts, and an unprocessed death-consequence marker. Fresh New Game visibly showed representative bodies lying still. | Fixed |
| Corpses cannot be selected or looted | Adjacent opening corpse `G_B82C1DC3_080B_D411_8F1D_00E02920220C` at local tile `(31,32)` | Alpha-visible corpse did not open its inventory. | Alpha-tested presentation resolves the corpse's stable ObjectID; an adjacent loot command opens M8E-owned corpse inventory and transfers through M3 authority. | Click routing treated every NPC as talk-only and had no typed corpse interaction. After that was added, physical testing exposed a second production lifecycle race: the HUD was created by the sector loader before `PlayerClickMoveInput.Awake` created `PlayerInteractionController`, so the HUD missed the interaction event. Owners: pointer/interaction routing and M12C presentation lifecycle. | Added typed `Loot` interaction with range/preflight at the coordinator boundary, routed dead NPC clicks to it, and added a small corpse screen/LOOT hover projection delegating transfers to M8E. The HUD now idempotently late-binds the interaction controller and unsubscribes on destroy. No collider authority was introduced. | Focused tests prove opaque-pixel selection of authentic corpses, validated coordinator loot, M8E transfer without cloning, and the late-created interaction-controller subscription. Physical New Game hover/click opened the authentic two-item corpse inventory; one item transferred once, disappeared from the corpse view, and appeared in the PC inventory. | Fixed |
| Walking animation looks wrong | Production New Game PC, ordinary click navigation | The walk cycle appeared to slide/jump relative to the actor and looked “funky.” | ART frame offsets accumulate as frames are displayed; horizontal deltas mirror with the facing; gameplay position stays authoritative; WALK returns to STAND at arrival. | Sprite construction ignored each frame's authored `offsetX/offsetY`, anchoring every frame only by its hotspot. This discarded source registration motion and caused visible frame-to-frame snapping/sliding. Owner: ART sprite presentation. | Build cumulative source frame offsets, mirror the horizontal contribution when required, and bake the cumulative delta into exact pivots. Gameplay transforms, movement timing, pathing, and animation cadence are unchanged. | Focused regression proves cumulative and mirrored offsets plus exact pivot math. Fresh New Game representative travel in multiple directions used continuous directional WALK presentation and returned cleanly to STAND. | Fixed |
| Player-facing UI cleanup | Opening production HUD | Corpse interaction had no usable affordance or inventory view. | Required opening interactions are visible and usable without changing gameplay ownership. | Missing presentation for the new corpse command. Broader HUD polish is outside this defect pass. | Added only the small `LOOT` hover label and bounded corpse inventory panel/button required for the reported defect. No general HUD redesign was performed. | Physical corpse hover, modal, transfer feedback, and PC inventory projection passed. | Fixed |

## Validation

### Focused and affected automated tests

- M13A focused: **6 passed / 0 failed / 0 skipped / 0 inconclusive**
- M5A dialogue and quest: **16/16**
- M8E death consequences and corpse loot: **8/8**
- M2A interaction kernel: **16/16**
- Player navigation: **21/21**
- M12C full-game UI: **18/18**
- directly affected regression total: **79/79**
- complete EditMode suite: **1058 passed / 0 failed / 0 skipped / 0 inconclusive**
- Unity 6000.0.71f1 compilation: clean
- `git diff --check`: clean

The prior complete-suite baseline was 1052. This pass adds six focused M13A-category tests.

### Physical production proof

A fresh character was created through the normal New Game UI and entered the authentic crash-site sector.

- Virgil dialogue 1324 opened on its authentic first line, exposed the normal response choices, and advanced after one authentic response.
- Representative pre-existing bodies loaded dead, remained on a static fallen frame, retained stable source identities/inventories, and did not award XP or process a new death consequence.
- The adjacent B82 corpse resolved through alpha-tested production selection, showed `LOOT`, opened a two-item corpse inventory, transferred one authentic item exactly once, and projected it in player inventory.
- Representative movement in multiple directions retained continuous source-offset WALK presentation and returned to STAND.
- No broader campaign progress was attempted.
- Final cleared Unity Console: **0 logs / 0 warnings / 0 errors**.

## User-discovered opening follow-up (2026-09-29)

This bounded follow-up started at `b568baef3b23013288a53602d05510f6d1fc1d55` after additional user playtesting.
It corrects only the three newly reproduced opening-area defects below. The earlier cumulative-offset walking entry is
retained as history, but its interpretation and implementation are superseded here.

### Virgil dialogue 1324: `mm` and `wa`

- **Symptom:** authentic progression reached `UnsupportedEffect 'mm'` at rows 71/120 (and the calling response rows
  58/118), plus `UnsupportedCondition 'wa'` at row 385's response set.
- **Source evidence:** retail rows 71 and 74 execute `mm1, mm2`; row 120 executes `mm1`; rows 386-390 test `wa0`.
  In arcanum-ce's dialogue dispatcher, `DIALOG_ACTION_MM` calls `area_set_known(pc_obj, value)`. `DIALOG_COND_WA`
  compares the speaker's `OBJ_F_NPC_FLAGS` bit `ONF_AI_WAIT_HERE` (`0x00000008`) with the requested zero/one value.
- **Semantics:** `mm N` source-validates area `N` and marks it known for the PC. `wa 1` passes when the speaking NPC's
  authoritative wait-here bit is set; `wa 0` passes when it is clear.
- **Owner/correction:** `CampaignStateService` remains the known-area authority and exposes pure area validation for
  transaction preflight. `ProductionDialogueContext` reads the NPC bit from persistent world state. Both operations
  are admitted by every production dialogue vocabulary; there is no Virgil, dialogue-1324, or area-1/2 special case.
- **Regression:** the source-shaped test reads the exact retail rows, proves opening Virgil has `wa0`, executes
  `mm1,mm2` through the normal production transaction, and verifies areas 1 and 2 become known.

### Source-initialized crash-site casualties

The HP-only assumption was incomplete. Original `critter_is_dead` is HP-based, but these placed records are not dead
in serialized HP. Their source-authored `SAP_HEARTBEAT` programs establish initial state: when the paired global flag
is clear they call `Kill(attachee)`; when set they toggle the actor off and remove the script. Original
`critter_kill` sets HP damage to 32000.

OpenArcanum now runs only a strict, state-only subset of a previously unestablished persistent critter's heartbeat
during load. It accepts the audited flag comparison/control flow plus `ToggleState`, `Kill`, script removal, and
return operations. Any other opcode rejects reconstruction rather than executing gameplay. `Kill` seeds M4B vitality
damage 32000 and the existing M8D corpse projection without a current-session death transition, AI, XP, or M8E
consequence. Existing saved object/vitality authority always wins.

| ObjectID | Proto | Heartbeat / flag | Relevant source state | Retail opening state | Before | After / presentation |
| --- | ---: | --- | --- | --- | --- | --- |
| `G_729DB2F6_503E_B84E_B5E3_0DADC947D188` | 17212 | 1781 / 2350 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_D3A6E04A_78E5_FE41_AE7A_555C6CBC77B7` | 17188 | 1782 / 2351 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_91A6CACB_B653_DD4A_A763_93911F389C9D` | 17100 | 1783 / 2352 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_F2C1B8B6_AA9F_AE4E_BDF1_92C1DAB6B64B` | 17256 | 1784 / 2353 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_0BBB1D21_0997_2A49_B8CD_0D0099667DED` | 17132 | 1785 / 2354 | HP alive; one inventory child | dead when flag clear | upright | damage 32000; fallen and lootable; inventory preserved |
| `G_81B28CDB_13AA_0048_8326_F2C432DFE976` | 17124 | 1788 / 2357 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_9C04A958_C5F2_784D_AA9C_5E76A603C764` | 17099 | 1789 / 2358 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_2798503E_F21C_724D_B1A7_2E968EB88EAA` | 17145 | 1790 / 2359 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_1BAD7FCC_C094_5148_875F_B72C51004AF2` | 17233 | 1791 / 2360 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| `G_75FFCBCC_04BF_234D_B380_6C7B60E4ECD3` | 17165 | 1792 / 2361 | HP alive; heartbeat initialization | dead when flag clear | upright | damage 32000; frozen fallen action 7 |
| Virgil `G_A09DCD63_7A15_D411_8F1D_00E02920220C` | 17102 | ordinary living control | no death initializer | alive | alive | standing/interactable |

Focused regressions prove all ten casualties load dead with stable identity, 32000 damage, fallen final frame, and an
unprocessed consequence marker, while Virgil and authentic wolf/scout controls remain alive. A synthetic
source-shaped heartbeat also proves both flag branches and fail-closed rejection of an unrelated mutation opcode.

### WALK ART movement-delta correction

The earlier pivot treatment double-applied movement. Source `object_inc_current_aid` advances the frame and adds that
frame's `offset_x/offset_y` into the original object's movement offset. These values are per-frame locomotion deltas
consumed by the original movement system, not independent sprite-registration offsets to layer over Unity's already
continuous root interpolation. `tig_art_frame_data` mirrors rotations 1-3 from the opposite source rotation and
transforms both hotspot and horizontal delta; it does not turn the delta into an absolute pivot.

Authority is now separated as follows:

1. coordinator/navigation route authority is unchanged;
2. `WorldObject.ApplyMovementState`/route following supplies the continuous interpolated root;
3. the sprite uses only exact source hotspot/mirror registration;
4. ART movement deltas are not accumulated into the Unity pivot;
5. camera motion follows the root and is not part of frame registration.

No interpolation, route, collision, destination, FPS, or navigation authority changed. The animator already preserves
frame phase across compatible directional loop rebuilds and returns to STAND only when the route completes.

Complete-cycle diagnostic, `art/critter/hmm/hmmv1xab.art`, 17 FPS:

| Frame | rot 2 raw `(x,y)` | old extra cumulative x | rot 2 hotspot `(x,y)` | corrected pivot `(x/w,(h-y)/h)` | rot 6 raw x |
| ---: | --- | ---: | --- | --- | ---: |
| 0 | `(4,0)` | 0 | `(12,74)` | `(12/22, 0/74)` | -4 |
| 1 | `(10,0)` | 10 | `(17,73)` | `(17/28, 1/74)` | -10 |
| 2 | `(8,0)` | 18 | `(25,73)` | `(25/45, 3/76)` | -8 |
| 3 | `(4,0)` | 22 | `(28,72)` | `(28/44, 4/76)` | -4 |
| 4 | `(6,0)` | 28 | `(25,73)` | `(25/35, 4/77)` | -6 |
| 5 | `(4,0)` | 32 | `(15,74)` | `(15/22, 4/78)` | -4 |
| 6 | `(8,0)` | 40 | `(17,73)` | `(17/29, 4/77)` | -8 |
| 7 | `(8,0)` | 48 | `(25,73)` | `(25/44, 4/77)` | -8 |
| 8 | `(4,0)` | 52 | `(29,72)` | `(29/45, 4/76)` | -4 |
| 9 | `(4,0)` | 56 | `(22,73)` | `(22/34, 1/74)` | -4 |

Rotation 0 carries y deltas `-2,-5,-4,-2,-3,-2,-4,-4,-2,-2`; rotation 6 carries the mirrored x deltas above. The old
extra cumulative column exposes the 56-pixel lurch layered over one Unity route. Correct rendered placement is now
`interpolated root + stable visual-child offset + source hotspot registration`; no ART delta is added twice.

### Follow-up validation

- M13A focused: **10/10**
- directly affected regressions: **79/79** (`M5A` 16, `M8E` 8, `M2AInteraction` 16,
  `PlayerNavigation` 21, `M12CFullGameUi` 18)
- complete EditMode suite, run once: **1058 passed / 0 failed / 0 skipped / 0 inconclusive**
- Unity 6000.0.71f1 compilation: clean
- fresh production New Game: the authentic crash sector loaded; audited casualties were fallen while Virgil and
  living controls remained standing; several long representative routes remained smooth through frame, path-node,
  and tile transitions and ended in a clean WALK-to-STAND transition; route destinations/navigation were unchanged
- source-shaped production dialogue execution: exact retail `wa0` and `mm1,mm2` completed against authoritative
  NPC/campaign state without an unsupported diagnostic
- final cleared Unity Console: **0 logs / 0 warnings / 0 errors**
- `git diff --check`: clean

## Deliberate limits

This pass does not redesign the HUD, add new campaign content, broaden unsupported dialogue opcodes beyond reached
source requirements, alter navigation, or run an autonomous campaign playthrough. Future user-discovered campaign
compatibility defects should be appended here only when they are reproduced and source-audited.

## Second opening follow-up: locomotion, sector presentation, and dialogue 1324 (2026-10-04)

This bounded follow-up starts at `03be353b2ef63a89f86c59642b1f1e6abc4a339b`. It fixes only the four
user-reported opening defects. It does not broaden the campaign pass or change route, collision, combat, save, or
world-transition authority.

### Source-authentic PC locomotion

The previous follow-up correctly removed WALK offsets from sprite pivots, but left Unity's constant-speed root
interpolation and the sprite animator as independent clocks. Original `object_inc_current_aid` instead advances one
ART frame and adds that displayed frame's authored movement delta to the object's movement offset. Source
`sub_437990` supplies the adjusted WALK frame rate from the critter's SPEED and body category.

The production player now uses that source animation clock as the locomotion clock. Each displayed WALK frame yields
its authored screen-space delta, projected onto the current tile-route direction; that bounded progress advances the
existing route/session authority. Hotspots remain presentation-only, path selection and collision remain unchanged,
and the actor returns to STAND only after reaching the final waypoint. A near-waypoint direction correction also now
uses the sign of the remaining displacement rather than rounding a fractional displacement to zero, preserving the
direction and animation phase through path-node transitions.

Representative `hmmv1xab.art` eastward cycle at the ordinary human 17 FPS source rate:

| Displayed frame | Source time (s) | Authored x delta (px) | Cumulative route progress (80 px tile) |
| ---: | ---: | ---: | ---: |
| 0 | 0.0000 | 4 | 0.050 |
| 1 | 0.0588 | 10 | 0.175 |
| 2 | 0.1176 | 8 | 0.275 |
| 3 | 0.1765 | 4 | 0.325 |
| 4 | 0.2353 | 6 | 0.400 |
| 5 | 0.2941 | 4 | 0.450 |
| 6 | 0.3529 | 8 | 0.550 |
| 7 | 0.4118 | 8 | 0.650 |
| 8 | 0.4706 | 4 | 0.700 |
| 9 | 0.5294 | 4 | 0.750 |

The diagnostic is intentionally source-shaped: the frame index, authored delta, frame timestamp, facing, and
fractional route position all come from the same clock. Entered tile nodes still commit through the production
session one at a time; rendering interpolates between those commits without adding a second constant-speed clock.

### Generic Virgil/NPC movement

The generic follower driver previously called an atomic one-step helper every 200 ms, so the authoritative NPC
position jumped directly between tiles while the sprite merely selected WALK. The movement service now plans and
returns the same authoritative route without mutating it. The production follower driver advances that route with
the same source WALK frame/delta clock as the PC, commits entered tiles through the session and navigation occupancy,
retains stable ObjectID ordering, and returns to STAND at arrival. A dynamic blocker rolls back the attempted entered
tile and cancels the route rather than corrupting occupancy. Party membership and follower policy are unchanged.

### Authentic adjacent-sector presentation

The crash-site black void was presentation, not missing retail terrain. The source map cache keeps a 3x3 sector
neighborhood around the current sector. Production terrain now builds that exact bounded window from existing source
sector files, offsets each 64x64 sector by its relative sector coordinate, and retains the central sector as the
camera/object-ownership frame. The crash-site sector `86570436012` resolves all nine authentic neighbors. Crossing a
central-sector boundary rebuilds the window around the new owner sector while the global route remains authoritative;
missing outer-map neighbors simply remain absent. The click input no longer rejects an otherwise valid global route
solely because its destination is outside the currently central 64x64 sector.

### Additional dialogue 1324 operations

The generic dialogue VM and production transaction now admit the exact source operations reached by this follow-up:

- `ss`: evaluates the existing source comparison semantics and is admitted by the production vocabularies.
- `ia`: resolves the PC's current area from START_MAP/townmap semantics; positive values mean exactly that area and
  negative values mean not that area. The authentic crash site resolves to area 2.
- `ce`: requests the existing character projection for the speaking NPC, opens it read-only, and performs no
  authoritative mutation.
- `wa`: applies the NPC wait-here bit (`0x00000008`) through persistent/runtime world state. Party membership is
  retained, matching the source leader-link restoration, while the follower is no longer eligible to accompany.

NPC wait flags participate in the existing dialogue transaction snapshot. Unsupported examination/effect paths still
fail closed. No Virgil-specific branch, retail-data edit, or broader unsupported opcode admission was added.

### Final validation

- focused M13A EditMode tests: **16 passed / 0 failed / 0 skipped / 0 inconclusive**
- affected regressions: **79/79** (`M5A` 16, `M8E` 8, `M2AInteraction` 16,
  `PlayerNavigation` 21, `M12CFullGameUi` 18)
- complete EditMode suite, run once at the end: **1068 passed / 0 failed / 0 skipped / 0 inconclusive**
- prior complete-suite baseline: **1058**; this follow-up adds ten focused tests
- Unity 6000.0.71f1 compilation: clean
- `git diff --check`: clean

One fresh production New Game was used for the bounded physical proof. The initial crash-site window rendered **9/9**
source sectors with no blend misses. The PC completed representative long movement and crossed
`86570436012 -> 86570436011 -> 86570436012 -> 86637544876`; terrain rebuilt around each new owner sector (the
outer-edge sector correctly had only 7 existing neighbors), movement remained continuous, and no teleport or black
void was observed. The run stopped there to avoid broad campaign progress. Generic Virgil/follower movement and the
additional dialogue operations are covered by production-path focused tests rather than a second physical campaign
run. Final cleared Unity Console: **0 logs / 0 warnings / 0 errors**.

The user-owned `OpenArcanumGraphicsConfig.asset` remains at `graphicsMode: 1` and is excluded from these commits, as
are local `GameData`, `HDAssets`, `output`, and `tmp` content. Save V1 is unchanged.

## Original Keyboard Shortcut Compatibility (2026-10-04)

This bounded pass audits the retail keyboard-reference diagram against the retail manual, source-derived behavior,
and the reference implementation. `ProductionKeyboardController` now owns only deterministic key phase, modal
precedence, text-entry suppression, and modifier intent. It delegates every result to the existing UI, M8 combat,
M9 party, movement, camera, save-slot, inventory, magic, technology, or interaction authority. The input layer does
not calculate gameplay outcomes and Save V1 remains unchanged.

The source broadcast table is exact: F1 Walk, F2 Attack, F3 Stay Close, F4 Spread Out, F5 Back Off, and F6 Follow.
Therefore the former OpenArcanum F6 Save/Load binding was a convenience conflict and has been removed; Save/Load
remains available from the HUD while retail F7/F8 use the dedicated `auto` slot. Editor validation menu commands also
remain available, but their global F8/F10/F11 accelerators were removed after physical validation proved that an
Editor accelerator could steal a production shortcut.

| Key | Original label | Exact source semantics | OpenArcanum implementation | Context | Physical validation status | Automated test status | Deferred / multiplayer-only notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Esc | Main Menu | Release closes the active interface first; otherwise opens the main menu | Presenter applies dialogue/confirmation/modal precedence, then opens Main Menu | All production contexts; text entry still permits Escape | Inventory, character, map, options, and post-load menu close/open paths proved | Modal precedence covered | None |
| F1 | Walk | Source broadcast order 500 on release, using the pointer location | M9B `PartyStateService.IssueOrder(Walk)` | Valid world pointer | `Followers: Walk` proved | Exact source index/order covered | No valid destination fails closed |
| F2 | Attack | Source broadcast order 501 on release, using the pointed target | M9B order plus existing forced-start combat path | Valid living target | Invalid-target rejection proved | Exact source index/order covered | No target means no mutation |
| F3 | Stay Close | Source broadcast order 502; enable following at close range | M9B formation authority | Party runtime | Feedback and state change proved | Range/follow state covered | None |
| F4 | Spread Out | Source broadcast order 503; enable following at spread range | M9B formation authority | Party runtime | Feedback and state change proved | Range/follow state covered | None |
| F5 | Back Off | Source broadcast order 504; stop following and disengage followers | M9B party/combat authority | Party runtime | Feedback and state change proved | Follow state covered | None |
| F6 | Follow | Source broadcast order 505; resume ordinary follow range | M9B formation authority; no Save/Load binding | Party runtime | `Followers: Follow` proved | Exact sixth order covered | HUD still exposes Save/Load |
| F7 | Auto Save | Release writes the automatic save slot | Existing save-slot authority, slot `auto` | Active session | Fresh crash-site auto-save proved | Dedicated auto slot covered | None |
| F8 | Auto Load | Release loads the automatic slot and rebuilds presentation | Existing load authority, slot `auto` | Existing auto-save | Auto-load and presentation rebuild proved after removing Editor collision | Dedicated auto slot covered | None |
| F12 | Screenshot | Down captures a screenshot | Unity capture to the user persistent-data Screenshots folder | Production game | Concrete PNG path and file creation proved | Dispatch covered | None |
| 1-0 | Quick slots 1-10 | Down activates the bound item/spell/skill; dialogue owns numbered replies | Coordinator-owned ten-slot binding bank; Inventory/Magic expose assignment | HUD; suppressed in text entry and dialogue | Slot 1 item assignment and activation boundary plus empty-slot rejection proved | All ten mappings, ownership, replacement, and learned-spell checks covered | No generic skill-action authority yet; skills are not fabricated |
| W | World Map | Down opens/closes world map | Existing Map screen | Player session | Proved | Screen dispatch covered | None |
| E | End Combat Round | Release ends the current turn only in active turn-based combat | Existing M8 combat UI/controller | Active turn-based combat | Production combat path retained; context proof automated | Inactive, real-time, and turn-based cases covered | No effect outside turn-based combat |
| R | Attack / Talk | Down toggles attack pointer; in active combat returns to talk/end-combat path | Existing UI/combat authority | HUD or combat | Attack-mode feedback proved | Controller boundary covered | None |
| T | Technology | Down opens/closes Technology | Existing Technology screen | Player session | Proved | Screen dispatch covered | None |
| I | Inventory | Down opens/closes Inventory | Existing Inventory screen | Player session | Proved | Screen dispatch covered | None |
| O | Options | Down opens/closes Options | Production presentation options | Player session | Proved | Screen dispatch covered | Key rebinding UI remains deferred; defaults are authoritative |
| A | Active Action | Down repeats the most recently successful quick-slot action | Coordinator-owned active-slot marker and existing item/spell command | After a successful quick-slot action | Production boundary retained; context proof automated | Controller boundary covered | No invented action when no slot has succeeded |
| S | Sleep | Retail sleep command | Explicit fail-closed feedback | Any supported single-player state | Feedback path inspected | Unsupported surface covered | Rest/sleep authority is not represented yet |
| F | Fate Points | Retail Fate interface | Explicit fail-closed feedback | Any supported single-player state | Feedback path inspected | Unsupported surface covered | Fate-point runtime/UI is not represented yet |
| K | Use Skills | Opens the existing Skills screen | Existing Skills screen | Player session | Proved | Screen dispatch covered | Generic skill quick-slot execution remains deferred |
| L | Logbook | Down opens/closes logbook | Existing Journal/Logbook screen | Player session | Proved | Screen dispatch covered | None |
| C | Character Info | Down opens/closes character projection | Existing Character screen | Player session | Proved | Screen dispatch covered | None |
| V | Version Info | Down displays the current application version | Production feedback surface | Player session | `OpenArcanum 1.0` proved | Dispatch boundary covered | Retail cheat-gated details are not invented |
| M | Use Magic | Down opens/closes Magic | Existing Magic screen | Player session | Proved | Screen dispatch covered | None |
| Comma `<` | Aim at Head | Held selects Head; release restores Torso | Existing M8G called-location authority | Active combat only | Production combat path retained; context proof automated | Head/down and Torso/up covered | No latch and no duplicate penalty logic |
| Period `>` | Aim at Arms | Held selects Arm; release restores Torso | Existing M8G called-location authority | Active combat only | Production combat path retained; context proof automated | Arm/down and Torso/up covered | Same behavior for melee/ranged authority |
| Slash `?` | Aim at Legs | Held selects Leg; release restores Torso | Existing M8G called-location authority | Active combat only | Production combat path retained; context proof automated | Leg/down and Torso/up covered | Same behavior for melee/ranged authority |
| Left/Right Shift | Stand and Attack | Held during click suppresses movement and submits through the selected combat target | Existing click targeting and M8 attack transaction | Valid combat target | Modifier-mouse injection was unavailable in the native backend; production path inspected | Held intent and authoritative attack path covered by affected tests | Invalid ground click performs no action |
| Left/Right Ctrl | Run | Held temporarily inverts the persistent Num Lock walk/run choice | Existing navigation timing and animation authority | Ground movement | Representative movement remained authoritative; modifier combination proof automated | XOR run/walk semantics covered | Does not move the player by itself |
| Left/Right Alt | Force Attack / Drag Corpse | Held click forces a legal neutral-target attack; held drag relocates a dead NPC | Existing combat start or coordinator corpse move with stable ObjectID/state | Valid target/corpse and destination | Native backend cannot hold Alt across mouse input; production path inspected | Modifier intent and directly affected combat/interaction regressions covered | Unsupported scenery destruction still fails closed |
| Space | Close Interface / toggle TB-RT | Down closes an open interface first; with no interface, toggles active combat mode | Existing UI close and M8/M8H mode authority | Modal first, otherwise active combat | Inventory-close precedence proved; combat mode path covered automatically | Both precedence branches covered | Inactive combat rejects without mutation |
| Home | Center Character | Release centers PC; when already centered in turn-based combat, centers current participant | Camera presentation only | Player session / turn-based combat | Detached camera and recenter proved | Controller boundary covered | No party-member cycling beyond source-supported behavior |
| Arrow keys | Scroll Camera | Held continuously pans; opposing/diagonal combinations compose | Camera presentation only | Game view focused | Right-arrow detach proved while gameplay continued | Four held directions and unchanged player position covered | Camera never moves the player |
| Num Lock | Toggle Run / Walk | Down flips persistent default; Ctrl temporarily inverts it | Input intent consumed by existing navigation | Ground movement | Production key path exercised; exact XOR state proved automatically | Default, toggle, and Ctrl inversion covered | No second movement clock |
| Enter | Chat Window | Opens broadcast/chat in the reference executable, including the SP UI surface | Explicit unavailable feedback; no fake chat | Not supported by current runtime | Fail-closed feedback proved | Return/KeypadEnter covered | Multiplayer/chat runtime intentionally unavailable |
| Print Screen / SysRq | Unlabeled | No retail diagram action | No production binding | N/A | Audited | Absence verified in map | Intentionally unavailable |
| Scroll Lock | Unlabeled | No retail diagram action | No production binding | N/A | Audited | Absence verified in map | Intentionally unavailable |
| Pause / Break | Unlabeled | No retail diagram action | No production binding | N/A | Audited | Absence verified in map | Intentionally unavailable |

### Validation result

- focused M13A keyboard/input: **23 passed / 0 failed / 0 skipped / 0 inconclusive**
- directly affected regressions: **177 passed / 0 failed / 0 skipped / 0 inconclusive**
- complete EditMode suite: **1091 passed / 0 failed / 0 skipped / 0 inconclusive**
- baseline before this pass: **1068**; this pass adds 23 counted focused cases
- Unity 6000.0.71f1 compilation: clean
- `git diff --check`: clean

Physical validation used a fresh authentic New Game at the crash site and stopped without campaign progression. It
proved the production screens, modal precedence, auto-save/load, screenshot, attack/talk toggle, party broadcasts,
camera detach/recenter, version and chat feedback, and a real inventory quick-slot assignment. The native automation
backend cannot hold a modifier while issuing a mouse event, and the fresh crash-site state had no active combat;
therefore modifier-click and active-combat permutations use the production-path automated evidence above rather than
manufactured world state. Five targeted and sixteen complete-suite warnings were the established intentional
fail-closed dialogue diagnostics. After inspection, the Console was cleared to **0 logs / 0 warnings / 0 errors**.

Default retail bindings are now centralized and deterministic. A user-remapping UI, a generic skill-action command,
sleep/rest, Fate, multiplayer/chat, and unsupported scenery/equipment destruction remain explicitly outside this
bounded pass. No dummy window or duplicate gameplay authority was added.

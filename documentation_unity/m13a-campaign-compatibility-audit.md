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

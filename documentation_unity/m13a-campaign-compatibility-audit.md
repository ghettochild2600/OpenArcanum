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

## Deliberate limits

This pass does not redesign the HUD, add new campaign content, broaden unsupported dialogue opcodes beyond reached
source requirements, alter navigation, or run an autonomous campaign playthrough. Future user-discovered campaign
compatibility defects should be appended here only when they are reproduced and source-audited.

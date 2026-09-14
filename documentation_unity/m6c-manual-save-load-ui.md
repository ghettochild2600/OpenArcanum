# M6C Manual Save/Load Presentation

Completed on 2026-09-13 with Unity 6000.0.71f1 on `feature/session-save-load`.

## Scope

M6C adds the first player-facing manual save/load surface over the authoritative M6B slot service. It exposes bounded
create, overwrite, enumerate, load, delete, confirmation, cancellation, metadata, and corrupt-slot diagnostics. It does
not move save authority into Unity presentation and does not add autosave, quicksave, cloud synchronization, original
Arcanum save compatibility, recovery backups, or a broader UI framework.

## Final ownership and call graph

```text
ProductionSaveLoadPresenter (Unity IMGUI, F6, drawing only)
        |
        v
SaveLoadPanelController (transient view state and player intent)
        |
        v
ISessionSaveSlotOperations
        |
        v
SessionSaveSlotService (safe IDs, metadata, files, atomic slot lifecycle)
        |
        v
SessionSaveService / SessionSaveMigrator
        |
        v
WorldMapSessionCoordinator and M1-M5 authoritative domain services
```

`WorldObjectSectorLoader` remains a composition/presentation owner. Its idempotent production composition adds one
`PlayerInputGate`, `ProductionDialoguePresenter`, `ProductionJournalPresenter`, and `ProductionSaveLoadPresenter` to
the existing coordinator object. It does not own save data or slot policy.

`SaveLoadPanelController` owns only open/mode/selection/confirmation/message state and immutable display projections.
It calls the narrow `ISessionSaveSlotOperations` boundary. `SessionSaveSlotService` remains the sole owner of safe
slot IDs, enumeration, authoritative metadata, validated load, atomic replacement, and deletion. Successful load still
transactionally replaces authoritative service roots through M6A and rebuilds disposable Unity presentation.

## Player-facing lifecycle contract

- F6 opens the manual panel in Save mode and closes it when already open. Escape cancels an active confirmation or
  closes the panel.
- Save mode starts on the next free deterministic `manualNN` ID. Existing slots can be selected for overwrite; a new
  slot can be selected explicitly after choosing an existing one.
- Load mode selects the first valid listed slot, while corrupt/incompatible slots remain visible and selectable so the
  player can see the diagnostic or delete the file deliberately.
- Successful create/overwrite refreshes the catalog and selects the written slot. Overwrite never calls the slot
  service before explicit confirmation.
- Successful load closes the panel. A failed load leaves the panel, slot list, and active authoritative session intact.
- Delete never calls the slot service before explicit confirmation. Cancellation performs no filesystem operation and
  reports that no save data changed.
- Slot display data is projected directly from M6B metadata: UTC timestamp, selected sector/current map, PC level,
  slot format version, and embedded session version. Presentation does not recompute gameplay state.
- Typed slot/session failures are mapped to concise player-visible messages without exposing partial restored state.

## Input and presentation lifecycle

`PlayerInputGate` is transient Unity presentation state. Opening the save/load panel cancels any pending navigation
route and interaction, then blocks new click movement, world interaction, and dialogue input. Closing, disabling, or a
successful load releases the gate. The gate never owns gameplay state.

The presenter rebinds safely after a transactional load and refreshes the catalog on every open. The validation proved
that there is exactly one save/load presenter, input gate, coordinator, navigation controller, interaction controller,
and visual owner per ObjectID after loading and continued play.

## Automated validation

- Unity compilation: clean, 0 compiler errors.
- Focused M6C EditMode tests: **26/26 passed**.
- Required M1-M6C regression matrix: **390/390 passed**.
- Complete EditMode suite: **561/561 passed**.
- Every run reported 0 failures, 0 skips, and 0 inconclusive tests.

The focused tests cover empty/list-failure states, authoritative metadata formatting, deterministic slot naming,
selection, create, overwrite confirmation/cancellation, load success/failure, corrupt visibility, typed errors, delete
confirmation/cancellation/failure, input-gate release, load/reopen refresh, and idempotent production composition.

## Physical Play Mode validation

Computer Use drove the actual TestTerrain Game view and the ordinary production APIs. The harness used a unique
temporary save directory and real source object `G_8781D726_74FE_0846_AD0A_88EE591B6383` (food prototype 10078) in
`maps/arcanum1-024-fixed/101602821845.sec`.

The physical run:

1. Opened F6 and created `manual01` at PC position A.
2. Closed the panel, physically clicked an unobscured source-grid target, completed navigation, and created
   `manual02` at position B.
3. Loaded A, then B, proving `A -> B -> A -> B` exact position restoration through the player-facing UI.
4. Selected malformed `m6ccorrupt`; the corrupt error remained visible and B stayed authoritative and unchanged.
5. Selected `manual02`, requested overwrite, and confirmed; the slot file timestamp advanced.
6. Selected `manual01`, requested delete, and confirmed; only that slot was removed.
7. Selected `manual02`, requested delete, and canceled; the slot remained intact.
8. Closed the panel, physically click-moved again, then physically clicked the real food. Normal navigation and
   interaction resumed and the food moved into the production PC inventory.

Final Play Mode result: **passed with 0 new warnings and 0 errors**. The temporary harness directory was removed after
the run. Separately, the user-confirmed local test slot `manual01` was permanently deleted; no other persistent save
slot or file was deleted.

## Deliberate limits and recommended next task

M6C does not add autosave, quicksave, cloud sync, cross-process slot locking, backup/recovery browsing, thumbnails,
original Arcanum save import, combat, followers, barter, or world travel. It does not serialize transient dialogue
views, pending navigation/interactions, or Unity objects beyond the established M6A/M6B contract.

The exact recommended next task is **M7A - audit and implement the first authoritative local map-transition slice**:
trace original teleport/jump-point and map-index/coordinate semantics, add one typed session-owned transition request,
and prove one real scripted/physical transition with a stable PC and return position. Keep town/world-map travel,
encounters, and broader clock/day-night behavior out of that first slice.

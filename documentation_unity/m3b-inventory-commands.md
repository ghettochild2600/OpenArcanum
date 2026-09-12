# M3B Pickup, Drop, and Inventory Commands

## Upstream source audit

This audit was completed before M3B production changes. References are to the read-only
`D:/OpenArcanum/Research/Repositories/arcanum-ce/src/game` reconstruction.

### Raw ownership and placement

- `obj.h:439-448` defines inventory-capable objects as the contiguous source types weapon through generic
  (`OBJ_TYPE_WEAPON`..`OBJ_TYPE_GENERIC`, OpenArcanum `ObjectType.Weapon`..`ObjectType.Generic`). Containers and
  critters own inventory but are not themselves items.
- `item.c:319-334` resolves an item's parent only when the object is an item and `OF_INVENTORY` is set.
- `object.c:3835-3863` removes a picked-up item from the loaded sector, sets `OF_INVENTORY`, and writes
  `OBJ_F_ITEM_PARENT`.
- `item.c:3680-3760` inserts into the correct container/critter inventory list, assigns an inventory location, and
  writes `OBJ_F_ITEM_PARENT`. Stack merging, equipment, encumbrance, AI, UI, magic/tech notifications, and
  `SAP_INSERT_ITEM` are higher layers and are not part of M3B's raw placement transaction.
- `item.c:4278+` removes an item only from its exact recorded parent, resets its inventory location, and performs the
  source's equipment/UI/AI/magic-tech notifications. M3A's compare-and-commit source placement supplies the required
  exact-parent invariant without importing those later systems.
- `object.c:3802-3832` performs the raw world drop: clear `OF_INVENTORY`, set the exact world location, zero draw
  offsets, add the object to the loaded sector when applicable, and invalidate rendering.

### Ordinary pickup

- `anim.c:646-668` defines `AG_PICKUP_ITEM`. If the initial attempt cannot run, it sets `AGDATA_RANGE_DATA` to **0**
  and pushes `AG_MOVE_NEAR_OBJ`. `anim.c:4502-4540` defines that range check with source `location_dist`; therefore an
  ordinary pickup approaches the item's exact tile, not the range-2 portal-use perimeter.
- `anim.c:7819-7890` revalidates actor and target, rejects destroyed/off actors, allows a contained source only when
  its parent is a junk pile, consumes one combat action point, plays the local-PC pickup sound, and calls
  `item_transfer(target, actor)` on the host.
- `item.c:604-687` runs `SAP_GET`, checks insertion/capacity policy, then performs `object_pickup` plus `item_insert`.
  Capacity/weight, magic-versus-tech pickup restrictions, inventory slot selection, stacking, action points, sound,
  decay, multiplayer, junk-pile access, AI, and scripts are explicitly deferred. M3B implements the ordinary
  production-PC, world-presented, unscripted transaction only.
- There is no general `OIF_NO_PICKUP` flag. `OIF_NO_NPC_PICKUP` applies to autonomous NPC behavior, not the production
  PC. M3B pickup eligibility is therefore: production actor, persistent item type, authoritative world placement in
  the actor's active sector, not off/destroyed/suppressed, currently presented, and exact source range 0 at execution.

### Ordinary drop

- `item.c:703-769` obtains the current parent, runs `SAP_DROP`, calls `item_check_remove`, chooses the parent's tile
  for distance 0 (or searches displacement locations for thrown/scattered drops), removes the item, and places it in
  the world.
- `item.c:3876-3889` rejects `OIF_NO_DROP` (`0x00000020`) and otherwise permits an unequipped item. Equipment removal,
  displaced/random drops, action animation, AI reacquisition, sounds, and scripts are deferred.
- M3B's narrow Drop command accepts an explicit valid tile in the production PC's currently selected sector, requires
  that PC to be the exact authoritative parent, rejects `OIF_NO_DROP`, and commits one M3A Contained-to-World transfer.

### Source events deliberately deferred

The original call paths may fire `SAP_GET`, `SAP_DROP`, `SAP_TRANSFER`, `SAP_INSERT_ITEM`, magic/tech pickup/drop
notifications, UI refreshes, equipment and encumbrance recalculation, AI notifications, sound, decay, multiplayer,
and action-point handling. They belong to later script, equipment, capacity, AI, combat, audio, and UI milestones.
M3B neither fabricates those outcomes nor bypasses M3A ownership state.

## Pre-implementation ownership and call graph

```text
literal click
  -> PlayerClickMoveInput (portal-only alpha hit selection)
  -> PlayerInteractionController (transient Use approach/cancellation)
     -> InteractionApproachPlanner (source-grid A*, supplied range)
     -> WorldMapSessionCoordinator.ExecuteInteraction (authoritative Use validation)

WorldMapSessionCoordinator.TransferItem
  -> validates exact expected ObjectPlacement and destination
  -> mutates PersistentObjectState.Placement once
  -> ObjectPlacementChanged
     -> WorldObjectSectorLoader removes or creates ordinary presentation
     -> source-grid navigation occupancy follows the presentation
```

M3A already provides the authoritative atomic mutation and presentation projection. It has no gameplay-level pickup,
drop, or owner-transfer policy. The M2 interaction controller already provides replacement, cancellation, approach,
and execute-on-arrival behavior, but assumes every command is portal Use at range 2.

## Smallest planned M3B change set

1. Extend the existing stable-identity interaction command/result boundary with PickUp, Drop, and Transfer payloads.
2. Add session-owned command validation that maps successful commands to exactly one M3A `TransferItem` call.
3. Retain resolved item flags in persistent state so the narrow Drop rule can enforce `OIF_NO_DROP`.
4. Parameterize the existing approach flow by command range/type; keep portal Use at 2 and pickup at source range 0.
5. Extend deterministic presentation-assisted alpha selection to eligible item types without making presentation
   authoritative.
6. Add focused synthetic and real-data EditMode coverage plus a narrow interactive validation command.

The real source-authored item fixture and completed validation results will be recorded below after inspection and
validation. No inventory UI, capacity, stacking, equipment, economy, stealing, scripts, combat, or M3C work is part of
this change.

## Implemented command and ownership model

```text
literal Game-view click
  -> WorldObjectTargetSelector (presentation-assisted alpha hit test)
  -> stable ArcanumObjectId + target type
  -> PlayerInteractionController (transient pickup approach/cancellation)
  -> PlayerNavigationController (existing deterministic source-grid route)
  -> WorldMapSessionCoordinator.ExecuteInteraction
     -> PickUp / Drop / Transfer policy and authoritative state validation
     -> M3A TransferItem(expected source, destination)
        -> one PersistentObjectState.Placement mutation
        -> ObjectPlacementChanged
           -> WorldObjectSectorLoader presentation/navigation projection
```

`WorldMapSessionCoordinator` is the gameplay authority. The click selector, interaction/navigation controllers,
`WorldObject`, sprite owner, and sector loader never own inventory state. Commands carry only stable actor/item IDs and
typed destinations. Results distinguish missing/invalid items, containment, range, exact-source mismatch, invalid
destinations, `OIF_NO_DROP`, and underlying transfer failure.

Pickup uses the existing interaction approach lifecycle at source range 0. Manual ground movement or a replacement
target cancels the pending command, and arrival revalidates the current authoritative placement before committing.
Drop requires the production PC to be the exact current parent, an integer tile in its active sector, and no
`OIF_NO_DROP` bit. Owner transfer permits PC-to-container and container-to-PC movement when the PC is one endpoint.
All successful commands delegate to the M3A compare-and-commit transaction exactly once.

The loader retains decoded item flags in persistent state. Its source-record pass remains responsible for normal
off/destroyed/inventory suppression. A second narrow projection pass creates only eligible session-retained world
items that have no source record in the active sector. This allows a source-authored item dropped into another sector
to survive destination reload without resurrecting or duplicating the old source projection.

## Real item fixture

The production proof used the source-authored item:

- sector: `maps/arcanum1-024-fixed/101602821845.sec`
- ObjectID: `G_8781D726_74FE_0846_AD0A_88EE591B6383`
- prototype: 10078
- type: `Food`
- authored tile: `(10,46)`
- ART ID: `0x60660004`
- item flags: `0x00000180` (`OIF_NO_DROP` is not set)
- supported use script: none

The owner-transfer proof used real container `G_8F454608_E327_1341_B85B_E7A5402D4758`, prototype 3052, in
`maps/arcanum1-024-fixed/101602821844.sec`.

## Validation

Unity 6000.0.71f1 compiled the final implementation with zero compiler errors. Computer Use drove the open
`TestTerrain` editor and issued a literal click at the prepared visible pixel of the real food presentation. The click
selected the exact stable ObjectID, started the normal range-0 route from outside pickup range, moved the production
PC with the existing walk/facing behavior, and committed one World-to-Contained(PC) mutation on arrival.

The same Play Mode run passed:

- manual-movement cancellation with no stale pickup or placement mutation;
- contained identity/state persistence through graphics rebuild, source-sector reload, and A-to-B-to-A traversal;
- PC-to-real-container and real-container-to-PC transfer with the same item identity and no world projection;
- drop in the foreign container sector with the original prototype/ART path and exactly one ordinary presentation;
- destination unload/reload through the retained-session-item projection, followed by source-sector verification that
  the authored record did not respawn;
- Original-to-Enhanced-to-original-mode rebuilds for both contained and world-presented states;
- runtime item `D_0000000000000001` through the same pickup/drop path, with identity/placement surviving rebuild and
  reload and the next allocation remaining deterministically `D_0000000000000002`;
- one coordinator, object owner/root, interaction controller, navigation controller, production PC runtime/sprite
  owner, and one presentation per persistent identity.

The harness recorded 0 new warnings and 0 errors. Focused and regression results were:

- M3B: 13 passed
- M3A: 8 passed
- M2B: 8 passed
- M2A: 16 passed
- PlayerNavigation: 21 passed
- M1A: 7 passed
- M1B: 11 passed
- WorldSessionState: 27 passed
- PortalArtResolver: 2 passed
- complete EditMode: 284 passed

Every run had 0 failures, 0 skips, and 0 inconclusive tests.

## Remaining M3 scope

M3B does not execute `SAP_GET`, `SAP_DROP`, `SAP_TRANSFER`, or `SAP_INSERT_ITEM`; source hooks were audited but their
production host/state consequences remain deferred. It also does not implement inventory/equipment UI, inventory
locations/slots, equip/unequip, stacking or quantities, capacity/weight, money/economy, theft, sounds, decay, AI,
combat action points, destruction, or save serialization.

The recommended M3C slice is authoritative equipment-location state and atomic equip/unequip commands for source
worn locations 1000-1008, including slot compatibility and failed-transaction invariants, while continuing to defer
inventory UI, stacking/capacity, combat effects, scripts, and serialization.

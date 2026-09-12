# M3C Authoritative Equipment State

## Upstream source audit

This audit was completed before M3C production changes. References are to the read-only
`D:/OpenArcanum/Research/Repositories/arcanum-ce/src/game` reconstruction.

### Worn locations and ownership

- `item.h:8-20` defines the exact contiguous worn locations: Helmet 1000, Ring1 1001, Ring2 1002,
  Medallion 1003, Weapon 1004, Shield 1005, Armor 1006, Gauntlet 1007, and Boots 1008.
- `item.c:2529-2566` implements `item_wield_get` by scanning `OBJ_F_CRITTER_INVENTORY_LIST_IDX` and returning the
  item whose `OBJ_F_ITEM_INV_LOCATION` equals the requested worn location. Equipped items are not moved into a
  separate source collection: they retain the same critter parent and ordinary inventory-list membership.
- `item.c:3680-3777` inserts an item into the container/critter inventory list, writes both
  `OBJ_F_ITEM_INV_LOCATION` and `OBJ_F_ITEM_PARENT`, and invokes `item_equipped` only when the location is in the
  worn range. Thus equipment is authoritative item placement state, not a Unity hierarchy or sprite property.
- `item.c:4278-4360` removes an item from its exact parent inventory, resets its inventory location, and invokes
  `item_unequipped` when its prior location was worn.

### Equip, replacement, and rollback

- `item.c:2570-2617` implements `item_wield_set`. It resolves the owner from the item's existing parent, checks the
  requested worn location, checks removal of both the new and occupying item, removes both when the slot is occupied,
  reinserts the old item at an ordinary inventory location, then inserts the new item at the worn location.
- If the displaced item cannot be reinserted, the source restores the new item to its saved inventory location and
  restores the old item to the worn location. A failed replacement therefore leaves both identities and locations
  unchanged.
- `item.c:2633-2654` implements unequip by checking removal and ordinary reinsertion before removing and reinserting
  the item. M3C preserves that all-or-nothing state contract while deferring capacity and grid-cell selection.

### Minimum source slot compatibility

- `item.c:2998-3029` derives an item's natural worn location. Weapons use Weapon 1004. Armor uses the coverage encoded
  by `OBJ_F_ITEM_INV_AID`: Helmet, Ring, Medallion, Shield, Torso, Gauntlets, or Boots. Generic items use Shield 1005
  only when `OGF_USES_TORCH_SHIELD_LOCATION` (`0x0001`) is present.
- `item.c:2657-2686` maps Ring2 1002 to Ring1 1001 for compatibility, then requires the derived location to match.
  Ring1 and Ring2 remain distinct occupancy locations with the same ring eligibility.
- `item.c:2690-2725` rejects a fixed two-handed weapon when Shield is occupied and rejects a Shield item while a fixed
  two-handed weapon is equipped. `obj_flags.h:185-186` defines `OWF_TWO_HANDED` as `0x0004` and
  `OWF_HAND_COUNT_FIXED` as `0x0008`.
- `obj.h:221-227` places `OBJ_F_GENERIC_FLAGS` at field 211; `obj_flags.h:260` defines the torch/shield-location bit.
  OpenArcanum already decodes inventory ART and weapon flags, so M3C only needs the missing generic flag scalar.

The source also checks polymorph/body state, magic allergy, broken art, crippled arms, race size, gender, critter ART
availability, hexed removal, ordinary inventory capacity, and other character/runtime properties. Those data and
systems are outside this slice. M3C fails explicitly on unsupported item/slot combinations but does not invent those
future character, combat, magic/tech, or presentation outcomes.

### Deferred source consequences

`item_equipped` and `item_unequipped` change critter ART, notify magic/tech systems, recalculate light, update UI, and
execute `SAP_WIELD_ON` / `SAP_WIELD_OFF`. Insert/remove paths also affect encumbrance, AI, hotkeys, stacking, scripts,
and inventory layout. All remain deliberately deferred. M3C owns only identity, parent, worn location, compatibility,
and atomic placement changes.

## Pre-implementation ownership and call graph

```text
WorldMapSessionCoordinator
  -> owns PersistentObjectState.Placement
     -> World(sector, tile) or Contained(parent)
  -> TransferItem validates exact expected source and commits one placement
  -> ObjectPlacementChanged
     -> WorldObjectSectorLoader projects or removes ordinary world presentation

ObjectInstanceReader
  -> already decodes authored OBJ_F_ITEM_PARENT, OBJ_F_ITEM_INV_LOCATION,
     OBJ_F_ITEM_INV_AID, and OBJ_F_WEAPON_FLAGS
  -> PersistentObjectState currently collapses every parented source item to Contained(parent)
```

No runtime object, sprite owner, loader, click controller, or demo component owns item location state. The missing
piece is a typed equipped placement in the same session authority, plus an atomic two-item replacement operation.

## Smallest planned M3C change set

1. Add a typed `WornLocation` for source values 1000-1008 and extend `ObjectPlacement` with
   `Equipped(parent, location)`, preserving parent identity as the containment owner.
2. Retain effective inventory ART, weapon flags, and generic flags in `PersistentObjectState`; preserve authored worn
   locations when source instances first enter the session.
3. Add deterministic session queries for ordinary contained children, equipped items, and exact slot occupants.
4. Add authoritative `EquipItem` and `UnequipItem` operations. Validate exact actor/item ownership, critter owner type,
   slot compatibility, ring equivalence, and fixed-two-hand/shield exclusion before mutation. For an occupied slot,
   commit the new equipped placement and displaced ordinary-contained placement together before notifying observers.
5. Keep existing pickup/drop/transfer policy intact. Equipped items cannot be dropped or owner-transferred until
   explicitly unequipped; all failed transactions preserve both item states and emit no placement event.
6. Add focused EditMode tests and a Play Mode validation harness using real authored equipment plus a dynamic item.
   Validate equip, replacement, unequip, failures, traversal/reload, graphics rebuild, unique presentation ownership,
   and unchanged M3B/navigation/session regressions.

Implementation details, real fixtures, and exact validation totals will be appended after successful validation.

## Implemented representation and command contract

`ObjectPlacement` now has three mutually exclusive forms:

```text
World(normalized sector, tile)
Contained(parent ObjectID)
Equipped(parent ObjectID, typed WornLocation)
```

`WornLocation` preserves the source values exactly. `PersistentObjectState.ParentIdentity` returns the same parent for
both `Contained` and `Equipped`, matching the source's unchanged `OBJ_F_ITEM_PARENT`; `ChildrenOf` enumerates only
ordinary contained inventory, while `EquippedItems`, `TryGetEquippedItem`, and `TryGetWornLocation` expose equipment
deterministically without consulting Unity objects.

The loader now retains the effective `OBJ_F_ITEM_INV_AID`, `OBJ_F_WEAPON_FLAGS`, and `OBJ_F_GENERIC_FLAGS` values.
Authored parented records whose source inventory location is 1000-1008 enter the session as typed equipment. Sector
validation rejects two distinct authored items occupying the same owner/location before registration. Any non-world
placement is suppressed by the ordinary world presentation path.

`EquipItem(actorId, itemId, location)` validates the actor as a session-known PC or NPC, exact item ownership, item
type, source location compatibility, `OIF_NO_DROP` removal policy, and fixed-two-hand/shield conflicts. A source ring
may occupy either distinct ring location. Weapons use Weapon; armor coverage is decoded from inventory ART; a generic
item uses Shield only with `OGF_USES_TORCH_SHIELD_LOCATION`.

When a location is occupied, both the new item's `Equipped` placement and the old item's ordinary `Contained`
placement are assigned before either placement event is raised. Observers therefore see only the final pair. If any
validation/removal check fails, neither placement changes and no event fires. Generic `TransferItem` explicitly
rejects an equipped source, so Drop and owner-transfer cannot bypass `UnequipItem`.

`UnequipItem(actorId, location)` resolves the exact occupant, applies the same source removal gate, and changes only
`Equipped(actor, location)` to `Contained(actor)`. ObjectID, prototype, parent, session object, and presentation
suppression remain unchanged.

## Final ownership and call graph

```text
input / validation harness
  -> equipment intent: actor ObjectID + item ObjectID + typed WornLocation
  -> WorldMapSessionCoordinator EquipItem / UnequipItem
     -> source-minimum eligibility and exact ownership validation
     -> atomic PersistentObjectState placement commit
     -> ObjectPlacementChanged after the complete transaction
        -> WorldObjectSectorLoader observes world/non-world projection only

session queries
  -> ChildrenOf: ordinary Contained items
  -> EquippedItems / TryGetEquippedItem / TryGetWornLocation: Equipped items
```

The coordinator remains the gameplay/session authority. The production PC GameObject, `WorldObject`, loader,
sprite owners, navigation, interaction controller, and demo scene remain presentation or transient-intent components.

## Real source fixture and Play Mode validation

The source-authored fixture was:

- sector: `maps/arcanum1-024-fixed/101602821844.sec`
- source container: `G_8F454608_E327_1341_B85B_E7A5402D4758`, prototype 3052
- item ObjectID: `G_0435F503_6600_6342_97B2_6D9E1A85A2F2`
- prototype/type: 8127 / Armor
- world ART: `0x60040082`
- inventory ART: `0x60041082`
- item flags: `0x00000000`
- decoded source location: Armor 1006

Computer Use drove Unity 6000.0.71f1 and performed a literal Game-view click on the authentic item's ordinary world
presentation. The production PC was prepared on the same tile because this armor projection blocks its tile and the
source pickup range is zero; M3B already separately proves routed range-zero pickup. The click selected the exact
stable ObjectID and executed the normal M3B pickup command.

The same run then passed:

- `World -> Contained(PC) -> Equipped(PC, Armor)` with the same authored identity and no world presentation;
- occupied Armor-location replacement by dynamic `D_0000000000000001`, followed by an atomic reverse swap;
- Original/Enhanced/original rebuilds with unchanged identities, placements, and no independent equipment sprite;
- A-to-B-to-A traversal while the authored item remained equipped to the deterministic production PC;
- source-sector unload/reload without authored-position resurrection;
- Unequip to ordinary PC containment, followed by the existing Drop command and exactly one world presentation;
- final world reload with one persistent state and one presentation;
- dynamic equip, rebuild, reload, and unequip through the same APIs;
- one coordinator, loader/root, navigation controller, interaction controller, production PC runtime/sprite owner,
  object state per identity, and deterministic inventory/equipment membership.

The final Play Mode harness recorded 0 new warnings and 0 errors. Unity compiled with zero compiler errors.

## Automated validation

- M3C focused: 13 passed
- M3B inventory commands: 13 passed
- M3A inventory state: 8 passed
- M2B SAP_USE: 8 passed
- M2A interaction: 16 passed
- PlayerNavigation: 21 passed
- M1A lifecycle: 7 passed
- M1B traversal: 11 passed
- WorldSessionState: 27 passed
- PortalArtResolver: 2 passed
- complete EditMode: 297 passed

Every run had 0 failures, 0 skips, and 0 inconclusive tests.

## Deferred work and recommended next slice

M3C does not apply equipped-item combat, armor, statistic, animation, paper-doll, light, magic/tech, AI, UI, hotkey,
encumbrance, capacity, script, or save effects. Race/gender/body-state eligibility, crippled arms, critter ART
availability, broken-item ART, and magic-allergy rules require later character/effect systems and remain explicit
future policy. Ammo is not a source worn location and is unchanged.

The exact recommended next milestone is M3D: authoritative stack quantity state with atomic compatible-stack
merge/split transactions and identity rules, including failed-transaction and reload/rebuild coverage. Continue to
defer inventory UI, weight/capacity, economy, combat consumption, scripts, and save serialization.

# M3A Inventory and Containment State

## Pre-code source semantics audit

The checked-in `arcanum-ce` sources and the clean mounted corpus establish the boundary for this slice:

- `OBJ_F_ITEM_PARENT` is the item's owner reference. `OF_INVENTORY` distinguishes contained items from world objects;
  pickup removes the item from the sector object list, sets that flag and parent, while drop clears the flag and writes
  a world location (`object_pickup` / `object_drop`).
- Containers and critters own explicit inventory-count/list fields. `item_insert` updates that list, inventory location,
  and the item's parent; `item_force_remove` removes the exact list entry and clears its inventory location. The two
  directions must agree. The engine validator rejects self ownership, null list entries and non-item children.
- `item_transfer_ex` validates scripts, carry weight, grid space, technophobia, stacking and worn-slot behavior before
  remove/insert. Those are gameplay/equipment policies layered above raw containment and are intentionally excluded
  from M3A. Stack merging may destroy the incoming object, so quantity/merge semantics are also excluded.
- Runtime-created items normally receive a new GUID through `obj_create_inst`. The original generator is intentionally
  nondeterministic. M3A therefore needs a documented OpenArcanum session identity, not a fabricated authored `A`, `G`
  or positional `P` identity. Source GUID generation remains evidence for object uniqueness, not permission to reuse an
  existing NPC or authored object.
- Contained records retain source location data, but it is not their active world placement. A later drop explicitly
  receives a world destination. Transfers do not derive authority from Unity Transforms or GameObject parenting.
- The source has no general nested-container chain: only `OBJ_TYPE_CONTAINER` and critters own the ordinary inventory
  list, while ordinary item types carry `OBJ_F_ITEM_PARENT`. M3A still rejects self/cyclic malformed relationships so
  corrupt or future data cannot create an invalid graph.
- Destroying containers/critters invokes special drop/persistence behavior and destroying stackable/key items can merge
  or erase identities. Because those rules are outside this slice, M3A will not expose general destruction and will
  reject any future non-empty-container removal until that behavior is implemented deliberately.

Real clean-data fixture selected before implementation:

- sector `maps/arcanum1-024-fixed/101602821844.sec`;
- container `G_8F454608_E327_1341_B85B_E7A5402D4758`, prototype 3052, map tile `(111884,96914)`;
- armor child `G_0435F503_6600_6342_97B2_6D9E1A85A2F2`, prototype 8127, the same retained source tile,
  `OBJ_F_ITEM_PARENT` equal to that container, inventory location 80, and effective flags `0x00001434`.

## Pre-code ownership graph and smallest change set

Current loading is:

```text
ObjectInstanceReader (authored identity + OBJ_F_ITEM_PARENT)
  -> WorldObjectSectorLoader
     -> WorldMapSessionCoordinator.GetOrCreate (PersistentObjectState)
     -> instance/OF_INVENTORY test skips presentation
     -> ordinary WorldObject -> WorldObjectSpriteOwner for remaining records
```

The smallest safe M3A change is:

1. Add an explicit `World` / `Contained(parent)` placement value to session-owned object state and make loader
   filtering consume that value.
2. Add coordinator queries plus one atomic transfer command with explicit result codes, parent/type/cycle validation,
   and post-commit placement notification.
3. Add an OpenArcanum-only monotonic session identity variant for runtime-created items, disjoint by ObjectID type from
   source-authored `A`, `G`, and `P` values; bind prototype lookup as immutable content input and expose only a minimal
   create-item-to-destination operation.
4. Let the existing sector loader reconcile a changed item by removing/restoring its ordinary world projection and
   navigation registration. The loader remains a presentation owner; it never decides containment.
5. Add focused EditMode coverage and a real-sector validator. Do not add inventory UI, equipment, stacking, capacity,
   scripts, interaction targeting, save serialization, or destruction.

This note records the design gate before production code changes. Final APIs, validation results, and any evidence-led
adjustments will be appended after implementation.

## Final ownership and call graph

```text
read-only .sec / prototype data
  -> WorldObjectSectorLoader decodes content
     -> WorldMapSessionCoordinator.ValidateSector / GetOrCreate
        -> PersistentObjectState owns identity + authoritative World/Contained placement

WorldMapSessionCoordinator.TransferItem / CreateItem
  -> validate expected source, item/owner types, destination, self/cycle rules
  -> commit one placement (or commit nothing)
  -> ObjectPlacementChanged
     -> WorldObjectSectorLoader reconciles the selected-sector projection
        -> SectorNavigationMap unregister/register
        -> ordinary WorldObject -> WorldObjectSpriteOwner presentation
```

`WorldMapSessionCoordinator` is the sole containment authority. `WorldObject`, `WorldObjectSectorLoader`,
`WorldObjectSpriteOwner`, the Unity hierarchy, Transforms, and Unity instance IDs do not encode inventory ownership.
The loader supplies immutable content lookup and projects committed state only. `ProductionPlayerLifecycle` continues
to own just the one PC presentation; PC inventory children refer to the existing session-owned PC ObjectID.

## Placement and lookup contract

Every persistent item state has exactly one `ObjectPlacement`:

- `World(normalizedSector, tile)` is eligible for ordinary presentation only in that sector and when normal visibility
  flags permit it.
- `Contained(parentObjectId)` has no independent world presentation or navigation occupancy.

`PersistentObjectState.AuthoredParentIdentity` permanently retains decoded `OBJ_F_ITEM_PARENT`, while mutable
`Placement` is current session truth. `ParentIdentity`, `TryGetPlacement`, and the sorted direct `ChildrenOf` query
provide object-to-parent, world/contained, and parent-to-child lookups without consulting Unity presentation.

The real corpus required one source-led adjustment: authored records can retain a parent that is not present in the
same sector load. In the validation sector, child `G_60D1D006_6592_8A4B_964D_05C4562AA7B8` refers to absent parent
`G_D0A6AD31_8FF1_4D42_B48A_841E932AD960`. Sector registration therefore preserves unresolved authored parent IDs.
New transfer destinations remain strict: they must resolve to a session-owned container, PC, or NPC. Known non-owner
types, duplicate identities, source-metadata collisions, self-parenting, and cycles are rejected.

## Atomic transfer and creation APIs

`TransferItem(itemId, expectedSource, destination)` is compare-and-commit. It checks the caller's expected current
placement before destination validation, then either changes one authoritative placement and publishes one event or
changes nothing. Its typed results are `Success`, `ItemNotFound`, `InvalidItemType`, `SourceMismatch`,
`InvalidDestination`, `ParentNotFound`, `InvalidParentType`, `SelfParent`, `CycleDetected`, and
`AlreadyAtDestination`. Failed results preserve placement, identity, and all owner-child queries.

`CreateItem(prototypeNumber, destination)` uses the loader-bound production prototype library, accepts item prototypes
only, validates the destination first, then registers one runtime-created state. Its additional explicit failures are
`PrototypeSourceUnavailable`, `PrototypeNotFound`, and `IdentityExhausted`. It does not write original data, generate
loot, run scripts, or create an inventory UI. A contained item needs no Unity object; a world item uses the existing
ordinary `WorldObject`/ART path.

## Dynamic identity decision

Runtime-created items use `ArcanumObjectIdType.SessionDynamic` (serialized type value 4) with keys such as
`D_0000000000000001`. Each session coordinator starts at sequence 1 and allocates monotonically in creation order,
skipping any occupied identity. The type discriminator makes the identity structurally disjoint from original authored
`A`, GUID-backed `G`, and positional `P` identities rather than relying on key text, a Unity instance, memory address,
or a movable world position. Identity is stable across every placement transfer, visual rebuild, unload/reload, and PC
sector transition. This in-memory deterministic sequence is deliberately provisional until M6 defines save schema,
restored counters, and long-lived dynamic-object namespaces.

## Presentation and lifecycle result

The loader caches decoded source/prototype inputs but filters and reconciles from session placement. A world-to-owner
commit unbinds the ordinary runtime, removes navigation occupancy, and destroys that presentation. An owner-to-world
commit rebuilds through the normal ART resolver and creates exactly one ordinary presentation at the authoritative
tile. EditMode cleanup now uses immediate destruction so rebuild validation matches Unity's object lifetime rules;
Play Mode continues to use normal deferred destruction.

Authoritative placement survives source-sector unload/reload and is not overwritten by decoded authored containment.
An item owned by the deterministic production PC remains attached to that same PC identity across the existing M1B
sector transition and on return. Graphics/visual rebuilds do not mutate containment, identities, world placement, or
the session's dynamic allocation state.

## Validation

The exact clean-data fixture remained:

- sector A `maps/arcanum1-024-fixed/101602821844.sec`, adjacent sector B
  `maps/arcanum1-024-fixed/101602821845.sec`;
- container `G_8F454608_E327_1341_B85B_E7A5402D4758`, prototype 3052;
- armor child `G_0435F503_6600_6342_97B2_6D9E1A85A2F2`, prototype 8127;
- decoded `OBJ_F_ITEM_PARENT` exactly equals that container ObjectID.

The focused EditMode category passed 8/8 (0 failed, 0 skipped). The final complete EditMode run passed 271/271 (0
failed, 0 skipped): M2B 8/8, M2A 16/16, PlayerNavigation 21/21, M1A lifecycle 7/7, M1B 11/11,
WorldSessionState 27/27, and PortalArtResolver 2/2.

The Play Mode harness loaded A and reconstructed the authored pair with no child projection; moved the child to world,
rebuilt one projection, moved it into the production PC, created `D_0000000000000001` from prototype 8127 in the PC,
reloaded A, crossed to B and returned, then dropped both items at distinct authoritative tiles. Both identities and PC
ownership survived, each dropped item had exactly one ordinary projection, and there was one coordinator, loader,
production PC lifecycle, navigation controller, object root, and consistent sprite-owner set. The final harness result
was `warnings=0; errors=0`; the Unity Console also showed zero warnings and zero errors.

## Remaining boundary and recommended M3B

M3A intentionally has no pickup/container interaction commands, inventory or equipment UI, worn slots, quantities or
stack merging/splitting, weight/capacity, equip/unequip, item use, money, merchants, theft, crafting, destruction,
scripts, decay, or save serialization.

The exact recommended next slice is **M3B: source-faithful pickup/drop and owner-to-owner inventory command rules on
top of M3A's atomic placement transaction**. It should connect stable M2 actor/target identities to rule-checked item
movement and explicit results, validate a real ground-item/container flow, and still defer UI, equipment, stacking,
economy, combat use, scripts, and save serialization.

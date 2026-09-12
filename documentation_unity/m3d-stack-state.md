# M3D Authoritative Stack State

## Upstream source audit

This audit was completed before M3D production changes. References are to the read-only
`D:/OpenArcanum/Research/Repositories/arcanum-ce/src/game` reconstruction.

### Quantity-bearing types and serialization

- `item.c:1036-1065` maps exactly two object types to stack state: Gold uses
  `OBJ_F_GOLD_QUANTITY`; Ammo uses `OBJ_F_AMMO_QUANTITY`. Every other item type returns no quantity field and is a
  singular object. Food, potions represented as Food/Generic items, scrolls, weapons, armor, keys, written items, and
  generic items are therefore not source stacks.
- `obj.h:151-159` and `obj.h:174-181` place the Ammo and Gold quantity fields in their type-specific object groups.
  `obj.c:3688-3696` and `obj.c:3711-3718` classify both fields as `OD_TYPE_INT32`. Instance serialization writes changed
  scalar fields through the ordinary object difference mask (`obj.c:3327-3358`), so an authored instance may override
  its prototype quantity while an unchanged instance inherits the prototype value.
- New Gold defaults to quantity 1 (`obj.c:2829-2833`). The four canonical ammunition prototypes default to quantity 10
  (`proto.c:6312`, `6326`, `6342`, `6358`). A quantity of one is still a quantity-bearing stack object, not a different
  item representation.
- The source creation/transfer functions accept signed `int` quantities and do not define a smaller per-type or
  per-prototype maximum. The representable positive source range is therefore 1 through `INT32_MAX`. Source callers
  supply positive values; zero is a no-op in the transfer helpers and no persistent zero-count object is intentionally
  created. M3D will reject loaded or requested quantities below one and reject arithmetic above `INT32_MAX` rather than
  reproduce undefined/overflow behavior.

### Compatibility and automatic inventory merge

- `item_find_first_matching_prototype` (`item.c:1291-1324`) compares the exact prototype object handle and nothing else.
  It does not compare flags, damage/condition, charges, magic/tech state, scripts, descriptions, owner, or inventory
  cell. Its generic-base-prototype exclusion is irrelevant to the only two quantity-bearing types.
- `item_check_insert` (`item.c:3639-3677`) explicitly treats a matching Gold/Ammo prototype in the destination owner as
  available room. `item_insert` (`item.c:3680-3727`) then adds the entire incoming quantity to the first matching
  destination stack, executes the existing insert consequence, destroys the incoming object, and returns without
  adding a second inventory-list entry.
- Thus source stack compatibility is: both objects are Gold or Ammo, both use a valid positive quantity, and both have
  the exact same prototype. Exact prototype identity already implies the same object type and, for ammunition, the same
  canonical ammo kind. Instance-specific flags or descriptions do not prevent source merging.
- Source automatic merging occurs on insertion into a critter/container inventory. `object_drop` (`object.c:3802-3832`)
  only places an object in a sector and does not search for or merge world piles. M3D will therefore keep world stacks
  independent and will not invent world merge/split behavior.

### Merge identity and destruction

- In `item_insert`, the pre-existing destination-owner stack survives. Its quantity is increased; the incoming source
  object is destroyed (`item.c:3717-3727`). Full merge identity is therefore destination-survives/source-is-consumed,
  not an arbitrary or lexicographic choice. This applies even when the consumed source has an authored ObjectID.
- Destruction removes a parented item from its inventory before marking it destroyed (`object.c:1272-1343`). A consumed
  authored source record must therefore remain absent when its sector data is loaded again; presentation and membership
  cleanup are consequences of authoritative object removal, not separate Unity ownership.
- No source maximum-stack branch exists. M3D will use checked positive `Int32` arithmetic as the serialization boundary
  and fail without mutation when a merge would exceed it.

### Partial quantity transfer and split identity

- `item_gold_transfer` (`item.c:2463-2513`) and `item_ammo_transfer` (`item.c:3113-3166`) leave the original object in
  place with a reduced quantity on a partial transfer. When the receiving owner already has a matching stack, that
  destination object survives and its quantity increases.
- When the receiving owner has no matching stack, the source creates a new canonical Gold/Ammo object at the receiving
  owner's location (`item_gold_create`, `item.c:2517-2525`; `item_ammo_create`, `item.c:3169-3178`) and inserts it. The
  original object retains its identity for a partial transfer; the newly separated quantity has a newly created runtime
  identity.
- A full transfer destroys the source object. An already-present compatible destination still survives; otherwise the
  source helper creates a new destination object. M3D's explicit split operation preserves the source identity and uses
  the existing monotonic session `D_` allocator exactly once for the split-off object. Its explicit merge operation
  preserves the selected destination identity and tombstones/removes a fully consumed source.

### Placement, equipment, and deferred consequences

- Source automatic stack merge is an inventory-insertion behavior. M3D supports explicit split and merge only for
  ordinary `Contained(parent)` placement with the same owner. Existing owner-transfer into a critter/container will use
  the same automatic destination-stack merge contract. World stacks retain quantity but are neither merged nor split;
  dropping does not merge.
- Gold and Ammo do not have source worn locations. M3C already rejects them as equipment. M3D rejects any malformed
  `Equipped` stack from split/merge and does not weaken the equipment boundary.
- Quantity survives pickup, ordinary owner transfer, drop, sector traversal, unload/reload, and graphics rebuild because
  it belongs to session state. Unity objects never represent quantity by duplication.
- UI quantity dialogs/labels, carry weight, combat ammo consumption, economy, pick-pocket limits, scripts and insert/
  destroy callbacks, vendors, crafting, loot generation, save serialization, and other consumption behavior remain
  outside M3D.

## Pre-implementation ownership and smallest change set

```text
ObjectInstanceReader / ObjectPrototypeDatabase
  -> already decode nullable AmmoQuantity and GoldQuantity overrides/defaults

WorldMapSessionCoordinator
  -> owns persistent ObjectID table and dynamic D_ allocator
  -> owns World / Contained / Equipped placement transactions
  -> currently has no quantity or authoritative removal/tombstone state

WorldObjectSectorLoader
  -> projects session-owned World state only
  -> must suppress source records whose identities were consumed by a merge
```

The smallest M3D change is:

1. Retain one nullable, validated `StackQuantity` on `PersistentObjectState`; null means a singular non-stack item.
2. Pass the effective Ammo/Gold instance-or-prototype quantity through the existing loader/session registration path.
3. Add deterministic `CanStack`, atomic partial/full merge, and atomic contained split operations to the session
   coordinator. Destination identity survives merge; original identity survives split; the split child uses the existing
   monotonic allocator.
4. Tombstone fully consumed identities and make source registration/presentation honor the tombstone so authored objects
   cannot resurrect. Commit all quantity/state-table mutations before notifying presentation observers.
5. Preserve quantity through existing placement/equipment operations. Integrate source automatic merge only when an
   ordinary transfer inserts into a destination owner with an existing compatible stack; world drop remains independent.
6. Add focused EditMode tests and a Computer Use Play Mode harness using one uncomplicated authentic Ammo/Gold stack and
   one runtime-created stack. Append the fixture, final call graph, validation totals, and remaining limitations after
   successful validation.

## Implemented ownership and transaction graph

```text
ObjectInstanceReader + ObjectPrototypeDatabase
  -> decode nullable instance/prototype AmmoQuantity or GoldQuantity

WorldObjectSectorLoader.RegisterSourceObjects
  -> chooses instance override, otherwise prototype default
  -> registers the effective positive Int32 quantity with the session

PersistentObjectState
  -> owns nullable StackQuantity (null means singular)
  -> owns the same ObjectID, prototype, and World / Contained / Equipped placement as M3A-M3C

WorldMapSessionCoordinator
  -> CanStack: positive Ammo/Gold + exact prototype
  -> SplitStack: validates, allocates one shared monotonic D_ identity, then commits both quantities/state membership
  -> MergeStacks: validates, commits both quantities, removes/tombstones a fully consumed source
  -> TransferItem: preserves quantity and performs the source insertion merge for a compatible destination-owner stack
  -> emits quantity/removal/placement notifications only after the authoritative transaction is complete

WorldObjectSectorLoader
  -> observes placement/removal after commit
  -> removes a consumed world projection once
  -> suppresses tombstoned authored records on reload
  -> never derives quantity from GameObjects, sprite owners, or hierarchy
```

The session coordinator remains the transaction boundary because it already owns object identity, placement,
containment membership, and the single dynamic-ID allocator. No new service or presentation authority was introduced.
`ObjectQuantityChanged` is an observer event, not a second state store. `ObjectStateRemoved` lets the loader retire a
world projection after commit; the tombstone table prevents a consumed authored ObjectID from being reconstructed when
its source sector is read again.

## Final contracts

### Quantity and compatibility

- `PersistentObjectState.StackQuantity` is non-null only for Ammo and Gold. Its valid range is `1..Int32.MaxValue`.
  Missing/nonpositive quantities on those source types and a quantity on any singular type fail explicitly.
- `PersistentObjectState.Restore` projects the authoritative value one-way into the existing
  `WorldObject.AmmoQuantity` or `WorldObject.GoldQuantity` cache used by source-compatible runtime consumers. Capture
  never imports those cache fields back into session state, so Unity runtime/presentation objects cannot become the
  quantity authority.
- An instance quantity wins over its prototype default. The real fixture below proves an Ammo instance quantity of 60
  is retained instead of the canonical prototype default of 10.
- `CanStack` requires two distinct, positive Ammo/Gold objects with the same exact prototype. Source does not require
  equality of item flags, descriptions, scripts, condition, charge, magic/tech values, owner, or cell. Owner/placement
  is validated separately by the requested transaction.
- No lower source maximum exists. M3D uses `Int32.MaxValue`, the serialized scalar limit, and rejects overflow before
  mutation.

### Merge identity and cleanup

- Explicit merge is available only to two ordinary `Contained` stacks with the same parent. The selected destination
  ObjectID survives. A partial merge leaves the source identity with its remaining quantity; a full merge removes and
  tombstones the source identity.
- Ordinary transfer into a PC/NPC/container automatically merges a compatible incoming stack, matching source
  `item_insert`. The pre-existing destination-owner stack survives and the incoming identity is consumed. The transfer
  result reports both the surviving identity and whether the source was consumed.
- Dropping into `World` never merges. Explicit world merge/split and any malformed `Equipped` stack operation return a
  typed failure with no mutation. Gold and Ammo remain ineligible for M3C equipment through the existing boundary.
- A consumed authored identity remains tombstoned for the session. Reload validates the source record but suppresses
  registration/presentation, so it cannot reappear or duplicate the survivor.

### Split and dynamic allocation

- `SplitStack(source, quantity)` accepts only `Contained` Ammo/Gold and requires `1 <= quantity < source quantity`.
  The original object retains its ObjectID and the remainder. The split-off stack copies its prototype and relevant
  instance state, retains the same owner, and receives exactly one identity from M3A's existing allocator.
- Allocation is monotonic across creation, split, rebuild, reload, and consumed-object tombstones. It skips any existing
  or tombstoned dynamic identity. Allocation exhaustion and all validation failures leave quantity, placement, state
  membership, and allocator-visible results unchanged.

## Authentic fixture

- Sector: `maps/arcanum1-024-fixed/101602821844.sec`
- Stack ObjectID: `G_9239E097_A8D2_C147_9F58_76077340C60E`
- Prototype/type: 7059 / Ammo
- Source quantity: 60 (instance override; prototype default is 10)
- World ART used by the ordinary resolver: `0x60000041`
- Initial placement: `Contained(G_8F454608_E327_1341_B85B_E7A5402D4758)`; that real container is prototype 3052
- Relevant flags/scripts: item flags `0x00000000`, `SAP_USE` 0
- Compatibility is carried by exact prototype 7059. A second real Ammo stack,
  `G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`, prototype 7058, quantity 70, flags 0, `SAP_USE` 0, supplied the incompatible
  merge/rollback proof. Neither fixture is selected, cloned, or reclassified as another item.

## Validation

Unity 6000.0.71f1 compiled the final production, tests, and validation harness with zero compiler errors.

- M3D focused: 18 passed, 0 failed, 0 skipped, 0 inconclusive.
- M3C: 13/13; M3B: 13/13; M3A: 8/8.
- M2B SAP_USE: 8/8; M2A interaction: 16/16.
- PlayerNavigation: 21/21; M1A lifecycle: 7/7; M1B traversal: 11/11.
- WorldSessionState: 27/27; PortalArtResolver: 2/2.
- Complete EditMode: 315 passed, 0 failed, 0 skipped, 0 inconclusive.

The Computer Use Play Mode run made a literal Game-view click on the real prototype-7059 stack and validated this
end-to-end sequence:

```text
source quantity 60
  -> PickUp keeps authored ObjectID and quantity 60
  -> Split 25 creates D_0000000000000001 and leaves 35 on the authored ObjectID
  -> split stack transfers PC -> real container with quantity 25
  -> transfer back auto-merges into the authored destination (quantity 60; D_1 consumed)
  -> Drop creates exactly one ordinary world presentation
  -> Original/Enhanced/reverted rebuild keeps identity/quantity
  -> reload and A -> B -> A keep quantity 60 and restore exactly one projection
```

The same run moved the real prototype-7058 comparison stack to the same owner and proved an incompatible merge changed
neither quantity nor placement. A runtime-created prototype-7059 stack received `D_0000000000000002`, split 4 into
`D_0000000000000003`, merged back with `D_2` surviving at quantity 10, and the next creation received
`D_0000000000000004`. Unique coordinator, loader/root, navigation/interaction controller, PC runtime/sprite owner,
state membership, and presentation checks passed. The harness recorded 0 new warnings and 0 errors.

## Deferred behavior and next milestone

M3D deliberately does not implement quantity labels/dialogs, inventory/container UI, weight or capacity, encumbrance,
economy/vendors, ammo consumption, combat item use, crafting, script callbacks, loot generation, decay/destruction
rules beyond stack consumption, or save serialization. It does not merge world piles or split world/equipped objects.

The exact recommended next milestone is **M4A — authoritative PC/NPC base attributes and derived-stat inputs**, starting
with a fresh source audit and retaining raw Strength and related critter state independently of Unity presentation. This
is the safest prerequisite for source-faithful PC carry capacity; after M4A, a bounded M3E can add item weight,
container/carry-capacity transfer guards, and encumbrance without inventing placeholder character rules. M4A must not
begin progression, combat, dialogue, spell/technology effects, UI, or save serialization.

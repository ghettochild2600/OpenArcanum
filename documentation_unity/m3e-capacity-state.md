# M3E — Source-faithful inventory weight and capacity

## Pre-implementation source audit

This audit was recorded before M3E production changes. Evidence comes from the `arcanum-ce` reconstruction (`obj.h`, `obj.c`, `item.h`, `item.c`, `stat.c`, and `critter.c`) and retail `.pro`, `.sec`, `.mob`, `.mes`, and `.art` data read through OpenArcanum's existing parsers. Original Steam data remains read-only.

### Item weight

- `OBJ_F_ITEM_WEIGHT` is object-field ordinal 89, a signed Int32 item field. Ordinary field inheritance is whole-value instance override, then prototype: `instance.Weight ?? prototype.Weight`.
- Source-created Keys default to 0, Gold to 1, and other item types to 10, but retail prototypes author their own values. These integers are tenths of a pound in UI-facing conversions: source throwing/UI code divides `item_weight` by 10.
- `item_weight(item, owner)` returns 0 for Gold before reading the stored field. Gold quantity therefore never contributes carried weight.
- Ammo and Gold are the only quantity stacks. After the Gold special case, Ammo weight is `storedWeight * floor(quantity / 4)`. This is intentionally not `quantity * storedWeight`: quantities 1–3 weigh zero, 4–7 weigh one stored-weight quantum, and so on.
- Non-stack items use the stored weight once. A stored zero remains zero.
- With an owner, the source can add `OBJ_F_ITEM_MAGIC_WEIGHT_ADJ` after scaling it through the item's magic/technology complexity and the owner's aptitude. M4A does not yet model aptitude or Item-caused effects, so M3E retains and exposes the authoritative stored weight and exact ordinary/Ammo/Gold quantity rules but explicitly defers magic/technology weight adjustment.
- `item_total_weight(owner)` enumerates the owner's direct inventory list and sums `item_weight`. It does not exclude worn locations, so equipped items count exactly like ordinary contained items.
- Containers are not item types and cannot be inserted through the ordinary item-transfer path. The source total-weight routine is direct rather than recursive; M3E therefore has no nested-container weight recursion.

### Character carry capacity

- `STAT_CARRY_WEIGHT` base is `500 * stat_level_get(actor, STAT_STRENGTH)`. The derived stat then passes through effects and the source derived-stat clamp of 300..10000.
- M3E has no carry-weight effects, so its exact admitted formula is `clamp(500 * effective Strength, 300, 10000)` using `CharacterStatService.GetEffectiveAttribute(actorId, Strength)`.
- Race and Gender matter only through that effective Strength query. The capacity calculator does not decode either modifier.
- The development PC has effective Strength 8 and capacity 4000 source weight units (400.0 lb). The selected M4A Human Female NPC has effective Strength 9 and capacity 4500 units (450.0 lb).
- `item_check_insert` rejects only when `currentLoad + incomingWeight > carryCapacity`; equality is accepted.
- The check occurs before `object_pickup` or `item_remove`, so a too-heavy failure performs no inventory mutation.

### Inventory and container room

- There is no `OBJ_F_CONTAINER_CAPACITY`, weight limit, count limit, volume limit, prototype override, instance override, or unlimited sentinel. Container fields include flags, lock/key data, inventory count/list, inventory source, and notification/padding fields only.
- Source room is a fixed inventory grid measured in 32-pixel cells. Critters have 10 columns by 12 rows (120 cells); ordinary Containers have 10 columns by 96 rows (960 cells).
- Each item's inventory ART first-frame size becomes `ceil(width / 32)` by `ceil(height / 32)` cells, with a 1×1 fallback if ART frame lookup fails. The first-fit insertion scan is horizontal: top-to-bottom rows, left-to-right columns.
- Equipped and hotkey locations do not occupy ordinary grid cells. A compatible Ammo/Gold stack already in the destination requires no new grid footprint.
- The original transfer path evaluates critter weight first, technophobia next, then stack/grid room. M3E implements only the weight and room portions; scripts, backgrounds, key-ring consumption, UI, and magic/technology restrictions remain deferred.

### Transfer and stack transaction semantics

- World pickup and owner-to-owner transfer call the insert check before removing the item from its source. Failure terminology is `ITEM_CANNOT_TOO_HEAVY` or `ITEM_CANNOT_NO_ROOM`.
- M3E maps those outcomes to typed `TooHeavy` and `NoRoom` results and performs the guard before the existing M3A atomic mutation.
- A compatible Ammo/Gold insertion merges into the existing prototype-matching destination identity. Capacity simulation must therefore evaluate the committed destination state: replace the existing stack's old total with the combined-quantity total and do not count the incoming identity or a second grid footprint.
- A split creates a second stack identity and footprint. Its two stack weights use the same independent four-unit floor rule. A fully merged/tombstoned identity contributes neither load nor room.
- Moving an item between ordinary containment and an equipped slot does not change carried weight because both are entries in the same critter inventory list. M3E does not apply equipment Strength bonuses.

### Authoritative boundaries

```text
retail prototype/instance weight + inventory ART
  -> PersistentObjectState (immutable source weight and footprint inputs)
  -> InventoryCapacityService (derived load, capacity, room, acceptance)
  -> WorldMapSessionCoordinator inventory/stack command guards
  -> existing M3A/M3D atomic mutation
  -> Unity presentation observes committed state only

CharacterStatService effective Strength
  -> InventoryCapacityService carry capacity
```

No mutable current-weight counter is stored. Load is derived from the authoritative placement, equipment, quantity, and tombstone state on every query.

## Smallest M3E change set

1. Retain the effective `instance ?? prototype` stored weight and source inventory-ART footprint on each session-owned item state; copy both to dynamic items and split stacks.
2. Add one plain domain `InventoryCapacityService` owned by the session coordinator. It derives total item weight, owner load, carry capacity, fixed grid capacity/load, and destination acceptance.
3. Bind source inventory-ART frame-size resolution from the world-object loader without making the loader or a Unity component authoritative.
4. Guard contained transfers before stack merge or placement mutation. Extend explicit stack merge/split only where their committed quantity/footprint changes require validation.
5. Add typed capacity failures, focused boundary/fixture/lifecycle tests, a real-data Play Mode validator, and regression runners. Do not implement encumbrance effects or any excluded domain.

## Real fixtures

- Ordinary weighted item: Food prototype 10078, `G_8781D726_74FE_0846_AD0A_88EE591B6383`, in
  `maps/arcanum1-024-fixed/101602821845.sec`. Its instance inherits prototype weight 50 (5.0 lb), inventory ART
  `art/item/i_jug.art` from `0x60661004`, and a 1x2-cell footprint.
- Stack: Ammo prototype 7059, `G_9239E097_A8D2_C147_9F58_76077340C60E`, quantity 60, stored weight 1,
  total weight 15, inventory ART `art/item/i_bullets.art`, and a 2x1-cell footprint.
- Container: prototype 3052, `G_8F454608_E327_1341_B85B_E7A5402D4758`, in
  `maps/arcanum1-024-fixed/101602821844.sec`. It has the source-standard 10x96 grid (960 cells) and 26 authored
  children; no weight-capacity field or unlimited sentinel exists.
- Character: Human Female NPC prototype 17101, `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, has effective Strength
  9 and capacity 4500. The production Human Male PC has effective Strength 8 and capacity 4000.
- Grid-fill input: authored bow prototype 6055, `G_1575DBCA_4990_C243_8184_524D51F7D533`, has inventory ART
  `art/item/i_bow02.art` and a 2x6-cell footprint. The lifecycle proof used ordinary dynamic copies of this real
  prototype to reach deterministic `NoRoom` without changing source data.

## Implementation and validation

### Runtime contract

- `PersistentObjectState` retains effective stored weight, immutable inventory footprint, and the current source-style
  inventory location. Source load takes the instance weight and location before prototype fallbacks; dynamic creation
  and split stacks copy the same authoritative inputs.
- `WorldObjectSectorLoader` resolves only source inputs: the inventory ART path and first-frame dimensions. It binds a
  footprint resolver to the session and never owns load, capacity, or acceptance decisions.
- The session-owned `InventoryCapacityService` provides typed unit/total weight, inventory load, character capacity,
  container capacity/load, deterministic first-fit location, and final-state acceptance. Load is recomputed from
  persistent placement/equipment/quantity state; there is no independently mutable current-weight counter.
- `WorldMapSessionCoordinator` invokes acceptance after the existing identity/source/destination checks and before any
  placement, quantity, tombstone, identity-allocation, callback, or presentation change. `TooHeavy` and `NoRoom` are
  explicit transfer/interaction/stack outcomes. Rejected dynamic creation does not consume a `D_` identity.
- Automatic stack insertion evaluates the surviving destination's combined quantity and uses no second footprint.
  Explicit merge evaluates both committed remainders; split reserves a second footprint before allocating an identity.
  Equip/unequip and occupied-slot replacement assign source-style locations while keeping equipped weight in the same
  direct owner total.

### Boundary and Play Mode proof

The production PC reported Strength 8 and capacity 4000. For a compact exact-boundary proof, the validator changed the
same typed PC character state to supported Halfling Female inputs, giving effective Strength 4 and capacity 2000, then
created 39 ordinary Food-prototype fillers for load 1950. A literal Computer Use click selected the authentic Food
ObjectID, the existing navigation/interaction path approached it, and pickup at `1950 + 50 == 2000` succeeded. The
world projection disappeared only after the authoritative commit.

The next ordinary dynamic Food item was attempted at load 2000 and returned `TooHeavy`. Placement, identity, load,
pending command state, and its single world presentation were unchanged; the interaction ended `Cancelled` with no
stale command. The validator then restored Human Male Strength 8/capacity 4000.

The same run proved PC-to-real-container and container-to-PC transfers, real Ammo quantity 60 weight 15, split/merge
math with no tombstone load, and weighted equipment equip/unequip without load drift. Fifty-nine real-prototype bow
copies filled the real container until deterministic first-fit fragmentation left 920 occupied cells; the next 2x6
item returned `NoRoom` before mutation and kept its one world presentation. Original/Enhanced/reverted rebuild,
unload/reload, B-to-A-to-B traversal, and a foreign-sector dropped item all retained exact queries and one projection.
There was one coordinator, object owner/root, navigation controller, interaction controller, production PC runtime, PC
sprite owner, and presentation per persistent identity. The harness recorded 0 new warnings and 0 errors.

### Automated validation

Unity 6000.0.71f1 compiled with 0 errors. Final results, all with 0 failures, skips, or inconclusive tests:

- M3E focused 21/21; M4A 13/13; M3D 18/18; M3C 13/13; M3B 13/13; M3A 8/8.
- M2B 8/8; M2A 16/16; PlayerNavigation 21/21; M1A 7/7; M1B 11/11.
- WorldSessionState 27/27; PortalArtResolver 2/2; complete EditMode 349/349.

### Deferred boundary and recommended next milestone

M3E intentionally does not implement magic/technology owner weight adjustment, technophobia/background/key-ring
insertion rules, equipment stat effects, encumbrance consequences, UI, economy, item use, scripts, combat, progression,
or save serialization. Container room is source grid geometry, not an invented weight/count capacity.

The exact recommended next milestone is **M4B — authoritative derived character resources: maximum/current hit points
and fatigue**. Audit their formulas, clamps, initialization, PC/NPC prototype-instance inputs, and damage/heal/fatigue
transaction boundaries first. Keep combat resolution, regeneration scheduling, equipment/spell/background effects,
death/unconsciousness behavior, UI, progression, encumbrance consequences, and serialization out of that bounded slice.

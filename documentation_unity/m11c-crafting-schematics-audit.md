# M11C Crafting / Schematics Audit

Date: 2026-09-28

Branch: `feature/session-save-load`

Starting HEAD: `bfdec357f4933d182864989da448a814a6078dc7`

## Outcome

M11C is complete as the bounded authoritative crafting/schematic runtime. The implementation reads the retail
schematic catalog, derives discipline recipes from M10B ranks, persists the source PC-only found-schematic list,
learns written schematics, deterministically resolves components, proves output capacity before mutation, and commits
consumption plus production as one rollback-protected transaction. It does not add the deferred crafting UI or begin
M12.

## Source authority

The primary code references were the Arcanum CE translations of retail behavior:

- `src/ui/schematic_ui.c`: seven-row recipe records, three prototype aliases per component/product, ingredient
  lookup, expertise checks, product-variant selection, consumption, quantity, and production;
- `src/game/schematic.h`: the fixed two-component/three-alias recipe structure;
- `src/game/tech.c` and `tech.h`: effective technology levels and found-schematic learning;
- `src/game/written.h`: written subtype 5 is a schematic;
- `src/game/item.c`: direct inventory lookup, ammo transfer, and item transfer behavior;
- retail `rules/schematic.mes`, `mes/schematic_text.mes`, written prototypes, and item prototypes.

The shipped catalog contains 118 recipe records in the audited 2000-5440 range. Each recipe begins at an ID divisible
by ten and occupies seven consecutive keys: name text ID, description text ID, art number, component 1 aliases,
component 2 aliases, product aliases, and product quantity. One- or two-value alias rows repeat their last value to
fill the source three-slot array.

## Definition and unlock model

`SchematicId`, `SchematicDefinition`, and `SchematicCatalog` are typed immutable source projections. Recipes below
4000 are discipline recipes. Their identity is the existing M10B formula
`1990 + 200 * discipline + 10 * degree`, with degrees Novice through Doctorate. Availability is always derived from
the character's current M10B effective degree; it is not redundantly persisted. The exact effective expertise table
is 0, 10, 20, 35, 50, 65, 80, and 100.

Recipes 4000 and above are found schematics. The source assigns their discipline for presentation from the primary
product prototype, but manufacture eligibility is determined by the actual component expertise checks. The runtime
therefore does not invent a second rank field on a found recipe.

## Found schematic learning

Only the production PC may learn a found schematic. The written item must be directly contained by that PC, have
source written subtype 5, and store a catalog recipe ID in `OBJ_F_WRITTEN_TEXT_START_LINE`. First learning appends the
ID and destroys the written object through M3. Duplicate learning reports `AlreadyKnown` and leaves the second item
untouched. There is no source forget operation.

`PersistentObjectState` now retains effective written subtype/start-line values independently of Unity presentation.
They use instance-over-prototype resolution, survive ordinary V1 object persistence, and rebuild onto `WorldObject`.

## Craft request and eligibility

`CraftingRequest` contains only the crafter ObjectID and schematic ID. The source does not ask the user to select a
particular instance, so the authority resolves components. Validation requires an existing character with vitality,
an alive crafter, a bound/known schematic, two eligible components, sufficient effective technology expertise for
each component, a resolvable ordinary product prototype, and enough final carry/grid capacity for every output.

Retail code explicitly blocks dead users when entering the schematic flow but contains no manufacture-time
unconscious, combat, map-location, AP, time, random failure, aptitude, or crafting-XP rule. M11C consequently adds
none. Crafting is instantaneous and deterministic. M4/M10B aptitude continues to affect technological item use, not
manufacture eligibility.

## Component resolution

The source search is the crafter's direct loose inventory only. Equipped items, nested containers, nearby world
objects, party inventory, and other owners are excluded. Each of the three prototype aliases is tried in source
order. Eligible instances are deterministic by inventory location and then stable ObjectID. Exact prototype identity
is accepted; the source-compatible shared-description match is also retained for equivalent prototype aliases.

There are exactly two component slots and each consumes one item unit. A singular object cannot satisfy both slots.
An ammo stack can satisfy both when it contains at least two units; one unit is consumed for each slot through the M3
ammo operation. Other components are destroyed through the existing singular-item operation. The source has no
condition requirement and does not split requirements across arbitrary multiple stacks beyond the two fixed slots.

Each selected component reads its own `OBJ_F_ITEM_DISCIPLINE` and the negated
`OBJ_F_ITEM_MAGIC_TECH_COMPLEXITY`. The latter is compared with M10B's effective expertise level. This is not a
recipe-wide prerequisite and technological aptitude is not recalculated.

## Output and atomicity

Product selection preserves the source rule: a nonzero component-1 alias index chooses that product index;
otherwise component 2's alias index chooses it. The recipe quantity creates that many ordinary production items at
the crafter's authoritative contained destination. Products use the existing dynamic ObjectID allocator, normal M3
item state, and therefore normal M11A value; there is no crafted-item subtype or special price.

Before commit, `InventoryCapacityService.EvaluateCraftOutputs` simulates exact consumed ammo/singular weight, freed
grid cells, every output footprint, and total carry weight. The commit then snapshots objects, tombstones, stack
quantities, placements, and the dynamic allocator; consumes exact inputs; creates every output; and restores the
snapshot on any unexpected failure. Failure yields no partial ingredient loss, phantom product, tombstone drift, or
dynamic-ID gap.

## Persistence

Save format remains V1. The optional `crafting` domain stores only ordered found-schematic IDs for the production PC.
Discipline recipes remain derived from M10B. Ordinary components/products and written objects remain ordinary world
objects. Earlier V1 documents without the domain load with empty found knowledge. Invalid identity, duplicate,
out-of-range, non-found, or source-unknown recipe entries fail during restore-plan construction before active state
changes. Requests, selected components, previews, results, UI state, and partial transactions never persist.

## Authentic vertical slice

| Path | Recipe | Components | Product | Acquisition / prerequisite |
|---|---:|---|---|---|
| Discipline | 2000, Healing Salve | 10061 + 10062 | 10059 x5 | Herbology Novice, dynamically exposed at effective level 10 |
| Found | 4020, Clockwork Physician | 10084 + 15116 | 15169 x1 | Written prototype 14095; component 10084 requires Herbology expertise 3 |
| Variant | 4450, Electro-Armor | 15108 + one of 8075/8083/8088 | 8229/8230/8231 x1 | Found recipe; output follows component alias index |
| Ammo stack | 5420, Muscle Maker | first component + ammo 7040 | retail product | Generic source-shaped stack path proven automatically; base-only ammo prototype is not currently exposed by production `ProtoLibrary` |

The known retail ambiguity is preserved: recipe 2000 produces prototype 10059 although the separately supported
usable Healing Salve is prototype 10079. M11C does not normalize or substitute it.

## Validation

- Unity 6000.0.71f1 compilation: clean.
- Focused `M11CCraftingSchematics`: **18/18**, failed/skipped/inconclusive **0/0/0**.
- Directly affected regressions:
  - M3A inventory: **8/8**;
  - M3D stacks: **18/18**;
  - M3E capacity: **21/21**;
  - M6A save/load: **25/25**;
  - M10B technology: **12/12**;
  - combined: **84/84**, failed/skipped/inconclusive **0/0/0**.
- Complete EditMode: **1001/1001**, failed/skipped/inconclusive **0/0/0**.
- Full-suite warnings: ten intentional fail-closed dialogue compatibility warnings from existing tests; cleared after
  inspection. Final Console: **0 logs, 0 warnings, 0 errors**.
- `git diff --check`: clean.

Computer Use physical Play Mode validation used TestTerrain, the production PC, retail sources, production
`WorldMapSessionCoordinator`, and the authentic Tarant sector. It proved recipe 2000 appearing immediately after
M10B Novice Herbology; written 14095 learning recipe 4020; non-consuming duplicate learning; exact 10084 + 15116
consumption; exact 15169 x1 creation; zero-mutation missing-component rejection; no time/AP/combat mutation; found
knowledge and committed output across Save V1; Original -> Enhanced -> Original presentation independence; and
**0 warnings, 0 errors**. The authentic ammo-stack fixture was recorded dependency-not-ready because prototype 7040
is present only in base data not exposed by the current loose-prototype production source; its generic transaction is
covered by the focused suite rather than fabricated physically.

## Closure assessment

1. **Required for M11C closure:** complete.
2. **Retail schematic content using completed runtime:** all valid catalog records are data consumed by the generic
   runtime; individual recipe demonstrations are content expansion, not new authority.
3. **Defer to M12 UI:** schematic browser, readiness indicators, component art, feedback, and player commands.
4. **Campaign-specific scripting:** granting/placing written recipes and authored quest/dialogue consequences. No
   required generic M5 operation was found; `SAT_START_SCHEMATIC_UI` is presentation routing.
5. **Source ambiguous / undecoded:** the 2000/10059 versus usable-10079 discrepancy and availability of base-only
   prototype fixtures are preserved, not approximated.
6. **Already implemented:** ranks/effective expertise (M10B), aptitude (M4/M10B), item/stack/capacity/dynamic identity
   (M3), persistence transaction (M6), and ordinary item value (M11A).

No M11C Phase 2 is required. M12 was not started.

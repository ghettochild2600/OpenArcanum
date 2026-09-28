# M11A Economy / Vendor Runtime Audit

Date: 2026-09-27
Branch: `feature/session-save-load`
Starting HEAD: `7355218892dc8920c454b6713350c071e2c99e11`

## Result

M11A is complete as the bounded authoritative economy/vendor runtime. The implementation identifies retail merchants
from ordinary NPC source fields, resolves their own or nearby substitute inventory, parses the shipped inventory-source
and buy-list tables, prices transactions with the source integer arithmetic, coordinates atomic item and Gold-stack
movement through M3, schedules source-time restocking, and persists committed economy state in the optional Save V1
domain. It does not add a merchant UI or begin M11B social systems or M11C crafting.

The production-data audit scanned 12,723 retail `.mob` objects and found 429 NPCs with a resolvable inventory-source
field. `rules/InvenSource.mes` contains 123 definitions. The audit also caught and corrected an early parser assumption:
the retail file is a single flat sequence per row (`minGold,maxGold chance,basicProto ...`), not comma-delimited records.

## Source archaeology

The reference implementation was traced through `game/item.c`, inventory-source refresh, barter entry, and the retail
`InvenSource.mes` / `InvenSourceBuy.mes` data.

### Merchant and inventory model

- A merchant remains an ordinary stable-ObjectID NPC. UI state does not make an NPC a merchant.
- Retail price multiplier, inventory-source ID, substitute-inventory ObjectID, NPC flags, and SAP_BUY_OBJECT script
  number are instance-over-prototype source fields.
- A valid nearby substitute container (within 20 tiles, same sector, not a follower) owns wares when present; otherwise
  the NPC's own inventory is used. Missing/distant/invalid substitutes fail closed unless the NPC itself has a valid
  source inventory.
- Dead merchants and party members are unavailable.
- Gold remains the existing M3 `ObjectType.Gold` stack. There is no parallel integer wallet.
- Substitute-store gold is transferred into the merchant purse when a transaction opens the economic path, matching
  the source barter normalization without giving presentation authority.

### Item worth and price formulas

`item_worth` uses 300 for an unidentified item. An identified nonzero raw worth is floored to 2. Raw worth 0 remains
unsellable rather than being promoted by the floor.

Haggle effectiveness is:

```text
effectiveness = 4 * effectiveHaggleRank + 10
+10 when effective Intelligence >= 20
Easy difficulty: effectiveness = effectiveness * 3 / 2
Hard difficulty: effectiveness = effectiveness * 3 / 4
clamp to [0, 95]
```

For PC -> merchant selling, integer operations occur in this order:

```text
adjustedWorth = worth
if retailMultiplier > 100:
    adjustedWorth = adjustedWorth * (3 * (100 - retailMultiplier) / 8 + 100) / 100
price = adjustedWorth * (effectiveness / 2 + 50) / 100
price = price * currentItemHP / maximumItemHP   // when maximum HP exists
negative price -> 0
price 1 -> 2
```

For merchant -> PC buying:

```text
price = worth + worth * retailMultiplier * (100 - effectiveness) / 10000
price = price * reactionPercent / 100
if price <= worth: price = worth + 1
price 1 or 2 -> 3
```

Reaction applies only to the PC buying direction. The exact multiplier is 200% below reaction 0,
`2 * (100 - reaction)` for 0-49, and `120 - 2 * reaction / 5` for 50-100. Every division is truncating integer
division at its source position. The typed `EconomyPriceBreakdown` exposes raw/effective worth, identification,
retail multiplier, effective Haggle rank/effectiveness/training, reaction and multiplier, condition, and final unit
price. Stack totals multiply the final unit price with checked arithmetic.

Haggle training has source-specific rule effects rather than an invented percentage: Apprentice affects retail UI
presentation only, Expert bypasses SAP_BUY_OBJECT and source buy-list vetoes, and Master also bypasses WONT_SELL and
equipped-merchant restrictions. The numerical price uses the M4 effective skill rank. Beauty/Charisma have no direct
price term; currently represented reaction enters through M4. Broader reputation, faction, and social relationship
inputs belong to M11B and were not approximated. Magick/Technology aptitude is not a source price input.

## Transactions and restrictions

`EconomyStateService` owns immutable requests, typed results, preview, price calculation, merchant resolution, and
commit. Preview and execution validate the complete transaction before authority changes. Execution snapshots M3
inventory/gold state and restores it on any transfer or payment failure.

- Buying validates merchant/customer/item/quantity, merchant ownership, WONT_SELL/equipped restrictions, PC gold,
  and M3 weight/grid capacity before mutation.
- Selling validates PC ownership, nonzero worth, unequipped state, stolen/fence rules, source buy-list membership, and
  SAP_BUY_OBJECT policy before mutation.
- A source buy script without a bound evaluator fails closed. Expert Haggle bypasses the source veto as in the source.
- Stolen items require the merchant fence NPC flag.
- Merchant affordability is finite. The source does not reject a sale when the purse is short; it clamps the payout
  to the merchant's current Gold balance, including a zero payout, while the item still transfers.
- Partial and complete Ammo/ordinary stack transactions delegate split, merge, identity, grid, and capacity behavior
  to M3. Gold uses the same M3 stack authority. Unit-price multiplication occurs once per requested quantity.
- Failed transactions leave gold, item parent, stack quantity, vitality, time, and presentation unchanged.

The exact destroyed-ART rejection path is recorded but not guessed because OpenArcanum does not yet expose a decoded
source-equivalent destroyed-art predicate. The implemented zero-worth, WONT_SELL, equipped, stolen/fence, buy-list,
buy-script, ownership, quantity, affordability, and capacity rules fail closed where their source inputs exist.

## Stock and restock lifecycle

`InventorySourceCatalog` parses authentic gold bounds, repeated chance/basic-prototype pairs, `ALL`, and accepted
basic-prototype lists. Basic prototype N resolves to loose prototype N+20.

Each resolved merchant gets one stable runtime record with merchant ObjectID, authoritative inventory owner,
inventory-source ID, absolute next-restock deadline, and generated identities. Restock:

- uses `SourceTimeService`, never Unity wall-clock time;
- schedules an inclusive random 12-24 source-game hours;
- removes nonpersistent inventory contents and preserves `OIF_PERSISTENT` contents;
- creates source-bounded random gold and item stock through M3 item creation;
- does not run for dead merchants or party members;
- fires once at a due deadline and schedules the next future deadline;
- prunes generated identities when stock/gold leaves a substitute inventory, so saves never contain stale ownership.

Player-sold nonpersistent wares therefore disappear at the next authentic refresh; persistent stock survives. Fixed
world inventory and all committed mutations remain ordinary world/M3 state between refreshes.

## Dialogue, world, and persistence boundaries

Retail dialogue uses the existing `b:` barter command. M11A supplies the authoritative service it will invoke, but no
shop presenter or final dialogue-to-window UI was added. Crime consequences, ownership reputation, faction policy,
reaction drift after deals, and campaign-specific merchant behavior remain M11B/campaign work. Crafting and schematic
transactions remain M11C. Final merchant presentation belongs to M12 UI.

Save format remains `OpenArcanum.SessionSave` V1. Existing world objects already persist item parent, quantity, and Gold.
M11A adds source fields needed to reconstruct merchant rules plus an optional economy domain containing only merchant
inventory owner/source, next deadline, and still-owned generated identities. Older V1 documents without the domain
remain valid. Load validates every ObjectID/reference before replacing the session and restores a fresh economy service
with the retail catalog rebound. There is no pending/preview transaction to serialize.

## Authentic vertical slice

The accepted production Play Mode proof used the retail inventor NPC
`G_C626B82F_5190_2C40_995A_00BCB987F7A5` in
`maps/arcanum1-024-fixed/68853695432.sec`. The source instance has retail multiplier 200 and inventory source 7.

The proof established:

1. authentic NPC identification and inventory-source resolution;
2. deterministic authentic stock and a 12-hour deadline;
3. one purchase with exact authoritative quote, Gold movement, and M3 stack handling;
4. one source-buy-list-compatible sale with exact/clamped merchant payout;
5. an insufficient-funds rejection with zero item/Gold mutation;
6. committed item, Gold, and restock schedule persistence through Save V1;
7. Original -> Enhanced -> Original rebuild independence;
8. exactly one restock at the due source-time boundary and no replay on the next tick;
9. validation cleanup restoring the pre-proof session; and
10. zero warnings and zero errors during the accepted run.

## Automated validation

- M11A focused EditMode: **20/20**, 0 failed, 0 skipped, 0 inconclusive.
  - authentic MES grammar and buy lists;
  - merchant/substitute recognition and invalid-merchant rejection;
  - exact buy/sell/reaction/Haggle/condition/unidentified/floor arithmetic;
  - successful and rejected transactions, capacity and quantity failure rollback;
  - finite/clamped merchant payout, Expert/Master and stolen/fence restrictions;
  - partial buy and partial sell quantity/Gold behavior;
  - restock replacement/persistence/scheduling and substitute generated-ID cleanup;
  - Save V1 committed state and schedule restore.
- Directly affected regressions: **126/126**.
  - M3A inventory 8/8, M3D stacks 18/18, M3E capacity 21/21 because M11A coordinates those authorities.
  - M4C progression 29/29 and M4D derived stats 25/25 because pricing consumes Haggle, Intelligence, and reaction.
  - M6A save/load 25/25 because M11A adds optional V1 persistence.
- Complete EditMode suite: **952/952**, 0 failed, 0 skipped, 0 inconclusive.
- Unity 6000.0.71f1 compilation: clean.
- The complete suite emitted nine pre-existing intentional dialogue compatibility warnings and zero errors. The final
  cleared Console was **0 logs, 0 warnings, 0 errors**.
- `git diff --check`: clean.

## Closure classification

1. **Required for M11A closure:** complete. No further core economy phase is required.
2. **Merchant content using the completed runtime:** additional retail merchant fixtures and buy-script evaluators can
   be added without another economy authority.
3. **Defer to M11B social systems:** reputation/faction/crime consequences, reaction changes caused by deals, broader
   theft ownership, social refusals, and campaign relationship policy.
4. **Defer to M11C crafting:** schematic acquisition, ingredients, recipes, product variants, and manufacture.
5. **Defer to M12 UI:** the final barter/shop window, Apprentice markup display, input flow, and item presentation.
6. **Campaign-specific content:** individual merchant scripts, services, schedules, dialogue gating, and authored
   exceptions.
7. **Source ambiguous/dependency not ready:** the exact destroyed-ART predicate and any economic effect that depends on
   social state OpenArcanum does not yet represent.

M11A should not remain open merely because every merchant has not been manually exercised. The generic runtime can
represent the decoded retail merchant model, so remaining definitions and campaign behavior are content/integration,
not a second M11A phase.

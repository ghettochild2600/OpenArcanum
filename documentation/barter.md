# Barter (trading with NPCs)

How the original engine runs a trade: where the goods come from, the exact price formulas, and what a
merchant will or won't trade. Sources are `arcanum-ce` files/lines so every claim can be checked.

## Contents

- [One window, seven modes](#one-window-seven-modes)
- [Entering barter from dialog](#entering-barter-from-dialog)
- [The merchant's store: substitute inventory](#the-merchants-store-substitute-inventory)
- [Price formulas](#price-formulas)
- [What can be traded](#what-can-be-traded)
- [Gold](#gold)
- [Barter chatter lines](#barter-chatter-lines)

## One window, seven modes

Barter is not a separate screen — it is mode 1 of the shared inventory UI (`ui/inven_ui.h:7`):

| Mode | Purpose |
|---|---|
| `INVENTORY` (0) | the PC's own inventory + paperdoll |
| `BARTER` (1) | trade with an NPC — NPC store on the left, PC on the right |
| `LOOT` (2) / `STEAL` (3) | containers & corpses / pickpocketing |
| `IDENTIFY` (4), `NPC_IDENTIFY` (5), `NPC_REPAIR` (6) | service variants |

Everything goes through one entry point, `inven_ui_open(pc, target, mode)` (`ui/inven_ui.c:475`,
retail address 0x572240). There is **no offer table**: every item dragged across the divide is bought or
sold the instant it is dropped (`sub_575200` buy / `sub_575360` sell, `ui/inven_ui.c:2319/2345`).

## Entering barter from dialog

A `.dlg` player option whose text is the token `b:` becomes a "let's trade" line pulled from the
generated-dialog tables (`mes/gd_pc2m.mes` / `gd_pc2f.mes`): keys **300–399**, or **1600–1699** when the
NPC is a party follower (`game/dialog.c:2409`, `sub_416C10`). Picking it clears the message window but
keeps the conversation alive, then opens the trade screen (`ui/dialog_ui.c:307`,
`dialog_ui_process_option` case 3). When the player closes the trade window, the conversation resumes at
the `b:` option's goto line (the engine parked it in `DialogState.field_17EC`; `game/dialog.c:530`).

Bartering with a **party follower** is the same window with pricing disabled — items just move
(`ui/inven_ui.c:1007`).

## The merchant's store: substitute inventory

Merchants do not sell what they carry. An NPC's `OBJ_F_NPC_SUBSTITUTE_INVENTORY` field holds a handle to
a **store container** — usually a chest hidden in a back room — and barter displays *that* container's
goods (`ui/inven_ui.c:988`). `critter_substitute_inventory_get` (`game/critter.c`) applies three rules:
NPCs only, never for party followers, and the chest must be **within 20 tiles** of the NPC. If any rule
fails, the NPC trades from its own pack.

When the window opens (`sub_572640`, `ui/inven_ui.c:662`):

- every gold pile inside the store is transferred **onto the NPC** — the store shows goods, the NPC holds
  the purse;
- unless the NPC is a follower, `item_identify_all` runs on both the NPC and the store, so wares are
  always shown identified.

## Price formulas

All arithmetic is C integer math (division truncates toward zero). Central function: `item_cost`
(`game/item.c:866`, 0x461F80).

**Base worth** (`item_worth`, `game/item.c:843`): an unidentified item is a flat **300 coins**; otherwise
`OBJ_F_ITEM_WORTH`, floored at **2**. Worth 0 means the item cannot be sold at all.

**Haggle effectiveness** (`game/skill.c`, `basic_skill_effectiveness`):

```
effectiveness = 4 × haggle_rank + 10          (rank 0–20 → 10–90)
              + 10 if Intelligence ≥ 20        ("extraordinary" stat bonus)
then, for the PC only: Easy +50% · Hard −25%   (game difficulty)
```

**Selling to a merchant** (what the NPC pays):

```
if (npc_markup > 100)
    worth = worth × (3×(100 − npc_markup)/8 + 100) / 100   // greedy merchants pay less
price = worth × (haggle/2 + 50) / 100                       // 50%…100% of worth
price = price × hp_current / hp_max                         // damaged goods pay proportionally
minimum 2 (a result of exactly 1 is bumped)
```

**Buying from a merchant** (what the PC pays):

```
price = worth + worth × npc_markup × (100 − haggle) / 10000
price = price × reaction_multiplier                          // see below
if (price ≤ worth) price = worth + 1                         // never at or below worth
minimum 3
```

`npc_markup` is the per-NPC `OBJ_F_NPC_RETAIL_PRICE_MULTIPLIER` field (a percentage — the merchant's
authored greed).

**Reaction multiplier** (`game/reaction.c:387`, 0x4C1150) — applies to buying only:

| NPC reaction | Price factor |
|---|---|
| below 0 (hostile) | ×200% |
| 0–49 | ×`2·(100 − r)`% (200% → ~102%) |
| 50–100 | ×`(120 − 2r/5)`% (100% → 80%) |

Selling is not reaction-priced, but after every deal `sub_4C11D0` (`game/reaction.c:412`) adjusts the
NPC's reaction by how lopsided the exchange was — overpaying a merchant makes it like you more.

With Apprentice haggle or better, the buy tooltip appends the markup percentage:
`100 × (price − worth) / worth` (`ui/inven_ui.c:3850`).

## What can be traded

**A merchant refuses to BUY** (`item_check_sell`, `game/item.c:936`):

- worthless items (`OBJ_F_ITEM_WORTH` = 0) and items showing their destroyed art;
- items vetoed by the NPC's `SAP_BUY_OBJECT` script or missing from its inventory-source *buy list* —
  both bypassed once the PC has **Expert** haggle training;
- **stolen goods** (`OIF_STOLEN`), unless the NPC is a fence (`ONF_FENCE`).

**A merchant refuses to SELL** (`item_check_buy`, `game/item.c:966`): items flagged `OIF_WONT_SELL` and
anything it currently wears — both overridden by **Master** haggle training.

Money constraints: a purchase the PC can't afford is refused outright; a merchant that runs out of coins
still takes your item but pays only **what it has left** (the payout is clamped to the NPC's gold,
`ui/inven_ui.c:2345`).

## Gold

Gold is its own object type (`OBJ_TYPE_GOLD`), basic prototype **9056** (`BP_GOLD`,
`game/descriptions.h:524`), with the amount in a single `OBJ_F_GOLD_QUANTITY` counter — one pile object,
not thousands of coins. `item_gold_transfer` (`game/item.c:2463`) merges into an existing pile or spawns
a new one, destroying stacks that reach zero.

## Barter chatter lines

The merchant's replies — shown in the status line the moment the player *picks an item up*
(`ui/inven_ui.c:3794`, `sub_578330`) — are resolved per situation by `dialog_copy_npc_generic_msg`
(`game/dialog.c:3203`):

1. **Per-NPC override**: if the NPC has a `SAP_DIALOG_OVERRIDE` dialog file, its line at key
   `range/100 + 11999` replaces the whole range (so a specific shopkeeper can have custom lines —
   sell 800 → line 12007, buy 1100 → line 12010).
2. **Otherwise** a random line from the generated tables `mes/gd_npc_m2m.mes` / `gd_npc_m2f.mes` /
   `gd_npc_f2m.mes` / `gd_npc_f2f.mes` — chosen by the NPC's gender × the PC's gender — within the range.

The picked line is a **printf template**: the UI formats the price into its `%d` (and for "buy for less",
a second `%d` receives the NPC's remaining gold).

| Range | Meaning |
|---|---|
| 800–899 | agrees to sell — "That will cost you %d" |
| 900–999 | won't sell this |
| 1000–1099 | "normally wouldn't sell" (Master-haggle override preface) |
| 1100–1199 | agrees to buy — "I'll give you %d" |
| 1200–1299 | won't buy this |
| 1300–1399 | "normally wouldn't buy" (Expert-haggle override preface) |
| 1400–1499 | would buy it, but can't pay full price — "%d … %d" |
| 4600–4699 | won't buy stolen goods |

The drag preview also runs the merchant's **`SAP_BUY_OBJECT` script** — the per-NPC veto hook
(`item_check_sell`, `game/item.c:947`): the script is attached to the NPC, triggered by the item, with
the PC as the extra object; returning "skip default" means "I won't buy that".

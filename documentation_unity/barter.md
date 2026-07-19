# Barter

How this reimplementation runs a trade with an NPC, and how faithfully it tracks the original engine.
For the original game's rules and formulas, see [`documentation/barter.md`](../documentation/barter.md).

## Flow

1. **A dialog option starts it.** A `.dlg` option written as the `b:` token renders as a "let's trade"
   line pulled from the generated-dialog tables (`gd_pc2m/f.mes`, keys 300–399 — or 1600–1699 for a party
   follower), exactly like the original. Picking it hides the dialog, opens the trade window, and — once
   the window closes — resumes the conversation at the option's target line
   (`InterfaceHud.PickBarter`).
2. **The trade window is the inventory window.** `InventoryWindow.OpenBarter(npc, onClosed)` reuses the
   dual-panel layout: the merchant's goods on the left (where loot shows a chest), the PC's grid on the
   right, plus barter-only readouts — the merchant's name and purse, the PC's gold, and a status line for
   the merchant's replies.
3. **Every drop is a transaction.** There is no offer table, matching the original: dragging an item
   across the divide buys or sells it on the spot (`InventoryWindow.TryTrade`). Money moves first; if it
   can't, the item snaps back. Trading with a party follower moves items freely with no gold involved.

## What the merchant sells

Merchants trade from their **substitute inventory** — a store container referenced by the NPC's
`OBJ_F_NPC_SUBSTITUTE_INVENTORY` field — when that container is loaded and within 20 tiles
(`ArcanumSectorDemo.SubstituteInventoryOf`); otherwise from their own pack, minus anything they wear.
When the window opens, gold piles on the merchant's side collapse into its purse (`Critter.Gold` — this
implementation keeps money as a counter on the character rather than as pile objects).

## Pricing

`BarterPricing` (in `Arcanum.Runtime`) is a line-for-line port of the engine's `item_cost`:

- **Worth**: `OBJ_F_ITEM_WORTH`, floored at 2; worth 0 means "I won't buy that".
- **Selling**: the merchant pays `worth × (haggle/2 + 50) / 100` — from 50% of worth with no skill up to
  100% at maximum haggle. High-markup merchants pay on a reduced worth. A broke merchant still takes the
  item but pays only what's left in its purse.
- **Buying**: the PC pays `worth + worth × markup × (100 − haggle) / 10000`, scaled by the reaction
  multiplier (80% at reaction 100 up to 200% when hostile), never at or below worth.
- **Haggle effectiveness**: `4 × rank + 10`, plus 10 with Intelligence 20+.

The math is locked by `BarterPricingTests` (EditMode).

## Merchant chatter and the sale veto

Picking an item up in barter shows the merchant's quote before you drop it, as the original does: "I'll
give you N for that", "That will cost you N", or a refusal (`InventoryWindow.OnItemDragStarted`). Lines
come from `NpcBarterLines` — the engine's chatter source: the `gd_npc_*` generated table matching the
NPC's and PC's genders, overridden per-NPC by lines 12007+ of its `SAP_DIALOG_OVERRIDE` dialog. Each
line is a printf template whose `%d` receives the price.

Two engine trade rules are enforced: the merchant's **`SAP_BUY_OBJECT` script** (a per-NPC veto over
what it buys — run through the script VM with the item as triggerer and the PC as the extra object), and
the **`OIF_WONT_SELL`** item flag (the merchant keeps the item; it shows but can't be bought).

## Known gaps vs the original

- Item identification, item-damage price scaling, and the gambling drop-box are not modelled.
- Merchant buy-lists, stolen-goods/fence rules, and the Expert/Master haggle-training overrides are not
  enforced (skill ranks exist; training tiers don't, so the veto and won't-sell rules always apply).
- No post-deal reaction drift and no merchant stock/gold regeneration.
- No "(N%)" markup hint on quotes (an Apprentice-haggle perk in the original).

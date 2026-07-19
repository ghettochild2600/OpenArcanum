# Magick — Arcanum's spell system & eye-candy data

*Arcanum: Of Steamworks and Magick Obscura* organises its magick into **16 colleges of 5 spell
levels each** — 80 player-castable spells in all — and **casting costs fatigue, not a separate mana
bar**. The visually interesting, reverse-engineered part is the **eye-candy chain**: a three-file
lookup (`mes/spell.mes` → `rules/SpellEyeCandy.mes` → `art/eye_candy/eye_candy.mes`) that turns a
spell number into the actual `.art` sprite files the engine plays while a spell is cast, flies, and
lands.

This describes the **original engine**, reconstructed from the `arcanum-ce` decompilation
(`src/game/magictech.c`, `animfx.c`, `spell.c`, `name.c`) and the shipped data files.

## The 16 colleges

Spells are grouped into 16 colleges, declared in order (`spell.h:6`):

| # | College | # | College |
|---|---|---|---|
| 0 | Conveyance | 8 | Meta |
| 1 | Divination | 9 | Morph |
| 2 | Air | 10 | Nature |
| 3 | Earth | 11 | Necromantic (Black) |
| 4 | Fire | 12 | Necromantic (White) |
| 5 | Water | 13 | Phantasm |
| 6 | Force | 14 | Summoning |
| 7 | Mental | 15 | Temporal |

Each college holds exactly **5 spells**, ordered by level (1–5). The mapping is purely positional
(`spell.h:142`):

```
COLLEGE_FROM_SPELL(spell) = spell / 5
LEVEL_FROM_SPELL(spell)   = spell % 5
```

So spell 0 is Conveyance level 1, spell 4 is Conveyance level 5, spell 5 is Divination level 1, and
so on up to spell 79 (Temporal level 5). The full ordered list of the 80 spell identifiers is in
`spell.h:28` — e.g. Conveyance runs Disarm / Unlocking Cantrip / Unseen Force / Spatial Distortion /
Teleportation; Necromantic White ends at Resurrect; Temporal ends at Tempus Fugit.

> The engine's internal magictech table (`MT_SPELL_COUNT = 223`, `magictech.h:15`) is larger than the
> 80 player spells — it also covers non-player and special effects. The 80 colleges×levels spells are
> the ones a character learns and casts.

A 17th index (`SPELL_MASTERY_IDX`, `spell.h:26`) sits past the 16 colleges and tracks which college,
if any, the character has reached **mastery** in.

## Casting cost is fatigue

**Arcanum has no mana bar for a character's own spells.** Casting drains **fatigue**: the spell's cost
is added to the caster's *fatigue-damage* pool, exactly like taking a hit to stamina
(`magictech.c:1650`):

```
fatigue_dam = critter_fatigue_damage_get(obj);
critter_fatigue_damage_set(obj, fatigue_dam + cost);
```

A cast is permitted right up to the point of **overexertion**. The gate (`magictech.c:1645`) is:

```
if (critter_fatigue_current(obj) - cost <= -15)  → cast fails
```

That is: you may cast as long as the cost would leave current fatigue **above −15**. Fatigue can be
driven *negative* (into the red) by overexerting — a character can keep casting past zero down toward
the −15 floor, at which point further casts are refused. (For maintained spells, an additional check
refuses to start one whenever it would leave the caster's fatigue already below zero,
`magictech.c:1657`.)

Wands, scrolls and other charged items are the exception: they cast from the item's own mana store
rather than the wielder's fatigue.

The **real cost** a caster pays is modified by who they are (`spell.c spell_cast_cost`, `0x4B1660`):

```
cost = base Cost from spelllist.mes
     × 2   if the caster is a dwarf
     ÷ 2   if the caster has mastered the spell's college
```

## The rules table: `rules/spelllist.mes`

Every spell's mechanics live in `rules/spelllist.mes`. Each magictech `n` owns a **50-key block**
starting at `1000 + 50·n` (tech abilities use base 3000; `magictech.c sub_450090`). The lines:

| Key | Line | Contents |
|---|---|---|
| +0 | targeting | `AoE:` flag list, plus per-action lists (`[Begin]AoE:`, `[End]AoE:`, `[Callback]AoE:`) and `Radius:`/`Count:`. The plain `AoE:` list is OR'd into **every** action; the `[Begin]` list ORs into the BEGIN action — their union is what the targeting cursor uses. |
| +1 | header | `IQ:`, `Cost:`, `Maintain: (cost @ period)`, `Duration: (period @ stat)`, `DurationStatInfo: (level @ modifier)`, `DurationTriggerCount:`, `Resist: (stat_name @ value)`, `Range:` (default 99), `Info: neutral/friendly/aggressive/may_be_aggressive`, `Disabled:` |
| +2 | animation | `CastingAnim:`, `Missile:`, per-action caster/target fx pairs (`[Begin]Caster:` …), `Is_Tech:` |
| +3 | stacking | `No_Stack:`, `Cancels_SF:`, `Disallowed_SF/TSF/TCF:`, `ItemTriggers:` |
| +4 | AI | `AI_Offensive:`, `AI_Defensive:`, …, `No_Resist:`, `No_Reflect:` |
| +5…+48 | components | up to 44 effect components (see below) |

Numeric pairs use the parenthesised `(a @ b)` form; `Resist:` pairs a stat **key name** from
`stat.c stat_lookup_keys_tbl` ("stat_strength" … "stat_charisma") with a save modifier that is almost
always negative. If the header omits `IQ:`, the willpower requirement defaults by spell level to
`{6, 9, 12, 15, 18}` (`magictech.c sub_457580`, `dword_5B0DE0`); the minimum *character* level per
spell level is `{1, 1, 5, 10, 15}` (`spell.c spell_minimum_levels`).

### Components — the effect mini-language

Each component line is `"[Action], <targeting overrides>, Type: <name>, <fields>"`, where the action
is one of `[Begin] [Maintain] [End] [Callback] [EndCallback]` and the `Type:` name resolves through
`rules/magictech.mes` keys 10–34 (NoOp, AGoal, …, Damage=16, Effect=19, EyeCandy=21, Heal=22,
Summon=28, …). The ones that carry most spells, with real examples:

```
{3755}{[Begin], Type: Damage, DmgType: Dmg_Normal, Dmg: 3-40, Dmg_Flags: Scaled}     // Harm
{4005}{[Begin], Type: Heal, Dmg: 5-30, Dmg_Flags: Scaled}                            // Minor Healing
{1755}{[Begin], Type: Effect, 161, Add, Count: 4}                                    // Strength of Earth
{1757}{[End],   Type: Effect, 161, Remove, Count: 4}
{2108}{[Begin], AoE: Tgt_Tile_Empty, Type: Summon, Proto: 4050}                      // Fireflash
```

- `Dmg: min-max` is a range; `DmgType:` is one of `Dmg_Normal, Dmg_Poison, Dmg_Electrical, Dmg_Fire,
  Dmg_Magic, Dmg_Acid`.
- `Type: Effect, <n>, Add, Count: c` applies entry `n` of `rules/effect.mes` **c times** (Strength of
  Earth is four stacked +1 ST effects, not one +4); the `[End]` action removes them again.
- `Type: EyeCandy, <t>, Add` attaches eye-candy slot `t` of the spell (see below) to each target;
  `Remove` detaches it. This is how a buff's glow starts and stops.

### ⚠ `Scaled` amounts are not dice rolls

`Dmg_Flags: Scaled` (present on most damage/heal spells) means the amount is **deterministic**,
walked along the range by the caster's magick aptitude (`MTComponentDamage_ProcFunc`,
`magictech.c:2270`):

```
amount = min + aptitude · (max − min) / 100     // aptitude ≤ 0 ⇒ just min
```

A technologist casting Harm from a scroll does 3 damage; a 100%-aptitude mage does 40 — no roll at
all. Only *unscaled* components roll `random_between(min, max)`.

## Maintained spells

**53 of the 80 spells never expire on their own** — they are *maintained*: the caster keeps paying
`Maintain: (cost @ period)` fatigue upkeep (cost fatigue every `period` seconds; ×2 for dwarves) until
the spell is dismissed, the caster can't pay (the −15 overexert floor again), or the caster dies.
Strength of Earth is `Maintain: (1 @ 10)` — 1 fatigue every 10 s, forever.

Concurrent maintains are capped at **Intelligence / 4**, max **5** (the maintain-bar slots —
`magictech.c sub_450B90`, `ui/spell_ui.c spell_ui_maintain_add`). Casting past the cap immediately
terminates the new spell with "Maintain terminated." College **mastery doubles durations** instead of
touching upkeep (`magictech.c sub_455250`).

## Durations and the ×8 time base

Timed spells schedule `TIMEEVENT_TYPE_MAGICTECH` events on **game time**, which advances at **8×
real time** (`timeevent.c:829`: `datetime_add_milliseconds(&game_time, 8 * delta)`). The magictech
scheduler multiplies its delays by 8 (`magictech.c sub_453FA0`) — the two cancel, so **spelllist.mes
periods are effectively real-time seconds**.

A duration spell's tick length is:

```
seconds = period + max(0, DurationStatInfo.level − casterStat) · DurationStatInfo.modifier
```

e.g. the "body" spells use `Duration: (0 @ 2), DurationStatInfo: (21 @ 180)` — `(21 − CN) · 180` s, so
a *lower* Constitution keeps the form longer. `DurationTriggerCount` schedules that many extra
`[Maintain]` ticks before the final `[End]` tick.

## Magick/tech items and aptitude

An item's leaning is its **complexity** — `OBJ_F_ITEM_MAGIC_TECH_COMPLEXITY`, positive for magickal,
negative for technological, 0 for neutral. A character's leaning is the derived
**magick/tech aptitude** stat (same sign convention). How well a character drives an item is the
average of the two, clamped to the item's own band (`item_effective_power`, `item.c:0x4614A0`):

```
power = (complexity + aptitude) / 2
magick item (complexity > 0):  clamp power to [0, complexity]   — a tech-y owner gets 0
tech item   (complexity < 0):  clamp power to [complexity, 0]   — a magickal owner gets 0
```

Two derived quantities the engine uses everywhere:

- **Effectiveness %** — `100 · power / complexity` (100 for neutral items;
  `item_effective_power_ratio`, `item.c:0x461540`). A wand or scroll in the hands of a half-committed
  caster casts at reduced effect; scripts read it through `SAT_GET_MAGICTECH_ADJUSTMENT_EX`
  (result = `(value · effectiveness + 50) / 100`).
- **Counter-aptitude resistance** — how much a *target's opposing* aptitude shrugs off an item's
  effect (`sub_461620`, `item.c:0x461620`): a magick item against a tech-y target (aptitude < 0)
  resists by `−targetAptitude`, softened to `−targetAptitude · (100 − sourceAptitude) / 100` when
  the wielder is magickal; mirrored for tech items against magickal targets; 0 when the leanings
  don't oppose. Scripts read it through `SAT_GET_MAGICTECH_ADJUSTMENT`
  (result = `value · (100 − resistance) / 100`).

## Resistance & saving throws

The per-target roll at cast time is `magictech.c sub_4537B0` (the similar
`magictech_cast_spell_fail_chance`, `0x453B40`, is the AI/item-use *estimator*). It runs only for
non-tech spells, only when caster ≠ target, and never for `No_Resist:` spells:

1. Base = the target's **magick resistance** %. A tech-aligned target (aptitude < 0) raises it:
   `res = 100 − (100 − res) · (apt + 100) / 100`.
2. Roll d100. `chance < res` ⇒ a **partial resist** of `res − chance` percent.
3. If the spell deals **no damage** and is **not maintained**: with a saving-throw stat the partial
   resist grants `+1 save bonus per 10%`; with **no** save stat, step 2's success means the spell
   simply **fails outright** (and any resistance above 40% is total immunity in the estimator).
4. **Saving throw** (spells with `Resist: (stat @ value)`, critter targets only):
   `difficulty = value + targetStat − saveBonus`; roll d20 ≤ difficulty ⇒ saved. A saved *damaging*
   spell is resisted **at least 50%**; a saved non-damaging, non-maintained spell fails completely.
   Extraordinary Willpower is blanket immunity to WP-resisted spells.
5. The surviving "partial resist" percentage is shaved off every damage component (minimum 1 point).

## The eye-candy (visual-effect) data chain

Every spell can show up to **six** distinct visual effects — one per phase of the cast. The phase is
the **eye-candy type** (`magictech.h:27`):

| Value | Type | When it plays |
|---|---|---|
| 0 | `CASTING` | on the caster as the spell is cast |
| 1 | `PROJECTILE` | the travelling missile, caster → target |
| 2 | `DESTINATION` | at the target where the spell lands |
| 3 | `SECONDARY_DESTINATION` | a second effect at the destination |
| 4 | `SECONDARY_CASTING` | a second effect on the caster |
| 5 | `DAMAGE` | when the spell deals damage |

Resolving a spell phase to a sprite is a **three-file lookup**:

| File | Key | Yields |
|---|---|---|
| `mes/spell.mes` | spell number (used directly) | the spell's display name |
| `rules/SpellEyeCandy.mes` | `spellNumber * 10 + fxType` | an entry string `"Art: N, Palette: …, Scale: …, Blend: …, Sound: …"` |
| `art/eye_candy/eye_candy.mes` | art num `N` | a base name → the sprite path `art/eye_candy/<name>_<F\|B\|U>.art` |

### 1. Spell number → name (`mes/spell.mes`)

The engine reads the name straight off the message file by the spell number — no arithmetic
(`magictech.c:1281`):

```
char* magictech_spell_name(int num) {
    mes_file_entry.num = num;            // key = the spell number itself
    mes_get_msg(magictech_spell_mes_file, &mes_file_entry);
    return mes_file_entry.str;
}
```

### 2. Spell + phase → eye-candy entry (`rules/SpellEyeCandy.mes`)

`rules/SpellEyeCandy.mes` is loaded as an *animfx list* with these parameters (`magictech.c:785`):

```
path       = "Rules\SpellEyeCandy.mes"
num_fields = 6     // six fx types per spell
step       = 10    // each spell's block of keys is 10 apart on disk
```

Because each spell owns a block of keys spaced **10 apart on disk** but only the first **6** are used
(one per fx type), the on-disk key for a given phase is:

```
mesKey = spellNumber * 10 + fxType
```

So spell 5's CASTING entry is key 50, its PROJECTILE entry is key 51, its DESTINATION entry key 52,
and so on; keys 56–59 in that block are unused padding before spell 6's block begins at key 60.
(Internally, after loading, the engine packs these into a dense array indexed `spell * 6 + fxType`,
since only 6 of every 10 slots carry data — see `magictech.c:6193`.)

Each entry is a comma-separated string parsed field-by-field (`animfx.c:1088`):

| Field | Meaning |
|---|---|
| `Art:` | the **eye-candy art number** `N` (required; without it there is no sprite) |
| `Palette:` | palette index applied to the sprite — **0–3**, despite the file's own comment saying 1–4 |
| `Scale:` | scale keyword (one of 50/62/75/87/100/125/150/200%; marks the effect auto-scalable) |
| `Blend:` | blend mode (`none, add, sub, mult, alphC, alphA, alphS`); anything but `none` renders translucently |
| `Sound:` | sound file number (if absent, derived from the CASTING entry's sound + a per-type offset `{0, 43, 24, 44, 32, 62}`) |
| `Flags:` | overlay flags — layer + animation behaviour (table below) |
| `Light:` + `Light Color: (r @ g @ b)` | an attached overlay light from `art/light/light.mes` |
| `Proj Speed:` | projectile speed — path steps advanced per anim tick (PROJECTILE entries; default 4) |

The `Flags:` tokens (`animfx.c:56`, table `off_5B76A8`):

| Token | Meaning |
|---|---|
| `overlay_f` | foreground overlay — drawn in front of the target (`<name>_F.art`) |
| `overlay_b` | background overlay — drawn behind the target (`<name>_B.art`) |
| `overlay_fb` | BOTH layers — the art ships two files, `_F` and `_B`, drawn around the target |
| `underlay` | flat under the object (`<name>_U.art`) |
| `animates` | play the clip once |
| `anim_forever` | **loop the clip until explicitly removed** — this is the visual of a maintained buff: the `[End]` action's `EyeCandy … Remove` component stops it when the spell drops |
| `tinting` / `can_autoscale` | colour-tint / allow scaling |

The `Art: N` value is handed to `tig_art_eye_candy_id_create` (`animfx.c:1145`) to build an
**eye-candy art id**, with the overlay/underlay type chosen from the parsed flags. An entry with no
`Art:` field produces no sprite (it may still carry just a sound).

### 3. Art number → sprite path (`art/eye_candy/eye_candy.mes`)

Finally, resolving an eye-candy art id to a file looks the art **number** up in
`art/eye_candy/eye_candy.mes` to get a base name, then appends an overlay-type suffix
(`name.c:1031`):

```
art\eye_candy\<name>_<F|B|U>.art
```

The suffix is the **overlay type** baked into the art id (`name.c:172`):

| Code | Overlay type |
|---|---|
| `F` | Foreground overlay (drawn above the scene) |
| `B` | Background overlay |
| `U` | Underlay (drawn beneath) |

So a CASTING entry of `Art: 12` with a foreground overlay flag becomes, after the name lookup,
something like `art/eye_candy/spell_cast_glow_F.art`.

## Not every spell has every phase

The six fx slots are a maximum, not a requirement — many entries are simply blank. The important
consequence is **instant and touch spells have no PROJECTILE entry**: nothing flies through the air,
so only the CASTING effect on the caster and the DESTINATION effect on the victim are populated. Harm
(internally "Cause Light Wounds") is the canonical example — it fills CASTING and DESTINATION but
leaves PROJECTILE empty. The engine reflects this directly: it only attempts a missile when a
PROJECTILE entry actually resolves for the spell (`magictech.c:6193`).

## Source references

- Colleges, level math, mastery index — `spell.h:6`, `:26`, `:142`
- Spell identifiers (80 player spells) — `spell.h:28`
- Fatigue cost / overexertion gate — `magictech.c:1644`–`:1660` (`sub_450420`)
- Cast cost modifiers (dwarf ×2, mastery ÷2) — `spell.c:359` (`spell_cast_cost`)
- spelllist.mes block layout & line parsers — `magictech.c:1375` (`sub_450090`), `:5649` (`sub_4578F0`,
  header), `:5717` (`sub_457B20`, anim/missile), `:5569` (`magictech_build_aoe_info`), `:5877`
  (`magictech_build_effect_info`)
- Per-spell defaults (IQ `{6,9,12,15,18}`, range 99, trigger count 0) — `magictech.c sub_457580`
- Scaled damage/heal formula — `magictech.c:2270` (`MTComponentDamage_ProcFunc`),
  `MTComponentHeal_ProcFunc`
- Resistance / saving-throw roll — `magictech.c sub_4537B0`; the estimator variant
  `magictech_cast_spell_fail_chance` `:3447`
- Maintenance scheduling & slots — `magictech.c:3643` (`sub_453FA0`), `sub_450B90` (INT/4),
  `ui/spell_ui.c spell_ui_maintain_add` (5 slots)
- Duration scaling (mastery ×2) — `magictech.c:4306` (`sub_455250`); game time = 8× real —
  `timeevent.c:829`
- Spell-name lookup (key = number) — `magictech.c:1281`
- Eye-candy list params (`step=10`, `num_fields=6`) — `magictech.c:785`
- Fx-type enum (CASTING…DAMAGE) — `magictech.h:27`; component fx id = `6·spell + type` —
  `MTComponentEyeCandy_ProcFunc` `magictech.c:2505`
- Entry string parsing (`Art:`/`Palette:`/…/`Flags:`/`Proj Speed:`) — `animfx.c:1061`–`:1160`; flag
  token table `animfx.c:56`
- Art number → `<name>_<F|B|U>.art` path & overlay codes — `name.c:1031`, `:172`
- Missile only when a PROJECTILE entry resolves — `magictech.c:6193`

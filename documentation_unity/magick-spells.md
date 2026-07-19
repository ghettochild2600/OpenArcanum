# The spell system (`Arcanum.Magick` + runtime casting)

Spells are fully data-driven from the original game's files and split across two layers:

- **`Arcanum.Magick`** (`Assets/_Game/Scripts/Magick/`) — an engine-free assembly (references only
  `Arcanum.Formats`) holding the spell **data model**, the **parsers** for the original rules files,
  and the **pure rules math**. Everything in it is unit-testable without Unity.
- **`Arcanum.Runtime`** — the casting pipeline on live critters (`Casting`), the maintained-spell
  tracker (`SpellMaintenance`), and the visuals (`SpellVfx`/`SpellVfxPlayer`).

## Arcanum.Magick

| Type | Role |
|---|---|
| `Spell` | one parsed spell: college/level, fatigue cost, range, targeting, damage/heal ranges (+ the `Scaled` flags), effect number + count, summon proto, maintenance (`cost @ period`), duration (`period @ stat` + stat-info), saving throw (`stat @ value`), and six `SpellFx` slots |
| `SpellFx` | one eye-candy slot: `.art` path (suffix from the overlay flags), palette, layer (F/B/FB/U), `Loops` (`anim_forever`), translucency, sound number, projectile speed, light colour |
| `SpellBook` | the spells a critter knows |
| `SpellRulesResolver` | `mes/spell.mes` + `rules/spelllist.mes` + `rules/magictech.mes` → the 80-spell catalog (each spell is a 50-key block at `1000 + 50·n`) |
| `SpellEyeCandyResolver` | `rules/SpellEyeCandy.mes` + `art/eye_candy/eye_candy.mes` → the six `SpellFx` slots per spell (`spell·10 + type`) |
| `SpellRules` | the ported engine math: cast/upkeep costs (dwarf ×2, mastery ÷2), the −15 fatigue overexert gate, aptitude-scaled amounts, the resistance/saving-throw roll, duration seconds, maintain slots (INT/4, max 5) |

See `documentation/magick.md` for what the original engine does; `SpellRules` cites the exact
functions it ports.

## Runtime casting

`Casting.Cast(caster, spell, target)` returns a `CastOutcome`:

1. **Pay** — known-spell check, a free maintain slot for maintained spells, then fatigue (a cast may
   overexert the pool down to −14, like the engine).
2. **Resist** — the per-target roll (`SpellRules.ResistRoll`); a full resist fizzles the cast (fatigue
   stays spent), a partial resist shaves a percentage off the damage.
3. **Apply** — heal and typed damage (through `CombatStats.ApplyDamage`, so armour/resistances apply;
   `Scaled` amounts come from the caster's `MagickAptitude`, unscaled ones roll), plus the EFFECT
   payload: the spell's `effect.mes` entry built into a live `Effect` and applied `Count` times.
4. **Maintain** — a maintained spell registers with `SpellMaintenance` and persists until the caster
   can't pay its upkeep, dies, or dismisses it (spellbook → *Maintaining* → End); ending removes the
   applied effects and fires cleanup hooks (looping fx stop, summons despawn, ground hazards lift).

Area and ground-targeted casts pay once, then each victim in the radius rolls its **own** resistance
(`Casting.ApplyToSecondary`).

`SpellMaintenance` is ticked by `WorldSimulation`, so upkeep freezes with the rest of game time
during turn-based combat.

## Visuals

Each spell's six eye-candy slots come straight from the data. The runtime plays:

- **Casting** on the caster (follows them),
- **Projectile** flying caster → target for missile spells, at the data's `Proj Speed`,
- **Destination** at the target — as a one-shot flash, or, when the data marks it `anim_forever`
  (maintained buffs like *Strength of Earth*), as a looping overlay attached to the target until the
  spell ends.

`SpellVfx` objects are pooled through the DI container; a slot with no art (or art missing from the
install) falls back to an animated procedural effect, so casts always read on screen.

## Testing

- `CastingTests` — costs/overexert, scaled amounts, resistance (full + partial), maintained upkeep
  and slot caps (dice injected via `Casting.Rng`).
- `SpellRulesResolverTests` / `SpellEyeCandyResolverTests` — the parsers, against entries shaped like
  the real shipped data.
- The **SpellGallery** test scene (`documentation_unity/test-scenes.md`) lays out all 80 spells and
  loops their casting/projectile/impact art for visual inspection.

# Sound & music

Arcanum's audio is **entirely data-driven**: the engine code contains no sound filenames, only integer
sound ids and the *key math* that picks an id for a game moment. Ids resolve to WAV files through a chain
of `.mes` tables — even the list of tables is itself a `.mes` file. Three subsystems cooperate:

1. **Selection** (`sfx.c`) — an event plus world state (materials, armor, tile surface) → an integer id.
2. **Playback** (`gsound.c` + TIG `sound.c`) — id → file, with an isometric positional volume/pan model.
3. **Schemes** (`gsound.c`) — the music/ambience playlist language, per map and sector, plus combat music.

Sources: `arcanum-ce` `src/game/gsound.c`, `src/game/sfx.c`, `src/game/snd.h`, `src/game/sfx.h`;
all values below verified against the shipped data files.

## Sound tables — id → file

| Data file | Role |
|---|---|
| `sound/snd_00index.mes` | manifest naming the SFX tables to load (`gsound.c:406`) |
| `sound/snd_critter.mes`, `snd_interface.mes`, `snd_item.mes`, `snd_melee.mes`, `snd_misc.mes`, `snd_spell.mes` | id → WAV filename |
| module `sound/snd_user.mes` | module-supplied table, loaded **last** |

Lookup searches the tables **in manifest order, first hit wins** (`gsound.c:361-388`) — a module can add
new ids in `snd_user.mes` but cannot shadow a stock id. SFX are WAVs inside the `.dat` archives; music is
**loose MP3s** under `modules/Arcanum/sound/music/`, streamed rather than preloaded.

⚠ Several shipped WAVs have malformed headers — chunk sizes that are negative or run past the end of the
file. The retail sound library tolerated them; any reader must clamp chunk sizes to the remaining bytes.

## Selection key math (`sfx.c`)

The id spaces are part of the format — the engine hardcodes these same constants:

| Event | Id formula | Source |
|---|---|---|
| Footstep, plate armor | base **2900** `+ rand(0,3)` | `sfx.c:298-359` |
| Footstep, by tile surface | DIRT 2904 · SAND 2912 · SNOW 2916 · STONE 2920 · WATER 2928 · WOOD 2932, `+ rand(0,3)`; chainmail adds **+4** on wood/stone/dirt | `sfx.c:331-351` |
| Melee hit fallback | `7000 + weaponMaterial·20 + targetMaterial·3 + rand(0,1)` | `sfx.c:277` |
| Item pickup / drop | `5950 / 5960 + materialClass` (8 classes); gold uses 5958 / 5968 | `sfx.c:84-99` |
| Interface | fixed ids **3000–3028** (`SND_INTERFACE_*`) → `snd_interface.mes` | `snd.h:4-32` |

Details that bite:

- The **tile surface** comes from the tile-name table's sound suffix (`a_name.c:467-503`), the **armor
  class** from bits 20–23 of the walker's critter art id (chain = 3, plate = 4/6).
- The melee **target material** is the victim's **worn armor's** material when it wears one, else its body
  material (`sfx.c:252-256`); a metal weapon lighter than 2000 weight units demotes from heavy to light.
- **Object-authored sounds**: every object can carry a personal sound bank base in `OBJ_F_SOUND_EFFECT`
  (common object field 33); the actual id is `base + offset`, with offsets per type (`sfx.h`): critters
  {critically-hit 0, dying 1, gruesome 2, fidget 3, attack 4, alert 5, agitated 6}, weapons {use 0, busted 1,
  destroyed 2, miss 3, hit1 4, hit2 5, crit 6, out-of-ammo 7}, portals/containers {open 0, close 1, locked 2}.
  A base of 0 means unauthored — fall back to the material math above. `OBJ_F_MATERIAL` (field 30) holds the
  material (materials.h order: Stone, Brick, Wood, Plant, Flesh, Metal, Glass, Cloth, Liquid, Paper, Gas,
  Force, Fire, Powder).

## Positional model (`gsound.c:862-945`)

- Distance is isometric screen distance with the **y delta doubled** before the Euclidean mix.
- Four positional **size classes** (SMALL / MEDIUM / LARGE / EXTRA_LARGE): scenery picks its class from
  `OSCF_SOUND_*` flags; critters and items default to LARGE (`gsound_size`).
- Per class, `sound/soundparams.mes` supplies `{min radius, max radius, max volume}` plus two global pan
  distances (`gsound.c:452-561`). Volume is **max** inside the min radius, **0** beyond the max radius,
  linear between. Stereo balance centers (64) while `|dx|` is under the min pan distance and saturates
  (0 or 127) at the max.
- `radius / 40` converts to tiles (`gsound.c:1148`); full volume is 127 (`GSOUND_VOLUME_MAX`).

## Music & ambience schemes

Two concurrent scheme **slots** — slot 0 music, slot 1 ambient. A map names its pair in `mapinfo.txt`
(`SoundScheme: music,ambient`, `map.c:1626`); sectors can override per sector (sector sound list).

`sound/schemeindex.mes` maps a scheme name to `#N`, the start line of up to 100 consecutive entries in
`sound/schemelist.mes` (`gsound.c:1382-1397`). Each entry is `file [options]` in a small option language:

| Option | Meaning |
|---|---|
| `/loop` | looping track (music) |
| `/vol:50` | volume percent |
| `/time:5-19` | only between those game hours (a looping track is gated in/out; hours wrap midnight) |
| `/freq:30` | ambient one-shot: every 250 ms tick, play when `rand(0,999) < freq` |
| `/scatter:100` | randomize pan/volume per one-shot |
| `/over` | a transition stinger: plays once **over** the current scheme, which then resumes |

**Combat music** (`gsound.c:1718-1786`): entering combat saves the active scheme, plays a random
`music/combat <1..6>.mp3` stinger, then loops `combatmusic.mp3`; leaving combat restores the saved scheme.

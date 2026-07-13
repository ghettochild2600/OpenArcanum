# Audio system

How this project implements the original game's data-driven audio (see
[`documentation/sound-and-music.md`](../documentation/sound-and-music.md) for the engine model being
ported). Three layers, with one rule throughout: **gameplay code never references audio** — it publishes
semantic events, and the audio side is a pure subscriber. No filename, sound id, or magic number lives
outside the format ports and the `.mes` data.

## Layer 1 — format readers (`Arcanum.Formats.Sound`, pure C#)

| Class | Responsibility |
|---|---|
| `SoundTable` | loads the `snd_00index.mes` manifest and every table it names (+ module `snd_user.mes` last); `Resolve(id)` returns the WAV path with the engine's first-hit-wins search order |
| `SoundParams` | `soundparams.mes` → per-`SoundSize` {min radius, max radius, max volume} + pan distances, with the engine defaults for absent keys; also the volume/balance formulas |
| `SoundSchemeTable` | `schemeindex.mes` / `schemelist.mes` → schemes of entries with the full `/loop /vol /time /freq /scatter /over` option language parsed into fields |
| `SfxSelect` | the `sfx.c` selection key math, pure static: `Footstep(armor, tileSound)`, `MeleeHit(...)`, `ItemPickup/Drop(material, weight, isGold)`, `ObjectSound(base, offset)` + the offset enums (`CritterSound`, `WeaponSound`, `PortalSound`) and the `Material` enum |
| `WavPcm` | WAV bytes → PCM samples (Unity-free). Guards against the malformed chunk sizes present in several shipped WAVs — clamp, don't trust |

Covered by `SoundFormatsTests` (EditMode).

## Layer 2 — the event hub (`GameEvents`)

One plain-C# hub, registered in the DI container, split into domain sections (`Combat`, `World`, `Items`,
`Ui`, …) — a publisher only ever touches its own section, and each section owns null-safe `Raise*`
helpers. Payloads are world objects; the subscriber decides what, if anything, they sound like. Wired
publishers: combat attack resolution and deaths, footsteps (walk-animation cadence, carrying the tile's
surface sound type), portal/container toggles, item pickup/drop/equip, map scheme changes, and the
`UiSound` enum for interface moments (button, window, book page-turn, level-up, quest-complete).

> The full game also publishes a `Magick` section (spell-cast audio). It is omitted here until the spell
> system migrates to this repository.

## Layer 3 — runtime services (`Arcanum.Runtime.Audio`)

| Class | Responsibility |
|---|---|
| `SoundBank` | boot-time preload: walks every entry of every sound table, reads the WAV from the VFS, decodes via `WavPcm` and bakes `AudioClip`s — runtime playback is a dictionary hit, zero I/O on the hot path. Music MP3s stay streamed, like the engine. Missing/bad files are logged once at boot |
| `AudioService` | the only Unity-audio owner: pooled `AudioSource`s; `PlayAt(id, worldPos)` applies the engine positional model (doubled-y isometric distance, `SoundParams` radii/volume/pan) relative to the listener (the player); flat `PlayUi(id)` for interface sounds; channel volumes persisted in settings |
| `MusicService` | the two scheme slots (music + ambient) with the 250 ms scheme tick: `/time` gating by game hour, `/freq` d1000 rolls for ambient one-shots, and the combat state machine (save scheme → random stinger → `combatmusic.mp3` loop → restore). MP3s stream via `UnityWebRequestMultimedia` |
| `AudioDirector` | the ONLY subscriber: wires `GameEvents` to `SfxSelect` and the services. Holds no tuning — the interface-id block (3000–3028) and the selection constants are the engine's own id space, cited to `snd.h`/`sfx.c` |

## Wiring

At boot (behind the loading screen) the game constructs `SoundBank.Load(vfs)`, an `AudioService`, a
`MusicService` and finally `new AudioDirector(events, sfx, music)` — after that, playing a sound anywhere
in the game is `events.Ui.Raise(UiSound.ButtonClick)` or `events.World.RaisePortalToggled(door, open)`.
Object-authored sounds work because the object readers parse `OBJ_F_MATERIAL` (field 30) and
`OBJ_F_SOUND_EFFECT` (field 33) into the runtime world objects.

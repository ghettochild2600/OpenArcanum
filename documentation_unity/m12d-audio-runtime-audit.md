# M12D Audio Runtime Audit

Date: 2026-09-29

Branch: `feature/session-save-load`

Unity: 6000.0.71f1

Status: **DONE M12D — Audio**

## Scope and Closure

M12D adds a source-backed production audio presentation layer over the completed M1-M12C authorities. It resolves
retail resources, presents interface/world/combat/magic/technology/dialogue audio, follows authored music and ambient
schemes, and reconstructs presentation after save/load or graphics rebuild. It does not own gameplay state, make
commands succeed, change combat outcomes, advance time, or extend Save V1.

The implementation is bounded to behavior supported by the retail data and the audited source. Missing or unsupported
audio fails silently without substituting gameplay behavior. Original Arcanum data is read through the existing VFS
and is never modified or committed.

## Source Archaeology

The reference audit used `D:/OpenArcanum/Research/Repositories/arcanum-ce`, especially the sound, SFX, animation,
dialogue, and combat paths. The recovered rules are:

- `sound/snd_00index.mes` names the retail `snd_*.mes` tables. Module `snd_user.mes` participates in the existing
  source search order. Numeric sound identifiers remain the authority rather than Unity-authored replacements.
- Effects and scheme ambience are PCM WAV. Music and dialogue voice are MP3.
- `sound/schemeindex.mes` and `sound/schemelist.mes` define two concurrent scheme slots: music and ambience. Their
  250 ms update recognizes `/loop`, `/anchor`, `/over`, `/freq`, `/time`, `/vol`, `/bal`, and `/scatter` semantics.
- Engine volume and balance use the source 0..127 scale. Positional effects use the PC listener, doubled isometric
  screen Y, linear distance attenuation, and horizontal stereo pan.
- Combat music saves the current schemes, plays a random combat stinger, loops `combatmusic.mp3`, and restores the
  prior scheme context. The world-map track remains `arcanum.mp3` when selected by source context.
- Dialogue voice uses `sound/speech/{dialogue:00000}/v{voice}_{m|f}.mp3`. A missing female resource falls back to the
  male path; a new line or dialogue termination interrupts/replaces the dedicated voice channel.
- Sector data supplies the music and ambient scheme identifiers consumed by the presentation layer.
- `SfxSelect` remains the source-shaped mapping for critter, weapon, material-impact, portal, container, and item
  events. Ordinary authored weapon impacts select Hit1/Hit2 with presentation-only randomness; combat RNG is not used.
- Spell phases use `9000 + college * 1000 + rank * 10 + phase`. Strength of Earth therefore routes cast/impact/end
  identifiers 12010/12015/12018; Minor Healing routes 21010/21015.

## Resource Resolution and Playback

`SoundBank` indexes the retail tables once, then decodes PCM clips lazily and caches both successful and missing
lookups. This avoids expanding the complete retail library during startup while preserving deterministic source
resolution. Loose module files and DAT-backed files share the existing `DatVirtualFileSystem` path contract.

`MusicService` streams loose MP3s directly. A DAT-only MP3 is copied to a deterministic generated path below
`Application.temporaryCachePath/OpenArcanumAudio` because Unity cannot decode an MP3 directly from a DAT byte stream.
The cache is disposable presentation data; retail `GameData` remains untouched. Scheme changes are idempotent, so an
unchanged area context does not restart music or duplicate loops.

`AudioService` owns a bounded 16-source one-shot pool. `MusicService` owns music/ambience loop sources and the combat
music source. `ProductionAudioPresenter` owns a separate non-positional voice source. The runtime exposes transient
`AudioPlaybackRecord` diagnostics containing the source identifier/path, category, clip, source, position, loop state,
and sequence. Those records support objective validation only and are never gameplay or save authority.

The presentation categories are Effects, Music, Ambience, Voice, and Interface. Voice replacement, bounded one-shot
pooling, independent loop slots, and source random variants provide the required concurrency/interruption behavior.

## Production Ownership and Integration

`ProductionAudioPresenter` is the single production bridge. It observes completed authoritative state and translates
that state to source sound identifiers and paths:

- `GameUiController` reports successful screen/dialogue/magic/technology results only after the corresponding
  authoritative command resolves. Rejected commands route the source invalid-action cue. Scheduled real-time actions
  do not pre-play success audio.
- World interactions present authored object-bank or item material sounds after the interaction succeeds.
- Combat presentation consumes the completed M8 attack sequence. Authored weapon use/miss/hit/critical sounds,
  material impact, target pain, and death are derived without changing AP, ammunition, vitality, turns, or death
  consequences. If a PC unarmed swing has no authored critter attack bank, no swing is fabricated.
- Spell and technology presentation observes the completed M10 result. Healing Salve has no dedicated retail spell
  row; an authored item sound wins when present, otherwise the explicit source Herbology interface cue 3018 is used.
- Dialogue presentation follows the current M5 line and its retail voice number.
- Sector selection supplies the current music/ambient schemes. Combat state temporarily replaces/restores music.

`WorldObjectSectorLoader` now preserves effective generic weight, material, sound-bank identifier, and art facts on
runtime objects and session weapons. These were already present in source objects but had not previously survived the
runtime conversion needed by audio presentation. `PersistentObjectState` gives runtime-created weapons the same facts.
No new gameplay authority is introduced.

## Save, Load, Transitions, and Graphics

Save format remains V1. Playback records, active sources, voice position, attack sequence consumption, and scheme
coroutine state are transient and are not serialized. Restore initializes the presenter from current authority, so it
does not replay a historical attack, spell, technology use, interaction, or voice line. Current sector context may
reconstruct at most the bounded music and ambience loops.

An unchanged same-area transition keeps the active source and advances playback rather than restarting it. An
Original -> Enhanced -> Original rebuild neither changes authority nor duplicates one-shots, voice, or scheme loops.
Audio resolution remains independent of graphics replacement availability.

## Automated Validation

Final validation on the exact committed source candidate produced:

- Unity compilation: clean.
- M12D focused EditMode: **17/17**, 0 failed, 0 skipped, 0 inconclusive.
- Targeted M12C UI API regression: **18/18**, 0 failed, 0 skipped, 0 inconclusive. This was the only older focused
  suite required because M12D adds the optional presentation binding to `GameUiController`.
- Complete EditMode: **1049/1049**, 0 failed, 0 skipped, 0 inconclusive.
- The complete suite emitted ten known intentional fail-closed dialogue compatibility warnings; they were inspected
  and the Console was cleared afterward.
- Final Unity Console: **0 logs, 0 warnings, 0 errors**.
- `git diff --check`: clean.

Focused coverage proves retail identifier resolution, safe missing lookup, lazy PCM caching, exact UI/spell/voice
routing, positional distance/pan, playback records, idempotent schemes, loose and DAT-only MP3 handling, output-probe
signal discrimination, post-authority UI presentation, authority independence, the Healing Salve ambiguity boundary,
and preservation of runtime-created weapon audio facts.

## Physical Production Validation

The Play Mode harness used the production `WorldMapSessionCoordinator`, `WorldObjectSectorLoader`,
`ProductionGameUiPresenter`, `ProductionAudioPresenter`, real VFS, authentic source objects, and production gameplay
services. It created an authentic Human male with Earth 1 and Herbology Novice, then proved:

- UI Inventory open routes exact cue 3012 to one concrete `AudioSource`.
- Strength of Earth routes 12010/12015 and maintained-effect cancellation routes 12018.
- Healing Salve commits through M10B, then presents documented fallback cue 3018.
- An authentic Virgil voiced line streams its exact DAT-backed MP3 and stops on dialogue interruption.
- An authentic portal presents its authored object-bank open/close sound.
- An authentic Bow attack presents exactly one authored release sound after the M8 attack resolves.
- A lethal unarmed attack against the authentic Polar Bear Cub presents material impact and death exactly once while
  leaving the unauthored PC swing silent; M8D/M8E remain the death/consequence authority.
- An authentic Tarant sector selects music scheme 5 and City ambience scheme 33 on distinct loop sources. The retail
  Tarant MP3 resolves, and a same-area transition continues without restart or duplicate loops.
- Save/load restores authority without replaying transient attack/voice audio or duplicating loops.
- Original -> Enhanced -> Original rebuild preserves the PC, audio context, and one-shot counts.

Every representative path checked a resolved clip, concrete source, playback progress/completion, and non-zero Unity
listener output. The final run reported `signalPeak=0.252783`, `signalBlocks=121`, `warnings=0`, and `errors=0`.

Automated validation proves that Unity generated and routed the intended audio signal, but does not independently
verify the final Windows-device-to-speaker acoustic path.

## Deliberate Deferrals and Rejections

- No synthetic sound is invented when an object/critter event lacks an authored bank entry.
- Healing Salve's 3018 fallback is explicitly documented rather than treated as a dedicated spell mapping.
- Broader campaign-specific audio triggers, additional compatible content, retail options UI, menu-specific pause
  policy, ducking, cinematics/intro audio, and optional higher-definition replacement audio remain later work.
- Audio does not change gameplay timing, AI, balance, world coordinates, collision, object placement, animation
  timing, save schema, or graphics selection.

M12D is complete. M13 was not started.

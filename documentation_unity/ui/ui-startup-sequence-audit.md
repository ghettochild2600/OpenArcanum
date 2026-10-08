# Retail Startup Sequence Audit

Status: partially certified from an isolated retail runtime copy on 2026-10-07; audio and click-only skip behavior remain blocked by the host retail runtime/input boundary.

## Safety and reference identity

The immutable reference is `D:\OpenArcanum\Source\Steam-Clean`. It contains the retail 1.0.7.4 executable
(2,007,040 bytes; SHA-256 `58690A30C3F8AADE42D2B598E5AB44526A4983C5A9CD713BD463778232242A59`) and an
installed `ddraw.dll` whose version description is `Arcanum fix`. Every one of the 1,569 reference files was
read-only at the start of the certification.

The retail runtime reads and writes relative paths. `arcanum-ce/src/game/gamelib.c` loads `arcanum.cfg`, updates the
`splash` setting, and creates or clears `Save\Current`; the clean distribution also already contains those paths.
Because the task's strict read-only requirement takes precedence over launching a write-capable process in the
reference directory, the certification used an exact disposable copy at
`D:\OpenArcanum\UIReference\RuntimeComparison\RetailSandbox`. The copied executable, configuration, intro movie,
file count, and byte count matched the clean reference before launch. Only the sandbox `arcanum.cfg` read-only
attribute was cleared so the retail process could persist its ordinary settings.

No retail media, screenshot, audio, executable, DLL, DAT, or sandbox file is tracked by OpenArcanum.

## Observed launch path

The first no-argument sandbox launch reached an 800 x 600 movie surface, extracted `movies\SierraLogo.bik` and
`movies\TroikaLogo.bik` into the sandbox TIG cache, and then terminated in the retail Miles Sound System library.
Windows recorded an access violation in `mss32.dll` 3.0.0.0 at offset `0x00014dd3`. This proves that an otherwise
unmodified launch cannot be certified with audio on this host; it does not establish a defect in OpenArcanum.

The source-supported `-nosound` switch was then used only in RetailSandbox. That run reached the Main Menu and
visually established this order:

1. Sierra logo movie, centered on the black 800 x 600 surface.
2. Troika Games logo movie, centered on the same surface.
3. intro movie ID 8, beginning with the armored figure footage.
4. the retail 800 x 600 Main Menu still life.

The observed sequence took under approximately 40 seconds from window creation to the Main Menu on this host. The
capture cadence was intended to identify ordering, not frame-accurate timing. Fades and movie-internal transitions
were visible. Audio was deliberately disabled after the reproducible retail DLL crash and therefore remains
unverified.

The rotating static startup splash was not isolated visually during the capture cadence. Source evidence proves
that `gamelib_splash` selects one `art\splash\*.bmp`, centers it without stretching, advances the `splash` setting,
and displays it before module startup completes. The sandbox configuration changed from `splash=2` to `splash=3`
after the successful run, which is consistent with that path. A later certification should capture the individual
static frame and its exact dwell interval.

## Source and data trace

| Stage | Source/data evidence | Result |
|---|---|---|
| publisher logo | `gamelib_logo()` calls `gmovie_play_path("movies\\SierraLogo.bik", 0, 0)` | visually observed first |
| developer logo | `gamelib_logo()` then calls `gmovie_play_path("movies\\TroikaLogo.bik", 0, 0)` | visually observed second |
| rotating splash | `gamelib_splash()` loads one centered `art\splash\*.bmp` and increments `arcanum.cfg` `splash` | source-proven; counter transition observed |
| intro | `main.c` calls `gmovie_play(8, GAME_MOVIE_NO_FINAL_FLIP, 0)`; retail `modules\Arcanum\movies\movies.mes` maps ID 8 to `50000.bik` | visually observed third |
| menu | `mainmenu_ui_handle()` opens the retail Main Menu after movie return | visually observed after intro |

`gmovie_play_path` extracts BIK content to a filesystem path, locks the game sound system, maps the supported fade,
keyboard-ignore, and final-flip flags, plays the blocking movie, and then unlocks sound. The two logo calls use no
flags. Intro ID 8 uses `GAME_MOVIE_NO_FINAL_FLIP`; no keyboard-ignore flag is present.

## Skip and repeated-launch behavior

- Escape during a later Sierra-logo run returned to the Main Menu within about five seconds when the sandbox intro
  movie was already disabled. This confirms that the active logo movie accepts Escape.
- Mouse skip and a second non-Escape key were not certified. Retail uses a DirectInput cursor that the Windows
  automation backend could display but not drive; inferring click behavior would be invalid.
- The first successful run advanced `splash=2` to `splash=3`, so later launches can show a different static splash.
- `modules\Arcanum\movies\50000.bik` existed, mapped exactly to movie ID 8, rendered the observed intro, and had
  SHA-256 `C42CE97F4E55A634669923B042AA02630D35DBD03B68E8AA51305228D6BB8162`. After that proof it was renamed only in
  RetailSandbox to `_50000.bik`. The file was not deleted and can be restored by reversing the rename.
- With the renamed intro and Escape on the active logo, the repeated test path reached the Main Menu quickly.

No source-supported retail windowed mode was established. `arcanum-ce` documents `-window`, but its source explicitly
states that the switch was borrowed from ToEE; it is not evidence that the 1.0.7.4 retail executable supports it.
No undocumented flag, wrapper, High Resolution Patch, UAP, injector, or binary patch was installed. Retail testing
therefore used the native 800 x 600 presentation.

## Final safety recheck

After all retail observations, `Steam-Clean` still contained 1,569 files and zero writable files. The reference
executable, `arcanum.cfg`, and intro movie hashes remained exactly
`58690A30C3F8AADE42D2B598E5AB44526A4983C5A9CD713BD463778232242A59`,
`EE67351907C0C56CD44A8A47DD6B4EA0C426E61927AD985DFEA59AE80AB67F96`, and
`C42CE97F4E55A634669923B042AA02630D35DBD03B68E8AA51305228D6BB8162`. No `Arcanum` process remained running.
The sandbox retained `_50000.bik` as the reversible renamed intro; the clean reference still retained the original
`50000.bik` name and bytes.

## OpenArcanum follow-up boundary

OpenArcanum must eventually reproduce this launch sequence from the user's legitimate local retail installation:

- source ordering for the rotating splash, Sierra logo, Troika logo, and intro movie ID 8;
- source transition, final-flip, audio-lock, and skip semantics;
- clean handoff into the existing reconstructed Main Menu;
- an optional user preference to skip startup media only after it has been certified or viewed.

That milestone must resolve media through mounted local retail data and must never redistribute BIK movies, logos,
audio, or other proprietary assets. It must also preserve a no-sound fallback without treating the host-specific
retail `mss32.dll` crash as source behavior. Full startup playback is outside the present UI certification batch.

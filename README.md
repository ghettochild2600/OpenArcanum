# OpenArcanum

OpenArcanum is a Unity 6 reimplementation and remastering project for *Arcanum: Of Steamworks and Magick Obscura*.
It reconstructs the game on a modern runtime while loading maps, artwork, audio, scripts, and other content from a
user's legitimate retail installation.

The project is an unofficial, non-commercial fan effort. No original Arcanum retail assets are distributed here,
and users must supply their own game files. Major runtime and gameplay systems are implemented, but campaign
compatibility, source-faithful interface reconstruction, and visual fidelity remain active work.

## Current status

Production New Game reaches the original campaign start, and the opening area is playable through the implemented
movement, interaction, combat, dialogue, party, and persistence systems. This is still a development project rather
than a finished replacement for the retail executable: broader campaign behavior is being validated from real play,
the gameplay HUD awaits final visual-fidelity review, and the Character Creation interface has not yet received its
source-faithful presentation pass.

The detailed implementation and validation record is maintained in [PROJECT_STATUS.md](PROJECT_STATUS.md).

## Implemented systems

- **World:** retail map and sector loading, adjacent-sector streaming, terrain, walls, doors, world objects, local
  transitions, and bounded world-map travel.
- **Characters:** attributes, skills, vitality and fatigue, derived statistics, experience and leveling, alignment
  and aptitude, Character Creation authority, and production New Game.
- **Movement and interaction:** click-to-move pathfinding, portals, containers, containment, inventory and equipment,
  pickup and drop, and corpse looting.
- **Combat:** turn-based and real-time scheduling, melee, ranged weapons and ammunition, line of fire, cover, called
  locations, critical results, defeat, death, corpses, and experience rewards.
- **NPCs and party:** bounded combat AI, followers, recruitment and dismissal, follower orders, travel, and combat
  integration.
- **Narrative:** dialogue, quests, journal projection, campaign state, and trainer integration.
- **Game systems:** core magic and technology runtimes, vendors and economy, reaction, reputation and faction state,
  and crafting and schematics.
- **Persistence:** versioned session saves, save slots, manual save/load, and dedicated auto-save/auto-load controls.
- **Presentation:** source-backed audio, Original and Enhanced asset resolution, modern display mapping, and an
  in-progress reconstruction of the retail interface.

Implemented means the bounded behavior recorded in the project documentation is present and tested. It does not
mean that every original quest, script, spell, item, or campaign edge case is already supported.

## Current focus

Near-term work is deliberately source-driven:

- physically closing gameplay-HUD visual fidelity at 800×600, 1080p, 1440p, and 4K;
- reconstructing the Character Creation presentation after that review closes;
- resolving campaign incompatibilities found through actual play; and
- filling proven world-presentation gaps without changing gameplay authority.

Known presentation gaps include production roof rendering and fading, source compositor ordering, day/night ambient
presentation, retail shadow sprites, equipment-driven critter appearance, and placed light/additive/nocturnal
presentation. These are not described as complete.

## Original and Enhanced presentation

**Original** mode decodes presentation from the user's retail data. **Enhanced** mode may substitute optional
replacement assets when an exact replacement exists, while retaining the same gameplay state, logical dimensions,
pivots, hit regions, and timing. Missing or invalid replacements fall back independently to Original assets.

The repository does not contain retail assets or a complete Enhanced asset set. Its committed default is Original
mode.

## Requirements

- Unity **6000.0.71f1**.
- A legitimate installation of *Arcanum: Of Steamworks and Magick Obscura*.
- Windows for the currently validated development and play workflow. Other platforms are not yet claimed as tested.

## Getting started

1. Clone this repository.
2. Open the repository root as a Unity project with Unity 6000.0.71f1.
3. At the project root, create a local `GameData/` directory or junction that points to the retail installation's
   `Arcanum` directory. Its root must contain `Arcanum1.dat` through `Arcanum4.dat`, and it must include
   `modules/Arcanum.dat`. `GameData/` is ignored by Git and must remain local.
4. Open `Assets/_Game/Scenes/OpenArcanum.unity`.
5. Enter Play Mode.
6. Choose **Single Player → New Game**, create a character, and begin the game.

Do not commit, upload, or redistribute anything from `GameData/`. Steam and GOG installs use the same project-local
mount shape once their retail `Arcanum` directory is linked as `GameData/`; no game data is downloaded by this
repository.

## Controls

Left-click moves or interacts according to the active cursor. Common screens use their source keyboard shortcuts;
combat supports attack mode, called-location modifiers, turn ending, and turn-based/real-time switching. See the
[player launch and controls guide](documentation_unity/player-launch-guide.md) for the current bindings and known
limitations.

## Documentation

- [`documentation/`](documentation/) records research into the original engine, formats, and data behavior.
- [`documentation_unity/`](documentation_unity/) records OpenArcanum architecture, implementation boundaries,
  source audits, validation, and visual reconstruction work.
- [PROJECT_STATUS.md](PROJECT_STATUS.md) is the current detailed roadmap and validation ledger.

## Development approach

OpenArcanum treats retail data and corroborated source behavior as authority. Gameplay state lives in bounded runtime
services; Unity presentation observes that state and submits commands rather than owning game rules. Changes are
checked with focused and regression tests, Unity compilation, and physical Play Mode validation appropriate to their
risk. Retail data is used locally for research and execution and is never part of the repository.

## Project lineage

OpenArcanum builds on earlier Unity Arcanum work from
[Suvitruf/unity-arcanum](https://github.com/Suvitruf/unity-arcanum). That project is retained as a direct Git
ancestor. OpenArcanum has since substantially expanded the production runtime integration, gameplay systems, tests,
documentation, user interface, and campaign-compatibility work. This lineage statement does not imply endorsement
or an active collaboration with the upstream author.

See [NOTICE.md](NOTICE.md) for attribution and reference acknowledgements.

## AI-assisted development

AI coding tools assist with implementation, source archaeology, documentation, and testing. Their output is
integrated against the project's source evidence, automated regression suite, and physical Unity validation rather
than treated as behavioral authority.

## Legal and asset policy

OpenArcanum is unofficial, non-commercial, and not affiliated with or endorsed by Troika Games, Activision,
Microsoft, or any other rights holder. *Arcanum* names, trademarks, software, and assets remain the property of their
respective owners. The repository includes no original retail game data; each user must provide files from a
legitimate copy. The project license applies only to the material covered by that license and grants no rights to
Arcanum assets.

## License

The project code is provided under the inherited [MIT License](LICENSE), including its existing copyright notice.
The preserved history and [NOTICE.md](NOTICE.md) describe the upstream lineage.

# M12C Full HUD / Game UI Audit

## Status

M12C is complete as the bounded player-operable presentation and controller layer over the authoritative M1-M12B
runtime. The production `TestTerrain` composition now presents a source-shaped main menu, source-backed character
creation, persistent gameplay HUD, and mutually exclusive gameplay screens without transferring gameplay ownership
into Unity widgets. No M12D audio or M13 campaign work was started.

## Original UI archaeology

The behavioral reference was the Arcanum CE translation under
`D:/OpenArcanum/Research/Repositories/arcanum-ce/src/ui`, principally `intgame.c/.h`, `compact_ui.c`,
`hotkey_ui.c`, `inven_ui.c`, `charedit_ui.c`, `spell_ui.c`, `tech_ui.c`, `schematic_ui.c`, `logbook_ui.c`,
`wmap_ui.c`, `follower_ui.c`, `dialog_ui.c`, `mainmenu_ui.c`, and `combat_ui.c`.

The retail interface is a persistent lower HUD plus one rotating/modal mode. `IntgameMode` separates the main HUD,
inventory, character editor, spell, skill, dialogue, barter, world map, logbook, schematic, follower, quantity,
written-item, and item views. Escape returns ordinary modes to the main interface, while dialogue, barter, and
world-map modes impose stronger input restrictions. F10 hides the interface; the original also binds options,
autosave, and load shortcuts. The lower HUD carries health/fatigue, selected action or weapon, recent/quick actions,
maintained spells, and combat state. Inventory behavior is paperdoll plus item storage with drag/drop and right-click
context behavior. Follower portraits combine status bars with a context menu. The source assumes classic fixed panel
proportions and bitmap UI resources.

M12C preserves the behavioral shape: one lower HUD, one modal coordinator, source-style keyboard shortcuts, explicit
target cursors, and authoritative command feedback. It uses a deterministic 1024x768 reference scale clamped to
0.75x-1.5x so screen pixels never become gameplay coordinates. The initial production skin is deliberately functional
IMGUI rather than a pixel-for-pixel reconstruction of the retail bitmap set. Original UI texture replacement and HD
variants remain presentation content, not a gameplay closure dependency.

## Architecture and authority

`GameUiController` owns only screen, selection, cursor, pending-target, creation-draft, and feedback state. Its view
models re-read the session on demand. Commands delegate to the established owners:

- M12B character creation and clean New Game finalization;
- M3 inventory, containment, equipment, stacks, weight, and Gold;
- M4 attributes, vitality, progression, derived statistics, alignment, aptitude, and skills;
- M5 dialogue and journal projections;
- M6C slot operations and M6 session restore;
- M7 destination projection and travel;
- M8I combat selection/preview/command state and M8 combat authority;
- M9B party membership;
- M10A spell casting/effects and M10B technological use;
- M11A merchant pricing/transactions and M11C schematic knowledge/crafting.

`ProductionGameUiPresenter` is the single drawing/input owner. Production composition keeps the earlier presenters as
their established controller/API components but disables their independent drawing/update loops, eliminating duplicate
panels and competing input-gate writes. The full presenter reuses the existing `CombatUiController` instance so world
click target resolution and the full HUD observe the same transient selection. World targeting resolves stable
`ArcanumObjectId` values through `WorldObjectTargetSelector`; colliders never become final authority.

The shared `PlayerInputGate` blocks navigation and ordinary interaction while a modal screen is open. Screens are
mutually exclusive. Escape cancels dialogue or save confirmation first, otherwise returns to the world/main menu.
Turn-based combat rejects ordinary screens while another actor owns the turn. Rebuild closes transient selections,
reopens the prior screen from authority, and never changes session state.

## Player-facing coverage

- **Main menu / New Game:** a no-session launch now shows New Game and Load Game instead of a blank world. Character
  creation enumerates mounted retail races, legal gender bodies, portraits, supported backgrounds, attributes, skills,
  spell colleges, and technology disciplines; validation and finalization are M12B-owned.
- **HUD:** authoritative HP, fatigue, weapon, ammunition, combat mode, AP, READY/BUSY/recovery, active maintained
  effects, feedback, screen shortcuts, and the reused M8I melee/Bow/called-location/End Turn controls.
- **Inventory:** stable-ID enumeration, quantities, weight, equipment slot, select, equip, unequip, and owner transfer
  commands. The production command boundary supports containers and stack state without UI-side mutation.
- **Character:** name/race/gender, level, XP, Character Points, alignment, aptitude, armor class, carry load/capacity,
  all attributes, all skills, effective ranks, training, and authoritative skill spending.
- **Magic:** the bounded source catalog, learned/available state, fatigue cost, self or world target selection,
  real-time scheduling, active maintained effects, and supported cancellation.
- **Technology:** all disciplines and learned/effective degree/level, owned supported technology items, self/world target
  selection, and real-time scheduling.
- **Schematics:** deterministic learned built-in/found definitions, source description, preview failure/component
  readiness, output projection, and the M11C craft transaction. `CraftingStateService.ProjectKnown` is the sole small
  additive production API required by the UI.
- **Barter:** stable merchant selection, source-owned merchant/player inventory, source price/failure projection, Gold,
  affordability/rejection feedback, and authoritative buy/sell execution.
- **Logbook:** source quest ordering, state labels, timestamps, and intelligence-appropriate prose.
- **Map:** current sector plus campaign-known source destinations and M7 travel submission. A separate invented reveal
  database or UI-owned route legality was not added.
- **Followers:** authoritative ordered members, HP/fatigue/dead/unconscious/forced status, and supported dismissal.
- **Dialogue:** NPC text, authored responses, number shortcuts, response execution, failure display, and clean teardown.
- **Save/load:** the existing M6C slot catalog, save/load/delete/confirmation state, metadata, error/status feedback,
  and post-load transient normalization are integrated into the common modal coordinator.

Keyboard shortcuts are I/C/M/T/K/L/W/P for the major screens, B for barter targeting, F6 for save/load, F10 for HUD
visibility, and Escape for back/cancel. Spell, technology, merchant, item, invalid, use, and attack cursor modes remain
presentation state and display explicit feedback.

## Rebuild, load, and resolution behavior

Every screen reconstructs from the coordinator. Save/load does not serialize a screen, item selection, pending target,
combat selection, or cursor mode. Map transition and graphics rebuild retain authoritative identities and consequences;
the presenter re-projects the current screen. Original -> Enhanced -> Original changes rendering only. Layout uses
screen-relative panels and a bounded reference scale, while world targeting converts the pointer through the production
camera and then resolves an ObjectID using the existing presentation selector.

## Automated validation

The `M12CFullGameUi` category contains **18 tests** covering HUD/vitality projection, modal ownership, no-session
fail-closed behavior, inventory identity/equipment routing, invalid selection rollback, M4 character projection, magic
catalog/target state, technology disciplines, deterministic known schematics, invalid merchant rollback, journal/map
source unavailability, party projection, integrated save/load state, rebuild rebinding, combat teardown state, and
single production-presenter composition.

- focused M12C: **18/18**
- required older focused regression: **M11C 18/18**, run because M12C adds the read-only `ProjectKnown` API
- complete EditMode suite: **1032/1032**, failed/skipped/inconclusive **0/0/0**
- Unity 6000.0.71f1 compilation: clean
- `git diff --check`: clean

The complete suite still emits the ten established intentional fail-closed dialogue diagnostics; they were inspected as
expected test output and the Console was cleared afterward.

## Physical production validation

The physical M12C harness began from the visible production main menu and finalized an authentic Human male New Game
with background 3, portrait 1005, Strength +1, Melee +1, Earth 1, Herbology Novice, and one remaining Character Point.
It then proved, through `ProductionGameUiPresenter.Controller` and authentic mounted data:

- authoritative HUD health/fatigue/item/readiness;
- starting armor 8157 unequip/re-equip through M3 and the character sheet's Strength/Melee values;
- Strength of Earth cast and maintained-effect cancellation;
- Healing Salve use from the Technology screen;
- written blueprint 14095 learning and schematic 4020 craft from authentic components 10084 + 15116 into 15169;
- quest 1005 logbook projection;
- authentic Virgil dialogue 1324 response 72, party enrollment, party status, and supported dismissal;
- one temporary manual slot save/load round-trip, HUD reconstruction, and deletion of the validation slot;
- authentic Tarant merchant source 7 restock, quoted price, and one exact-Gold purchase;
- known Tarant projection and the authentic four-sector Bates-return -> Tarant travel;
- one turn-based and one real-time attack against the authentic Polar Bear Cub;
- Original -> Enhanced -> Original rebuild with unchanged identity and reopened Character screen;
- exactly one full UI presenter and one production PC presentation.

The accepted physical run ended with **0 warnings and 0 errors**.

## Deliberate boundaries and ambiguities

The source's recent-action/quick-use slots, paperdoll bitmap, drag gesture, right-click context menu, animated rotating
windows, and complete tooltip prose require additional presentation assets or item-use definitions that the completed
authoritative runtime does not yet expose as a generic command. M12C keeps all existing item actions operable with
explicit buttons and does not invent a parallel item-use authority. Likewise, current M7 exposes source destination
discovery/travel but no authoritative local-map fog/reveal image; the bounded Map screen therefore presents the current
sector and world destinations without fabricating reveal state. Options, intro movies, audio, and HD UI art remain
M12D/polish boundaries. These omissions do not prevent ordinary New Game, inventory/equipment, combat, spell,
technology, crafting, barter, journal, travel, follower, dialogue, or save/load operation.

## Closure

M12C is fully complete for the bounded functional Full HUD / Game UI milestone. The completed runtime is operable from
the production UI without an Editor-only harness for ordinary play. No later milestone was started and nothing was
pushed.

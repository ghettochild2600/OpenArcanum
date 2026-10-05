# Launching and Playing OpenArcanum in Unity

## Open the production game scene

1. Open the OpenArcanum Unity project.
2. In the Project window, open `Assets/_Game/Scenes/OpenArcanum.unity`.
3. Press the Play button at the top of the Unity Editor.

The game opens at the OpenArcanum main menu. `TestTerrain` remains available for development and validation, but it is not the normal player entry scene.

## Start a new game

1. Select **New Game** from the main menu.
2. Enter a character name.
3. Choose a supported race, gender, portrait, and background.
4. Spend all five starting character points across attributes, skills, spell colleges, or technology disciplines.
5. Scroll to the end of the character-creation form and select **Begin Game**.

The game only enables finalization when the character is valid and all five starting points have been spent. A successful character enters the authentic campaign start in `maps/arcanum1-024-fixed/86570436012.sec`, near local tile `(30, 32)`.

## Basic play controls

- Left-click an accessible ground location to move.
- Left-click a nearby world object or character to interact when the active cursor and game state permit it.
- Press `I`, `C`, `M`, `T`, `K`, `L`, `W`, or `O` for Inventory, Character, Magic,
  Technology, Skills, Logbook, World Map, or Options.
- Press `Escape` to close the current interface; with no interface open, it opens the Main Menu.
- Press `Space` to close an open interface. During active combat with no interface open, it switches between
  turn-based and real-time mode without resetting combat state.
- Press `R` to toggle Attack/Talk mode. In turn-based combat, release `E` to end the current turn.
- Hold comma `<`, period `>`, or slash `?` during an attack to aim at the Head, Arms, or Legs.
- Hold either `Shift` while clicking a target to stand and attack without approaching.
- Hold either `Alt` while clicking to force a legal attack against a neutral target; hold `Alt` while dragging a
  corpse to move it through world-state authority.
- Press `Num Lock` to toggle the default walk/run mode. Holding either `Ctrl` temporarily inverts that choice for
  the next ground move.
- Hold the arrow keys to pan the camera. Press `Home` to recenter on the player/current turn-based participant.
- Press `F1` through `F6` for the source follower orders Walk, Attack, Stay Close, Spread Out, Back Off, and Follow.
- Press `F7` to auto-save and `F8` to auto-load. Press `F12` to save a screenshot.
- Assign an owned item or learned spell from Inventory or Magic to slots `1` through `0`; press that number on the
  main HUD to activate it. Dialogue keeps ownership of its numbered response keys.
- Press `A` to repeat the most recently successful quick-slot action and `V` to show the application version.

The same production screens are also available from the HUD buttons along the bottom of the Game view.

`S` (Sleep), `F` (Fate), generic skill quick-slot activation, and Enter broadcast/chat are not available in the
current single-player runtime and fail closed instead of opening placeholder interfaces. Print Screen/SysRq,
Scroll Lock, and Pause/Break have no retail game action. The retail default bindings are fixed for now; the Options
screen does not yet expose remapping.

## Save a game

1. Select **Save/Load** on the HUD.
2. Leave the **Save** tab selected.
3. Select **Create** to make the suggested save slot, or select an existing slot and choose **Save / Overwrite**.
4. Confirm an overwrite when prompted.

For the dedicated automatic slot, press `F7` during play.

## Load a game

From the main menu, select **Load Game**. Select the desired slot and choose **Load Selected**.

During play, select **Save/Load** on the HUD, select the **Load** tab, choose a slot, and select **Load Selected**.
Press `F8` to load the dedicated automatic slot. The saved player, campaign sector, inventory, and supported session
state are reconstructed through the normal production save/load path.

## Exit Play Mode

Press the Play button at the top of the Unity Editor again. `Ctrl+P` also toggles Play Mode on Windows when the Unity Editor has focus.

In a standalone player build, the main menu also provides **Quit**. Unity Play Mode is always ended with the Editor Play control instead.

## Current limitations

- OpenArcanum is still under active development. Only systems recorded as complete in `PROJECT_STATUS.md` should be treated as available.
- Character creation contains a long source-authentic choice list. Use the vertical scroll bar to reach attributes and the final controls. In a narrow Unity Game view, use the horizontal scroll bar near the bottom if **Begin Game** is off-screen.
- Unsupported source mechanics remain unavailable or fail closed rather than using invented behavior.
- `TestTerrain` and the Editor validation commands are development tools; normal play does not require them.

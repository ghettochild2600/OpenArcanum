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
- Press `I` for Inventory.
- Press `C` for Character.
- Press `M` for Magic.
- Press `T` for Technology.
- Press `K` for Crafting.
- Press `L` for the Logbook.
- Press `W` for the World Map.
- Press `P` for Party.
- Press `B` for Barter with the selected target.
- Press `F6` for Save/Load.
- Press `F10` to show or hide the HUD.
- Press `Escape` to close the current production UI screen.

The same production screens are also available from the HUD buttons along the bottom of the Game view.

## Save a game

1. Press `F6` or select **Save/Load** on the HUD.
2. Leave the **Save** tab selected.
3. Select **Create** to make the suggested save slot, or select an existing slot and choose **Save / Overwrite**.
4. Confirm an overwrite when prompted.

## Load a game

From the main menu, select **Load Game**. Select the desired slot and choose **Load Selected**.

During play, press `F6`, select the **Load** tab, choose a slot, and select **Load Selected**. The saved player, campaign sector, inventory, and supported session state are reconstructed through the normal production save/load path.

## Exit Play Mode

Press the Play button at the top of the Unity Editor again. `Ctrl+P` also toggles Play Mode on Windows when the Unity Editor has focus.

In a standalone player build, the main menu also provides **Quit**. Unity Play Mode is always ended with the Editor Play control instead.

## Current limitations

- OpenArcanum is still under active development. Only systems recorded as complete in `PROJECT_STATUS.md` should be treated as available.
- Character creation contains a long source-authentic choice list. Use the vertical scroll bar to reach attributes and the final controls. In a narrow Unity Game view, use the horizontal scroll bar near the bottom if **Begin Game** is off-screen.
- Unsupported source mechanics remain unavailable or fail closed rather than using invented behavior.
- `TestTerrain` and the Editor validation commands are development tools; normal play does not require them.

# SDG Adventure CompletePlayer

Unity 2023.1.22f1 portable first-person character module. Import the single `SDGAdventure_CompletePlayer.unitypackage` into your own project, open your level, and drag `Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab` into the Hierarchy. Set its spawn position and ensure the scene has only one player, camera and AudioListener, then press Play. A floor and suitable lighting are needed. The demo is optional.

## Project prerequisites

- Install Unity Input System 1.7.0 and Unity UI/uGUI 1.0.0 in Package Manager.
- In **Project Settings > Player > Active Input Handling**, choose **Both**, then restart the Editor if requested.
- The package contains the complete SharedPlayer runtime, its Input Actions, UI font, and the font license. No other SDG Adventure package is needed.

The default player visual is the shared procedural recycling worker, adapted from the accepted C_Main worker geometry and unarmed poses. It uses copied Rust and Steel textures in `Resources/SharedWorker`, and PlayerPerspectiveRig hides it in first-person and displays it in third-person. No C_Main scene or scripts are required. The previous fk.fbx remains in the package as an unused legacy asset; its redistribution authorization has not been established.

## Default controls

WASD move, mouse look, Space jump, Shift run, Ctrl crouch, Z prone, Q/E lean, F interact, Tab inventory, 1–5 quick slots, P task list, Esc pause. Inventory and quick-slot inputs are integration events. The Demo adds a test panel and five-slot selection display; a real inventory implementation is not included. Use **Pause > Controls** to rebind keys, change mouse sensitivity, save settings, or restore defaults. Saved settings use PlayerPrefs in each project.

The demo includes a floor, jump platform, obstacle, low tunnel, interactable device, and three tasks. The worker uses the C_Main procedural movement, grounded/airborne, crouch and prone pose formulas, driven by CharacterController velocity and height. It does not require Animator or root motion. Optional weapon pose integration can use the worker joint/anchor accessors and SetWeaponPoseOwner; a weapon implementation is not included. Game levels can implement `IPlayerInteractable` and `IPlayerTaskSource`; load tasks through `PlayerTaskJournal.Load` and update them with `SetCompleted`. The status HUD appears when a level calls `PlayerHudCanvas.SetStatus` with nonempty text. Optional level actions can be supplied through `PlayerPauseMenu.SetLevelActions`, and a test return point through `SetTestReturnPoint`.

The pause menu always includes Continue, Controls, Tasks, Restart Level and Return to Level Select. By default Restart reloads the player's current scene when it is enabled in Build Settings. Set the `Selection Scene` field on the prefab instance to your own scene's full path or name and add that scene to Build Settings; an unset or unavailable destination shows a message and leaves the menu open. A level can instead provide callbacks through `SetLevelActions`, which take precedence. No SDG Adventure selection scene is assumed.

## Rebuild and checks

In the source project, **SDG Adventure > Complete Player > Build and Export** regenerates the prefab and demo and overwrites the package in the project’s `Exports` folder. Run it only after the required acceptance checks. **Validate** checks essential prefab components, bones, scripts, and dependencies. Run the demo in Play Mode to inspect visuals and controls; batch tests cannot certify physical keyboard and mouse experience.

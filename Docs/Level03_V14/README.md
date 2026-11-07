# Level03 V14 — review and validation status

Base: `72cf8b4605268d28e0c6c4188eeb36cf7fafd5f1` (V13).

Prone now uses the existing Input System action, settings menu, conflict handling and persisted binding overrides. Q/E are reserved for the existing lean controls. Level03 retains its existing stance, clearance and jump handling. The solid procedural sight cylinder is replaced by an open frame; the FPS ADS offset centers that frame on the camera firing axis. The existing camera ray and damage path are unchanged.

## Validation — incomplete

Unity 2023.1.22f1, batchmode, nographics, selected PlayMode suites: 39 executed, 23 passed, 16 failed, 0 skipped. Of the three new tests, two passed (binding persistence/conflict/reset and sight geometry), one failed (synthetic keyboard event). Of the 36 existing tests, 21 passed and 15 failed. Twelve existing failures occurred before prone state was entered, two existing interaction tests failed, and the existing menu test failed on cursor lock. These failures have not been proven to be baseline failures; batchmode focus/cursor behavior is a possible factor, not a verified explanation. Full PlayMode and EditMode suites, visual ADS alignment, and manual gameplay acceptance were not run. See `PlayModeResults.xml` for exact test names and assertions. No existing assertions were removed.

## Local Unity checks still required

Open the existing project with Unity 2023.1.22f1 and load `Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity`. In Test Runner run PlayMode suites `M7SharedPlayerTests`, `M6IntegrationTests.C3V11ThirdPersonWeaponTests`, `M6IntegrationTests.C3V12FinalFollowupTests` and `M6IntegrationTests.C3V13BakedFactoryTests`, then run the full EditMode and PlayMode suites. In Play mode, use the settings menu to rebind prone, check old and new keys, restart and restore defaults; test standing under a low ceiling and a ceiling appearing while rising. Equip the quantum projector, verify the open sight against the screen crosshair and target, shoot and check damage, and switch FPS/TPS near walls. Check movement, stairs, crouch, jump, inventory, interactions, tasks and the baked factory. Do not treat this draft as accepted until these checks and the failed tests are resolved.


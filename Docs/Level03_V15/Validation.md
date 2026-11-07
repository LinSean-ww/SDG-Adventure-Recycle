# Validation

Unity 2023.1.22f1 (c1416845ad01), Windows Editor. Commands use `-batchmode -runTests -testPlatform <platform> -projectPath <project> -testResults <xml> -logFile <log>`. PlayMode comparisons additionally use `-nographics`; the graphics EditMode run omits it. Commands did not use `-quit`.

| Run | Total | Passed | Failed | Skipped | Evidence |
| --- | ---: | ---: | ---: | ---: | --- |
| V14.1 baseline EditMode, nographics | 14 | 13 | 1 | 0 | [XML](Baseline_V141_EditMode.xml) |
| V15 EditMode, graphics | 14 | 14 | 0 | 0 | [XML](V15_EditMode_Graphics.xml) |
| V15 final EditMode, graphics | 14 | 14 | 0 | 0 | [XML](V15_Final_EditMode.xml) |
| V14.1 baseline PlayMode, nographics | 216 | 166 | 47 | 3 | [XML](Baseline_V141_PlayMode.xml) |
| V15 PlayMode, nographics, sequential | 216 | 168 | 45 | 3 | [XML](V15_PlayMode.xml) |
| V15 PlayMode, sequential repeat before PlayerPrefs cleanup change | 216 | 168 | 45 | 3 | [XML](V15_PlayMode_Repeat.xml) |
| V15 PlayMode after PlayerPrefs cleanup change | 216 | 167 | 46 | 3 | [XML](V15_Final_PlayMode.xml) |
| V15 selected reload, V13 and binding tests | 10 | 10 | 0 | 0 | [XML](V15_Targeted.xml) |
| PlayerPrefs and prone key tests after cleanup change | 2 | 2 | 0 | 0 | [XML](V15_PlayerPrefs_Targeted.xml) |
| New close-wall failure, isolated rerun | 1 | 1 | 0 | 0 | [XML](V15_CloseWall_Targeted.xml) |
| V14.1 selected tests with graphics | 21 | 4 | 17 | 0 | [XML](V141_Focused_Graphics.xml) |
| Synthetic prone key focus experiment | 1 | 1 | 0 | 0 | [XML](Experiment_ProneFocus.xml) |
| Interaction fixture focus experiment (reverted) | 23 | 4 | 19 | 0 | [XML](Experiment_InteractionFocus.xml) |

The EditMode baseline failure is an unexpected `No graphic device is available to initialize the view` log in `C3TextLifecycleTests.PlayModeCycleOne`. A graphics-enabled run passes all 14, but it is a different command and includes the test change; it does not establish the nographics suite is fixed.

The three existing PlayMode skips are `C3V8FactoryAccessAndFeedTests.BRoomIsHiddenAndOneVisibleConveyorReachesThreeSorters`, `FormerYellowStripeRemainsTraversableAtReportedPosition`, and `VisibleStairIsRealAndConnectsToOriginalMaintenanceWorkstation`. Their XML reason says V8-only geometry was replaced in V9 and points to `C3V9VisibleIndustrialRouteTests`. No skips were added.

The targeted run includes both V14.1 reload tests, the V13 baked factory test and seven `PlayerSettingsTests`. The V14 sight test is in the full suite; no separate sight-only run was made. The focus experiment changed two Input System settings together, so it does not isolate which of those settings is decisive. The reverted interaction experiment did not resolve its 19 failures. The two full V15 runs before the PlayerPrefs cleanup change had identical per-test outcomes. After that change, the full run newly failed `CloseWallRaisesHeldRifleAndKeepsBothHandsOnGrips`: muzzle clearance was 0.01532 versus required >0.025. The isolated rerun passed; the cause and any relationship to the change are unproven. No test-order randomization or new interactive/manual acceptance was performed. These results do not prove FPS/TPS, shooting, damage, physical traversal or UI behavior in a focused player window.

No `error CS` compiler error was found in the inspected baseline and selected-run logs. The logs contained expected environment and test errors; the XML records test failures. All original XMLs in this directory are retained, including the experimental runs.

The detailed logs generated during this investigation were retained locally under `C:\Users\h7796\AppData\Local\Temp\SDG_Level03_V15_Logs`; they are not part of the Git commit.

Two simultaneous Unity projects had independent Library directories but shared the same Windows PlayerPrefs namespace. The first V14 and V14.1 PlayMode runs overlapped. `C3UiView` writes `SDG_C3_LastPuzzleOptionOrder` on entry; the overlapping runs may have changed this preference and affected the option-order test. The pre-test value was not captured and cannot be restored reliably. Tests were subsequently run sequentially. Other persisted state has not been proven unaffected.

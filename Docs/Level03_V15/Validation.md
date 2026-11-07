# Validation

## Phase 2

All runs below used Unity 2023.1.22f1 on Windows. The PlayMode runs use `-batchmode -nographics`; the EditMode run uses graphics. `RunIsolatedTests.ps1` exports the two existing project PlayerPrefs registry keys, runs one Unity process at a time, and restores and verifies their values afterward. This protects the captured values but is not an isolated OS account; another process writing the same keys concurrently would interfere. Backups and editor logs remain in the user's temporary directory, not the repository.

| Run | Total | Pass | Fail | Skip | Evidence |
| --- | ---: | ---: | ---: | ---: | --- |
| Final targeted groups; seed 71, one repeat | 14 | 14 | 0 | 0 | [XML](Phase2_Final_Targeted_Retry.xml) |
| Input and pause groups | 27 | 27 | 0 | 0 | [XML](Phase2_Input_Targeted.xml) |
| Cursor, interaction and factory groups | 64 | 63 | 1 | 0 | [XML](Phase2_InteractionGroups.xml) |
| Weapon, V3 and reload; seed 17, two repeats | 13 | 13 | 0 | 0 | [XML](Phase2_FactoryWeapon_Repeat.xml) |
| UI safety; seed 71, two repeats | 2 | 2 | 0 | 0 | [XML](Phase2_Safety_Targeted.xml) |
| Order-leak tests; seed 71, two repeats | 2 | 2 | 0 | 0 | [XML](Phase2_OrderLeak_Targeted.xml) |
| Full EditMode, final | 14 | 14 | 0 | 0 | [XML](Phase2_Final_EditMode.xml) |
| Full PlayMode before final factory/transition changes | 217 | 201 | 13 | 3 | [XML](Phase2_PlayMode.xml) |
| Full PlayMode, seed 71 and one repeat before final factory/transition changes | 217 | 201 | 13 | 3 | [XML](Phase2_Final_PlayMode_Seed71.xml) |
| Full PlayMode, final, seed 71 and one repeat | 217 | 205 | 9 | 3 | [XML](Phase2_Final_PlayMode.xml) |
| Lighting comparison, graphics enabled | 1 | 0 | 1 | 0 | [XML](Phase2_Lighting_Graphics.xml) |

The 64-test group retains the display-versus-held rifle geometry failure. An intermediate seed 71 run exposed two order-sensitive failures; the targeted order-leak test and the later full seeded run passed both. The recorded `repeatIteration` property reports completed repetitions; these are not separate test-case counts in the XML.

The final nine failures are:

1. `C3FactoryFullArtTests.StageA_UpperDeckChipRemainsCollectibleAndHudHintIsCurrent`: saved chip position differs from the old coordinate; the subsequent ray and collection assertions were not reached.
2. `C3M2BlockoutPlayModeTests.PlayerCapsuleCanUseBothStairsAndMainWalkway`: the old route starts at obsolete coordinates; the newer `C3V6AccessRegressionTests.BothStairsCanCarryACharacterControllerOntoTheSecondFloor` passed using the saved stair entrances and real controller.
3. `C3SceneBugFixTests.OriginalControllerMovesJumpsAndPhysicalSupportsBlock`: the spawn movement ended at x = -1.05499983 versus required x < -1.06665528. Identify actual capsule contacts and retest before proposing a minimal fix; a scene defect has not been established.
4. `C3V11VisualUpgradeTests.SharedRifleGeometryUsesViewmodelOffAndAssemblyDisplayOnShadows`: the baked display has 45 renderers and the live V14 sight model 48. This is an actual presentation mismatch, not a hard-coded count in the assertion.
5. `C3V5SpatialIntegrityTests.BDoorHasVisibleFloorHeightConveyorAndOverheadConnection`: obsolete V8 belt name/layout; confirm current interface and clearance requirements before replacing its spatial assertions.
6. `C3V6AccessRegressionTests.ConveyorIsInsideTheMainHallAndConnectsToSorter`: obsolete V8 belt reference; the current P2 connection checks pass, but this historical hall-boundary condition still needs review.
7. `C3V7CZoneBoundaryAndAisleTests.CToBIsClosedAndTheReceivingBeltStopsAtThreshold`: the old shutter is absent; verify the current B/C boundary design before changing this check.
8. `C3V7CZoneBoundaryAndAisleTests.FormerObstructingStairRampHasMovedAwayFromReportedPosition`: the old ramp and route names are absent; map the aisle and side-stair constraints to current colliders.
9. `M6VisualRuntimeTests.DirectVersusSelectionSceneLighting`: all 360,000 pixels differ from an old reference in both nographics and graphics-enabled runs. The cause and whether the historical reference remains applicable are unverified; a visual comparison of the current accepted scene is required.

No skips were added. The three skips still describe V8 geometry replaced by V9. No new manual gameplay or visual acceptance was performed.

The first Phase 2 input run attempted to restore PlayerPrefs through a read-only registry handle. Those writes failed and its pre-run in-memory snapshot was lost. That run may have changed existing project preferences; the earlier value cannot be recovered. Subsequent runs used persistent exports and writable handles and reported verified restoration. A separate compilation-failed run was stopped after its wrapper stalled; its saved registry exports were re-imported and both keys verified identical to those backups. No game window or manual re-acceptance was performed.

## Phase 1

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

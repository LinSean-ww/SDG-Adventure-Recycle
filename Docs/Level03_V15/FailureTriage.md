# Latest pre-integration status

The latest full graphics-enabled run passed six of the nine Phase 2 failures and retained three failures: the obsolete M2 stair route, the spawn movement/contact assertion, and the unstable V15 lighting reference. See [PreIntegrationAcceptance.md](PreIntegrationAcceptance.md) and [final XML](PreIntegration_Final_PlayMode.xml) for the latest evidence and blocker assessment. The phase-specific tables below preserve earlier results and should not be read as the final status.

# Phase 2: historical failures

The final seeded run repeated each passing test once; 37 of the 46 historical failures passed and nine failed. Results below link to the raw XML. A pass does not independently prove a shared root cause. The Phase 1 ledger that follows is retained as historical analysis; its retest column describes Phase 1, not the final state.

| Historical failed test | Phase 2 |
| --- | --- |
| `M6IntegrationTests.C3FactoryFullArtTests.StageA_UpperDeckChipRemainsCollectibleAndHudHintIsCurrent` | Failed |
| `M6IntegrationTests.C3M2BlockoutPlayModeTests.PlayerCapsuleCanUseBothStairsAndMainWalkway` | Failed |
| `M6IntegrationTests.C3SceneBugFixTests.OriginalControllerMovesJumpsAndPhysicalSupportsBlock` | Failed |
| `M6IntegrationTests.C3V11RecyclingPresentationTests.ProductionUguiReceivesARealClickWithoutReactivationOrPuzzleSubmission` | Passed |
| `M6IntegrationTests.C3V11ThirdPersonInteractionTests.FirstPersonKeepsItsTwoMetreRayAndOnceOnlyFCollection` | Passed |
| `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonMaterialsKeepActivationSortingAndOnceOnlyRecovery` | Passed |
| `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonRejectsCameraWallsBodyWallsAndFloors` | Passed |
| `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonUsesBodyDistanceForAllThreeRealQuestParts` | Passed |
| `M6IntegrationTests.C3V11ThirdPersonInteractionTests.UiContextAndBothUpdateOrdersNeverDispatchTwoInteractions` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.AssemblyNativeRayRequiresMaterialsConsumesExactlyOneAndCannotRepeat` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.CollectibleDisableReenableAndPickupClearTheCueWithoutRepeatingRewards` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.ConsoleNativeRayOpensTheOriginalPuzzleAndActivationFlow` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.InspectionNativeRayShowsTheOriginalReportWithoutGrantingMaterials` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.MaintenanceKeepsItsExistingBenchAndNearbyChipWithoutInventingAnFTask` | Passed |
| `M6IntegrationTests.C3V11VisualUpgradeTests.SharedRifleGeometryUsesViewmodelOffAndAssemblyDisplayOnShadows` | Failed |
| `M6IntegrationTests.C3V12FactoryRepairTests.ScenePlacedPartsCanBeCollectedWithFOnceInFirstPerson` | Passed |
| `M6IntegrationTests.C3V12FactoryRepairTests.ScenePlacedPartsCanBeCollectedWithFOnceInThirdPerson` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.AirborneSpaceCannotQueueALandingOrDoubleJump` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.FirstPersonZThenSpaceStandsBeforeOneGroundedJump` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.HeldCtrlUnpronesToCrouchWithoutADelayedJump` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.JumpDisabledStillAllowsSafeStandingButNoImpulse` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.LowCeilingRejectsSpaceInBothViewsAndZStillRecoversAfterLeaving` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.MaintenanceChipStillAcceptsRealFOnceInFirstPerson` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.MaintenanceChipStillAcceptsRealFOnceInThirdPerson` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.NewCeilingDuringRecoveryCancelsJumpAndReturnsToProne` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.SlowStandExpiresTheRequestInsteadOfJumpingMuchLater` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.ThirdPersonZThenSpaceStandsBeforeOneGroundedJump` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.UiAndUnlockedCursorCancelAnExistingBufferedJump` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.UiSpaceDoesNotUnproneOrJumpInEitherView` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.ViewSwitchDuringRecoveryKeepsExactlyOneJumpSubscriptionAndTakeoff` | Passed |
| `M6IntegrationTests.C3V12FinalFollowupTests.ZCancelsAPendingProneJump` | Passed |
| `M6IntegrationTests.C3V3CirculationTests.AllFivePedestrianSectionsHavePhysicalFloor` | Passed |
| `M6IntegrationTests.C3V3CirculationTests.ConveyorHasOneFeedAndThreePhysicalIntakeOutputPairs` | Passed |
| `M6IntegrationTests.C3V3CirculationTests.StairRampsReachTheirDecksWithoutBarrierAcrossExit` | Passed |
| `M6IntegrationTests.C3V4BFeedAndAccessTests.BZoneHasVisibleInputBeltRollersAndConveyorLift` | Passed |
| `M6IntegrationTests.C3V4BFeedAndAccessTests.RaisedCeilingAndRealAccessibleStairEndpointsExist` | Passed |
| `M6IntegrationTests.C3V4BFeedAndAccessTests.WestStairRampCanCarryARealCharacterController` | Passed |
| `M6IntegrationTests.C3V5SpatialIntegrityTests.BDoorHasVisibleFloorHeightConveyorAndOverheadConnection` | Failed |
| `M6IntegrationTests.C3V6AccessRegressionTests.ConveyorIsInsideTheMainHallAndConnectsToSorter` | Failed |
| `M6IntegrationTests.C3V7CZoneBoundaryAndAisleTests.CToBIsClosedAndTheReceivingBeltStopsAtThreshold` | Failed |
| `M6IntegrationTests.C3V7CZoneBoundaryAndAisleTests.FormerObstructingStairRampHasMovedAwayFromReportedPosition` | Failed |
| `M6IntegrationTests.M6VisualRuntimeTests.DirectVersusSelectionSceneLighting` | Failed |
| `SDGAdventure.Testing.C3AbandonedFactoryUpgradeTests.RuntimeArtHasBFeedAndNonBlockingDecay` | Passed |
| `M7SharedPlayerTests.PlayerInputServiceTests.Level03MenuPausesResumesAndUsesExistingTransitions` | Passed |
| `M7SharedPlayerTests.PlayerSettingsTests.ProneActionRespondsOnlyToCurrentKey` | Passed |
| `SDGAdventure.PlayerControlTests.PlayerControlPlayModeTests.RepeatedPauseSettingsCyclesRestoreGameplayState` | Passed |

Evidence: [historical XML](Historical_V141_PlayMode.xml), [final Phase 2 XML](Phase2_Final_PlayMode.xml), and [targeted verification](Phase2_Final_Targeted_Retry.xml).

## Phase 2 interpretation and remaining work

- F, Z and pause: the OS returned an unlocked cursor in batch mode. Tests now use an Editor-only simulated cursor backend and real Input System events with focus settings restored in teardown. Production still reads the OS cursor; UI context still blocks gameplay. The relevant groups passed; the focus settings were changed together, so their individual contributions are not isolated.
- V3/V4 factory tests: former V8 names and old west/east coordinates conflict with the serialized V13 design. Updated tests verify two sets of 22 solid treads, the east landing, connected current P2 infeed surfaces and rollers, a physical west-stair walk and the existing three sorter paths. No scene geometry was edited.
- Close-wall rifle: the prior six-frame wait was frame-rate dependent because the production pose advances with deltaTime. The test now allows 0.2 seconds of game time within a one-second real-time limit and retains the original grip and 0.025 clearance assertions. The final seeded repeated run passed; this does not prove clearance at every intermediate frame.
- Menu and coordinator ordering: prior scene-owned menus are temporarily disabled and restored; the module test compares its previous result and always unsubscribes. These targeted tests and the seeded full run passed.
- Remaining nine failures are listed in Validation.md. The old M2 west-stair route uses retired coordinates; a newer actual-character test crosses both current stairs and the rear walkway successfully. The original-controller spawn movement still needs a collider/contact trace before deciding whether it is a real defect or an obsolete displacement assumption. Do not move equipment or rebake the scene on this evidence alone.
- Rifle mismatch: the baked scene contains the old `Sight` while the live generator makes two `SightSide` and two `SightRim` renderers. The display and held models are genuinely different; the original structural equality test remains failing. A separately reviewed display update would be needed to make them match, subject to the scene/geometry constraints.

# Failure triage — incomplete

This ledger covers all 46 failures in the historical V14.1 XML plus the additional failure in the new V14.1 HEAD baseline and the new unstable final-run failure. Original messages for historical failures come from Historical_V141_PlayMode.xml. The V14 result below is from the sequential run; PlayerPrefs were shared across runs. Unresolved means the root cause or correction has not been demonstrated.

## `M6IntegrationTests.C3V11ThirdPersonWeaponTests.CloseWallRaisesHeldRifleAndKeepsBothHandsOnGrips`

- Historical and V14.1 HEAD result: passed. Both initial V15 full runs also passed.
- Final V15 full run: failed because muzzle clearance was 0.0153198848, while the assertion requires greater than 0.025. The isolated rerun passed.
- Investigation: the test advances the pose for six frames; timing or prior state may matter, but neither has been established as the cause. A real clearance problem has not been ruled out.
- Changed file / disposition: none; unresolved. See [final full XML](V15_Final_PlayMode.xml) and [isolated XML](V15_CloseWall_Targeted.xml).

## `M6IntegrationTests.C3FactoryFullArtTests.StageA_UpperDeckChipRemainsCollectibleAndHudHintIsCurrent`

- Original error: Expected: (-13.20, 5.37, 19.25) But was: (-13.20, 5.37, 20.52)
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3M2BlockoutPlayModeTests.PlayerCapsuleCanUseBothStairsAndMainWalkway`

- Original error: West stair did not reach the upper level. Expected: greater than 4.25f But was: 0.629999936f
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3SceneBugFixTests.OriginalControllerMovesJumpsAndPhysicalSupportsBlock`

- Original error: C3 player must walk into the factory from the new spawn. Expected: less than -1.06665528f But was: -1.05499983f
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11RecyclingPresentationTests.ProductionUguiReceivesARealClickWithoutReactivationOrPuzzleSubmission`

- Original error: Expected: Locked But was: None
- Investigation: The observed OS cursor lock is None in batch mode. Requested cursor state has not been separated from OS state.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11ThirdPersonInteractionTests.FirstPersonKeepsItsTwoMetreRayAndOnceOnlyFCollection`

- Original error: Expected: False But was: True
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonMaterialsKeepActivationSortingAndOnceOnlyRecovery`

- Original error: Expected: 1 But was: 0
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonRejectsCameraWallsBodyWallsAndFloors`

- Original error: Expected: 1 But was: 0
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11ThirdPersonInteractionTests.ThirdPersonUsesBodyDistanceForAllThreeRealQuestParts`

- Original error: Expected: False But was: True
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11ThirdPersonInteractionTests.UiContextAndBothUpdateOrdersNeverDispatchTwoInteractions`

- Original error: Expected: 1 But was: 0
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.AssemblyNativeRayRequiresMaterialsConsumesExactlyOneAndCannotRepeat`

- Original error: Expected: True But was: False
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.CollectibleDisableReenableAndPickupClearTheCueWithoutRepeatingRewards`

- Original error: Expected: False But was: True
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.ConsoleNativeRayOpensTheOriginalPuzzleAndActivationFlow`

- Original error: Expected: True But was: False
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.InspectionNativeRayShowsTheOriginalReportWithoutGrantingMaterials`

- Original error: Expected: True But was: False
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.MaintenanceKeepsItsExistingBenchAndNearbyChipWithoutInventingAnFTask`

- Original error: Expected: False But was: True
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V11VisualUpgradeTests.SharedRifleGeometryUsesViewmodelOffAndAssemblyDisplayOnShadows`

- Original error: Both uses must share the same rifle construction. Expected: 45 But was: 48
- Investigation: V13 baked display retains the old optic, while the current generator replaces one solid part with four frame parts. The structural divergence is real; its appropriate treatment remains open.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FactoryRepairTests.ScenePlacedPartsCanBeCollectedWithFOnceInFirstPerson`

- Original error: Expected: False But was: True
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FactoryRepairTests.ScenePlacedPartsCanBeCollectedWithFOnceInThirdPerson`

- Original error: Expected: False But was: True
- Investigation: Cause not yet isolated; input/cursor gating, state leakage or fixture assumptions require targeted investigation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.AirborneSpaceCannotQueueALandingOrDoubleJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.FirstPersonZThenSpaceStandsBeforeOneGroundedJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.HeldCtrlUnpronesToCrouchWithoutADelayedJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.JumpDisabledStillAllowsSafeStandingButNoImpulse`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.LowCeilingRejectsSpaceInBothViewsAndZStillRecoversAfterLeaving`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.MaintenanceChipStillAcceptsRealFOnceInFirstPerson`

- Original error: Expected: False But was: True
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.MaintenanceChipStillAcceptsRealFOnceInThirdPerson`

- Original error: Expected: False But was: True
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.NewCeilingDuringRecoveryCancelsJumpAndReturnsToProne`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.SlowStandExpiresTheRequestInsteadOfJumpingMuchLater`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.ThirdPersonZThenSpaceStandsBeforeOneGroundedJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.UiAndUnlockedCursorCancelAnExistingBufferedJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.UiSpaceDoesNotUnproneOrJumpInEitherView`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.ViewSwitchDuringRecoveryKeepsExactlyOneJumpSubscriptionAndTakeoff`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V12FinalFollowupTests.ZCancelsAPendingProneJump`

- Original error: Expected: True But was: False
- Investigation: Cursor is None in batch runs while the production controller and interactor require Locked; this can block the exercised path. Each failure still needs independent isolation.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V3CirculationTests.AllFivePedestrianSectionsHavePhysicalFloor`

- Original error: Gap in 2F route at (-4.65, 5.40, 17.50) Expected: True But was: False
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V3CirculationTests.ConveyorHasOneFeedAndThreePhysicalIntakeOutputPairs`

- Original error: Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V3CirculationTests.StairRampsReachTheirDecksWithoutBarrierAcrossExit`

- Original error: Expected: String containing "V8_MAIN_STAIR" But was: "C_Floor"
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V4BFeedAndAccessTests.BZoneHasVisibleInputBeltRollersAndConveyorLift`

- Original error: B to classification path missing: V8_B_To_C_ContinuousRubber Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V4BFeedAndAccessTests.RaisedCeilingAndRealAccessibleStairEndpointsExist`

- Original error: Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V4BFeedAndAccessTests.WestStairRampCanCarryARealCharacterController`

- Original error: Walker's forward route got blocked before the 2F deck Expected: greater than 15.0f But was: 8.89569187f
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V5SpatialIntegrityTests.BDoorHasVisibleFloorHeightConveyorAndOverheadConnection`

- Original error: V5 B-door conveyor prefab was not loaded Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V6AccessRegressionTests.ConveyorIsInsideTheMainHallAndConnectsToSorter`

- Original error: Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V7CZoneBoundaryAndAisleTests.CToBIsClosedAndTheReceivingBeltStopsAtThreshold`

- Original error: The B room must be hidden from the C factory Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.C3V7CZoneBoundaryAndAisleTests.FormerObstructingStairRampHasMovedAwayFromReportedPosition`

- Original error: Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M6IntegrationTests.M6VisualRuntimeTests.DirectVersusSelectionSceneLighting`

- Original error: DirectC3 differs from the original-project C3 image. Expected: less than or equal to 36 But was: 360000
- Investigation: No graphics device was used. A graphics-controlled comparison is still required.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `SDGAdventure.Testing.C3AbandonedFactoryUpgradeTests.RuntimeArtHasBFeedAndNonBlockingDecay`

- Original error: Expected: not null But was: null
- Investigation: The old layout, name, position or traversal assertion conflicts with the current scene; whether superseded or a real passage/collider defect remains unverified.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `SDGAdventure.Testing.EcoEnergyPickupPlayModeTests.QuestionOptionsChangeBetweenLevelEntries`

- Original error: Re-entering Level03 must change the displayed option order Expected: not equal to "201" But was: "201"
- Investigation: Concurrent runs shared the last-order PlayerPrefs key; interference is possible. Sequential V15 passed, but a root cause is not proven.
- Sequential V14 result: Passed.
- Changed file / disposition: `Assets/_Modules_/05_Testing/PlayMode/EcoEnergyPickupPlayModeTests.cs` now restores the key after this test; other scene-loading tests and concurrent processes remain unisolated. Cause unresolved.
- V15 full retest: Passed ([XML](V15_Final_PlayMode.xml)).

## `M7SharedPlayerTests.PlayerInputServiceTests.Level03MenuPausesResumesAndUsesExistingTransitions`

- Original error: Expected: Locked But was: None
- Investigation: The observed OS cursor lock is None in batch mode. Requested cursor state has not been separated from OS state.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

## `M7SharedPlayerTests.PlayerSettingsTests.ProneActionRespondsOnlyToCurrentKey`

- Original error: Expected: 1 But was: 0
- Investigation: Synthetic events were suppressed under the original focus configuration. Configuring both focus settings together makes the original key and rebind assertions pass; the decisive setting remains unisolated.
- Sequential V14 result: Failed.
- Changed file / disposition: Assets/_Modules_/05_Testing/PlayMode/M7/PlayerSettingsTests.cs; test-only focus configuration, targeted and full V15 pass.
- V15 full retest: Passed ([XML](V15_Final_PlayMode.xml)).

## `SDGAdventure.PlayerControlTests.PlayerControlPlayModeTests.RepeatedPauseSettingsCyclesRestoreGameplayState`

- Original error: Expected: 2 But was: 3
- Investigation: Global Canvas count differs: 3 in the baseline and 4 in the separate graphics run. Ownership and residual objects remain unisolated.
- Sequential V14 result: Failed.
- Changed file / disposition: None; unresolved.
- V15 full retest: Failed ([XML](V15_Final_PlayMode.xml)).

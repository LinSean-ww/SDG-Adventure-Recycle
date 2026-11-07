# Baseline comparison

Phase 2 added one explicit cursor-state separation test. Before the final V4 test updates, both the ordinary full run and the fixed-seed 71 run with one repeat reported 217 tests, 201 passed, 13 failed and 3 skipped; their per-test outcomes matched. After the final V4 and transition changes, the final fixed-seed run with one repeat reported 217 tests, 205 passed, 9 failed and 3 skipped. Of the 46 historical failures, 37 passed in that final run. See [Validation.md](Validation.md) for the runs and their exact scope.

| Version and run | Commit | Tests | Pass | Fail | Skip | Evidence |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Historical V14.1 PlayMode | Before final input-state change | 214 | 165 | 46 | 3 | [XML](Historical_V141_PlayMode.xml) |
| V14 PlayMode, sequential | `ae1830661403705772056c2600bfedce16398416` | 214 | 165 | 46 | 3 | [XML](Baseline_V14_PlayMode_Sequential.xml) |
| V14.1 PlayMode before changes | `51d41af60ccf9f47e4508e1bae99a8c3d14d1cb6` | 216 | 166 | 47 | 3 | [XML](Baseline_V141_PlayMode.xml) |
| V15 PlayMode | V14.1 + test change | 216 | 168 | 45 | 3 | [XML](V15_PlayMode.xml) |
| V15 final PlayMode | V14.1 + two test changes | 216 | 167 | 46 | 3 | [XML](V15_Final_PlayMode.xml) |

Each of the 214 names in the V14 run is present in V14.1. The two new names are `C3V141ReloadTests.ReloadBlocksShotsAndTransfersAmmoOnce` and `SwitchingWeaponCancelsWithoutTransferringAmmo`, both passed. The 46 failures in the sequential V14 XML are also failures in the V14.1 XML. The additional V14.1 failure, `QuestionOptionsChangeBetweenLevelEntries`, passed both sequential V14 and V15 runs, but the initial V14.1 execution overlapped the first V14 run and both shared PlayerPrefs. This is not proof of a version regression or a fix.

The historical XML was recovered from `C:\Users\h7796\AppData\Local\Temp\SDG_V141_PlayMode.xml`. Its 46 failed test names all failed in the sequential V14 run and in the new V14.1 HEAD baseline. This establishes matching outcomes, not identical root causes. [FailureTriage.md](FailureTriage.md) records those 46 and the extra HEAD failure. The initial overlapping V14 result is also retained as [XML](Baseline_V14_PlayMode.xml).

Both PlayMode versions used Unity 2023.1.22f1, `-batchmode -nographics -runTests -testPlatform PlayMode`, with independent worktrees and Libraries. They did not use isolated PlayerPrefs. The sequential V14 and V15 runs did not overlap another test run. V14.1's original full baseline did overlap; consequently shared-state-sensitive comparisons are qualified. Tests that happened to have the same result are not automatically proven to have the same cause.

The final run newly failed the close-wall third-person rifle clearance assertion after two previous V15 full passes; an isolated rerun passed. This outcome is unstable and its cause is not established.

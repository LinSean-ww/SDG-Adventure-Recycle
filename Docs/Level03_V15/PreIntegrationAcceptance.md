# Level03 V15 — pre-integration acceptance

**Decision: do not enter formal integration yet.** The final complete PlayMode run has three unresolved failures, including an observed spawn collision and an unstable visual reference. The user-accepted V14.1 gameplay has not been re-accepted interactively after these changes.

## Evidence

Unity 2023.1.22f1; one Editor process at a time; graphics-enabled batch mode; test output redirected outside the project. `RunIsolatedTests.ps1` backed up and verified restoration of the two project PlayerPrefs keys after each run.

| Run | Total | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Final EditMode | 14 | 14 | 0 | 0 | [XML](PreIntegration_Final_EditMode.xml) |
| Final PlayMode | 217 | 211 | 3 | 3 | [XML](PreIntegration_Final_PlayMode.xml) |
| Focused factory, chip, rifle, reload and spawn run | 17 | 16 | 1 | 0 | [XML](PreIntegration_Targeted_Retry.xml) |
| Old lighting reference, graphics enabled | 1 | 0 | 1 | 0 | [XML](PreIntegration_Lighting.xml) |
| Candidate current-scene lighting reference, graphics enabled | 1 | 0 | 1 | 0 | [XML](PreIntegration_Lighting_Reference.xml) |

The three skipped tests remain the existing V8-only geometry tests whose XML directs readers to V9 coverage; no skips were added. The final run used the ordinary test order, not a further random-order or repeat run. The Phase 2 seeded/repeated results in [Validation.md](Validation.md) predate these acceptance changes.

## Review of the previous nine failures

1. **Upper chip:** the historical position was superseded by the V12 workbench position. The updated position and unobstructed ray test passed; real F-key collection, once only, passed in both FPS and TPS. The previous stored scene was not moved.
2. **Two stairs and walkway:** the old M2 route still fails because it starts at retired coordinates. The existing `BothStairsCanCarryACharacterControllerOntoTheSecondFloor` passed with the real player capsule traversing both current stairs and the rear walkway without teleporting between them. The unchanged M2 failure remains an automated acceptance blocker; the alternative test is evidence of current route traversal, not proof of every possible route.
3. **Spawn movement:** the production controller requests a forward move at speed 4. In the failing step the player moves from approximately `(-0.82, 0.63, 18.95)` to `(-1.06, 0.87, 18.95)`, with contacts on `POLLUTION pressure controller` and `WASTEWATER treatment grated foundation`. The old assertion requires x below `-1.06665528`, while actual x is `-1.05499983`. This identifies the contacts but does not prove whether continued play can safely navigate around them. It is a gameplay acceptance blocker until the spawn path is inspected in the Game View and any minimal fix is separately verified. No collider or factory object was moved.
4. **Rifle sight:** at runtime only, the baked display's old `Sight` is retained but hidden and the same `C3VisualBuilder.OpenRifleSight` used by the held weapon creates the four current frame pieces. Its material owner is disposed with the workstation owner. The saved scene and baked object manifest remain intact. The visible renderer structure/shadow comparison, V13 reload/rebind and V14.1 reload tests passed. Interactive visual verification remains advisable.
5. **V5 infeed:** the floor-height V8 belt was replaced by the current elevated P2 route. The revised test checks physical surfaces and joints, the upper wall aperture and enabled doorway colliders. It passed.
6. **V6 main belt:** the revised test checks current connected surfaces and actual main-belt bounds; it passed.
7. **V7 B/C boundary:** the current accepted design uses a nonblocking PVC curtain and solid surrounding safety structures rather than the old opaque shutter. The updated test verifies these and the elevated connected infeed; it passed.
8. **V7 aisle:** the revised test verifies both sets of 22 enabled stair treads, the west stair's location outside the central aisle, current deck links and absence of the retired invisible ramp; it passed.
9. **Lighting reference:** the old image visibly depicts the earlier factory layout, whereas the current image shows the saved V15 factory. A candidate current-scene reference is saved as `Assets/_Modules_/05_Testing/ReferenceImages/M6_V15_CurrentDirectC3.png`; it is provisional, not a user-approved visual baseline. A second graphics-enabled capture differed in 10,309 pixels, and the final run differed in 7,580, both exceeding the unchanged limit of 36. The visible green glow changed between captures, but its exact contribution is not isolated. The test fails before its later selection-transition/GI assertions, so those assertions were not verified. Establish a reproducible visual fixture and obtain visual review before treating the new reference as authoritative. This remains an integration blocker.

The first attempt at the rifle runtime update destroyed the authored sight and caused the V13 manifest test to fail; the final implementation instead keeps that object inactive. The subsequent focused and final full runs passed the V13 manifest test. Earlier Phase 2 PlayerPrefs restoration failure and the possibility of changed pre-existing preferences are recorded in [Validation.md](Validation.md); the current acceptance runs reported verified restoration.

No main branch or existing PR #7, #8 or #9 was changed. Formal integration was not started.

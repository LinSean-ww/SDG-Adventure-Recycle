# Level03 V15 — stability investigation (incomplete)

See [PreIntegrationAcceptance.md](PreIntegrationAcceptance.md) for the latest pre-integration decision and final test results; earlier sections below describe the preceding phases.

Base: `51d41af60ccf9f47e4508e1bae99a8c3d14d1cb6` (V14.1). This is a draft investigation, not an acceptance or a claim that all 46 historical failures have been resolved.

The prone binding test temporarily configures Input System focus handling for its synthetic keyboard events and restores both settings afterward. It still checks the original key, the old key after rebinding, and the new key. The option-order test now restores the prior `SDG_C3_LastPuzzleOptionOrder` value (or absence) in `finally`. Other tests that load the scene can still modify that key, and separate Unity processes still share it. No gameplay code, scene, factory geometry or materials were changed.

See [BaselineComparison.md](BaselineComparison.md), [FailureTriage.md](FailureTriage.md), and [Validation.md](Validation.md) for raw-result links, limitations and outstanding failures. The historical `SDG_V141_PlayMode.xml` was recovered from the user-specified temporary path and retained as [Historical_V141_PlayMode.xml](Historical_V141_PlayMode.xml).

## Phase 2

`PlayerCursor` records requested lock state separately. Normal gameplay continues to read the OS cursor state; an Editor-only simulation allows PlayMode tests to exercise the input paths without a window. UI context and `GameplayEnabled` checks remain in place. The input fixtures restore the cursor simulation and Input System focus settings. Pause tests check that UI context still prevents gameplay even when simulated cursor lock is forced.

Legacy V3/V4 checks now exercise the saved V13 treads, landing, connected belt surfaces and rollers instead of removed V8 names and coordinates. The close-wall test waits for elapsed game time rather than six frames, retaining its grip and clearance assertions. Two tests were made order-independent by detaching pre-existing menu subscriptions during the test and comparing a module's prior result rather than assuming it is null. No scene, mesh, material or factory geometry was edited.

The baked assembly display and the runtime weapon still have different sight geometry; their existing equality assertion remains unresolved. Other outstanding scene and lighting failures are tracked in [FailureTriage.md](FailureTriage.md). No new interactive acceptance was performed.

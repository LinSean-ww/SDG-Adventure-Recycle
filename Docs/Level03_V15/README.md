# Level03 V15 — stability investigation (incomplete)

Base: `51d41af60ccf9f47e4508e1bae99a8c3d14d1cb6` (V14.1). This is a draft investigation, not an acceptance or a claim that all 46 historical failures have been resolved.

The prone binding test temporarily configures Input System focus handling for its synthetic keyboard events and restores both settings afterward. It still checks the original key, the old key after rebinding, and the new key. The option-order test now restores the prior `SDG_C3_LastPuzzleOptionOrder` value (or absence) in `finally`. Other tests that load the scene can still modify that key, and separate Unity processes still share it. No gameplay code, scene, factory geometry or materials were changed.

See [BaselineComparison.md](BaselineComparison.md), [FailureTriage.md](FailureTriage.md), and [Validation.md](Validation.md) for raw-result links, limitations and outstanding failures. The historical `SDG_V141_PlayMode.xml` was recovered from the user-specified temporary path and retained as [Historical_V141_PlayMode.xml](Historical_V141_PlayMode.xml).

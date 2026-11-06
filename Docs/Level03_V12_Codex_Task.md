# Level03 V12 / Codex 修正任務（2026-11-05）

**Start:** repository `LinSean-ww/SDG-Adventure-Recycle`, based on latest `main` after PR #4, baseline SHA `737c3e5eb7b7b6df77570325203e0436961e6377`.
**Branch:** `codex/level03-v12-visual-navigation-vfx-20261105` (created for this task; do not modify main).
**Unity:** 2023.1.22f1; scene `Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity`.
**Existing Windows verification:** seven PlayMode suites 55/55 PASS, Console 0 Error (user reported, not tested by Codex).
**Screenshots:** eight annotated/source screenshots are in `Level03_Codex_References.zip` provided separately in ChatGPT; ask user to attach the ZIP to your Codex task if not visible.

## Five required fixes

1. **Editor-versus-Play scenery:** The same C_Main looks like an old dark factory before Play, but very different bright/industrial runtime meshes after Play. First compare matched cameras and lighting; trace runtime builders `C3ReferenceFactoryEnvironment.Awake/ReplaceMismatchedLegacyArt`, `C3RecyclingLineGeometry.Awake`, `C3V3Circulation.Awake`, and relevant roots (`C_OnlyFactory`, `C_Factory_Art`, `M3_Treasure`, `C_CollisionProxies`). Make the authored/editor preview show the intended final appearance without double generating, leaking resources, or destroying colliders/gameplay. Do not misdiagnose different camera angles as different scenes.
2. **B entrance stairs:** A sloped segment of East/B stair blocks visibility of the B entrance sign. Rebuild the affected segment as a real horizontal walkway/landing with necessary stairs and railings, along the red mark in screenshot 04. Investigate `C3V3Circulation.BuildStair(EastStairX, "B_EAST")`. Do NOT simply hide or move the sign. Keep walkability, collision, all 1F/2F connections and A-side structure.
3. **Filter module + fuse clipping:** The filter module at the machine/platform edge and fuse embedded in the sorter shelf/railing must be repositioned in their valid locations, not float or clip. Identify actual gameplay objects and their visual children; preserve F interaction, activation/sorting, hints, LOS, once-only rewards and restart behavior.
4. **Sorter / camera clipping:** Reproduce screenshot 07 near `M3_Treasure/C3_P2_RecyclingLine/SORTER_MAIN_BODY_2/FRONT_TOP`. Determine actual Player Controller penetration vs camera clipping vs incomplete collider/mesh match. Fix locally; preserve conveyor functioning and character navigation. A `C3 movement blocked by` diagnostic alone is **not** proof of clipping.
5. **Weapon VFX:** `EcoQuantumWeaponView.OnShot(hitPosition)` currently uses ~0.13s line effect and muzzle light. Implement a clearly visible, original cyan/orange quantum tracer with luminous core, fading tail, unmistakable muzzle flash, optional impact spark, correct hit point/path in first & third person. Preserve existing hitscan, damage, ADS, gun handling, Q/E, near-wall weapon retraction and FPS→TPS pose reset from `6ea4c80`. Reuse/pool VFX resources, cleanup on reload, prevent material/GO leaks; Unity 2023.1.22f1 render pipeline compatible.

## Constraints and acceptance
- **Only Level03 and directly necessary shared files**. Do not change other level folders, unrelated UI/prone fixes, mission rewards, inventory, or ProjectSettings.
- Never address tests by deleting or weakening assertions or adding arbitrary waits. Add targeted geometry, pickup, VFX, lifecycle & scene parity tests. Keep seven existing suites and all prior 55 passing. Do not claim testing unless executed.
- Five separate reviewable patches/commits preferred, with root cause, affected files, test cases and visual checklist. Before and after scene/camera match screenshots and manual tests at 1280×720.
- Verify stairs pass standing/prone and camera/LOS, pickup F access, sorter route, tracer looks good, 100 shots no lingering VFX, 3 scene reloads no duplicate/stray objects.
- In Codex Cloud, if Unity Windows unavailable, label **Unity compile / PlayMode / visual test NOT RUN** and prepare patch and step-by-step Windows test instructions.
- Do not commit directly to or push to `main`; no automatic PR merge. Do not overwrite unrelated user work, reapply A-I patches, or force push.
- Deliver actual source/scene edits, diff/patch, precise root causes and Windows validation instructions; not merely a plan.

Important: Codex must receive the eight screenshots as attachments, as GitHub source alone does not include them.

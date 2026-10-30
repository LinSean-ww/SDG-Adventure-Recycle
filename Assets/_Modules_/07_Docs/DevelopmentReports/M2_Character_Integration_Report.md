# SDG Adventure｜M2 角色模型、骨骼動畫與 M2-H 攝影機整合報告

日期：2026-10-27（Asia/Taipei）  
Unity：2023.1.22f1  
整合方案：C（保留 C3 玩家控制器與互動系統）

## M2-A：安全檢查

- 專案根目錄為 `C:\Users\h7796\Desktop\Project\Codex Work\Unity\SDG Adventure`，`ProjectVersion.txt` 為 2023.1.22f1。
- `CodexImport` 的 `fk.fbx`、`PlayerMove.cs`、`CameraLook.cs`、`BoneController.cs` 均存在。三份 C# 原檔為 Big5 中文編碼；未改寫、未移動、未匯入三份原始腳本。匯入 FBX 與來源 SHA-256 相同：`9991CB57C29EC09E17860A238CADD4FA8221BB754CDA145E9690D0ECF7BA1D61`。
- 開始時 Git 已有未提交的 `C3SceneBugFixTool.cs`、`C_Main.unity` 修改，以及 C3 UpperSupport 工具、測試、備份等未追蹤檔。這些既有成果未清除；未執行 commit、push、reset 或 clean。
- M2 修改前備份：`DevelopmentReports/M2_Character_Backups/20261027_104309_1de93587/`，含正式 `M2_Player.prefab`、`C_Main.unity` 及各自 `.meta`，逐檔 SHA-256 核對一致。
- M2-H 修改前備份：`DevelopmentReports/M2H_Camera_Backups/20261027_112340_f8c5eeb1/`，含 M2 完成狀態的 `M2_Player.prefab`、原 `C3FirstPersonController.cs`、隔離 Prefab、驗證工具／測試及各自 `.meta`，逐檔 SHA-256 核對一致。
- 匯入目的位置沒有同名 `fk.fbx`；沒有覆蓋 `ONLYfactory.fbx` 或其他資產。

## M2-B：模型匯入

- **PASS**：`fk.fbx` 以原始位元組複製到 `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Models/fk.fbx`，Unity ModelImporter 產生 `.meta`、GUID `0c04f3ef165e93742ae4fee01b08e661`，可載入為 GameObject。
- **PASS**：僅 `fk.fbx` 的 ModelImporter 設為 `bakeAxisConversion=True`（`.meta` 中 `bakeAxisConversion: 1`）；`globalScale=1`、`useFileScale=True`、`fileScale=0.01`。未修改其他 FBX。
- **PASS**：匯入模型根 `fk` 為位置零、旋轉零、比例 1。SkinnedMeshRenderer `people` 的匯入 bounds 高約 12.86 單位；視覺子物件設等比 `0.13999872`，對應約 1.8 單位角色高度，局部位置零、Y 軸旋轉 -90°。`BODYBONE` 自身局部比例 100；故不可只看 FBX 根比例推斷場景身高。
- **PASS**：Unity 匯入後有 `BODYBONE/downbone/topbone/head`、`LtopHand`、`RtopHand`、`BODYBONE/Ltopleg`、`BODYBONE/Rtopleg`。原 BoneController 的四條 `Transform.Find` 路徑均能在 `fk` 根下找到。`people` 為 SkinnedMeshRenderer（11 根骨骼）。沒有觀察到 Animator 或 AnimationClip 子資產。
- **PASS（隔離靜態畫面）**：X、Z 軸觀察截圖確認模型可渲染，修正視覺子物件橫向朝向。原點、比例與地板的靜態對齊已檢查。
- **NOT TESTED**：正式 C3 關卡中各種動作、光線及近距離視角下的完整視覺品質；仍需人工 Editor Play Mode 檢查。

## M2-C：來源腳本分析

- `PlayerMove.cs`：以 `CharacterController`、舊版 `Input.GetAxis` 與 `Input.GetButtonDown("Jump")` 控制平移、跳躍、重力。與既有 C3 控制器重複，未匯入或掛載。
- `CameraLook.cs`：靈敏度 2；原設定的 `playerBody=fk`、`Head=head`；相機 local X 俯仰、`[-60°,48°]` 限制、本體 Y 轉向、head X 轉向。直接掛載會與 C3 現有滑鼠旋轉重複。M2-H 改將這些行為整合到原控制器：Player Body 是 `M2_Player` 根 Transform，Head 是實際 `fk/BODYBONE/downbone/topbone/head`。原腳本未掛載。
- `BoneController.cs`：四肢路徑吻合，但以 WASD 按住判斷走路，碰牆也會播放動作；待機與移動均寫入固定 local Euler 角，會覆蓋匯入姿勢。原腳本未匯入或掛載。
- 新增 `C3BoneController.cs`：只控制四肢；以 `CharacterController.velocity` 的水平實際速度決定動畫；骨骼轉動乘上匯入時的 localRotation，保留原始姿勢。不控制玩家根物件、Camera 或 head。中文註解記錄相容原因。隔離及正式 Prefab 均只掛此一骨骼腳本。

## M2-D：Prefab 整合

原 `M2_Player`：根 Transform、CharacterController、`C3FirstPersonController`、`C3PlayerInteractor`，子物件 `Main Camera`（Camera、AudioListener）。

整合後：

```text
M2_Player  ← Player Body；保留 CharacterController、C3FirstPersonController、C3PlayerInteractor
├─ Main Camera  ← 保留 Camera、AudioListener；C3 控制器 viewTransform
└─ fk  ← 新增視覺子物件，scale≈0.14、local Y=-90°、C3BoneController
   └─ BODYBONE/downbone/topbone/head  ← C3 控制器 headBone
```

- **PASS**：先建立 `Assets/Tests/M2Character/M2_Player_CharacterTest.prefab` 與 `M2_CharacterIsolation.unity`；Unity 驗證後才修改正式 `M2_Player.prefab`。正式 Prefab GUID 及原始控制器、互動器、Camera、AudioListener 元件均保留。
- **PASS**：正式 `C_Main.unity` 及其 `.meta` 的 SHA-256 與 M2 前備份相同，沒有修改正式場景；場景引用更新後仍解析為一個 C3 玩家、一個 Camera、一個 AudioListener、零 Missing Script。
- **PASS**：M2-H 的 `headBone`、`viewTransform`、靈敏度 2、俯仰 `-60°～48°` 均經 Unity SerializedObject 檢查。沒有 `CameraLook` 元件；根物件左右轉、原 Camera 上下轉、head 在 LateUpdate 依玩家世界右軸跟隨一次，並保留 FBX 原始骨骼旋轉。Camera 不在 head 子階層內。

## M2-E／M2-H：驗收結果

| 項目 | 狀態 | 證據與界限 |
|---|---|---|
| Unity 匯入與 C# 編譯 | PASS | Unity 2023.1.22f1 批次匯入與最終 Test Runner 編譯完成，無 `error CS`。 |
| FBX、Bake Axis Conversion、head／四肢路徑 | PASS | `M2_Character_Inspect_Elevated.log`、`fk.fbx.meta`。 |
| Prefab 引用、單一控制器／Camera／AudioListener、Missing Script | PASS | `M2H_Camera_Wiring.log`，C3 場景檢查 1／1／1、Missing Script 0。 |
| 待機手臂、行走四肢、碰牆後停止走路動畫 | PASS | `M2H_Character_GroundAdjusted.xml` Play Mode 測試。 |
| 玩家根左右轉、Camera 上下轉、-60°／48° 限制、head 跟隨、轉向後移動方向 | PASS | 同一 Play Mode 測試以確定的視角輸入呼叫控制器邏輯，並檢查場景 Transform。這不是實體滑鼠操作。 |
| 跳躍、重力、落地碰撞 | PASS | Play Mode 以既有控制器 `TickMovement` 及測試地板驅動；先用額外向下 `CharacterController.Move` 排除 skin width 邊界。這不是實體鍵盤操作。 |
| C3 關卡載入、拾取／解謎狀態、結算、重開、返回選關 | PASS | `M2H_C3Flow_PlayMode.xml` 既有流程測試 1／1 通過，透過公開關卡方法驅動。 |
| 中立、抬頭 60°、低頭 48° 的隔離第一人稱靜態遮擋 | PASS | Editor 截圖；低頭畫面與隱藏模型後的對照 PNG SHA-256 相同。 |
| 實際滑鼠左右／上下、WASD、空白鍵、E 鍵與 UI 點擊 | NOT TESTED | 批次 Play Mode 測試未注入實際桌面鍵鼠事件。 |
| 正式 C3 場景中跑動、蹲下、轉頭時的頭部遮擋、抖動與視覺手感 | NOT TESTED | 需人工 Unity Editor Play Mode／Game 視窗驗收。 |
| Animator 混合與內建動畫衝突 | NOT TESTED | 目前 FBX 未觀察到 Animator／AnimationClip；若後續加入動畫，須重新驗證。 |

曾有初版測試因批次影格取樣過快、`WaitForEndOfFrame` 在無圖形模式等待，以及測試地板與 CharacterController skin width 的接地邊界失敗；已修正測試流程。最終 `M2H_Character_GroundAdjusted.xml` 為 3／3 PASS，`M2H_C3Flow_PlayMode.xml` 為 1／1 PASS。沒有為通過測試而修改 C3 移動、跳躍、重力或碰撞計算。

## M2-F：變更與還原

- 新增：`Models/fk.fbx`、`.meta`；`Scripts/C3BoneController.cs`、`.meta`；`Assets/Editor/M2CharacterValidation.cs`、`.meta`；`Assets/Tests/M2Character/` 隔離 Prefab／場景及 `.meta`；`Assets/Tests/PlayMode/M2CharacterIntegrationTests.cs`、`.meta`；本報告、測試 XML／log、截圖與備份。
- 修改：正式 `Prefabs/M2_Player.prefab`；M2-H 最小修改 `Scripts/C3FirstPersonController.cs`；隔離測試 Prefab 隨 M2-H 更新。其 `.meta` 與 GUID 保留。
- 未修改的受保護檔：`C_Main.unity`、`ModuleCoordinator.cs`、`ModuleRegistry.cs`、`ModuleDefinition.cs`、`C3ModuleAdapter.cs`、`C3PlayerInteractor.cs`、ProjectSettings、Packages、其他 FBX 與關卡。`C_Main.unity` 在任務前已有未提交變更，與本次備份雜湊一致。
- 還原 M2-H 相機行為：關閉 Unity 後，從 `DevelopmentReports/M2H_Camera_Backups/20261027_112340_f8c5eeb1/` 將 `C3FirstPersonController.cs`、`M2_Player.prefab` 及各自 `.meta` 複製回原相對路徑；這會保留 M2 模型與骨骼整合狀態。
- 還原整個 M2：關閉 Unity 後，從 `DevelopmentReports/M2_Character_Backups/20261027_104309_1de93587/` 還原原 `M2_Player.prefab` 及 `.meta`，再移除本次新增資產及其 `.meta`。如需還原場景，該備份亦有原 `C_Main.unity` 與 `.meta`。還原前核對目前使用者變更；未執行還原。

## M2-G：結論

- FBX 匯入、相容骨骼動畫與正式 Prefab 整合：**已完成**。
- M2-H 攝影機行為：**已整合至現有 C3 控制器**；沒有第二套 CameraLook、Camera 或 AudioListener。
- 母專案模組接口：**未修改**；既有 C3 流程測試通過。
- 可進入 **M3 人工驗收**；M3 尚未執行。請在 Unity Game 視窗實際操作滑鼠、WASD、跳躍、蹲下、E 互動與解謎 UI，並檢查動態頭部遮擋和動畫視覺效果。
- 本階段到此停止；未執行 Git commit 或 push。

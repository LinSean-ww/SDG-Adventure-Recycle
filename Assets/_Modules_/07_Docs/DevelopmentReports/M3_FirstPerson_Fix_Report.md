# M3 第一人稱視角修正報告

## 原因與重現

- `M2_Player` 的 `Main Camera` 位於角色頭部附近，`fk` 的 `people` SkinnedMeshRenderer 原本在 Default layer，Camera 的 Culling Mask 包含該 layer。角色自身因此可以進入第一人稱畫面。
- C3 靜態渲染比較中，將測試視點降至本地 Y=0.85、pitch=-60° 時，模型啟用和停用的畫面相差 18,747/230,400 像素。使用者另外確認站立正視、低頭／蹲下及轉頭／移動都會遮擋；這些人工狀態沒有被靜態渲染逐一重現。
- `C3BoneController` 只控制肢體；攝影機與 head 骨骼由 `C3FirstPersonController` 控制，未發現第二套攝影機旋轉邏輯。

## 修正

先在 `Assets/Tests/M2Character/M2_Player_CharacterTest.prefab` 隔離驗證，通過後套用至正式 `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Prefabs/M2_Player.prefab`：

1. `fk` 階層設為 Unity 內建 Ignore Raycast layer 2，原有 Main Camera 的 Culling Mask 排除 layer 2。其他包含 layer 2 的攝影機仍可看到完整模型。
2. 原 `people` SkinnedMeshRenderer 保留顯示，停止自身投影；在 `fk` 下新增 `M3_FirstPersonShadow`，使用相同 mesh 與 11 個相同骨骼，設定為 Shadows Only 並放在 Default layer，以保留第一人稱陰影。
3. Camera、AudioListener、CharacterController、C3FirstPersonController、C3PlayerInteractor、C3BoneController、head 參考和 C3 模組接口沒有替換或修改。

## 驗證

| 項目 | 結果 | 證據或限制 |
|---|---|---|
| 隔離場景第一人稱模型遮擋 | PASS | 站立 Y=1.5 與低視點 Y=0.85，各 5 個 pitch，共 10 組；模型顯示開關的畫面差異均為 0 像素。 |
| 正式 C3 場景第一人稱模型遮擋 | PASS（靜態渲染） | 同上 10 組均為 0/230,400 差異像素，見 `M3_Formal_FirstPerson.log`。 |
| 第三人稱完整模型 | PASS（靜態渲染） | 隔離場景外部視角模型差異 12,714 像素；正式 C3 場景 11,049 像素。 |
| 第一人稱陰影 | PASS（隔離場景） | 隔離場景 shadow-only 對照 2,173 差異像素；正式 C3 場景當前光照與機位沒有量到差異，該場景的陰影外觀待人工確認。 |
| 骨骼連結、單一 Camera/AudioListener、Interactor | PASS | 正式場景 11 個代理骨骼與原 renderer 相同，Camera 1、AudioListener 1，見 `M3_Formal_Rig.log`。 |
| 移動方向、頭部轉動與角度限制、跳躍落地、肢體動畫 | PASS（PlayMode 自動化） | `M3_Regression_PlayMode.xml`，相關 3 個測試通過。 |
| C3 載入、重啟、返回選關、重新進入 | PASS（PlayMode 自動化） | `M3_Regression_PlayMode.xml`，流程測試通過。 |
| 人工 WASD、滑鼠、E 鍵、移動時遮擋、動態模型外觀 | NOT TESTED | 需要 Unity Game 視窗實際操作。 |
| Game 視窗 Console Error | NOT TESTED | Batchmode 測試無 C# 編譯錯誤，Game 視窗 Console 待人工查看。 |

## 外觀與限制

- 靜態正面、側面、背面影像可見完整低多邊形人物與四肢；本次未改模型比例、Y=-90° 朝向或 0.14 縮放。靜態影像不能代替動態外觀人工驗收。
- 內建 Ignore Raycast layer 2 目前未用於 C3 其他物件；模型自身沒有碰撞器，玩家碰撞由根物件 CharacterController 保持。其他攝影機若也排除 layer 2，就看不到人物，須依該攝影機用途檢查 Culling Mask。
- 本次沒有修改 `C_Main.unity`、其 `.meta`、Prefab `.meta`、任何其他關卡、控制腳本或 ProjectSettings，也未 commit/push。

## 檔案與備份

- 修改：正式 `M2_Player.prefab`、隔離測試 `M2_Player_CharacterTest.prefab`。
- 新增：`Assets/Editor/M3FirstPersonDiagnostics.cs` 及 Unity 產生的 `.meta`；本報告與 `DevelopmentReports/M3_*` 診斷影像／記錄。
- 修改前備份：`DevelopmentReports/M3_Fix_Backups/20261027_114758_8394e06c/`，包含上述既有 Prefab 與 `.meta`，以及正式場景與 `.meta`。

M3 具備重新人工驗收條件；不進入 M4。

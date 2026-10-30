# SDG Adventure M6｜C3 視覺回歸調查與修復

日期：2026-10-24（Asia/Taipei）  
範圍：只修改 `SDG Adventure_M6_TEST`。原始 `C3_TreasurePuzzle`、正式 `SDG Adventure` 與 `ONLYfactory.fbx` 未修改。真實 Game 視窗的視覺結果仍待使用者人工驗收。

## 1. 結論與可重現證據

根因是 **C3 場景載入後天空盒環境光探針尚未生成，`RenderSettings.ambientProbe` 為零**。此場景使用 Skybox 環境光、沒有 Lighting Data Asset 或 Lightmap；工廠牆面與天花板大量依賴間接環境光。Unity 直接載入 C3 或從 M6 選關入口載入後，未更新前 `ambientProbe` 的 RGB L00 均為 `0,0,0`。在同一場景、同一鏡頭只執行一次 `DynamicGI.UpdateEnvironment()`，RGB L00 變為 `0.1803,0.2257,0.3069`，黑色牆面與不規則暗區在自動渲染截圖中消失。這是針對環境光更新，不增加燈、不改材質、場景或 FBX。

| 800×450 同一 C3 鏡頭 | Ambient L00 RGB | 平均亮度（0–255） | 亮度低於 20 的像素 |
|---|---|---:|---:|
| 修復前：直接載入 C3 | `0,0,0` | 66.34 | 67.01% |
| 修復前：從選關入口載入 C3 | `0,0,0` | 66.34 | 67.01% |
| 原始 C3 的隔離參照副本：新匯入後直接載入 | `0,0,0` | 66.34 | 67.01% |
| 同一場景呼叫 `DynamicGI.UpdateEnvironment()` 後 | `0.1803,0.2257,0.3069` | 153.06 | 0.08% |
| 修復後：直接載入及從入口載入 | `0.1803,0.2257,0.3069` | 153.06 | 0.08% |

前後截圖：`VisualReference/BeforeFixDirectC3.png`、`VisualReference/AfterFixDirectC3.png`、`VisualReference/AfterFixFromSelection.png`。原始專案的隔離參照副本在**測試副本內**建立並執行 Unity Editor API；原始專案本身只讀，參照副本已在保存證據後移除。參照副本的報告為 `VisualReference/ReferenceEditorProbe.txt`、`VisualReference/ReferenceRuntimeProbe.txt`；原始比對日誌與 XML 為 `ReferenceProbeUnity.log`、`ReferenceVisualRuntime.log`、`ReferenceVisualRuntimeResults.xml`。整合副本修復後的報告為 `VisualProbe.txt`、`VisualRuntime/RuntimeProbe.txt`、`VisualFixAcceptanceResults.xml`。

原始獨立專案在使用者先前的 Editor 工作狀態顯示正常；該狀態可能已有先前生成的環境光資料。這是推論，不當作已證實的原始專案狀態。可直接證實的是：相同來源資產的全新隔離匯入也重現零探針與黑畫面，且環境更新單獨消除該現象。

## 2. 比對結果與排除項目

| 項目 | Unity／檔案證據 | 判定 |
|---|---|---|
| `GraphicsSettings.asset`、`QualitySettings.asset` | 原始 C3 與測試副本 SHA-256 完全相同；Unity API 均為 Built-in、Ultra、Shadows All。 | 無差異。 |
| `ProjectSettings.asset` | 逐行比較只見專案名稱、產品 GUID、Cloud 專案 ID 等識別欄位差異；色彩空間與 Rendering Path 相同。 | 未見渲染設定差異。 |
| C3 場景與 Lighting Settings | `C_Main.unity` SHA-256 相同；Skybox 環境光強度 1，Default-Skybox，無 Lighting Data Asset、無 Lightmap；一盞 Directional Light 與一盞設備 Point Light，均為 Realtime 且無陰影。 | 場景設定相同；零環境光探針是執行時狀態。 |
| `ONLYfactory.fbx` 與 importer | FBX 與 `.meta` SHA-256 完全相同，GUID `96a581a8f5e930c4a9b52806b4597863` 保留；Importer 的材質模式、法線匯入、切線、UV 與縮放設定相同。 | 無 FBX／Importer 修改或遺失。 |
| 材質、Shader、Renderer | 七個獨立 `.mat` SHA-256 相同；Unity API 對兩邊新匯入場景輸出逐行一致：35 個 Renderer，0 個空材質槽，0 個不支援 Shader，0 個近黑色 `_Color`；工廠內嵌材質均為 Standard，基色約 `(0.906,0.906,0.906)`。 | 排除材質丟失、黑色材質與 Shader 不支援。 |
| 法線、背面剔除、幾何 | 原 FBX 與 importer 完全相同；已知 FBX 自交多邊形警告仍存在。相同鏡頭的暗區只因環境光更新而恢復，無模型改動。 | 不支持 FBX 幾何變更是此次大面積黑色主因；其他角度的幾何瑕疵仍須人工檢查。 |
| 資產引用 | 原 C3 Runtime 57 檔與基準一致，除了 M6 原已授權的 `C3CompletionScreen.cs` 轉接；32 個來源 `.meta` GUID 全部相同。先前 Unity 依賴檢查、Missing Script 與 FBX／Prefab 匯入均 PASS。 | 未發現遺失引用。 |

逐檔複核見 `VisualFixStaticValidation.txt`：來源基準雜湊差異 0、移植後未改動資產雜湊差異 0、GUID 差異 0、Graphics/Quality 雜湊差異 0。另將原始 C3 的 M6 完整清單 98 檔重新與 SHA-256 基準比對，差異 0。場景 SHA-256 仍為 `ddcda92b04cc60062b9f2cadf6080c9ab13da11726de0bbb29b9d5ca962192e6`；FBX 仍為 `bc6fb179fffb74fd7fa11434c28d53e17bb2413b3f7a9032fbb309440e4a2058`。

## 3. 最小修復與前後差異

1. 修改 `Assets/_Modules_/C3_TreasurePuzzle/Adapter/C3ModuleAdapter.cs`：驗證 C3 場景僅有一組 Player／Camera／AudioListener 後，在每次關卡載入時呼叫一次 `DynamicGI.UpdateEnvironment()`。其餘 C3 玩法腳本、場景、FBX、材質、Graphics/Quality 均未更動。
2. 修改 `Assets/Core/ModuleSystem/ModuleCoordinator.cs`：完整回歸發現「直接重載目前同一關」時，舊場景已銷毀的 Adapter 仍被介面參考視為非空，可能跳過新場景初始化與環境更新。現在只有 Adapter 確實仍屬於**目前載入的 Scene**時才視為已綁定。這不改正式重啟流程的目標場景。
3. 新增 `Assets/Editor/M6VisualProbe.cs` 與 `.meta`、`Assets/Tests/PlayMode/M6VisualRuntimeTests.cs` 與 `.meta`，供 Unity Editor API 比對及畫面回歸門檻；不屬於 C3 Runtime 資產。
4. 本報告、靜態驗證、Unity 日誌、XML、前後 PNG 和重新產生的 Windows Build 保存在 `DevelopmentReports/M6/`。

修復前後的 **場景、FBX、FBX importer、材質、Graphics/Quality、Lighting Settings 檔案差異均為零**；新增的渲染效果來自 C3 Adapter 載入時更新環境光探針。畫面測試的同一鏡頭位置、旋轉、Skybox、品質設定亦相同。

## 4. 最終測試

| 測試 | 狀態 | 結果／證據 |
|---|---|---|
| 靜態資產、設定、GUID、來源雜湊 | PASS | `VisualFixStaticValidation.txt` 全部差異為 0。 |
| Unity 編譯與 Editor 資產驗證 | PASS | `VisualFixEditorValidateFinal.log`：21 項檢查、0 FAIL；無 C# error。 |
| PlayMode 模組流程＋視覺回歸 | PASS | `VisualFixAcceptanceResults.xml`：2 TOTAL／2 PASS／0 FAIL／0 SKIPPED；視覺測試要求直接載入及入口載入時環境探針非零、黑色像素比例低於 5%。實測均約 0.08%。 |
| Windows x64 Build | PASS | `VisualFixBuildFinal.log`：Succeeded，0 errors、8 warnings；重新產生 `WindowsBuild/SDGAdventure_M6_TEST.exe`。警告主要為原 C3 的過時 `FindObjectOfType` API。 |
| 使用者 Game 視窗、所有角度的實際畫面 | **待人工驗收** | 自動渲染證實固定鏡頭改善，不代表所有視角、螢幕或機器皆已視覺 PASS。 |
| Windows 執行檔實際 GUI 遊玩 | SKIPPED | 已成功 Build，但未在可聚焦視窗人工操作。 |

另在完整套件測試中，先前同關直接重載曾短暫重現零探針，定位為舊 Adapter 參考；修復後完整套件再次跑 **2/2 PASS**，`VisualRuntime/RuntimeProbe.txt` 顯示直接載入、入口載入與再次更新 GI 均為同一非零探針和相同亮度。

## 5. 人工驗收與未完成項目

1. 確認沒有正在執行的 Unity Editor 批次程序，再以 Unity 2023.1.22f1 開啟**測試副本**。本輪最後程序檢查未見 Unity Editor，只有 Unity Hub 的 `unity.exe serve`；測試副本與暫存參照副本均未見 `UnityLockfile`。若再次出現專案鎖提示，先查明程序命令列，不要終止未知程序。
2. 從 `M6_DevelopmentSelection.unity` Play 進入 C3，檢查出生點的牆、天花板與工廠各方向，確認不再大面積純黑或出現角度相關黑塊。可與 `VisualReference/BeforeFixDirectC3.png`、`AfterFixFromSelection.png` 對照。
3. 直接開啟整合副本的 `C_Main.unity` 再 Play；檢查同一畫面與多角度、材質顏色、控制台位置、碰撞、Console Error。
4. 完成關卡、重新開始、返回入口、再次進入，確認每次進入的光照與 UI／操控一致，沒有新黑區或多重 Camera／AudioListener。
5. 視需要執行 Windows Build 進行同樣的畫面驗收；記錄 PASS／FAIL、Unity Console、視角與截圖。正式母專案仍不得修改。

本輪停止於測試副本修復與自動驗證，等待使用者及 ChatGPT 審查；不宣告人工視覺 PASS。

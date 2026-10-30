# SDG Adventure M6｜測試副本整合技術報告

日期：2026-10-24（Asia/Taipei）  
修改範圍：僅 `SDG Adventure_M6_TEST`。正式 `SDG Adventure` 與原始 `C3_TreasurePuzzle` 未修改。未執行 Git commit、push、reset 或 clean。此報告是技術結果，尚待使用者與 ChatGPT 人工驗收。

## 1. 基準與移植

- 執行前確認測試副本有 `Assets/`、`Packages/`、`ProjectSettings/`；Unity `2023.1.22f1`。原有 `M_TEST`、`SampleScene` 保留。執行前 63 個資產／設定檔的 SHA-256 存於 `PreIntegrationBaseline.csv`。
- 唯讀查閱正式母專案的 `M_TEST/MSOP.md`、示範 `ModuleAdapter.cs` 和空的 `Core/ModuleSystem`；查閱 C3 最新任務清單、M5 結案摘要、M6 盤點與保護紀錄。M_TEST 範例方法未被視為正式 API。
- C3 的 M6 `M6_AssetInventory.csv` 逐檔 SHA-256 與目前來源完全一致；必要場景、四個 Prefab、原 FBX、12 個 Runtime 腳本、七個材質和 `.meta` 均存在。測試副本原有 GUID 與來源 GUID 交集為 0。
- 移植到 `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/`：25 個 Runtime 資產及 32 個來源 `.meta`，共 57 檔。32 個原始／目的 GUID 全部相同，見 `GUIDComparison.csv`。其中 `C3CompletionScreen.cs` 在複製後為母專案的重新開始及返回流程作小範圍轉接；它的 `.meta` 及 GUID 不變。其餘 56 檔的內容 SHA-256 與 M6 基準相同。`C3TestInteractable` 因正式場景仍引用而保留。
- 原 FBX 及 `.meta` 原樣複製；無文字取代 GUID、沒有重製 FBX。C3 的 Editor 工具、PlayMode Tests、報告、Library、Packages、ProjectSettings 未移植。
- `StaticValidation.txt` 記錄：全 Assets GUID 重複 0、Runtime 缺 `.meta` 0、Runtime YAML 未解析 GUID 0、來源基準雜湊差異 0、32 個來源 GUID 比對差異 0。Unity `AssetDatabase` 亦成功匯入原 FBX，解析 C3 場景依賴、Prefab 與場景腳本。FBX 的畫面呈現仍須人工檢查。

逐檔交付、大小與 SHA-256 見 `ChangeManifest.csv`。分類包括 57 個來源 Runtime 檔、Core 三個 C# 及其 `.meta`、C3 Adapter 及 `.meta`、Editor 驗證程式、PlayMode 測試、測試入口場景、Build Settings、日誌與 Windows Build 輸出。Unity 另產生 `ProjectSettings/SceneTemplateSettings.json`。既有 `.vscode/settings.json` 在執行前已呈修改狀態，本輪沒有編輯。

## 2. 正式模組系統與 API 契約

`Assets/Core/ModuleSystem/`：

| 類別 | 責任與契約 |
|---|---|
| `ModuleDefinition` | 建構時提供 `Id`、`DisplayName`、`ScenePath`、`AdapterType`。`IsValid(out error)` 要求小寫 ASCII ID（字母起首，後接字母／數字／底線）、非空顯示名稱、`Assets/.../*.unity` 路徑，以及非抽象 `MonoBehaviour` 且實作 `IModuleAdapter` 的型別。 |
| `ModuleRegistry` | `TryRegister(definition, out error)` 拒絕空／無效定義、重複 ID 或同一場景重複註冊；`TryGet(id, out definition)` 與 `TryGetByScene(path, out definition)` 查詢。註冊表在 Play Mode 初始化時重置。 |
| `IModuleAdapter` | `ModuleId`、`Initialize(ModuleCoordinator)` 回傳是否成功、`Shutdown()` 解除事件。Adapter 由 Coordinator 建在關卡場景內，隨場景卸載。 |
| `ModuleCoordinator` | `Instance` 是 `DontDestroyOnLoad` 的唯一實例；重複實例在 `Awake` 銷毀。`TryLaunch(id, out error)` 先驗證註冊與 Build Settings，接著以 `LoadSceneMode.Single` 載入；成功回傳表示載入請求已接受，場景綁定在 `sceneLoaded` 完成。`RestartCurrent(out error)` 重新載入註冊場景；`ReturnToSelection(out error)` 回到測試入口。`CurrentModuleId`、`LastResult` 與 `Completed` 事件供全域流程使用。`ReportCompletion(sender, result)` 僅接受當前 Adapter、對應 ID 且 `Completed=true` 的首次結果。 |
| `ModuleResult` | `ModuleId`、`Completed`、`CollectedParts`、`PuzzleAttempts`、`PlaySeconds`。切換到入口後保留最近結果供顯示；啟動／重啟關卡時清除。 |

`C3ModuleAdapter` 以 `AfterAssembliesLoaded` 註冊 `c3_treasure_puzzle`，路徑為 `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`。場景載入後限定在該 Scene 根物件搜尋，要求恰有一個 `C3DeviceState`、一個 C3 Player、一個 Camera 與一個 AudioListener；訂閱 `C3DeviceState.Completed`，檢查 `LevelCompleted`、`IsActivated`、三零件及 `PuzzleSolved`，再發送一次結果。`Shutdown` 解除訂閱。關卡原有控制器、互動、HUD、控制台與結算仍由 C3 管理。

Coordinator 不建立 Player、Camera、AudioListener 或關卡 HUD；測試入口自有一個 Camera/AudioListener，Single Scene 切換時卸載。切換前恢復 `Time.timeScale=1`、游標可見且解鎖；C3 Player 進入時自行鎖定游標。ESC、E、P 的遊戲輸入由 C3 原有腳本處理。C3 原有靜態 UI 指標在 `OnDisable` 清除，場景重載後進度、解謎與設備狀態均重新建立。

## 3. 場景與流程

`ProjectSettings/EditorBuildSettings.asset` 依序啟用：

1. `Assets/Scenes/M6_DevelopmentSelection.unity`，GUID `c0cc22a26ea9f2f42a77f309b3964090`；畫面明示「M6 開發驗證選關入口」，使用 IMGUI 按鈕載入 C3。
2. `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`，保留原 GUID `e86b74fb01731774ca2319c713131b2b`。

入口按鈕 → `TryLaunch` → Single Scene 載入 C3 → Adapter 訂閱 `Completed` → C3 原有三零件、SDG 12 順序與設備啟動全部成立 → Coordinator 收一次結果 → C3 結算畫面。結算畫面的「重新開始本關卡」呼叫 `RestartCurrent`，按註冊路徑重載 C3；「返回 M6 開發驗證選關入口」呼叫 `ReturnToSelection`。兩者都不以 active scene 的名稱推測目的地。原 `M_TEST` 及 `SampleScene` 檔案未覆蓋，未列入本次 Build Settings。

## 4. 自動驗證結果

| 項目 | 狀態 | 證據與邊界 |
|---|---|---|
| 來源 SHA-256、GUID、`.meta` 與 YAML GUID | PASS | `StaticValidation.txt`、`GUIDComparison.csv`：來源差異 0、32 GUID 差異 0、全專案重複 0、缺 `.meta` 0、未解析引用 0。 |
| Unity 匯入／C# 編譯 | PASS | `UnityConfigureElevated.log` 完成匯入、腳本編譯及入口場景建立，無 C# error；C3 原始 `FindObjectOfType` 產生 CS0618 過時警告。 |
| Editor 自動資產與註冊驗證 | PASS | `UnityValidate.log`：21 個檢查、0 FAIL；含場景／Prefab／FBX、依賴、Missing Script、單一 Player/Camera/AudioListener、有效與無效／重複 ID、缺 Adapter、未知 ID、缺場景。 |
| Unity Test Runner PlayMode | PASS | `PlayModeResultsFinal.xml`：1 TOTAL／1 PASS／0 FAIL／0 SKIPPED。涵蓋未完成條件、一次性結果、結算畫面重新開始與返回、再進入與清除狀態。 |
| Windows x64 Build | PASS | `UnityBuild.log`：Succeeded，0 errors，10 warnings；輸出 `WindowsBuild/SDGAdventure_M6_TEST.exe`。Build 產出 138 檔，約 84.5 MB。 |
| Windows 執行檔實際 GUI 啟動與鍵鼠體驗 | SKIPPED | 本輪未在可聚焦 Game 視窗操作獨立執行檔。 |
| 完整視覺、FBX 材質、碰撞手感及 640×360 UI | SKIPPED | 需人工 Play Mode／Windows Player 驗收，不能由匯入或 Build PASS 推論。 |
| Unity Test Runner EditMode 專用套件 | SKIPPED | 已以 Editor 批次方法執行 21 個自動檢查；未另建 NUnit EditMode suite。 |

首次沙箱內批次啟動因 LicensingClient IPC 逾時而中止（`UnityConfigure.log`，退出碼 199）；在允許存取本機授權服務後重試成功。PlayMode 首次測試程式有 `UnityEngine.Object` 型別轉換編譯錯誤（`UnityPlayMode.log`），已在測試副本修正，後續 `PlayModeResults.xml` 與最終 `PlayModeResultsFinal.xml` 均通過。這些早期失敗日誌保留以供追溯；最終狀態以上表為準。Unity 授權服務啟動時日誌仍有授權驗證訊息，之後取得 entitlement 並完成所有驗證；沒有發現關卡 Runtime 例外。

## 5. 已知限制與待驗收

- 批次測試透過公開關卡方法驅動，不等同真實 WASD、滑鼠、E、P、ESC、GUI 按鈕點擊與視覺驗收。人工步驟見 `ManualAcceptanceChecklist.md`。
- `FindObjectOfType` 的 CS0618 警告來自 C3 原 Runtime，未為消除警告而改動已驗收的玩法程式。Adapter 自身只在已載入的 C3 Scene 範圍搜尋。
- 若其他模組未來加入，需各自提供唯一 ID、場景與 Adapter，再明確註冊；本次正式 Registry 只註冊 C3。
- 本次入口明示為開發驗證用途；正式主選單與視覺設計仍待母專案後續工作。

## 6. 日後正式母專案合併清單

待人工驗收後，由使用者另行決定是否合併。所需變更為：複製 `Assets/Core/ModuleSystem/` 三個腳本與 `.meta`；`Assets/_Modules_/C3_TreasurePuzzle/` 全部 Runtime、Adapter 與父目錄 `.meta`；若要沿用驗證入口則複製 `Assets/Scenes/M6_DevelopmentSelection.unity` 與 `.meta`；在正式母專案 Build Settings 啟用入口與 C3 場景並核對 GUID；按正式 UI 需求接 `ModuleCoordinator.TryLaunch`、`Completed` 與 `ReturnToSelection`。`Assets/Editor/M6IntegrationSetup.cs`、`Assets/Tests/`、`DevelopmentReports/M6/WindowsBuild/` 僅供驗證，無需當作正式 Runtime。合併前再檢查正式母專案現況、GUID 衝突、套件與編譯；不得直接以測試副本覆蓋正式母專案。

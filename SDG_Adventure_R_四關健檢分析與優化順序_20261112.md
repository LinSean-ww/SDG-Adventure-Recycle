# SDG Adventure_R 四關健檢分析與優化順序

報告日期：2026-11-12
資料來源：使用者上傳 `SDG_Audit_20261112_233143_519.zip`，靜態唯讀掃描。
專案：`F:\SDG Adventure_R`；Unity：`2023.1.22f1`。

## 範圍與結論

- 本次工具未執行 Unity、PlayMode、Profiler；以下數值是資產與 YAML 靜態掃描，不代表實測 FPS 或遊戲正確性。
- 工具的 `00_Findings.csv` 只有 1 筆 `REVIEW / Performance`：11 個來源資源檔案各 >= 40 MiB。
- 13 個 Unity 場景；Build Settings 啟用 6 個場景，包含主選單、四關與 `M6_DevelopmentSelection` 測試場景；四關正式場景均存在。
- `.meta` 中未檢出重複 GUID（0）或 GUID 讀取問題（0）。工具的 `NullScriptRefs=0` 不等於 Unity Inspector 已確認所有腳本正常。
- 工具另記錄 `UnresolvedScriptGuids`：Level01=9、Level04=5、主選單=14；可能是內建套件/掃描索引限制，不能依此直接聲稱 Missing Script。
- 舊安裝日誌顯示設定面板 v1.1 曾寫出成功記錄；更早的存檔面板 v2 日誌同時有 CS0103 錯誤與成功字樣。因此目前能否正常編譯，仍應以現場 Unity Console 的狀態驗證。

## 四關數據盤點

| 關卡 | 場景 | 物件 | 腳本組件 | 燈光 | MeshRenderer | Collider | Rigidbody | 攝影機 | 專案風險／觀察 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| Level01 | `Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity` | 50 | 38 | 1 | 13 | 36 | 11 | 1 | 9 個未能以工具索引解析的腳本 GUID；需 Unity 確認 |
| Level02 | `Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity` | 205 | 5 | 8 | 0 | 177 | 172 | 0 | 大量 Rigidbody；需確認哪些應該真正運動、是否動態生成玩家 |
| Level03 | `Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity` | 3,473 | 85 | 27 | 2,810 | 732 | 0 | 0 | 幾何物件與碰撞器最密集；需先量測 CPU/GPU、渲染及光照 |
| Level04 | `Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity` | 40 | 23 | 1 | 5 | 19 | 5 | 2 | 資源中有大型貼圖；需確認是否實際使用及匯入設定 |

> 注意：表中的攝影機／渲染器數量是場景文件內可見的元件。0 不代表進入關卡後沒有攝影機或可見模型；Prefab 與動態載入可能改變執行時數量。

## 必須優先解決的跨關卡基線

1. 在 Unity 2023.1.22f1 開啟 `F:\SDG Adventure_R`，等待匯入完成。
2. 清空 Console 訊息後重新編譯／進入 PlayMode，記下 **紅色 Error 數**；有紅字先排除，不要直接套用效能補丁。
3. 依序從主選單進入 Level01 → Level02 → Level03 → Level04，確認生成玩家、攝影機、HUD、任務起點、終點與返回主選單可用。
4. 存讀檔至少測：手動存檔、關閉遊戲再開啟、讀檔回到正確關卡與位置；若背包／解謎狀態未還原，列為功能缺口，不要聲稱完整存檔。
5. 啟動優化前備份現有專案或使用獨立 V2 複製工作區；每次只更改一關並記錄性能基線。

## 建議優化優先順序

### P0 — 編譯、場景與共享系統驗收（優先且必要）
- 確認 Console 紅字為 0；把舊日誌的 CS0103、CS0234 與目前實際錯誤區分。
- 確認每關可從主選單進入與返回；Level02、03 的遊戲攝影機是否由共用玩家 Prefab 動態建立。
- 檢查正式 Build Settings 是否需要保留 `M6_DevelopmentSelection` 測試場景；有依賴時不要直接移除。
- 確認 HUD、玩家名稱、血量、經驗與存讀檔功能在場景切換後一致。

### P1 — Level03：渲染與場景結構（最高效能調查優先）
- 先用 Unity Profiler 量測 CPU、GPU、Batches、SetPass、GC Alloc、記憶體及幀時間；固定解析度與相同遊戲位置拍照留存。
- 用 Frame Debugger 確認渲染器是否有大量重複 Draw Call；再評估 LOD、可視距離、靜態批次或 GPU Instancing。
- 針對 732 個碰撞器檢查非必要細小碰撞，以及可否以簡單 Box Collider 取代複雜 Mesh Collider。
- 27 盞燈要檢查陰影與重疊照明成本；不能未量測就全面改為烘焙或關閉陰影。
- 另外驗收 B/D 任務閘門、FPS 操作、樓梯通行與任務結算。

### P2 — Level04：大型貼圖（最高資源容量調查優先）
- 11 個 >=40 MiB 原始貼圖均在 Level04 的 `Plasma Guns Lite` 或 `Sci-Fi ToiletMech` 資源目錄，來源檔大小合計約 **744.1 MiB**；不是實際 VRAM 用量。
- 檢查哪些材質實際引用這些貼圖；未使用的資源不能僅因大就直接刪除。
- 特別確認 `Plasma_SMG_Mat_Normal_OpenGL.png`（161.1 MiB）和 `Plasma_SMG_Mat_Normal.png`（98.8 MiB）是否為不同法線貼圖工作流程或重複素材。
- 可於複製工作區比較 Max Size（例如 4096 / 2048）、平台壓縮、Normal Map Import 與 Mip Maps，保留前後畫質／記憶體／載入時間的驗收數據。
- 另驗收武器、拾取、觸發器、角色生命值與通關流程。

### P3 — Level02：物理元件與互動
- 205 個 GameObject 中檢出 172 個 Rigidbody、177 個 Collider，應在 PlayMode 檢查是否有大量無需運動的物件在參與物理模擬。
- 使用 Physics Profiler 檢查 FixedUpdate 成本與同時接觸數；確認各 Rigidbody 的 Use Gravity／Is Kinematic 設定。
- 有 8 盞燈，可一起檢查陰影，但需量測成本。
- 回歸驗收機關、碰撞與任務步驟，不破壞正常可動的物件。

### P4 — Level01：遊戲引導與場景引用
- 專案場景有 1 個攝影機、36 個 Collider、11 個 Rigidbody。
- 在 Unity Editor 逐個確認 9 個未解析 GUID 是否真的造成 Missing Script，並測試出生點、移動、互動教學、關卡目標與通關觸發。
- 優化目標是減少玩家不知道下一步要做什麼的情況；不建議先追加大型特效。

### P5 — 整合及最終驗收
- 再次檢查四關 HUD、頭像／姓名、血量／經驗、暫停、設定及存檔。
- 測試 1280×720 與 1920×1080；確認 UI 不重疊、存檔不錯置、切換關卡不重複載入角色。
- 建立一致的效能比較條件，避免單次 FPS 值誤導。

## 建議的下一個實作動作

**先做 P0：Unity 實際 Console + 四關啟動驗收。** 靜態報告不能證明目前編譯及 PlayMode 狀態。拿到 P0 結果後，優先進入 Level03 的 Profiler 量測，再處理 Level04 的大型貼圖導入設定。每個優化改動都應先備份、單關測試，驗收通過才進入下一關。

## 來源檔案

- `00_Findings.csv`、`01_Overview.txt`
- `04_AllScenes.csv`、`05_SceneScriptsAndNames.csv`、`06_FourLevels.txt`
- `07_BuildSettings.txt`、`08_IntegrationChecks.txt`
- `09_LargeAssets.txt`、`10_RecentLogSignals.txt`

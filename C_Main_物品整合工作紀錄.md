# C_Main 物品整合盤點與工作紀錄

## 狀態：未完成

| 項目 | 現況 | 分類 |
|---|---|---|
| 物品資料 | EcoAdventureRuntime.inventory 為 id、名稱、數量清單；相同 id 堆疊，無容量上限 | 已存在 |
| 快捷欄 | quickSlots 五個 id，selectedSlot 為零起算索引；背包按鈕寫入目前格 | 已存在；已增加加入物品時的選取格優先、再找第一空格，已在欄中不重複配置 |
| 手持 | 武器使用 EcoQuantumWeaponView；其他物品目前使用程式生成的簡化幾何形狀，綁定相機 | 需要整合原始模型、第一／第三人稱及空手顯示 |
| 主線零件 | C3CollectiblePart 依距離、可視性使用 C3HintVisuals 與點光源；C3TreasureProgress 另存任務進度，拾取後場景物件停用 | 已存在；不可直接當成一般物品丟棄 |
| 工坊材料 | EcoWorkshopInteractable 經排序／工廠狀態驗證，成功後停用材料點；材料用於製作 | 已存在；丟棄與回收仍需設計 |
| 武器及彈藥 | 武器製作後加入背包；彈匣與備用彈為 runtime 全域欄位，射擊與裝填沿用原系統 | 已存在；尚無可安全攜帶彈藥狀態的世界掉落物 |
| 一般拾取 | EcoLootPickup 可加入物品並銷毀物件；未發現場景資產使用它 | 已存在元件；尚未整合 C_Main 互動 |
| 丟棄 | 無完整世界掉落、物理、回撿或狀態轉移 | 需要新增；本次尚未實作 |
| Drop 鍵位 | Gameplay/Drop 預設 G，輸入服務與設定介面已加入；沿用既有衝突檢查及 PlayerPrefs 機制 | 已修改，尚需 Unity 實測；目前沒有觸發丟棄的消費者 |

背包顯示前 12 種是 UI 限制，並非儲存容量限制。工坊材料及主線物品的既有任務與製作規則不可因丟棄而改變；目前所有類型均未支援丟棄。持有的普通物品模型仍是原先的替代形狀，不符合本任務最終要求。已儲存舊版自訂鍵位若占用 G，也需要遷移策略，尚未處理。

## 本次修改

- `Assets/_Modules_/01_Shared/Player/Input/Resources/SDGPlayerControls.inputactions`：Drop 動作及 G 綁定。
- `Assets/_Modules_/01_Shared/Player/Scripts/PlayerInputService.cs`：Drop 輸入狀態及事件，Gameplay context 限制。
- `Assets/_Modules_/01_Shared/Player/Scripts/PlayerMenuCanvas.cs`：丟棄物品改鍵列。
- `Assets/_Modules_/02_Levels/Level03/Scripts/EcoAdventureRuntime.cs`：新增物品時配置快捷欄，先選取空格，否則依順序找空格；相同 id 已在欄中則保留該格。
- `Assets/_Modules_/05_Testing/PlayMode/M7/PlayerSettingsTests.cs`：新增 Drop 綁定測試並將既有借用 G 的測試改為 N。
- `Assets/_Modules_/05_Testing/PlayMode/M7/ReloadRestartPersistenceTests.cs`、`Assets/_Modules_/05_Testing/SharedPlayer/Automated/PlayerControlPlayModeTests.cs`：避免原先改綁 G 與新預設衝突。

這些檔案在本次開始前已有其他未提交變更；上述僅描述本次新增內容。

## 驗證與人工驗收

Unity 2023.1.22f1 Play Mode 測試因 Licensing Client 連線逾時而以代碼 199 結束，未產生測試結果。實際通過 0、失敗 0、跳過數未知（測試未啟動）；編譯與 Console 結果未知，不能宣稱通過。Drop、物理、回撿、手持原模型、全部輸入回歸與實際畫面均未驗收。完成前應在 C_Main 實測取得主線零件與工坊材料、各種快捷欄狀態、手持模型、改鍵與重啟；完成掉落實作後再測 G、碰撞落地、發光、改綁互動回撿、堆疊數量及武器彈藥狀態。不要將模擬輸入當作畫面人工驗收。

## 後續 CompletePlayer 整合計畫（未執行匯出）

在 C_Main 功能與人工驗收完成後，檢視共用 Drop Input Action、自訂鍵位與通用輸入事件，再更新 Demo 回饋、文件和測試，最後依使用者後續授權決定是否匯出。


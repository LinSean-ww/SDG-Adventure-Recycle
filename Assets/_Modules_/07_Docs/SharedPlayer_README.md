# 共用第一人稱玩家

在 Unity 2023.1.22f1 直接開啟 `Assets/_Modules_/05_Testing/SharedPlayer/PlayerControlTest.unity`，按 Play 即可試玩；不需加入 Build Settings。

安裝前須在 Package Manager 安裝 Input System 1.7.0 與 uGUI 1.0.0，並將 Active Input Handling 設為 Both。模組的繁體中文字型位於 `Resources/SharedPlayer/Fonts/`，其 SIL OFL 1.1 授權與版權說明位於 `Assets/_Modules_/06_ExternalAssets/ThirdPartyNotices/SharedPlayer/`；匯出 `.unitypackage` 時須包含整個 `Assets/_Modules_/01_Shared/Player/` 與上述字型授權目錄，不可只匯出 Scripts 或 Prefab。

## 關卡使用方式

1. 每個單一載入的關卡放入一個 `Prefabs/SharedPlayer.prefab`。Prefab 已包含 CharacterController、Camera、AudioListener、第一人稱控制、互動器，以及執行時建立的 Canvas 暫停／設定／任務介面。
2. 場景互動物件的 MonoBehaviour 實作 `IPlayerInteractable`。其 Collider 須位於可被射線命中的層，預設互動距離為 3 公尺。
3. `PlayerInputService` 由程式自動建立，跨場景只保留一個；不要在場景另建輸入服務或持久化 Player。
4. 選單有可選的測試返回點欄位。測試場景將其設為 `PlayerSpawn`，提供「返回測試起點」按鈕。
5. 關卡任務資料由關卡腳本實作 `IPlayerTaskSource.GetTasks()`，並呼叫玩家上的 `PlayerTaskJournal.Load(source)` 載入。進度改變時呼叫 `SetCompleted(taskId)`；任務標題、描述與完成數會即時更新。`Assets/_Modules_/05_Testing/SharedPlayer/PlayerControlTestTaskDemo.cs` 是獨立示例，正式關卡不依賴它。

遊戲時啟用 Gameplay 與 Global 動作；暫停／設定／任務介面只啟用 UI 動作。預設 P 開關任務清單，設定中的「任務清單」重綁會同步套用於開啟與關閉；Esc 在任務介面先關閉任務，在設定中返回暫停選單，在暫停選單中繼續遊戲。重綁期間 Esc 取消擷取。設定以版本化 PlayerPrefs 資料儲存；恢復預設會清除按鍵覆寫、靈敏度與按住／切換模式。所有遊戲 HUD 與選單以 uGUI Canvas 繪製，避免 IMGUI 事件與文字快取在重新 Play 後影響排版。

此模組尚未接入 C3；C3 原有 E 互動與控制器維持原狀。


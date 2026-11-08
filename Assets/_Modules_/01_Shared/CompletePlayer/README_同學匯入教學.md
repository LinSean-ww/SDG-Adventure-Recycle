# 同學匯入教學

## 環境

建議 Unity 2023.1.22f1。透過 Package Manager 安裝 Input System 1.7.0 與 Unity UI（uGUI）1.0.0；在 Project Settings > Player > Active Input Handling 選 Both，按提示重新啟動 Unity。套件本身不會安裝 Package Manager 套件或改寫專案設定。

## 匯入及使用

1. 備份目標專案；用 Assets > Import Package > Custom Package 選取已驗證的 `SDGAdventure_CompletePlayer_v1.3.unitypackage`，保留勾選的相依資產與 .meta。
2. 將 `Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab` 放進自己的場景，設置出生位置與朝向，並放置有 Collider 的地板。本套件設計為不包含 Demo 場景。
3. 確認場景只有一個玩家、Camera 和 AudioListener。按 Play；Esc 開啟暫停／操作設定。若要使用預設的重啟和返回選關，將相關場景加入 Build Settings，並在 Prefab 實例的 PlayerPauseMenu 設定 Selection Scene，或由關卡呼叫 SetLevelActions。
4. 不需要 Demo 或 C_Main 場景及其腳本。不要在同一專案重複匯入另一份相同名稱的共用玩家程式或 Input Actions。

## 背包／快捷欄整合

核心模組只提供輸入，不含正式背包介面或物品資料。Demo 測試背包及五格顯示不包含在此封裝清單中，也不代表正式背包功能。`SDGAdventure.SharedPlayer.PlayerInputService.Instance` 提供 `InventoryPressed` 和 `SlotPressed(int slot)`；格號為 1～5。服務在場景載入前建立並跨場景存活。關卡元件啟用時訂閱、停用時解除訂閱；不要每次開啟選單都新增訂閱。範例將事件排隊到 Update 再切換 Action Map，避免在輸入回呼中直接切換：

```csharp
using SDGAdventure.SharedPlayer;
using UnityEngine;

public class LevelInventoryInput : MonoBehaviour
{
    PlayerInputService input;
    bool inventoryOpen, toggleQueued;
    int slotQueued;

    void OnEnable()
    {
        input = PlayerInputService.Instance;
        if (input == null) { enabled = false; return; }
        input.InventoryPressed += QueueInventory;
        input.SlotPressed += QueueSlot;
    }
    void OnDisable()
    {
        if (input == null) return;
        input.InventoryPressed -= QueueInventory;
        input.SlotPressed -= QueueSlot;
    }
    void QueueInventory() => toggleQueued = true;
    void QueueSlot(int slot) => slotQueued = slot;
    void Update()
    {
        var menu = GetComponent<PlayerPauseMenu>(); // 若腳本不在玩家物件上，請自行指定玩家選單
        if (toggleQueued && (menu == null || menu.State == PlayerMenuState.Playing) &&
            (inventoryOpen || input.Context == PlayerInputContext.Gameplay))
        {
            inventoryOpen = !inventoryOpen;
            input.SetContext(inventoryOpen ? PlayerInputContext.UI : PlayerInputContext.Gameplay);
            PlayerCursor.LockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = inventoryOpen;
            // 在此顯示或隱藏自己關卡的背包 UI。
        }
        if (slotQueued != 0 && !inventoryOpen && input.Context == PlayerInputContext.Gameplay)
        {
            int selectedSlot = slotQueued; // 在此交給自己的快捷欄系統。
        }
        toggleQueued = false;
        slotQueued = 0;
    }
}
```

`InventoryPressed` 在 Gameplay 與 UI 均可觸發，以便再次按鍵關閉背包；UI 包含暫停與任務清單，必須依自己的面板狀態過濾。快捷欄事件只在 Gameplay 觸發。若背包支援 Esc、其他面板或關卡切換，整合時須協調游標、輸入 context 和 PlayerPauseMenu 的狀態。也可用 `InventoryWasPressed`、`SlotWasPressed(1..5)` 輪詢。避免同時訂閱事件和輪詢造成重複處理。

## 素材與授權

中文 UI 字型及其 SIL OFL 授權、公告位於 `Assets/_Modules_/06_ExternalAssets/ThirdPartyNotices/SharedPlayer`。角色 `fk.fbx` 複製自本專案 Level03；現有檔案未找到其作者及再散布授權資料，對外散布前請向素材提供者確認。內部匯入也應遵循團隊素材使用約定。

## v1.2 工人視覺

CompletePlayer 預設使用由 C_Main 工人視覺與非持武器姿勢公式整理出的共用元件，不需要 C_Main 或其控制器。它根據角色速度、是否著地及膠囊高度表現走跑、跳躍期間、蹲下與趴下；實際畫面請在目標場景確認。可選武器可透過 CompletePlayerWorkerAppearance 的 WeaponAnchor、左右肩肘手及 SetWeaponPoseOwner 接口整合，模組不含武器或持槍 IK 實作。舊 fk.fbx 留在來源專案，預定從新版套件排除；其授權仍待確認。

## 本次匯出狀態與驗收提醒

目前新版尚未完成匯出或 TEST 人工驗收；請勿將既有套件視為本次新版。Prefab 本身僅提供背包與快捷欄輸入，Demo 的面板為測試用途。Esc > 操作設定可切換第一／第三人稱，並調整靈敏度、跑步／蹲下模式和鍵位；設定保存在目標專案的 PlayerPrefs，恢復預設會重設按鍵與視角。R／G 僅提供裝填／丟棄輸入，不含彈藥或物品掉落實作。

匯入前先比對已有共用資產的 GUID 與版本，避免覆蓋隊友已有修改。場景中只啟用一組玩家、相機與 AudioListener；選單會在找不到 EventSystem 時建立一個，應先確認場景已有的 EventSystem 及輸入模組可用。Missing Script 應先檢查 Console 編譯錯誤、必要 Package 和 GUID；Missing Material 應檢查對應素材與渲染管線，不要刪除他人場景元件。

角色舊模型 fk.fbx 的授權未確認；使用中的 Rust／Steel 貼圖與 Level03 同名素材內容相同，原始來源及再散布授權亦需確認。使用者確認授權前勿分發。

## 預設操作

W／A／S／D 移動、滑鼠旋轉、Space 跳躍、左 Shift 奔跑、左 Ctrl 蹲下、Z 趴下、Q／E 探頭、F 互動、Tab 背包輸入、1～5 快捷欄輸入、P 任務清單、Esc 暫停、R 裝填輸入、G 丟棄輸入。Esc > 操作設定可切換 FPS／TPS、調整靈敏度及鍵位；改鍵會檢查衝突，設定保存在目標專案。第三人稱的探頭表現在角色上。

## 來源專案手動匯出（待驗證）

確認 Unity 已正常授權且 Console 無編譯錯誤，在正式來源專案選擇 SDG Adventure > Complete Player > Export Runtime v1.3 (No Demo)。這個命令不重建 Prefab 或 Demo，會檢查輸出檔不存在及執行 Prefab／依賴檢查，並封裝列明的共用資產；輸出至 Exports/SDGAdventure_CompletePlayer_v1.3.unitypackage。若檔案已存在或檢查失敗，命令會停止。完成後仍須核對實際套件內容及 GUID，確認未含 Demo、C_Main 與 fk.fbx；不可僅憑命令成功就對外分發。正式背包 UI 缺漏及 Rust／Steel 授權仍須解決。

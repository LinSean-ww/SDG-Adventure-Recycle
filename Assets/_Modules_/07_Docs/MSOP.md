# SDG Adventure｜組員關卡交付 SOP

適用版本：Unity 2023.1.22f1。正式模組根目錄為 `Assets/_Modules_/`。

## 1. 先認領關卡插槽

正式插槽固定為 `02_Levels/Level01` 至 `Level08`。`Level03` 已由 C3 尋寶解謎關使用；其他插槽目前僅預留，不能把預留資料夾當成已完成的關卡。資料夾名稱與註冊 ID 不同；C3 ID 固定為 `c3_treasure_puzzle`。新關卡 ID 必須以小寫英文字母起首，只含小寫字母、數字、底線，且全專案唯一。

## 2. 在自己的 Unity 專案製作

在獨立 Unity 專案開發與測試。正式交付包含：關卡場景、所需 Prefab、Scripts、模型／材質／貼圖／音訊、對應 `.meta`、Adapter、套件版本、第三方授權及測試紀錄。不要把 `Library`、`Temp`、建置輸出或整個巢狀 Unity 專案複製進母專案 `Assets`。

## 3. 玩家與共用資源

先評估 `01_Shared/Player`。若採用 SharedPlayer，單一載入的關卡場景只放一個玩家、一個 Camera、一個 AudioListener；使用其既有輸入、互動與 UI。若有專用玩家，交付說明理由及與共用資源的邊界。C3 已使用自有控制器與互動器，不得因目錄整理而替換。

## 4. Adapter 與完成回報

Adapter 實作既有 `IModuleAdapter`：`ModuleId`、`Initialize(ModuleCoordinator)`、`Shutdown()`。註冊資料提供唯一 ID、顯示名稱、正式場景 `Assets/.../*.unity` 路徑及 Adapter 型別。關卡完成條件真正成立後，透過 Coordinator 回報一次 `ModuleResult`；關卡結束或切換時解除事件訂閱。不得自行建立第二套 Registry 或 Slot 系統。

## 5. 交付前驗收

檢查 Unity 編譯及 Console、所有 `.meta` GUID、場景／Prefab Missing Script、模型與材質引用。從開發入口測試：載入、未完成時不誤結算、完成只回報一次、重開後狀態歸零、返回入口、再次進入。記錄 EditMode、PlayMode 與人工鍵鼠測試；自動測試不能代替人工操作。

## 6. 整合進母專案

整合負責人先在隔離副本匯入，檢查 GUID 與路徑衝突，將關卡正式資產放進指定 `LevelXX`，Adapter 放進 `03_Integration/Adapters`，開發測試資產放進 `05_Testing`。更新 Build Settings 及程式路徑，通過回歸後再提出正式套用方案。正式母專案不由組員的獨立專案直接覆蓋。

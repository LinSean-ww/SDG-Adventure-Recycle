# SDG Adventure｜模組架構

Unity：2023.1.22f1。正式模組皆由 `Assets/_Modules_/` 管理。

| 目錄 | 責任 |
|---|---|
| `00_Core` | 現有 ModuleDefinition、ModuleRegistry、ModuleCoordinator 與核心預留分類。 |
| `01_Shared` | 可重用的 Player、Camera、Input、UI、Audio、Environment。SharedPlayer 保持可運作的內部結構。 |
| `02_Levels/Level01`～`Level08` | 八個固定關卡插槽；只有 Level03 有現有正式內容。 |
| `03_Integration` | 各關 Adapter 與整合驗證資料；沿用現有 IModuleAdapter 接口。 |
| `04_Game` | 未來正式主選單、跨關 UI 與流程；目前沒有正式主選單。 |
| `05_Testing` | 開發入口、範例、Editor 工具、隔離場景、測試 Prefab、EditMode／PlayMode。 |
| `06_ExternalAssets` | 第三方字型與可搬移授權資料。 |
| `07_Docs` | 組員規範、架構及需長期保存的 Markdown 開發文件。 |

`Assets/StreamingAssets/FontLicenses` 因 Unity 特殊資料夾規則保留在 Assets 根層；`Packages`、`ProjectSettings` 也維持 Unity 專案根層。巢狀 `Resources` 保持現有 `Resources.Load` 相對路徑。`M6_DevelopmentSelection` 是開發驗證入口，歸 `05_Testing/Scenes`，不能當作正式主選單。

## 關卡註冊

資料夾插槽不等於 ID。Registry 依唯一 ID 及唯一場景路徑註冊關卡；Coordinator 驗證 Build Settings 後載入，建立 Adapter，接收首次有效完成回報，並處理重開及返回。C3 的註冊 ID `c3_treasure_puzzle`、原場景與玩家資產 GUID 不因目錄遷移而改變。

## C3 與 SharedPlayer

Level03 保留 C3 的 `M2_Player`、C3FirstPersonController、C3PlayerInteractor、C3BoneController、場景、解謎與結算。SharedPlayer 是供其他關卡採用的共用套件，目前未取代 C3 玩家。任何新關卡都需檢查場景內只有一個 Player、Camera、AudioListener。

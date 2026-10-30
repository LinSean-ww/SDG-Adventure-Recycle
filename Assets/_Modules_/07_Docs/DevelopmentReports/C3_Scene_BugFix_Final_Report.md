# C3 第三關場景 Bug 修復與美術優化最終報告

日期：2026-10-27。專案：`SDG Adventure`，Unity `2023.1.22f1`，Built-in Render Pipeline。正式場景：`Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`。以下位置均為 Unity **World Position**、角度為 World Euler，尺寸與 Bounds 為 Unity 世界單位，除非另註。

## 結論與保護狀態

已完成本次要求的可安全場景修正。原始 `ONLYfactory.fbx` 根物件仍為 Identity，來源 FBX SHA-256 為 `BC6FB179FFFB74FD7FA11434C28D53E17BB2413B3F7A9032FBB309440E4A2058`，未改動來源資產。原有 `M2_Player` 仍掛 `C3FirstPersonController` 與 `C3PlayerInteractor`；C3 場景內 M7 共用玩家元件數為 0。三零件 ID、收集腳本與原 Collider、控制台解謎和通關狀態機均保留。本次未修改 C3 既有玩家控制程式、`Assets/Shared/Player/`、其他關卡，亦未執行 Git commit/push/reset/clean。

施工前備份位於 `DevelopmentReports/C3_BugFix_Backups/`：`C_Main_PreBugFix_20261027.unity.bak`（SHA-256 `A36D64506876B61CDDBFFDFED084A8A005C494C4FF3BF00E648A0C92630D8824`）、`C3_Assets_PreBugFix_20261027.zip`（SHA-256 `64B326B5E0D691E6A61100CF1B2175BEFE12E0D6D0FAD4651C1C806EC6FFB2F5`）、施工前 Git 狀態及 M7 檔案雜湊清單。備份包含正式場景與 `_C3_TreasurePuzzle` 資產樹及相關 C3 Editor/測試檔，保留了當時的未提交工作。

## 修正前後對照

| 問題 | 施工前 | 完成後與核對依據 |
|---|---|
| 水管與高架支架可穿越 | 部分新增結構僅有 Renderer | 新增 32 個管線／安裝件 Collider、8 個鋼構斜撐 CapsuleCollider；西側管線射線首先命中 `Collision_MainWestTrunk`，CharacterController 移動測試確認主要管線與斜撐阻擋。小型裝飾未加碰撞。 |
| 管線漂浮、端點不明 | 西側主幹與設備接點缺少視覺固定 | 補接頭、穿牆套管、牆面供料連接、天花吊架與管夾；保留原管線主要配置與通道。局部與牆面代理 Collider 的 9 處 AABB 重疊均為設計上的固定件嵌入。 |
| 暖色工業燈漂浮 | 五盞暖燈只有發光點 | 五盞各增吸頂板、吊桿、燈罩與燈座，上端接到 y=9.9 的天花代理結構。 |
| 黑黃警示板漂浮／擋視線 | 入口、西側、二樓牌位置偏離承載面 | 入口牌固定南牆、西側牌固定西牆、二樓牌固定北側護欄並加緊固件；樓梯動線自動測試通過。 |
| 控制台朝向及位置 | `(-0.9,1,18.95)`，Y=0°，正面朝 -Z | `(-0.65,1,18.95)`，Y=+90°，正面朝 -X；背面 Collider 最大 X=-0.375，恰接東牆代理最小 X=-0.375。從室內 3 m 互動射線首先命中控制台。 |
| 青色燈／導引長棒 | 導引柱與球為可見測試標記；燈懸空 | `Cyan_Console` 移至 `(-0.75,2.37,18.95)` 並加牆座；純視覺導引柱、球、底座 3 個 Renderer 停用，節點保留；無玩法引用被移除。 |
| 設備與道具穿插 | 東側桶與廢棄容器 Collider 重疊 | `Barrel_East` 沿 +X 移 0.7；全場可見美術 Renderer 超出 C 區外殼數為 0，設備 Collider 相互重疊數為 0，與原 FBX 網格的非預期 AABB 重疊數為 0。 |
| 三零件測試外觀 | 黃圓柱、青方塊、綠球 | 保留三個 prefab 實例根物件與收集腳本／Collider，只停用原根 Renderer，各加入無 Collider 的工業零件視覺子物件：陶瓷保險絲、綠色電路板、折疊濾芯。視覺 Bounds 位於原互動 Collider 內。 |
| 白色測試互動柱 | 白色示範外觀 | 改為深色維修終端；`C3TestInteractable` 及 BoxCollider 保留。 |
| 天花圓形構件粉橘色 | FBX 子網格 13 `siteC_Big_Chimney` 使用原匯入材質及受暖光影響 | 僅覆蓋場景實例的 Renderer 材質槽 13 為 `M_DarkIndustrialSteel`；槽 12 天花維持 `M_AgedConcrete_Ceiling`。FBX 幾何和 Transform 不變。 |
| 英文標示及光照 | 電氣牌、設備牌與維修紀錄為英文；西側管線偏暗 | 三處改台灣繁體中文並固定於牌面，使用既有 `NotoSansCJKtc-Regular.otf`；增加一盞無即時陰影的西側牆燈，環境色調整為 `(0.34,0.36,0.38)`，方向光強度 0.36。 |

## 主要 Transform 變更

下表僅列根物件主要位置；新增支架、視覺子節點以及所有物件的 **World Rotation、Lossy Scale、Renderer Bounds、Collider Bounds** 詳見 `C3_BugFix_Evidence/Before_Transforms.tsv` 與 `After_Transforms.tsv`。數值取自 Unity Editor 場景探測，不是估算。

| 物件 | 施工前 World Position | 完成後 World Position | World Rotation 變更 |
|---|---:|---:|---|
| `M4_PuzzleConsole` | (-0.900,1.000,18.950) | (-0.650,1.000,18.950) | Y: 0° → 90° |
| `Warning_EntryFaultWarning` | (-20.900,2.650,2.900) | (-20.900,2.650,0.500) | 無 |
| `Warning_WestElectricalDanger` | (-24.380,2.550,17.800) | (-24.820,2.550,17.800) | 無 |
| `Warning_UpperRestrictedMaintenance` | (-12.500,5.450,19.720) | (-12.500,5.230,19.910) | 無 |
| `Barrel_East` | (-7.000,0.550,22.800) | (-6.300,0.550,22.800) | 無 |
| `M3_Fuse` | (-20.000,0.980,22.000) | 同前 | 根物件無變更 |
| `M3_ControlChip` | (-12.500,4.900,18.500) | 同前，仍在二樓 | 根物件無變更 |
| `M3_FilterModule` | (-4.000,1.030,18.000) | 同前 | 根物件無變更 |

五盞既有暖燈 World Position 未變：入口 `(-20.9,6.8,4.1)`、西側 `(-22,7.15,19.2)`、中央 `(-12.5,7.1,9.5)`、東側 `(-4.1,7.1,13.4)`、二樓 `(-14,8.05,15.5)`；新增物理安裝模型接至天花。新增西側照明 `Warm_WestPipeInspection=(-24.48,4.15,11)`。繁體文字牌最終位置詳見 `StageH_ChineseSigns.txt`。

## Collider 與引用

`C3_BugFix_Evidence/Collider_Changes.csv` 逐一列出物件路徑、Collider 類型及前後 Bounds：**新增 40、修改世界 Bounds 2**。40 個新增項目為管線／管夾／吊架 32 個，以及主平台斜撐 8 個；修改世界 Bounds 為控制台及移位的東側桶。新增的零件視覺、燈具外殼、標示與文字不增加互動 Collider。原 M2 白模 BoxCollider 數仍為 157；美術層 Collider 現為 54。管件近接原牆的 9 個 AABB 交集為固定用錨點，已單獨歸類，其他牆體代理交集為 0。

最終靜態核對：三個 `C3CollectiblePart` ID 分別為 `fuse`、`control_chip`、`filter_module`；`M4_PuzzleConsole` 1 個、淨化設備 1 個；三零件與控制台的近距互動射線首命中均正確。未更動既有收集狀態機、解謎順序或設備啟動判定。

## 修改檔案與資產

- 正式場景：`Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`。
- 新增四個 Built-in Standard 材質及 `.meta`：`ArtFull/Materials/M_C3_CeramicFuse.mat`、`M_C3_ControlBoard.mat`、`M_C3_BrassContacts.mat`、`M_C3_FilterMedia.mat`。既有 `M_DarkIndustrialSteel` 僅被場景實例引用。
- 新增施工與驗證工具及 `.meta`：`Assets/Editor/C3SceneBugFixTool.cs`；新增 PlayMode 圖形／角色碰撞測試及 `.meta`：`Assets/Tests/PlayMode/C3SceneBugFixTests.cs`。
- 調整既有、未提交的 C3 Editor 施工工具：`Assets/Editor/C3FactoryArtDetails.cs`（繁體中文牌／字級）、`C3FactoryArtLighting.cs`（驗證燈數與實際燈光資料）、`C3FactoryFullArtBuilder.cs`（最終稽核委派本次檢查）。未改 C3 遊戲程式碼。
- 新增本報告、施工前備份及 `DevelopmentReports/C3_BugFix_Evidence/` 下的檢查文字、TSV、CSV、Unity XML／log 和 PNG 截圖。

本次在既有 `C_Factory_Art` 階層下增加管線碰撞／連接、燈具固定件、牌面固定件、零件視覺及照明子節點。原 `M3_Treasure`、`M4_PuzzleConsole`、`M2_Player` 核心階層與腳本引用未拆換。

## 自動化驗證與證據

Unity 批次啟動、腳本編譯及場景載入均成功。施工階段 A–I 局部探測與最終 PlayMode log 未找到 `error CS`、`Compilation failed` 或 `Exception:`。`C3_BugFix_Evidence/Final_StaticAudit.txt` 結果為 `PASS`，涵蓋原始建築根 Transform、M7 未接入、物件計數、Bounds、Collider 重疊及互動射線。

| 驗證項目 | 結果 | 證據 |
|---|---|---|
| 原 C3 CharacterController 移動、跳躍及主要管線／斜撐阻擋 | PASS（自動） | `Final_ControllerAndSupports.xml` |
| 兩座樓梯及主走道膠囊體路線 | PASS（自動） | `Final_StairRoute.xml` |
| 三零件、控制台與關卡完成 | PASS（自動） | `Final_CollectionConsole.xml` |
| 未收齊與錯誤順序防護、正確順序、啟動 | PASS（自動） | `Final_PuzzleGuards.xml` |
| 二樓晶片可收集與 HUD 提示 | PASS（自動） | `Final_ChipHint.xml` |
| 母專案完成回報、重新開始與返回選關測試流程 | PASS（自動） | `Final_MotherFlow.xml` |
| 9 張關鍵視角截圖生成 | PASS（自動生成，另經人工目視檢查） | `VisualReview.xml`、`After_*.png` |

所有上述 XML 均 `Passed`、1/1、failed=0。自動測試只覆蓋其設定的射線、路徑與狀態，不等於真人完整遊玩驗收。現場截圖包括 `After_EastWallConsole.png`、`After_WestPipeAndHangers.png`、`After_EntryWarning.png`、`After_WestElectricalChineseSign.png`、`After_FuseModel.png`、`After_FilterModel.png`、`After_ChipAndChineseSign.png`、`After_Reactor.png`、`After_UpperCeiling.png`。截圖可核對控制台貼牆、暖燈與警示牌固定、零件外觀、中文缺字及天花改色。

## 未修正項目、限制與人工驗收

本次未發現必須停止的引用衝突。仍需人工核對以下 `NOT_TESTED` 項目；不得將上表自動結果視為其 PASS：

1. 在 Unity 實際以鍵鼠操作，確認 WASD、滑鼠視角、跳躍、E 鍵互動與樓梯上下行時的手感及鏡頭遮擋。
2. 沿兩層、所有設備和管線周圍巡視，確認全視角沒有小面片穿插、浮空裝飾、不可見阻擋或意外卡點。
3. 實際尋找與收集三零件，目視 HUD、解謎、設備啟動、結算、重新開始及返回選關流程。
4. 以不同解析度／品質設定檢查繁體中文字級、亮度、暗部通道、光照及效能；本次沒有正式效能量測。
5. 美術品質審核：零件採幾何與簡化材質，光影仍偏暗，並非照片級資產；可於後續美術階段製作貼圖、細節磨耗與更清楚的照明導引。

## Git 與 M7 隔離檢查

施工前工作樹已包含 C3 場景及遊戲腳本、美術工具、`DevelopmentReports/`，以及 `.vscode`、`Packages`、`ProjectSettings`、`Assets/Shared/Player/`、M7 測試等未提交變更；本次保留原狀。施工後 `git status --short` 仍顯示這些既有髒檔，加上本次 C3 工具、測試、材質與報告，沒有 Git commit/push。`Assets/Shared/Player/` 共 83 個檔案的施工前／後 SHA-256 比對差異 **0**，清單在 `C3_BugFix_Backups/M7_Sha256_PreBugFix_20261027.csv` 與 `M7_Sha256_PostBugFix_20261027.csv`；C3 場景的 M7 元件計數亦為 0。M7 仍屬後續獨立整合任務。


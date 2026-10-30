# C3 雙層廢棄淨化工廠完整場景建造報告

日期：2026-10-26（Asia/Taipei）  
專案：`SDG Adventure`／Unity 2023.1.22f1／Built-in Render Pipeline  
正式場景：`Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`

## 1. 施工基準與完成範圍

已先閱讀 `C3_TwoFloor_Scene_Audit.md` 與 `C3_M2_TwoFloor_Blockout_Report.md`，以 M2 現有的雙樓梯、局部平台及 157 個實體 BoxCollider 為施工基準。施工前備份為 [C_Main_PreFullArt_20261026.unity.bak](C3_FullArt_Backups/C_Main_PreFullArt_20261026.unity.bak)，SHA-256 為 `1F77422725CB7E1BEB9E1F565B45DA7A6CD037A7CA1A263D26B537A887890700`。備份位於專案內，未執行 Git commit、push、reset 或 clean。

已完成：控制晶片搬往二樓；既有白模樓梯、走道、護欄與支柱的金屬視覺替換；一樓淨化、分類、泵浦及過濾設備；二樓維修工作站；管線、貨櫃、鐵桶、工具箱及維修紀錄；程序式工業材質；局部暖／冷／警示光；原創環境與啟動音。`ONLYfactory.fbx` 原檔與外殼未修改。**美術仍屬程序式中等細節的可玩場景，非照片級寫實成品。**

本次無外部模型、貼圖或音效下載。新增貼圖與 WAV 音效皆由專案內 Editor 工具產生；標示使用 Unity 內建字型。

## 2. 修改與新增檔案

| 類別 | 檔案／目錄 | 內容 |
|---|---|---|
| 修改 | `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity` | 晶片 Transform、`C_Factory_Art`、既有 C 區外殼的場景實例材質、燈光設定；保留 FBX 資產及核心物件。 |
| 修改 | `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scripts/C3TreasureProgress.cs` | 僅更新晶片線索文字，指出二樓高架維修平台與西側樓梯。 |
| 新增 | `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scripts/C3FactoryAudioCue.cs` | 訂閱既有 `C3DeviceState.Activated` 播放啟動音；不變更通關狀態機。 |
| 新增 | `Assets/Editor/C3FactoryFullArtBuilder.cs`、`C3FactoryArtConstruction.cs`、`C3FactoryArtMaterials.cs`、`C3FactoryArtLighting.cs`、`C3FactoryArtAudio.cs`、`C3FactoryArtDetails.cs` | 分階段建造與 Editor 靜態稽核。 |
| 新增 | `Assets/Tests/PlayMode/C3FactoryFullArtTests.cs` | 晶片、提示、解謎、音訊與畫面擷取測試。 |
| 新增 | `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/ArtFull/` | 63 個自製 Mesh 資產、15 個正式材質、1 個建造用佔位材質、9 張貼圖、2 段 WAV 音訊及 `.meta`。逐檔見 [Asset_Manifest.csv](C3_FullArt_Evidence/Asset_Manifest.csv)。 |
| 新增 | `DevelopmentReports/C3_FullArt_Backups/`、`C3_FullArt_Evidence/` 與本報告 | 施工基準、測試 XML／日誌、稽核文字與視覺擷取。 |

施工前已存在的 `.vscode/settings.json`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectSettings.asset`、`Assets/Shared/Player`、`Assets/Tests/PlayMode/M7` 及 M2 相關未提交檔案均保留；本任務沒有主動編輯這些工作。`ONLYfactory.fbx` 與其 `.meta` 在最終 Git 狀態中無變更；FBX 的最終 SHA-256 為 `BC6FB179FFFB74FD7FA11434C28D53E17BB2413B3F7A9032FBB309440E4A2058`。

## 3. 場景 Hierarchy 與配置

新增根物件 `C_Factory_Art`，位於正式場景中，與 `ONLYfactory`、`C_TwoFloor_Blockout`、`M3_Treasure`、`M4_PuzzleConsole` 及 `M2_Player` 平行。以下數量為**直接子物件**，完整物件名稱與世界座標見 [Final_StaticAudit.txt](C3_FullArt_Evidence/Final_StaticAudit.txt)。

| 子階層 | 直接子物件數 | 主要內容 |
|---|---:|---|
| `Architecture_Additions` | 30 | 開放式護欄視覺、樓梯弦梁／踏面鼻端、二樓板下鋼梁、I 型支柱與斜撐。M2 原碰撞維持。 |
| `Floor1_Industrial` | 3 | 西側兩座配電櫃、東側工具櫃。 |
| `Floor2_Industrial` | 3 | 晶片維修桌、配電箱、管制警示板。 |
| `IndustrialMachines` | 4 | 淨化反應槽、廢棄物分類線、進料泵、東北雙筒過濾組。 |
| `Pipes` | 4 | 西側維修主管、北側過濾管、反應槽回流管、二樓線纜導管。 |
| `Props` | 9 | 兩座廢料箱、三只鐵桶、兩塊警示板、兩個工具箱。 |
| `Materials_Visuals` | 0 | 保留分類節點；材質為 `ArtFull/Materials` 專案資產，不須場景物件。 |
| `Lighting` | 8 | 5 暖色工業燈、2 冷青指示燈、1 紅色故障燈。 |
| `Audio` | 2 | 循環環境底噪與設備啟動音節點。 |
| `Decorations` | 11 | 8 處油污／灰塵貼片、3 處設備或維修紀錄文字。 |

### 一樓

玩家仍於西南入口 `(-21.000, 0.580, 4.000)` 出生；入口有故障警示燈與標誌。西側 `x≈-23` 為配電維修區；中央 `(-12.5,0.55,9.5)` 放置淨化反應槽，左側 `(-16,0.55,7.5)` 為分類線，右側 `(-8.5,0.55,8)` 為進料泵。東北 `(-5,0.55,21)` 為過濾設備；原控制台仍在 `(-0.900,1.000,18.950)`。保留原本 1F 地板、外牆、天花板及出入口代理碰撞。

### 二樓

保留 M2 局部高架布局：主走道中心 `(-13.5,4.4,15.4)`、尺寸 `(17,0.2,3.2)`；晶片平台中心 `(-12.5,4.4,18.5)`、尺寸 `(3,0.2,3)`；觀察平台中心 `(-7,4.4,18.5)`、尺寸 `(4,0.2,3)`。**以上是 M2 原場景物件的世界中心／BoxCollider 尺寸，單位 Unity unit**，不是本次新增的幾何測量值。西梯中心線 `x=-21.1`、東梯 `x=-5.9`，均由南側 `z≈6.18` 上行至 `z≈13.9`；角色可西上、沿主走道東行、東下。二樓保留對中央作業區的俯瞰。晶片維修桌在 `(-13.2,4.5,19.28)`；附近維修紀錄寫明晶片移交上層。

## 4. 核心互動與世界座標

下表座標皆為 **Unity 場景 World Position，單位 Unity unit**，由 Unity Editor 載入正式場景的 [StageA_Chip.txt](C3_FullArt_Evidence/StageA_Chip.txt) 與 [Final_StaticAudit.txt](C3_FullArt_Evidence/Final_StaticAudit.txt) 取得；並非從圖片估讀。

| 物件 | 最終世界座標 | 引用／狀態 |
|---|---|---|
| `M3_Fuse` | `(-20.000, 0.980, 22.000)` | 原 `fuse` ID、Collider 與收集元件保留。 |
| `M3_ControlChip` | `(-12.500, 4.900, 18.500)` | **已搬至二樓**；原 Prefab／`control_chip` ID／Collider／收集腳本保留。3 unit 內射線第一命中為晶片。 |
| `M3_FilterModule` | `(-4.000, 1.030, 18.000)` | 原 `filter_module` ID、Collider 與收集元件保留。 |
| `M4_PuzzleConsole` | `(-0.900, 1.000, 18.950)` | 原 `C3PuzzleConsole`、`C3DeviceState`、進度及謎題引用流程保留；射線第一命中為控制台。 |
| `M2_Player` | `(-21.000, 0.580, 4.000)` | 原第一人稱控制器及相機保留。 |

`C3ModuleAdapter` 由母專案 `ModuleCoordinator` **執行時建立**，故它不在 Scene 靜態 Hierarchy 中；其完成回報、重新開始及返回選關已於 [Final_MotherFlow.xml](C3_FullArt_Evidence/Final_MotherFlow.xml) 驗證。

## 5. 模型、材質、燈光與聲音

63 個新增 Mesh 資產包含反應槽筒體、濾筒、分類線、泵浦、配電櫃、開放護欄、樓梯構件、鋼梁、支撐柱、管線、箱體、鐵桶及警示板；逐項檔名與大小在 [Asset_Manifest.csv](C3_FullArt_Evidence/Asset_Manifest.csv)。大部分設備採多件組合 Mesh，碰撞只配置在 14 個主要設備／道具裝配體，避免細小裝飾攔住玩家。

正式材質：`M_AgedConcrete_Floor`、`M_AgedConcrete_Wall`、`M_AgedConcrete_Ceiling`、`M_DarkIndustrialSteel`、`M_RustedSteel`、`M_PaintedMetalGrating`、`M_WornOliveMachinePaint`、`M_WornWarningYellow`、`M_OxidizedPipe`、`M_OilyRubber`、`M_FadedMaintenancePaper`、`M_OilDustOverlay`、`M_CyanInstrument`、`M_RedFaultIndicator`、`M_WarmIndustrialLamp`。9 張 256×256 原創程序式貼圖對應混凝土、鋼、鏽蝕、格柵、掉漆、警示、管線、油污及紙張。`M_BlockoutPlaceholder` 是建造中產生的額外資產，最終新增美術 Renderer 無佔位材質。原 FBX 的 C 區地板、天花板及四面牆只在**場景實例的 6 個材質槽**改為新材質；FBX 資產未覆寫。

燈光使用環境色 `(0.29,0.32,0.34)`、原方向光強度 `0.32`、輕度霧密度 `0.008`。8 盞新增局部點燈全無即時陰影，讓入口、反應槽、走道、控制台與晶片區可辨識；位置見 [Final_StaticAudit.txt](C3_FullArt_Evidence/Final_StaticAudit.txt)。音訊為自行合成的 8 秒循環低頻工廠底噪及 3 秒設備啟動聲；啟動聲在 `C3DeviceState.Activated` 後播放。聲音設定與來源見 [StageF_Audio.txt](C3_FullArt_Evidence/StageF_Audio.txt)。

## 6. 自動驗證與證據

所有以下 PASS 均指 **Unity 自動化檢查**，不等同真人遊玩驗收。Unity Editor 最終靜態稽核成功結束，腳本無新增編譯錯誤；[最終靜態稽核](C3_FullArt_Evidence/Final_StaticAudit.txt) 與 [日誌](C3_FullArt_Evidence/Final_StaticAudit_Unity.log) 記錄原 FBX 根 Transform 為單位變換、核心元件數量、M2 157 個碰撞體、新增設備 14 個碰撞體及新增設備碰撞體與 C 區牆代理交集數 0。

| 項目 | 結果 | 證據／測試界線 |
|---|---|---|
| Unity 編譯及階段 B–F Editor 驗證 | **PASS** | [材質](C3_FullArt_Evidence/StageD_Materials.txt)、[燈光](C3_FullArt_Evidence/StageE_Lighting.txt)、[音效](C3_FullArt_Evidence/StageF_Audio.txt)、[環境敘事](C3_FullArt_Evidence/StageF_Narrative.txt)、最終 Editor 日誌。 |
| 玩家由 1F 西梯上樓、2F 走道東行、東梯返回 1F；護欄阻擋、板面承載 | **PASS** | [Final_Route.xml](C3_FullArt_Evidence/Final_Route.xml)：以實際 `CharacterController.Move` 和現有 Collider 測試；仍需真人以 WASD 與滑鼠驗收。 |
| 控制晶片位置、Collider、三零件數、互動、HUD 線索 | **PASS** | [Final_Chip.xml](C3_FullArt_Evidence/Final_Chip.xml)、[StageA_Chip.txt](C3_FullArt_Evidence/StageA_Chip.txt)。 |
| 三零件／控制台必要射線未被新增設備遮蔽 | **PASS** | [StageC_Industry.txt](C3_FullArt_Evidence/StageC_Industry.txt)：四條指定 3 unit 內射線第一命中各自目標；其他玩家角度未逐一覆蓋。 |
| 未收齊零件不能解謎；錯序不能過關；正序、確認啟動、結算與啟動聲 | **PASS** | [Final_Logic.xml](C3_FullArt_Evidence/Final_Logic.xml)。 |
| 母專案模組完成回報、重新開始、返回選關、再次進入 | **PASS** | [Final_MotherFlow.xml](C3_FullArt_Evidence/Final_MotherFlow.xml)。 |
| 圖形模式場景擷取 | **PASS（擷取完成）** | [一樓反應槽](C3_FullArt_Evidence/Final_1F_Reactor.png)、[二樓走道](C3_FullArt_Evidence/Final_2F_Deck.png)、[二樓晶片區](C3_FullArt_Evidence/Final_2F_Chip.png)；[Final_Visual.xml](C3_FullArt_Evidence/Final_Visual.xml)。人工美感與可讀性仍待審核。 |
| 原 FBX 內部精確三角面穿插與全部視角遮擋 | **NOT_TESTED** | 已做牆代理 AABB、指定射線與三張視角擷取；沒有逐三角面完整掃描。 |
| 真人鍵鼠、E 鍵、卡點、音量、幀率及完整探索 | **NOT_TESTED** | 自動化不能替代人工 Play Mode 驗收。 |

## 7. 已知限制、人工驗收與下一步

- 材質為 256×256 程序式貼圖；生鏽、磨損與油污已建立辨識差異，但細節與貼圖解析度尚未達照片級寫實。格柵是**表面紋理搭配實心安全碰撞板**，未做實際透孔幾何。警示牌與維修紀錄是簡短英文文字；若需完整中文環境敘事，需另外配置合適字型與版面。
- 二樓晶片和控制台能由指定射線碰到，仍應由真人以正常第一人稱高度、滑鼠視角及 E 鍵實走檢查所有接近方向。音量、方向感、閃爍需求與效能也需在目標設備上調整；本次未加入閃爍腳本或大量即時陰影。
- 原始 FBX 大範圍室內幾何只以代理碰撞、局部畫面及遊玩路線驗證；邊角、所有視角的精確穿插與場景其他區域的視覺影響仍待人工檢查。
- `git diff --check` 對 Unity 序列化的 Scene YAML 空欄位回報尾端空白；Unity 編譯及場景測試均已通過。未為消除格式警告而改寫 Unity 的序列化資料。

人工驗收建議依序：① 從入口以 WASD／滑鼠沿兩條樓梯完成完整環線並觀察走道、護欄及支柱；② 以 E 收集三零件、確認 HUD 與二樓晶片提示；③ 分別測試未收齊、錯序、正序、啟動、結算、重新開始和返回選關；④ 檢查各設備與外殼有無穿插、線索標示是否遮擋、暗部能否安全辨路；⑤ 戴耳機確認環境聲與啟動聲音量；⑥ 在目標硬體測量幀率。下一輪可替換高解析度合法貼圖、加入更精細的格柵與燈具、改善中文標示與設備故事線、視人工結果微調照度和碰撞。

目前已停止施工，等待最終人工驗收。

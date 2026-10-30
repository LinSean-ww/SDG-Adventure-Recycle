# C3 二樓吊桿與工業支架接合修正報告

日期：2026-10-27（Asia/Taipei）  
專案：`SDG Adventure`／Unity 2023.1.22f1／Built-in Render Pipeline  
正式場景：`Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`

## 1. 範圍與修正前狀態

施工前 Git 工作樹的已追蹤檔案無修改；原本已有未追蹤的 C3 `.bak`／`.zip`、M6 WindowsBuild 等，均未清理或覆蓋。施工前場景副本 `C3_UpperSupport_Evidence/C_Main_PreUpperSupport_20261027.unity.bak` 的 Git blob 雜湊為 `b9a9b4bac5b292d18b489308dac5336d3866c2a2`，與階段性備份 Commit `4bff5808` 中的正式場景相同。完整施工前／後 Hierarchy、World Position、World Euler、Lossy Scale、Renderer Bounds 與 Collider Bounds 分別見 [Before_Inventory.tsv](C3_UpperSupport_Evidence/Before_Inventory.tsv) 與 [After_Inventory.tsv](C3_UpperSupport_Evidence/After_Inventory.tsv)。兩份資料皆由 Unity Editor 載入實際場景取得；修正前資料由上述原場景副本重新探測。

修正前五盞暖色燈均有 `CeilingMount/SuspensionRod`、`CeilingCrossbar`、`CeilingPlate`、`BellShade`、`LampSocket`；八根 `CeilingCableHanger` 從架空導管 `y=7.15` 延伸至碰撞代理底面 `y=9.9`。但 [FBX 屋頂射線](C3_UpperSupport_Evidence/Before_RoofRaycasts.tsv) 與 [子網格取樣](C3_UpperSupport_Evidence/Before_RoofCoverage.tsv) 確認：

| 原物件／吊點 | 實際 FBX 屋頂狀態 | 修正前問題 |
|---|---|---|
| `Warm_EntrySafety`、`Warm_WestService`、`Warm_EastService` | 正上方在 `y=10.0` 有實體網格 | 固定板頂在 `y=9.9`，與可見屋頂有約 `0.1` 間隙。 |
| `Warm_CentralReactor`、`Warm_UpperWalkway` | 正上方是大型圓形結構的開口，`y≈10` 無屋頂三角面 | 固定板只接到覆蓋全區的不可見碰撞代理，玩家視角呈現懸空。 |
| `CeilingCableHanger`，`x=-20,-6`，`z=14.8,16.0` | 四個上端正上方有屋頂網格 | 吊桿有代理接點，但缺乏可見錨座。 |
| `CeilingCableHanger`，`x=-15,-10`，`z=14.8,16.0` | 四個上端位於開口下方 | 吊桿上端沒有實際可見承重結構。 |
| `WestWallHanger_*`、`NorthWallHanger_*`、`ReactorDeckHanger_*` | 已接西／北牆與主平台下緣 | 未發現需要移位的端點，保留原位。 |

上述圓形結構是 `ONLYfactory/factory` 的 FBX 子網格 13；平屋頂是子網格 12。屋頂代理 `C_CollisionProxies/C_Ceiling` 的 Collider 為 `(-25,9.9,0)..(0,10.1,25)`，**不能**用該代理的接觸判定取代可見網格接觸。FBX 匯入後的實際子網格 Bounds 見 [Before_FBX_SubmeshBounds.txt](C3_UpperSupport_Evidence/Before_FBX_SubmeshBounds.txt)。

## 2. 施工方式及新增物件

在 `C_Factory_Art/Architecture_Additions/UpperServiceSupportFrame` 下新增 3 條窄型 I 梁，沿 X 軸橫跨圓形開口，位於 `z=9.5`、`14.8`、`16.0`；每條梁有 `LowerFlange`、`Web`、`UpperFlange`，兩端各有一塊 `RoofAnchor_-20`／`RoofAnchor_-6`。六個錨點均由 Unity FBX MeshCollider 射線確認在實際屋頂 `y=10.0`，中間三個開口取樣仍無屋頂命中。梁位於圓形幾何下方，沒有封閉開口或修改 FBX。

同階層新增：

| 物件／數量 | 用途與接點 |
|---|---|
| `RoofBridge_9.5`、`RoofBridge_14.8`、`RoofBridge_16.0`，各 5 個可見構件 | 將中央燈及兩條架空導管的負載傳至 `x=-20`／`-6` 處實體屋頂。 |
| `ConduitBridgeTie_-20/-15/-10/-6`，4 根 | 橫接 `z=14.8` 與 `z=16.0` 的雙導管上方鋼梁，使吊掛關係可辨識。 |
| `Warm_UpperWalkway_BridgeTie`，1 根 | 在 `x=-14` 橫接兩條鋼梁；下緣與二樓暖燈固定板頂端在 `y=9.62` 相接。 |
| `ConduitSaddle_<x>_<z>`，8 個 | 位於每根導管吊桿下端，包住 `y=7.15` 的原架空導管。 |

新增結構合計 32 個 GameObject（含容器），沒有新增 Collider。原八根吊桿的 BoxCollider 保留；新增件最低 Renderer Bounds 高於 `y=6.9`，高於二樓走道頂 `y=4.5` 加一般站立角色高度 `1.8` 的空間。原 M2 樓板、樓梯、護欄與支柱的 Collider／位置均未更動。

## 3. 原有吊桿與燈具前後數值

以下均為 Unity 世界座標，旋轉是 World Euler，縮放是 Lossy Scale。未特別標示的 X、Z、旋轉和非 Y 軸縮放前後相同；完整數值及 Bounds 可在前後 TSV 逐列核對。

### 暖色燈根物件：五盞全部保留原位

| 燈具 Hierarchy（皆在 `C_Factory_Art/Lighting/` 下） | 修正前後 World Position | World Euler／Lossy Scale |
|---|---|---|
| `Warm_EntrySafety` | `(-20.900,6.800,4.100)` | `(0,0,0)`／`(1,1,1)` |
| `Warm_WestService` | `(-22.000,7.150,19.200)` | `(0,0,0)`／`(1,1,1)` |
| `Warm_CentralReactor` | `(-12.500,7.100,9.500)` | `(0,0,0)`／`(1,1,1)` |
| `Warm_EastService` | `(-4.100,7.100,13.400)` | `(0,0,0)`／`(1,1,1)` |
| `Warm_UpperWalkway` | `(-14.000,8.050,15.500)` | `(0,0,0)`／`(1,1,1)` |

每盞燈下列子物件皆在 `CeilingMount/` 下；燈的 `Light`、`IndicatorLens`、`BellShade`、`LampSocket` 保持原位與原方向。吊桿 World Euler 全部保持 `(0,0,0)`。

| 燈具 | `SuspensionRod` World Position 前 → 後 | Lossy Scale 前 → 後 | 固定板頂端前 → 後 |
|---|---|---|---|
| 入口 | `(-20.900,8.405,4.100)` → `(-20.900,8.440,4.100)` | `(0.085,2.830,0.085)` → `(0.085,2.900,0.085)` | `9.90` → `10.00`，貼實體屋頂 |
| 西側 | `(-22.000,8.580,19.200)` → `(-22.000,8.615,19.200)` | `(0.085,2.480,0.085)` → `(0.085,2.550,0.085)` | `9.90` → `10.00`，貼實體屋頂 |
| 中央反應爐 | `(-12.500,8.555,9.500)` → `(-12.500,8.390,9.500)` | `(0.085,2.530,0.085)` → `(0.085,2.200,0.085)` | `9.90` → `9.62`，接 `RoofBridge_9.5` |
| 東側 | `(-4.100,8.555,13.400)` → `(-4.100,8.590,13.400)` | `(0.085,2.530,0.085)` → `(0.085,2.600,0.085)` | `9.90` → `10.00`，貼實體屋頂 |
| 二樓走道 | `(-14.000,9.030,15.500)` → `(-14.000,8.865,15.500)` | `(0.085,1.580,0.085)` → `(0.085,1.250,0.085)` | `9.90` → `9.62`，接新增橫向 Tie |

五個 `CeilingCrossbar` 的 World Position Y：原皆 `9.790`；入口／西側／東側變為 `9.890`，中央／二樓變為 `9.510`。其 World Euler 均為 `(0,0,270)`、Lossy Scale 均為 `(0.090,0.560,0.090)`，前後未變。五個 `CeilingPlate` 的 World Position Y：原皆 `9.835`；前三個實體屋頂點變為 `9.935`，中央／二樓變為 `9.555`。其 World Euler 均為 `(0,0,0)`、Lossy Scale 均為 `(0.600,0.130,0.450)`，前後未變。

### 架空導管吊桿：八根位置配置保留

完整路徑前綴：`C_Factory_Art/Pipes/Pipe_MountsAndConnections/`。全部 World Euler `(0,0,0)`；X、Z 未移動；下端仍接 `Pipe_UpperCableConduit` 的 `y=7.15`。`x=-20/-6` 四根仍由實體屋頂錨固，World Position Y `8.525`、Lossy Scale `(0.080,2.750,0.080)` 均未改。開口下方四根只縮短上端以接 I 梁下緣：

| 原物件 | World Position 前 → 後 | Lossy Scale 前 → 後 |
|---|---|---|
| `CeilingCableHanger_-15.0_14.8` | `(-15.000,8.525,14.800)` → `(-15.000,8.385,14.800)` | `(0.080,2.750,0.080)` → `(0.080,2.470,0.080)` |
| `CeilingCableHanger_-15.0_16.0` | `(-15.000,8.525,16.000)` → `(-15.000,8.385,16.000)` | 同上 |
| `CeilingCableHanger_-10.0_14.8` | `(-10.000,8.525,14.800)` → `(-10.000,8.385,14.800)` | 同上 |
| `CeilingCableHanger_-10.0_16.0` | `(-10.000,8.525,16.000)` → `(-10.000,8.385,16.000)` | 同上 |

其他支架用途與狀態：`WestWallHanger_4.0/9.0/14.0/18.6/22.0` 五根接西牆與西側水管；`NorthWallHanger_-21.0/-16.0/-10.5/-5.0` 四根接北牆與北側供料管；`ReactorDeckHanger_14.4/17.0/19.5` 三根接反應爐回流管與平台下緣；`C_TwoFloor_Blockout/Supports` 的十二根實體支柱承托主平台與晶片平台。這些既有物件的 World Position／Euler／Lossy Scale 前後均相同，逐根數值及 Bounds 見前後 TSV。沒有移除原有支架、吊桿或工業設備。

## 4. 生成工具同步與檔案清單

- 修改正式 `C_Main.unity`：新增上述 32 個連接物件，調整 4 根開口吊桿與 5 盞暖燈各 3 個安裝子件。場景 YAML 以 fileID 比對：原 2044 區塊沒有刪除，新增 120 區塊，僅 20 個原區塊有變更（19 個原物件 Transform，另 1 個父階層子物件清單）。
- 修改 `Assets/Editor/C3SceneBugFixTool.cs`：原工業燈施工階段同步呼叫 `C3UpperSupportFixTool.ApplyInOpenScene`；後續重跑整段生成流程時，使用實體屋頂錨固的支架規則。燈具驗證改為依實際屋頂與橫梁接點高度檢查。
- 新增 `Assets/Editor/C3UpperSupportFixTool.cs` 及 `.meta`：提供局部施工、從施工前副本盤點、屋頂網格取樣和驗證；重複施工會拒絕建立第二組結構。
- 新增 `Assets/Tests/PlayMode/C3UpperSupportVisualTests.cs` 及 `.meta`：僅產生四個本次結構的玩家視角截圖。
- 新增本報告及 `C3_UpperSupport_Evidence/` 下的 Unity 日誌、TSV、XML、四張 PNG 與施工前場景副本。沒有修改 `ONLYfactory.fbx`、`Assets/Shared/Player/`、M7 測試、其他關卡、遊戲狀態機、玩家移動或互動程式。

原 `ONLYfactory.fbx` SHA-256 仍為 `BC6FB179FFFB74FD7FA11434C28D53E17BB2413B3F7A9032FBB309440E4A2058`，與前次 C3 報告相同。沒有使用 Git reset／checkout／clean，沒有 Commit 或 Push。

## 5. Unity 自動檢查與可核對視覺證據

| 檢查 | 結果與範圍 |
|---|---|
| Unity C# 編譯、正式場景載入 | PASS；`CompleteTie_Unity.log` 沒有 `error CS` 或 `Exception:`；局部 Editor 驗證完成。 |
| 結構端點與圓形開口 | PASS（靜態）；6/6 個外端錨點命中實體 FBX 屋頂，3/3 個中間取樣仍為開口；5 個燈位與 8 根管線吊桿的配置保留。詳見 [After_Validation.txt](C3_UpperSupport_Evidence/After_Validation.txt)。 |
| 碰撞／動線 | PASS（局部自動）；美術層 Collider 仍 54、M2 BoxCollider 仍 157；新增件 Collider 為 0。原兩座樓梯與主走道的膠囊路線測試 1/1 Passed，見 [StairRoute.xml](C3_UpperSupport_Evidence/StairRoute.xml)。 |
| 核心互動射線 | PASS（局部自動）；保險絲、二樓晶片、濾芯及控制台 4/4 個原測試射線首命中不變；M7 共用玩家元件數 0。 |
| 視角截圖測試 | 1/1 Passed；[Visual_AfterTie.xml](C3_UpperSupport_Evidence/Visual_AfterTie.xml) 僅確認截圖流程成功，不能替代真人視覺驗收。 |

四個修正後玩家視角：[架空導管與鋼梁](C3_UpperSupport_Evidence/Upper_ConduitBridge.png)、[西側實體屋頂錨點](C3_UpperSupport_Evidence/Upper_WestBridgeAnchor.png)、[反應爐燈橫梁](C3_UpperSupport_Evidence/Upper_ReactorLampBridge.png)、[二樓走道燈與橫向連接](C3_UpperSupport_Evidence/Upper_WalkwayLampBridge.png)。

## 6. 限制、人工驗收與 Git 狀態

尚未宣稱真人視角驗收 PASS。請最後在 Unity 中以正常玩家高度沿兩座樓梯與二樓走道巡視，特別看圓形開口、中央與二樓暖燈的固定板、雙導管吊桿下端和新 I 梁端部；確認沒有細小穿插、視覺遮擋或碰撞卡點。完整三零件尋寶、解謎與通關流程沒有因本次未改遊戲邏輯而重跑，其先前 PASS 紀錄保留，真人流程仍可於最終人工驗收一併確認。

最終工作樹有本次未提交的正式場景、兩個 Editor／測試工具及報告證據；原先未追蹤的 `.bak`／`.zip` 和 M6 WindowsBuild 仍保留。沒有暫存檔案，沒有 Commit 或 Push。

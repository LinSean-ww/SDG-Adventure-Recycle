# C3_TreasurePuzzle｜雙層工廠場景唯讀盤點與設計前置

盤點日期：2026-10-26（Asia/Taipei）  
狀態：**設計候選，待人工審核；未建造、未修改場景或 C#**。

## 1. 範圍、來源與座標規則

- 已從目前工作區確認專案根目錄為 `C:\Users\h7796\Desktop\Project\Codex Work\Unity\SDG Adventure`；`ProjectSettings/ProjectVersion.txt` 為 **2023.1.22f1**。`DevelopmentReports` 已存在。
- 場景：`Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`；模型：同目錄上層的 `Models/ONLYfactory.fbx`，其 `.meta` GUID 為 `96a581a8f5e930c4a9b52806b4597863`。既有 `DevelopmentReports/M6/VisualProbe.txt` 記錄管線為 Built-in；本次沒有啟動 Unity 重新驗證渲染設定。
- **S** 表示從 Scene/Prefab YAML 的 Transform、Collider 直接讀取，或依零旋轉、單位縮放父物件相加所得的**序列化世界座標**；不是 Play Mode 動態值。本報告以 +X 稱東、+Z 稱北，Y 向上；單位依場景配置視為 Unity unit（通常為公尺）。**P** 表示本報告提出的未建造候選尺寸／座標。**F** 表示直接解析 FBX 原始頂點、材質多邊形、模型變換後，依場景碰撞代理匹配推導的 Unity 軸向；不是 Unity 匯入後的 `Renderer.bounds`。所有區間均為軸對齊範圍。
- 本次只讀取磁碟上的檔案；未打開 Unity Editor 執行 `Renderer.bounds`、Physics overlap、可行走或互動實測。這些項目一律列為**待確認**，尤其是 FBX 其餘材質區塊、欄柱及視線遮擋。沒有把設計尺寸當成測量結果。

## 2. 場景現況與 Hierarchy

`C_Main.unity` 的 `SceneRoots` 記錄 7 個根層物件。以下為主要層級；`ONLYfactory` 是 FBX Prefab 實例，內部模型節點未完整展開於 Scene YAML，需在 Unity Hierarchy 進一步核對。

| 根物件／主要子物件 | 現況與作用 | 來源 |
|---|---|---|
| `ONLYfactory` | FBX 實例；根 Transform 位置 `(0,0,0)`、旋轉 `(0,0,0,1)`、縮放 `(1,1,1)`。模型內有 `siteC_floor`、`siteC_ceiling`、`siteC_wall_1..4`、`siteC_Big_Chimney` 等材質／幾何分區；**不能把材質名直接當成可獨立移動的 GameObject**。 | S：Scene `PrefabInstance`；F：FBX `Geometry/LayerElementMaterial` |
| `Directional Light` | 場景方向光。 | S：Scene |
| `C_LevelMarkers` → `PlayerSpawn`、`TreasureArea`、`PuzzleConsoleArea`、`FinishArea` | 無 Collider 的定位標記。`PuzzleConsoleArea` 下有控制台與青色導引標記。`FinishArea` 只是標記，通關依設備狀態。 | S：Scene |
| `C_CollisionProxies` → `C_Floor`、`C_Ceiling`、`C_Wall_1_Left/Right/Lintel`、`C_Wall_2/3`、`C_Wall_4_Near/Far/Lintel` | C 區地、頂、牆的啟用非 Trigger BoxCollider；另有 B、D 側門地坪、門框裝飾和安全擋板。FBX importer 的 `addColliders: 0`，目前主要實體邊界由這些代理提供。 | S：Scene、FBX `.meta` |
| `M2_Player` | 玩家 Prefab 實例，含第一人稱控制器、CharacterController、相機及 E 鍵互動器。 | S：Scene + `Prefabs/M2_Player.prefab` |
| `M2_TestInteractable` | 起點附近的測試互動方塊，有非 Trigger BoxCollider；建造時應保留其可測性或在後續另行決策。 | S：Scene |
| `M3_Treasure` → `M3_Fuse`、`M3_ControlChip`、`M3_FilterModule` | 三個收集 Prefab；根物件還持有進度、謎題、設備、HUD、結算等場景狀態元件。 | S：Scene + 3 Prefab |
| `PuzzleConsoleArea` → `M4_PuzzleConsole` → `Housing`、`Display`、`ControlStrip`、`StepIndicator1..3`、`M5_ActivationLight` | 實際 E 鍵控制台；顯示器和設備啟動燈附屬於控制台。 | S：Scene |

一樓入口配置目前是「C 區內出生」，玩家實例與 `PlayerSpawn` 標記不在同一個 Y 高度；名為 `FinishArea` 的標記沒有觸發器，不能據此推斷走到該處會通關。

## 3. 互動物件與世界座標

表內座標均為 **S／Unity 序列化世界座標（x,y,z）**，來自 `C_Main.unity` 的實例覆寫或父子 Transform 相加。碰撞尺寸為 Collider 在目前 Prefab/Scene 縮放下的世界尺寸，並非 Renderer bounds。

| 物件 | 位置 S | Collider／引用／互動 |
|---|---:|---|
| `M2_Player` 實際出生 | `(-21, 0.58, 4)`；朝向約 `+X`（Y 軸 90°） | CharacterController 高 `1.8`、半徑 `0.3`、坡度 `45°`、台階 `0.3`。根座標是腳底附近；落地後是否沉降到地坪 `y=0.55` 待 Play Mode 確認。 |
| `PlayerSpawn` 標記 | `(-21, 1.5, 4)` | 空 Transform，與實際玩家相差 `0.92` Y；非出生邏輯證據。 |
| `M2_TestInteractable` | `(-18.5, 1.45, 4)` | BoxCollider 尺寸 `(0.6,1.8,0.6)`，非 Trigger；`C3TestInteractable`。 |
| `M3_Fuse` 保險絲 | `(-20, 0.98, 22)` | CapsuleCollider，非 Trigger，縮放後約半徑 `0.14`、高度 `0.76`；`C3CollectiblePart`，ID `fuse`。 |
| `M3_ControlChip` 控制晶片 | `(-12.5, 0.95, 14)` | BoxCollider 尺寸 `(0.66,0.18,0.5)`，非 Trigger；`C3CollectiblePart`，ID `control_chip`。**現況仍在 1F**。 |
| `M3_FilterModule` 過濾模組 | `(-4, 1.03, 18)` | SphereCollider，非 Trigger，縮放後半徑 `0.35`；`C3CollectiblePart`，ID `filter_module`。 |
| `M4_PuzzleConsole` | `(-0.9, 1, 18.95)` | BoxCollider 尺寸 `(0.9,1.4,0.55)`、中心相對 `(0,0.2,0)`，非 Trigger；連到 `M3_Treasure` 的 progress、puzzle、device。 |
| `M5_ActivationLight` | `(-0.9, 1.85, 18.6)` | Point Light，控制台子物件；`C3DeviceVisual` 參照它與 Display。 |
| `TreasureArea`／`PuzzleConsoleArea`／`FinishArea` | `(-20,1,20)`／`(-2,1,20)`／`(-2,1,4)` | 都只是標記；`M3_PlannedConsoleGuide` 的根與 `PuzzleConsoleArea` 重合。 |

三個零件的 Prefab `progress` 欄位均為空引用 `{fileID: 0}`；`C3CollectiblePart.Awake()` 會以 `FindObjectOfType<C3TreasureProgress>()` 尋找場景進度元件。Scene 中零件為 `M3_Treasure` 子物件，進度元件在其根上。拾取後加入對應 ID 並停用零件物件。這是**程式碼與序列化關係**，實際 Play Mode 引用解析仍待驗證。控制台的三個狀態引用則在 Scene 中明確指向 `M3_Treasure` 元件。

## 4. 可用空間、FBX 與高度

| 資料 | 範圍／高度 | 證據與可信度 |
|---|---|---|
| C 區實體地坪代理 | `x=-25..0`、`z=0..25`、BoxCollider `y=0.35..0.55`，頂面 `y=0.55` | **S**：`C_Floor` 中心 `(-12.5,0.45,12.5)`，尺寸 `(25,0.2,25)`。碰撞有效範圍已由序列化資料確認。 |
| C 區頂部代理 | 同 `x/z`，Collider `y=9.9..10.1`，底面 `y=9.9` | **S**：`C_Ceiling` 中心 `(-12.5,10,12.5)`，尺寸 `(25,0.2,25)`。 |
| 名義垂直淨高 | `9.9-0.55=9.35` | **S 計算**：僅地坪代理至頂部代理，不代表全區無梁、設備或 FBX 幾何。 |
| FBX `siteC_floor` 面 | 推導約 `x=-25.02..-0.29`、`z≈0..25.02`，`y≈0.50` | **F**：FBX 工廠主 Geometry 的 `siteC_floor` 材質多邊形，結合模型內 `factory` 變換及與 Scene 代理吻合的軸向；Unity 匯入後 Renderer bounds 待確認。 |
| FBX `siteC_ceiling` 面 | 推導約 `x=-25.02..0.02`、`z=0.30..25.02`，`y≈10.00` | **F**：同法；代理天花板底面更低，設計以 `y=9.9` 為限制。 |
| FBX `siteC_Big_Chimney` 材質區 | 推導包圍盒約 `x=-17.52..-3.58`、`z=3.84..20.95`、`y=10.00..21.62` | **F**：材質分組的軸對齊頂點範圍。煙囪自天花板高度向上延伸；不證明 1F/2F 沒有其他模型構件。 |
| `factory` 主幾何的整體來源包圍盒 | 推導約 `x=-25.03..25.03`、`z=-25.03..25.03`、`y≈0..46.28` | **F**：FBX 原始主 Geometry 全部頂點（包含 C 區外、屋頂及工業物）；**不是整個 ONLYfactory Prefab 的 Unity Renderer.bounds**。完整 Prefab Bounds 待 Unity Editor 確認。 |

`ONLYfactory` 的 **Scene 實際根 Transform 已確認為單位變換**。FBX 本體採 Z-up、`UnitScaleFactor=1`；上述 F 範圍是原始頂點及模型變換後，用 C 牆/地代理校核的 Unity 軸向推導。`Renderer.bounds`、每個進口子 Mesh 的 Bounds、模型內管線／平台／柱的精確佔位均**待確認**。現有 Scene 序列化資料沒有獨立的 2F 樓板、樓梯或護欄。FBX 有 C 區牆、地板、天花板和大型煙囪，可作保留的建築外殼。FBX 雖有 `CoreLine`、`LineOutPut`、`Pillar` 等材質分區，依原始材質多邊形的推導 Bounds 位於 C 區外或天花板以上，**不能當作 C 區室內已存在的管線／支柱／平台**。`FNC_FactoryNormalColor` 等共用材質幾何仍可能佔據預定二樓空間，不能僅靠整體包圍盒排除穿插。

C 區南側牆 `z≈0.32` 在 `x≈-15.5..-9.5` 留 D 門洞；東側牆 `x≈-0.3` 在 `z≈9.5..15.5` 留 B 門洞。Scene 門洞地坪頂面同為 `y≈0.55`，上方 lintel 代理最低約 `y=3.5`，提供約 `2.95` 的代理淨高。但門外有 `D_Safety_*`／`B_Safety_*` 非 Trigger 擋板；這些是**可辨識的出入口形狀，不是已確認可通行路線**。原始 FBX 的門框和牆面亦需在 Editor 核對。

## 5. 玩家能力與既有邏輯約束

- `M2_Player.prefab` 與 `C3FirstPersonController.cs`：站立 `1.8` 高、蹲下 `1.1` 高；眼高 `1.5/0.85`；步行 `4`、跑步 `7`、蹲行 `2` Unity unit/s；允許 Space 跳躍，`jumpHeight=1.2`，重力 `20`。跳高是程式設定的理論值，不能當作可跳過缺口的安全保證。第一人稱、滑鼠視角、ESC 游標行為需保留。
- `C3PlayerInteractor.cs`：鎖定游標時由相機向前作最遠 `3` unit 的 Physics raycast（忽略 Trigger），**僅第一個命中**可成為互動目標；按 `E` 觸發。新護欄、樓梯、管線和平台 Collider 可能先於零件或控制台擋住射線。
- `C3TreasureProgress.cs` 需要 `fuse`、`control_chip`、`filter_module` 三 ID。`C3PuzzleState.cs` 的固定正解為 **廢棄物分類 → 資源回收 → 污染處理**。`C3PuzzleConsole.cs` 要求收齊零件、解題後再次確認設備啟動；`C3DeviceState.cs` 完成並發送事件；`C3CompletionScreen.cs` 提供結算、重新開始與返回選關。`C3ModuleAdapter.cs` 會確認場景中恰有一套設備／玩家／相機／AudioListener，並向母專案回報完成。第二層設計不得新增重複的這些核心元件。
- 單純新增靜態平台原則上不改變狀態機，但**不能宣稱「必定不影響」**：若阻斷零件射線、走道、控制台、出生碰撞或把控制晶片移到無法抵達／看見的位置，收集與通關會實際失效。新增 2F 後須做 Play Mode 回歸。

## 6. 雙層配置候選（全部 P，尚未建造）

設計採 **1F 維持入口、維修／淨化設備、保險絲、過濾模組與控制台；2F 金屬走道及控制晶片尋寶**。1F 保留中央通行與東側控制台視線；2F 以西側上樓、東側下樓形成環線，避免把外側有擋板的 B/D 門洞當作疏散或必經路。舊鏽金屬、局部暗燈、密集管線和廢棄設備屬美術提案；新管線與道具應避開玩家 `1.8` 高的行走淨空及互動射線。

| 候選構件 | 建議 P／Unity 世界空間 | 依據與待確認條件 |
|---|---|---|
| 2F 主走道 | 走道頂面 `y=4.50`，板厚約 `0.20`（底面 `4.30`）；平面 `x=-22..-5`、`z=14..17`，寬 `3.0`、長 `17`。實際可走區可縮為 `1.8..2.2` 寬並留護欄。 | 地坪到走道頂面升高 `3.95`；板底至地坪代理頂面 `3.75`；走道頂至天花板代理底面 `5.40`。這些只由 **S 代理與 P 平台**計算，FBX 淨空待查。 |
| 西側上行樓梯 A | `x=-23..-21.2`，約 `z=6..14`；由南 `z≈6,y≈0.55` 向北升至 `z≈14,y=4.50`。建議淨寬 `1.8`、22 個約 `0.18` 升高／`0.36` 踏深的踏步，水平投影約 `7.92`。 | 以玩家 `0.3` 半徑、`0.3` Step Offset 和地坪／平台高差制定的**設計值**；實際樓梯幾何、踢面、入口及頂端接縫待 Unity 行走測試。 |
| 東側下行樓梯 B | `x=-6.8..-5`，約 `z=6..14`；從北端 2F 走道向南下降至 1F。寬度、升高、踏深同 A。 | 和控制台 `(-0.9,1,18.95)` 保持平面距離，但抵達 1F 後仍需測試通往過濾模組與控制台的動線。 |
| 2F 控制晶片候選 | 可考慮 `(-12.5, 4.9, 15.5)` 附近，配局部維修平台；**這是待審核的移位提案，不是現況**。 | 須保留原 Prefab、ID、Collider、腳本及收集引用；晶片高度與表面支架需依實際視線／碰撞調整。現況晶片仍位於 `(-12.5,0.95,14)`。 |
| 護欄、支柱 | 暴露邊護欄建議高 `1.1`、扶手/立柱碰撞連續；只在樓梯接入口留缺口。支柱先以 `x≈-21.5,-13.5,-6` 與 `z≈15.5` 作定位候選，截面與數量**待確認**。 | 避開現有晶片平面位置、1F 主通道、FBX 梁柱、零件 `3` unit 射線及任何門洞；不得在未做重疊檢查前固定。 |

**候選遊玩路線**：玩家從 `(-21,0.58,4)` 出發，在 1F 找西北側保險絲 `(-20,0.98,22)` → 返回西側樓梯 A → 2F 北側高架走道／維修平台尋找晶片 → 由東側樓梯 B 回 1F → 收集東北側過濾模組 `(-4,1.03,18)` → 到東側控制台 `(-0.9,1,18.95)` 解題與啟動。路徑只描述設計意圖；FBX 原始構件可能使路徑需要改線。

## 7. 穿插、碰撞及動線風險與驗證門檻

| 風險 | 現有證據／影響 | 下一階段確認 |
|---|---|---|
| FBX 內部干涉 | 代理顯示 2F 名義高度充足，FBX `siteC_Big_Chimney` 自 `y≈10` 起；但共用材質 `FNC_FactoryNormalColor` 與其他模型可能在 `y=0.55..9.9` 有結構／裝飾。不能由材質整體 Bounds 排除走道或支柱穿插。 | Unity Editor 展開 FBX，量各 Renderer.bounds；在 `y≈4.3..6.3` 做水平切面及候選 BoxCollider overlap；核對管線／設備。 |
| 天花板／頭部與上樓 | 2F 頂面至頂部代理 `5.4`，理論足夠，但樓梯頂、梁和管線可能使站立 `1.8` 高角色被擋；蹲行不可作主要動線。 | A/B 全段以 CharacterController 步行、跑步、蹲行測試，量最窄淨高及頂端接縫。 |
| 射線被新 Collider 擋住 | 互動器只取 `3` unit 內第一個非 Trigger 命中；新護欄／裝飾與上下層地板可能遮蔽三零件或控制台。 | 逐件從玩家正常站位對準 E 鍵互動，含新 2F 晶片的近距離視線。 |
| 現況晶片位於走道下方 | 晶片平面位置 `(-12.5,14)` 正鄰近候選走道南緣；若先造板但不遷晶片，可能藏在板下或被支柱／樓梯擋住。 | 同一施工階段安排晶片改位與可收集測試，維持原 Prefab/ID/引用。 |
| 門洞被誤作通路 | B/D 門外安全擋板啟用且非 Trigger；外部走廊是否可用未驗證。 | 不將其納入必經動線；如未來要開放，須另行設計並審核。 |
| 狀態機／母專案 | 通關由三零件、解謎、設備啟動驅動；額外 Player/Camera/Device 會觸發 Adapter 的場景所有權檢查失敗。 | 完整收集 → 正解 → 啟動 → 結算 → 重開／選關回歸；檢查 C3ModuleAdapter 的完成回報。 |

## 8. 下一階段建造工作清單（**僅供審核，尚未執行**）

1. 先在 Unity Editor 量 `ONLYfactory` 各相關 `Renderer.bounds`、`MeshFilter.mesh.bounds` 與 C 區內部管線／設備位置，記錄世界空間結果；檢查建議樓梯、平台、護欄、支柱包圍盒是否與 FBX 及碰撞代理交疊。
2. 人工確認西上東下路線、兩段樓梯與 2F 晶片目標位置；若 FBX 干涉，先調整設計座標，再開始建造。
3. 以新增獨立物件／Prefab 製作 2F 板、樓梯、欄杆與支柱，保留 `ONLYfactory.fbx`、原 Scene/Prefab/Material 及所有既有 C#；放置前備份並走審核流程。
4. 依批准座標搬移**現有** `M3_ControlChip` 實例，保持 `control_chip` ID、Collider、C3CollectiblePart 與進度關係；不要產生第四個零件。
5. 佈置鏽蝕金屬、廢棄設備、管線與局部照明，分層管理碰撞；保護站立淨高、樓梯入口、1F 零件和控制台視線。
6. 在 Unity Play Mode 完成行走、兩層往返、三零件 E 鍵收集、謎題正解與錯誤重試、設備啟動、結算、重新開始、返回選關及母專案模組回報檢驗；記錄實測座標、碰撞與畫面證據。

**人工審核前停止**：本報告只新增於既有 `DevelopmentReports`，未修改 `C_Main.unity`、FBX、Prefab、Material 或 C#，也未建立二樓。

# C3_TreasurePuzzle｜M2 雙層工廠白模建造報告

日期：2026-10-26（Asia/Taipei）  
Unity：2023.1.22f1，Built-in Render Pipeline  
狀態：**M2 白模已建立並完成指定的自動檢查；待人工驗收。M3 美術未開始。**

## 1. 施工範圍與工作樹保護

專案根目錄已核對為 `C:\Users\h7796\Desktop\Project\Codex Work\Unity\SDG Adventure`，先閱讀 `DevelopmentReports/C3_TwoFloor_Scene_Audit.md`。本次修改的關卡場景為 `Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity`。原 `ONLYfactory.fbx`、Prefab、Material、現有 C# 遊戲邏輯及其他關卡均未修改。

施工開始前的 Git 狀態已有 `.vscode/settings.json`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectSettings.asset` 修改，以及 `Assets/Shared/Player`、`Assets/Tests/PlayMode/M7`、`DevelopmentReports` 的未追蹤內容。本次未覆蓋或清理這些既有工作，亦未執行 commit/push。

施工前備份為 [`C_Main_PreBlockout_20261026.unity.bak`](C3_M2_Backups/C_Main_PreBlockout_20261026.unity.bak)，SHA-256 `ddcda92b04cc60062b9f2cadf6080c9ab13da11726de0bbb29b9d5ca962192e6`，建立時與原場景完全相同。施工後的 [序列化完整性檢查](C3_M2_SceneIntegrity.txt) 顯示：原有 179 個 YAML 區塊無缺漏，原有 GameObject、Transform、Component、PrefabInstance 區塊逐字不變；只有 `SceneRoots` 增列 `C_TwoFloor_Blockout`。新增 825 個序列化區塊。

Unity 儲存 Scene 時依 fileID 重排 YAML 區塊。`git diff --check` 將部分**原本就存在、但在 diff 中因重排顯示為新增**的空欄位行標為 trailing whitespace；序列化區塊逐字比對確認這些原件未被修改。本次沒有為了消除該格式警告而改寫既有物件。

本次另外新增 Editor 專用工具 `Assets/Editor/C3M2BlockoutTool.cs` 和 Play Mode 測試 `Assets/Tests/PlayMode/C3M2BlockoutPlayModeTests.cs`。兩者不參與 C3 遊戲邏輯，不更動既有 C#。

## 2. Unity Editor 空間驗證

Editor 實測來源為 [Bounds 探測資料](C3_M2_Bounds_Probe.txt)；該資料直接讀取 Unity 匯入後的 `Renderer.bounds`、各 `Mesh.GetSubMesh(i).bounds` 轉換到世界空間，以及原 Collider bounds。`ONLYfactory` 根 Transform 為位置 `(0,0,0)`、旋轉 `(0,0,0)`、縮放 `(1,1,1)`。

| 空間／構件 | Unity 世界空間實測 Bounds 或代理範圍 | 白模設計影響 |
|---|---|---|
| `ONLYfactory/factory` 整體 Renderer | min `(-25.025,-0.0002,-25.025)`；max `(25.0236,46.2816,25.0249)` | 涵蓋多區及屋頂，不能單靠整體包圍盒判斷室內穿插。 |
| `siteC_floor` 子網格 | min `(-25.0241,0.5000,-0.0048)`；max `(-0.2898,0.5002,25.0167)` | 對照一樓地坪。 |
| `siteC_ceiling` 子網格 | min `(-25.0177,9.9998,0.3000)`；max `(0.0177,10.0000,25.0224)` | 二樓頂面 `y=4.5`，距頂部碰撞代理底面 `y=9.9` 有 `5.4` Unity unit 的名義淨高。 |
| `siteC_Big_Chimney` 子網格 | min `(-17.5169,9.9998,3.8360)`；max `(-3.5789,21.6170,20.9465)` | 主要位於天花板以上。 |
| 原 C 區地坪／天花板碰撞代理 | 地坪頂 `y=0.55`；天花板底 `y=9.9`；平面 `x=-25..0,z=0..25` | 二樓板頂 `y=4.5`，板底 `y=4.3`；一樓板下名義淨高 `3.75`。 |

Unity Editor 以**暫時 MeshCollider**對原 FBX 主網格進行實際 Physics overlap 檢查，並以已知地板、牆面盒作陽性對照，兩者均命中原模型；主走道、晶片平台、觀察平台及東西樓梯的候選佔用／站立盒均未命中，詳見 [候選空間檢查](C3_M2_Candidate_Space_Check.txt)。建造後又逐一檢查全部 157 個新增 Collider：原 FBX 網格交疊 `0`，原牆／頂碰撞代理交疊 `0`，結果記於 [建造後驗證](C3_M2_PostBuild_Validation.txt)。支柱底面與原一樓地坪頂面對接，未穿入原模型。

## 3. 新增 Hierarchy

共新增 **177 個 GameObject**，其中 **157 個白模 Cube 都有啟用、非 Trigger 的 BoxCollider**；其餘為分組或純定位標記，沒有 Collider。全部掛在新根物件下；沒有重排既有關卡階層。

```text
C_TwoFloor_Blockout
├── Floor1_Additions
│   ├── F1_EntryStart_Zone
│   ├── F1_WestMaintenance_Zone
│   ├── F1_CentralOperations_Zone
│   ├── F1_NortheastFilter_Zone
│   ├── F1_Console_Zone
│   ├── F1_WestStairAccess
│   └── F1_EastStairLanding
├── Floor2_Platforms
│   ├── F2_MainWalkway
│   ├── F2_ControlChipBay
│   └── F2_ObservationDeck
├── Stairs
│   ├── Stair_West_Up / Tread_01..22
│   └── Stair_East_Down / Tread_01..22
├── Railings
│   ├── R_Stair_West_Up / GuardLeft_01..22, GuardRight_01..22
│   ├── R_Stair_East_Down / GuardLeft_01..22, GuardRight_01..22
│   └── 10 條平台邊緣護欄
├── Supports / 12 根獨立支柱
└── FutureGameplayMarkers
    ├── Future_M3_ControlChip_2F_Only
    └── F2_ObservationPoint
```

`Floor1_Additions` 是一樓功能分區及樓梯入口的**空 Transform 標記**；本次沒有在原一樓作業區加設會遮蔽零件、控制台或出生點的隔間。二樓只設局部平台，未鋪滿整個 C 區。

## 4. 新增物件位置與尺寸

下表均為 **Unity Editor 實測／保存後的世界座標**，格式 `(x,y,z)`，尺寸為 BoxCollider 世界 AABB `(X,Y,Z)`；單位為 Unity unit。所有 **177 個新增物件逐筆座標與尺寸**見 [C3_M2_NewObjects.csv](C3_M2_NewObjects.csv)。CSV 中無 Collider 的群組／標記以尺寸 `(0,0,0)` 表示「不適用」，不是實體尺寸。

| 結構 | 世界中心／尺寸 | 說明 |
|---|---|---|
| 2F 主走道 | `(-13.5,4.4,15.4)`／`(17,0.2,3.2)` | 頂面 `y=4.5`，從西側樓梯連到東側樓梯。 |
| 控制晶片尋寶平台 | `(-12.5,4.4,18.5)`／`(3,0.2,3)` | 與主走道北側連接；只是預留平台。 |
| 觀察平台 | `(-7,4.4,18.5)`／`(4,0.2,3)` | 可向一樓作業區俯瞰。 |
| 西側樓梯 22 階 | 中線 `x=-21.1`；每階 `(1.8,0.18,0.36)`；`z=6.18+0.36i`、頂面 `y=0.55+(i+1)×3.95/22`，`i=0..21` | 朝 +Z 上樓；第一階底 `y≈0.55`，最後一階頂 `y=4.5`、北端 `z=13.92`，與主平台南端 `z=13.8` 重疊銜接。每階升高約 `0.1795`，低於原 CharacterController `0.3` Step Offset。 |
| 東側樓梯 22 階 | 中線 `x=-5.9`，階數／尺寸／Z 和 Y 規則同西側 | 玩家朝 -Z 下樓，連回一樓。 |
| 樓梯雙側護欄 | 每階左右各 `(0.12,1.1,0.36)`；X 在各樓梯中心 ±`0.96`，中心 Y 為該階頂面 +`0.55` | 88 個連續的白模護欄段；平台邊緣另設 10 條護欄，兩個樓梯接入口保留開口。 |
| 平台邊緣護欄 | 高 `1.1`、厚 `0.12`，各段中心、長度見 CSV | 兩個樓梯入口未被橫向欄杆封住。 |
| 12 根支柱 | 每根 `(0.25,3.75,0.25)`，中心 `y=2.425`；X/Z 見 CSV | 底面 `y=0.55`、頂面 `y=4.30`，承接板底，不在原晶片或控制台位置。 |
| 未來晶片標記 | `(-12.5,4.9,18.5)`／無 Collider | **僅供後續 M3 搬移審核；不是晶片實例**。原 `M3_ControlChip` 仍在 `(-12.5,0.95,14)`。 |

## 5. 編譯、碰撞、通行及玩法驗證

| 項目 | 結果 | 證據／界線 |
|---|---|---|
| Unity 腳本匯入與編譯 | **PASS** | Unity batchmode 成功執行 Editor Probe、Build、Validate 及 Play Mode 測試；相關日誌沒有 `error CS`。 |
| 新結構 Collider | **PASS** | [建造後驗證](C3_M2_PostBuild_Validation.txt)：157/157 為啟用實體 BoxCollider。 |
| 原 FBX／邊界穿插 | **PASS（檢查範圍內）** | 暫時原模型 MeshCollider 陽性對照有效；157 個新增 Collider 與原 FBX 主網格及原牆／頂碰撞代理的 Physics overlap 均為 0。手工視覺巡檢仍需人工進 Editor 核對。 |
| 樓梯／平台幾何銜接 | **PASS** | 每階約 `0.1795` 升高；第一階接一樓地坪，最後一階與板頂 `y=4.5` 對齊，Z 投影重疊 `0.12`。 |
| 玩家上樓、橫越、下樓、防墜 | **PASS（受控 Play Mode 路徑）** | [M2 Play Mode 結果](C3_M2_PlayModeResults.xml)：真實 CharacterController 沿西樓梯上樓、主走道橫越、東樓梯下樓，且中央南側護欄阻擋站立膠囊。測試直接呼叫 `CharacterController.Move`，不等同真人 WASD 手動驗收。 |
| 出生點與原互動引用 | **PASS（靜態）** | 玩家出生點與三零件、控制台位置不變；場景各有 3 零件、1 progress/puzzle/device/player/console；控制台三狀態引用存在。四條正常站位射線第一命中分別為保險絲、晶片、過濾模組、控制台。 |
| 收集、解謎、設備啟動 | **PASS（元件 Play Mode）** | [M2 Play Mode 結果](C3_M2_PlayModeResults.xml)：逐一呼叫原零件互動元件後數量為 3，依原正解經控制台確認並完成設備啟動。沒有新增第四零件或修改收集 ID。 |
| 結算、重開、返回選關及母專案回報 | **PASS（既有整合 Play Mode）** | [M6Flow 回歸結果](C3_M2_FlowResults.xml)：既有 `C3CompletionRestartReturnAndReentry` 測試通過。該測試透過狀態 API 完成零件收集，與前項元件互動測試互補。 |
| 第一人稱真人 WASD、滑鼠視角、鍵盤 E 與 HUD 實際觀看 | **NOT_TESTED** | 自動測試未合成真實鍵盤／滑鼠輸入；需人工 Game 視窗驗收。原控制器／Interactor/HUD 程式與序列化原件未改動。 |
| 各角度互動、護欄全周、低幀率或不同畫質 | **NOT_TESTED** | 自動測試僅涵蓋指定路線、四條指定互動射線與中央一段防墜護欄。 |

## 6. 畫面與人工驗收

使用 Direct3D 11 的 Unity 圖形批次模式擷取 [一樓視角](C3_M2_VisualReview/From1F.png) 與 [二樓視角](C3_M2_VisualReview/From2F.png)；[視覺擷取測試](C3_M2_VisualResults_Graphics.xml) 通過。畫面顯示白色平台、階梯、護欄和支柱，且原工廠天花板與既有零件／控制台仍可見。先前 `-nographics` 擷取為均勻灰色，已由有效圖形擷取覆蓋；灰圖未用於驗收結論。

![一樓看向高架白模](C3_M2_VisualReview/From1F.png)

![二樓高架走道白模](C3_M2_VisualReview/From2F.png)

人工驗收仍需在 Unity Editor/Game 視窗檢查：從原出生點以 WASD/滑鼠走完整條路線、E 鍵實際拾取三零件、HUD 即時提示、控制台距離與視線、錯誤解謎重試、設備啟動及結算 UI、重新開始／選關，以及樓梯兩側、所有平台邊緣和視覺穿插。上述未經真人操作的項目維持 **NOT_TESTED**。

## 7. 後續 M3 美術改造建議（未執行）

1. 在白模階層逐件替換為鏽蝕金屬格柵、踏板、護欄與支柱，保留目前可行走表面和碰撞外形；不要修改原 FBX 外殼。
2. 一樓西側設維修設備，中央設淨化設備與管線，東北側強化過濾設備視覺；避開零件和控制台的 3 unit 互動射線及玩家站立淨高。
3. 二樓以局部故障照明、線束和維修平台營造尋寶視線；保持可俯瞰一樓的開口與兩條樓梯的入口辨識。
4. **在獨立後續任務**中，通過人工通行驗收後再搬移現有 `M3_ControlChip` 至尋寶平台，並同步更新任務提示；保留原 Prefab、`control_chip` ID 和狀態機。

**停止點**：M2 白模與必要自動檢查已完成。控制晶片尚未搬移；未開始正式材質或 M3 美術。等待人工驗收。

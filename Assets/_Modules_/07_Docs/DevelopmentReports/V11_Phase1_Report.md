# V11 Phase 1 — Safe Factory Circulation

工程狀態：已實作，等待 Unity 2023.1.22f1 驗收。**Unity PlayMode: NOT RUN**。
Unity 編譯、EditMode、Game／Scene 截圖與人工操作驗收均為 **NOT RUN**；本機沒有 Unity Editor，不能將下列靜態證據當成實際通行成功。

## 基準及實際生成關係

基準為 `main` 的 V10 提交 `d32caf009afb4da6f22a766f192f6920e3f10d8f`。工作分支為 `feature/v11-factory-rebuild`，不合併 main。

`C_Main.unity` 原場景保留 C_CollisionProxies、M2_Player prefab、M3_Treasure、三項零件 prefab、M4_PuzzleConsole、Floor1_Additions、FutureGameplayMarkers 及 C_Factory_Art。
`C3FactoryPresentation.Awake()` 先建立 C3_SortingSimulation，再依序 AddComponent：

1. EcoFactoryWorkshop：Awake 建立原量子組裝台、材料檢測台、分散材料與能源物；OnEnable／OnDisable 訂閱／取消 device.Running，Start 僅在首次找不到 sorter 時重試。
2. C3ReferenceFactoryEnvironment：Awake 隱藏被替代的舊美術、把原 WasteSortingLine 移至機組位置、建立既有三台機器、輸送帶、工作站美術與管線；OnEnable／OnDisable 訂閱／取消 device.Running。
3. C3V3Circulation：同一個既有元件與 GUID，精確退役舊結構並建立 V11 動線；不訂閱任務事件，沒有新增工廠生成器或每幀建模。移除依賴 Start 再打開 Renderer 的補救流程，因原桌／箱在前一階段已完整排除於隱藏清單之外。

Presentation 的 Activated 訂閱、EcoAdventureRuntime 的持久背包與 sceneLoaded 訂閱均未改動。V11 自建 root、材質、護欄 Mesh 於元件銷毀時清理；停用／啟用元件不重建，場景重載的數量檢查已加入待執行測試。

## 問題與修改

| 修改前 | 修改後 |
| --- | --- |
| 只實例化 V9 東側一座樓梯；西側平台沒有對應樓梯 | 西側 A 與東側 B 各有 22 個實體踏板、下方平台、外側鋼樑及兩側護欄 |
| V3/V7/V8/V9 OBJ 與獨立 BoxCollider 分別指定尺寸 | 新踏板／平台直接共用 Unity Cube 的 Transform 與 BoxCollider；護欄模型與簡化碰撞由同一組端點建立 |
| 整個 blockout 所有非 Trigger Collider 被停用 | 只對已盤點的 157 個無腳本、無 Trigger、無子物件的舊結構葉節點停用 Renderer／Collider；其他節點保持啟用 |
| 整個舊美術樹被廣泛關閉碰撞，原維修桌也變無碰撞 | 原桌／箱完整子樹不被隱藏，保留原 Collider；53 個舊美術碰撞以固定路徑清單維持原 V10 退役狀態 |
| 維修桌卡在僅 2.16m 深的後方路線中 | 後方平台加深為 3.2m；原桌、配電箱及實際晶片根物件同步北移 1.27m，保留所有引用、模型及物品 ID |
| TryCollect 與 Interact 各 AddItem 一次 | 由 TryCollect 唯一負責背包加入及 Changed；Interact 只在成功後停用原物件，三種零件均保有一次性判定 |
| 低垂的裝飾管夾可能進入二樓頭部空間 | 縱向管線基準提升至 7.6m，最低管夾約 6.835m；玩家站立頭頂 6.3m |

完整逐物件處理及原因見 [V11_Phase1_LegacyCleanup.csv](V11_Phase1_LegacyCleanup.csv)。12 個舊 OBJ 資產保留但不再實例化，既有 `.meta`／GUID 均未修改。原 C_CollisionProxies 的地面、牆、屋頂與 B 入口安全碰撞未停用。

## 尺寸與人工驗收位置

- 生產玩家原參數：Radius .30、Height 1.80、Center (0,.90,0)、Step Offset .30、Slope Limit 45°、Skin Width .08；本次不調整玩家控制器。
- A 中心 X=-23.20；B 中心 X=-1.55；兩座樓梯由 Z=3.80 到 13.70，地面 Y=.56 到平台 Y=4.50。22 階，每階高 .17909m、深 .45m，等效坡度 21.70°。
- 實體踏板保留上段下方空間，不用填至一樓的實心楔形堵住 B／C 入口；兩側鋼樑與模型共用碰撞尺寸。沒有伸入一樓通道的隱形坡面。
- 踏板／側廊寬 1.80m，護欄內淨寬 1.72m；相對直徑 .60m 加兩側 skin .16m，剩餘 .96m。入口橫樑底端約 Y=3.02m，站姿頭頂約 Y=2.36m。
- 西／東側廊從 Z=13.70 連至 18.10；後方平台 X=-24.10..-.65、Z=18.10..21.30，全部頂面 Y=4.50。
- 維修桌 (-13.20,4.50,20.55)，晶片 (-13.20,5.37,20.52)，配電箱 (-7.25,4.50,20.49)。沿 Z=19.20 巡檢線抵達桌前，無需跳欄杆；桌前緣 Z=20.26，離主巡檢線 1.06m。
- GUARD_WEST_OUTER、GUARD_REAR_NORTH、GUARD_REAR_SOUTH、GUARD_EAST_WALL_INNER_AFTER_STAIR 等六段二樓邊界，加八段樓梯／下平台側欄，共 14 個凸 MeshCollider。頂桿、中桿、踢腳桿與立柱為黃色實際模型；簡化護欄碰撞只覆蓋該欄杆外框，延伸至踏面以擋住趴下玩家，不橫跨樓梯出口。
- 新踏板／平台不使用 OBJ 匯入，避免再次引入 X 鏡像問題。護欄三角面繞序已以數學檢查向外；**實際世界 Renderer.bounds、Collider.bounds、匯入後 mesh、護欄阻擋與玩家物理仍需 PlayMode 驗證**。

本機 Unity 人工路線：一樓 D 側入口 (-12.5,.56,2) → 西側 A 下平台 (-23.2,.56,3.1) → A 上端 → 西廊 → 後方巡檢線 → 維修桌，瞄準晶片按既有 F 互動 → Tab 確認只有一個 control_chip 且任務更新 → 繼續向東 → B 下樓 → 一樓。反方向也要走一次。檢查四個轉角、側欄與樓梯側欄，包含趴下推向護欄；另從 (-3.5,.56,10.7) 穿越東側樓梯下方至 B 入口。數字鍵、射擊、瞄準、Z、Q/E、R、Tab 與鍵鼠手感保留為人工驗收項目。

## 原功能保護界線

控制晶片仍是原 M3_ControlChip prefab 的 C3CollectiblePart，`partId=control_chip`、`progress` 原場景引用不變；不是 Future_M3_ControlChip_2F_Only 空標記，也不是工作桌本體發物品。原桌 Collider 與可見模型保持一致，實際零件位於桌面。

保留原三零件條件、控制台、紙／金屬／塑膠分類邏輯、兩個互動工作台、分散材料與能源回收、持久背包／快捷欄、武器組裝與跨場景繼承。保留原三分類機、輸送帶與 V9 B 進料的八項 OBJ 顯示，未重建 B／C 塑膠簾或 Phase 2/3 設備。原 FPS 與背包／武器程式未更改；保留原互動按鍵 F，避免占用原 E 探頭。

## 執行證據與待驗收測試

| 檢查 | 結果 | 限制 |
| --- | --- | --- |
| Git diff --check | PASS | 僅差異空白檢查 |
| C# Tree-sitter 語法解析（第三關 32 檔及新增測試；另檢查整個 PlayMode 目錄） | PASS | 不代表 Unity C# 型別解析或編譯成功 |
| 場景 YAML 前後比對 | PASS | 物件 ID、元件、引用不變；僅桌／箱／晶片三項 Z 改動 |
| 既有 meta、Prefab、Packages、ProjectSettings 與受保護玩法檔逐位元比對 | PASS | 僅檔案未變，不代表玩法已執行通過 |
| 套件／asmdef JSON、157 結構葉與 53 舊碰撞路徑核對 | PASS | 靜態內容檢查 |
| 踏階高度、坡度、通道淨寬、護欄三角面外向性 | PASS | 設計尺寸檢查，非 CharacterController 行走 |
| Unity 編譯／EditMode | NOT RUN | 未安裝 Unity Editor |
| Unity PlayMode | NOT RUN | 未安裝 Unity Editor |
| 鍵鼠、畫面、音效與真實 Game／Scene 截圖 | NOT RUN | 需使用者本機試玩 |

新增 PlayMode 類別 `M6IntegrationTests.C3V11SafeCirculationTests`（七項，全部 **NOT RUN**）：

1. `WalkACollectExactlyOneChipAndReturnDownB`：從一樓入口用原 TickMovement → CharacterController.Move 走 A、到桌前用原 FindTarget 射線取得晶片；檢查背包數量、Changed 次數、重複互動、再走 B 回一樓。
2. `WalkBThenRearConnectionAndDescendA`：反方向完整巡檢路線，不以 Transform 傳送取代行走。
3. `GroundBCDoorApproachRemainsOpenUnderTheEastFlight`：原生產膠囊穿過東側樓梯下方前往 B 入口。
4. `EveryGuardStopsStandingAndProneCapsulesIncludingCorners`：站姿／趴下撞向六段邊界、外轉角、兩座樓梯及下平台兩側。
5. `WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions`：世界模型／碰撞界限、14 個護欄、地面抽樣與頭部淨空。裝飾 OBJ 的 AABB 只作候選篩選，再以實際匯入三角面測相交，不把空 AABB 當阻擋。
6. `LegacyCleanupPreservesFloorTriggersAndSurvivesSceneReload`：原根節點、地面及入口代理碰撞、舊幾何退役、停用再啟用與場景重載物件數。
7. `OriginalPartsConsoleSortingBenchesAndWeaponStillOperate`：三零件各入袋一次、控制台未完成防護、原解謎與啟動、真實三種分類週期→材料拾取→檢測／組裝台→快捷欄→射擊耗彈→場景重載裝備繼承。

既有 `C3V9VisibleIndustrialRouteTests` 保留檔名／GUID，將已替換的三個樓梯／平台斷言遷移到現行結構；B 輸送帶檢查與原地面條紋通行檢查保留。其結果亦為 **NOT RUN**。

更早的 V3～V8 歷史佈局測試含已被 V10 移除的 V8/V3 物件名稱，尚未整理或執行；不宣稱全歷史測試套件通過。其他既有功能回歸測試亦未執行，不能以程式未變更推定其結果。

執行方式：以 Unity 2023.1.22f1 開啟專案後，在 Window → General → Test Runner → PlayMode，先執行 C3V11SafeCirculationTests 與更新後 C3V9VisibleIndustrialRouteTests，再執行適用的原玩法回歸套件。
批次指令（待執行，UNITY_EDITOR 指向實際已授權 Editor；輸出在專案外）：

```bash
cd /path/to/SDG-Adventure-Recycle
export SDG_ADVENTURE_TEST_OUTPUT_ROOT="$(dirname "$PWD")/SDG-Adventure-Recycle_TestOutputs/V11_Phase1"
mkdir -p "$SDG_ADVENTURE_TEST_OUTPUT_ROOT"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests \
  -testPlatform PlayMode \
  -testFilter 'M6IntegrationTests.C3V11SafeCirculationTests;M6IntegrationTests.C3V9VisibleIndustrialRouteTests' \
  -testResults "$SDG_ADVENTURE_TEST_OUTPUT_ROOT/playmode.xml" \
  -logFile "$SDG_ADVENTURE_TEST_OUTPUT_ROOT/playmode.log"
```

檢查當次 XML 的實際執行數、失敗與跳過，不將零測試當成功；若 -nographics 造成渲染限制，使用本機 Editor 執行模型／淨空測試並記錄原因。必要驗證完成前，本 Phase 1 僅能作為待審查實作，不能宣稱最終驗收合格。

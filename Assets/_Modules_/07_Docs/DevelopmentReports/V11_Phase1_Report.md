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
| C# Tree-sitter 語法解析（第三關與 PlayMode 共 52 檔，含碰撞記錄輔助元件） | PASS | 不代表 Unity C# 型別解析或編譯成功 |
| 場景 YAML 前後比對 | PASS | 物件 ID、元件、引用不變；桌／箱／晶片三項 Z 改動，另恢復 M2_TestInteractable 的 Renderer；其 Collider 與腳本不變 |
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


## PR #2 回查修正（相對 9800e95）

本輪繼續同一分支與 Draft PR #2，未建立新 PR／分支、未合併 main、未發布環境。**Unity 2023.1.22f1 編譯、EditMode 與七項 PlayMode 均為 NOT RUN**。

### 可由程式碼確認的問題

1. **原互動測試物件是隱形障礙。** M2_TestInteractable 的 MeshRenderer（fileID 737892122）為 0，但 BoxCollider 與 C3TestInteractable 啟用。恢復原 Renderer=1，保留模型、Collider、位置、腳本與 GUID。相對 9800e95，C_Main 唯一修改即此旗標。
2. **東側裝飾支柱侵入通道。** 原中心 X=-.75、寬 .30，內緣 -.90，侵入平台外緣 -.65 內側；中心線測試不會碰到它。移至 X=-.45，內緣 -.60 位於護欄外緣 -.61 之外。支柱為無 Collider 裝飾網格，此為可確認的視覺穿模風險；實際行走仍待測。淨空測試增加左右 .35m 掃描。
3. **fixture 會刪除原持久狀態。** 舊 setup 銷毀 EcoAdventureRuntime，teardown 建立空實例，不能保留先前背包／快捷欄。現在先保存原實例、暫停其 Update 並移除其 sceneLoaded 訂閱，再建立專用測試實例。結束時先停用並卸載測試場景、銷毀僅由 fixture 建立的實例，再還原原 singleton 與訂閱。額外驗證原背包、快捷欄及 factoryPowered 未變；保留原 enabled 狀態。場景本身仍採 Single 載入，沒有聲稱還原原場景物件。
4. **全域輸入／偏好可能污染後續測試。** 保存並還原 Input Context、衝刺／蹲下切換狀態、PlayerPrefs 視角偏好（含原本未設定的情況）、Time.timeScale 與游標。採 finally 清理，反射檢查或 setup 失敗時仍還原全域狀態。原先新增的 NonParallelizable 標記已在下述 Safe Mode 相容性修正中移除，隔離邏輯保留。
5. **護欄測試可能被無關障礙擋住而誤判。** 新增 fixture 專用 C3V11CollisionRecorder，從原 CharacterController 的 OnControllerColliderHit 記錄側向接觸。所有 14 段護欄的站姿／趴下案例，除位置與防墜外，必須實際接觸指定 Collider；單純「沒有移動」不算護欄證據。

### 碰撞與互動回查範圍

- 依既有 157 個 blockout 葉節點及 53 個舊美術退役路徑分類後，直接序列化於 C_Main 的其餘 22 個非 Trigger Collider 已逐一回查；舊美術下僅有原維修桌與配電箱兩個必要實體碰撞。地面、牆、屋頂、入口安全邊界、控制台與原互動測試物件皆保留。此數量不包含 Prefab 匯入及執行時新增物件，不能等同完整 PlayMode 碰撞掃描。
- 加入獨立 runtime 檢查：枚舉整個 C_Factory_Art 的 Collider，不使用生成器的退役清單來斷言自己正確；遇到未分類的活躍非 Trigger Collider 會失敗，沒有自動關閉未知 Collider。另掃描場景其他有可見模型子樹的 Collider，要求至少一個 Renderer 啟用，保留必要無 Renderer 的地面／牆等代理碰撞。
- 雙樓梯與護欄生成器本輪未再改動：22 階、階高 .17909m、坡度 21.70°，對照真實 prefab 的 StepOffset=.30、SlopeLimit=45、Radius=.30、SkinWidth=.08。頂階接側廊 Z=13.70、側廊接後平台 Z=18.10，護欄接點共用座標。這是尺寸與程式碼檢查，**不是通行 PASS**。
- 晶片保留 control_chip、原 C3CollectiblePart 與 progress 引用；原 TryCollect 唯一授予邏輯不變。測試增加晶片 Collider 在原維修桌面上方、X/Z 位於桌面內的斷言，仍經原 FindTarget 射線及 Interact 檢查取得結果。
- 沒有確認目前反射成員名稱失配。強化為依參數型別解析方法、明確檢查欄位／屬性／事件存在性、核對 Changed 的 Action 型別，及測試庫存實例確實等於 singleton。

### 本輪證據

| 檢查 | 實際結果 |
| --- | --- |
| C# AST 語法解析（52 檔） | PASS，非 Unity 編譯 |
| 49 處反射呼叫／成員與原宣告，以及 TickMovement、SceneLoaded、Instance、InventoryItem 契約靜態核對 | PASS，尚未執行 Invoke |
| 相對 9800e95 的場景 YAML 比對 | PASS，僅 Renderer 啟用旗標；所有 fileID／引用／物理／座標不變 |
| 既有 meta、Prefab、Packages、ProjectSettings 及十個玩法／動線來源逐位元比對 | PASS |
| 457 個 Assets GUID 唯一性 | PASS |
| 已序列化 Collider 分類、踏階參數及支柱邊界計算 | PASS，靜態檢查 |
| git diff --check | PASS |
| Unity 編譯／EditMode／七項 PlayMode | NOT RUN |

七項 UnityTest 名稱保留，C3V11CollisionRecorder 僅為測試輔助 MonoBehaviour，不是第八項測試。更新後的七項測試、反射呼叫、fixture 隔離與 MeshCollider 淨空探針，仍須在 Unity Test Runner 真正執行。請先重複執行 fixture 兩次，再與其他回歸測試交錯執行，檢查 singleton、背包、輸入與游標是否復原。使用者需完成原報告人工路線、護欄角落、F 晶片互動、B/C 入口及一樓玩法試玩；ChatGPT 審查與人工驗收前不合併。


## Safe Mode 編譯修正（基準 306996f，Windows Unity 2023.1.22f1）

**正式驗收阻擋：** 使用者在 Windows Unity 2023.1.22f1 開啟 306996f 後進入 Safe Mode，尚未成功進入正常 PlayMode。Console 的實際編譯結果為 **FAIL（使用者本機）**：

```text
Assets/_Modules_/05_Testing/PlayMode/C3V11SafeCirculationTests.cs(15,6):
CS0246: The type or namespace name 'NonParallelizableAttribute' could not be found
CS0246: The type or namespace name 'NonParallelizable' could not be found
```

### 原因與最小修正

上一輪新增的 `[NonParallelizable]` 引用了目前 Unity 編譯組件無法辨識的屬性。檔案已經有 `using NUnit.Framework;`，重加 using 無法處理此問題。

鎖定依賴為 `com.unity.test-framework` **1.3.9** → `com.unity.ext.nunit` **2.0.3**。Unity 官方 registry 的 2.0.3 `description` 明確描述這是以 **NUnit 3.5 為基礎的客製版本**，不是 NUnit Framework 2.0.3，也不能把新版 NUnit 文件的所有 API 視為可用。可確認的相容性問題以使用者 CS0246 為準，沒有推測本機 DLL 更細部的建置原因。

本次僅移除該屬性並補註原因，不建立自訂空屬性、不增加套件、不更改版本或測試組件。修改檔案只有：

- `Assets/_Modules_/05_Testing/PlayMode/C3V11SafeCirculationTests.cs`
- 本工程報告 `V11_Phase1_Report.md`

全部七個 `[UnityTest] public IEnumerator` 方法、UnitySetUp／UnityTearDown、所有其他方法與斷言逐位元保持不變，`C3V11CollisionRecorder.cs` 及其 meta 不變。C_Main、樓梯／平台／護欄／Collider、晶片、背包、武器、分類設備、工作台、場景轉換與 FPS 原始碼均不變。

### 引用、排程及隔離檢查

`M6Integration.PlayModeTests.asmdef` 的 `references` 仍是 `SDGAdventure.TestOutput`、`Unity.ugui`；`optionalUnityReferences=[TestAssemblies]`，`overrideReferences=true`，`precompiledReferences=[nunit.framework.dll]`，`defineConstraints=[UNITY_INCLUDE_TESTS]`。沒有停用 assembly 或用條件編譯隱藏七項測試。

以 C# AST 掃描整個 Assets，未找到任何 `[Parallelizable]`、`[NonParallelizable]`、`[LevelOfParallelism]` 的 class／fixture／assembly 層級宣告；另未找到自訂 TestRunnerApi／ExecutionSettings／NumberOfTestWorkers／執行緒並行設定。移除不支援的標記本身不會加入平行設定。保留 Unity Test Runner 的既有執行路徑；實際執行順序與跨 fixture 隔離仍須 Test Runner 驗證。

原有 EcoAdventureRuntime.Instance、inventory、quickSlots、factoryPowered 保存／還原檢查、SceneManager 事件訂閱隔離、輸入 Context、衝刺／蹲下切換、視角 PlayerPrefs、Time.timeScale、游標及 finally 清理均未改動。請實測單獨執行、連續執行兩次、與其他 fixture 前後交錯執行，並在測試失敗後檢查上述狀態、場景卸載與訂閱是否復原。這些情境目前全部 **NOT RUN**。

### 本次實際檢查結果

| 檢查 | 結果 | 範圍及限制 |
| --- | --- | --- |
| git diff --check | PASS | 僅測試檔與本報告變更 |
| C# AST 語法解析（52 檔） | PASS | 非 Unity 編譯；不做組件型別解析 |
| 反射契約 49 處及 TickMovement／SceneLoaded／Instance／InventoryItem | PASS | 原宣告與方法簽章靜態核對；未執行 Invoke |
| asmdef、manifest、packages-lock JSON／引用／版本 | PASS | 全部保持原版本與內容 |
| 七方法及 setup／teardown／隔離方法存在性與內容比對 | PASS | 七項測試可發現的來源結構不變，實際 Test Runner 發現為 NOT RUN |
| 平行設定與修改範圍掃描 | PASS | 未新增平行設定；除測試檔／報告之外所有已追蹤檔案與 306996f 相同 |
| NUnit 版本及已知不相容 API 使用檢查 | PASS | 官方 metadata 與使用者錯誤證據；移除已知不支援的標記 |
| 官方 NUnit DLL／Test Runner 原始碼直接檢查 | BLOCKED | tarball 302 至 cdn.packages.unity.com 後被網路政策以 403 拒絕；官方 metadata 對應 Test Framework 原始碼 commit 的公開 GitHub 讀取為 404，未取得 DLL 或該版本排程原始碼 |
| Unity Compilation | NOT RUN | 本環境找不到可用、已授權的 Editor，不能判定 Windows 修復已驗收 |
| Unity EditMode／七項 PlayMode | NOT RUN | NUnit、UnitySetUp／TearDown／Test、Assert、IEnumerator、Reflection、SceneManager、CharacterController、ComputePenetration、UnityAction 與記錄元件的實際編譯／執行仍待本機 |

官方版本中繼資料來源：`https://packages.unity.com/com.unity.ext.nunit` 與 `https://packages.unity.com/com.unity.test-framework`。調查用資料與工具只放於專案外；未把未驗證套件裝進專案，也未更改網路設定或發布環境。

### Rider 訊息獨立待查

使用者另回報 `Unloading broken assembly Library/ScriptAssemblies/Unity.Rider.Editor.dll, this assembly can cause crashes in the runtime`。尚無證據證明它與 CS0246 為同一根因。未刪除 DLL、Library 或 Rider 套件。先重新編譯；若 CS0246 消失後仍出現此訊息，保留完整 Console 與 Editor.log 作為下一輪獨立問題。

### 使用者重新驗證

1. 關閉 Unity Editor。
2. GitHub Desktop 切至 `feature/v11-factory-rebuild`。
3. 執行 Fetch origin。
4. 執行 Pull origin，確認取得本次修正。
5. 重新以 Unity 2023.1.22f1 開啟專案。
6. 檢查 Console，確認兩項 CS0246 消失且沒有新的紅色 C# 錯誤。
7. **只有確認沒有編譯錯誤後才退出 Safe Mode**，再開 Test Runner 確認七個原測試都能被辨識。正常開啟 Editor 不代表測試已通過。
8. 若仍有紅色錯誤，保留完整訊息（含檔案、行列與錯誤碼）回報；Rider 訊息若持續也一併保留，不先刪快取／套件。

本次交付是最小修正提交；最終編譯修復驗收需使用者本機確認無新增 C# 錯誤且可退出 Safe Mode。編譯修復驗收不等於 V11 Phase 1 遊戲驗收。PR #2 保持 Draft、不合併 main，等待 Console 回報；本次不繼續功能開發。


## Safe Mode 接續修正：CS0161（基準 af29e79）

使用者新的 Windows Unity 2023.1.22f1 Console 截圖顯示：

```text
C3V11SafeCirculationTests.cs(417,28): error CS0161:
'C3V11SafeCirculationTests.WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions()':
not all code paths return a value
```

這是正式編譯阻擋，使用者本機此版本結果為 **FAIL**。前次語法解析無法證明方法的回傳控制流程正確，漏掉了這個問題。該方法宣告 IEnumerator，但只有同步斷言、沒有 yield 或回傳值，因此不是合法的迭代器方法。

最小修正：在所有幾何、護欄、工作桌與輸送帶斷言之後補上 **`yield break;`**，使方法成為合法協程並在執行完斷言後結束。沒有提前略過斷言、改成 void、刪除測試或停用 assembly。相對 af29e79，程式碼僅新增這一行；另一修改檔為本報告。七項 UnityTest 名稱與標記保留，其他方法（包含 setup／teardown／隔離）逐位元不變；場景、玩法、碰撞記錄器、meta、Packages 與 asmdef 均不變。

已執行結果：

- **PASS**：整個 Assets 共 106 個 C# 檔語法解析；針對 830 個方法（含 97 個具體 IEnumerator 方法）的回傳／yield 掃描，修正前精確指出此方法，修正後沒有同類「完全缺少 return／yield／throw」候選。此規則不等同 C# 編譯器的完整控制流程分析。
- **PASS**：49 處反射契約、鎖定套件與 asmdef 引用、七項測試與方法內容保存、修改範圍及 git diff --check。
- **Unity Compilation: NOT RUN**；**Unity PlayMode: NOT RUN**（七項全部未執行）。仍需使用者本機實際編譯驗收。

截圖中的 CS0618 是 FindObject(s)OfType 過時 API 警告，本次沒有擴大改寫相關 API。Rider 的 `Unloading broken assembly ...Unity.Rider.Editor.dll` 仍獨立待查；沒有證明其與 CS0161 同根因，沒有刪除 DLL、Library 或套件。

關閉 Unity，於原 feature/v11-factory-rebuild Fetch／Pull 新提交，再開啟 Unity 檢查 CS0161 是否消失及是否還有紅色編譯錯誤；無編譯錯誤後再退出 Safe Mode。若 Rider 訊息仍存在，保留更新後 Console／Editor.log 再處理。原 PR #2 保持 Draft，不合併 main；不將此一行修正或靜態檢查當作編譯／遊戲驗收通過。

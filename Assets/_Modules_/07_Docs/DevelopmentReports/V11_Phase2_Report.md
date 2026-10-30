# SDG Adventure V11 Phase 2 — 工程交付與待驗收項目

本報告區分**已實作**及**已驗收**。沒有 Unity Editor 的來源檢查不能代表編譯、PlayMode 或遊戲通過。

- Phase 1 基準：`ed26bfc9e281737e96421be149f7b539eb79326c`。
- Head：`feature/v11-phase2-recycling-line`；Base：`feature/v11-factory-rebuild`，依賴尚未合併的 [Draft PR #2](https://github.com/LinSean-ww/SDG-Adventure-Recycle/pull/2)。不修改 #2、不合併 main、不強制推送。
- 專案 Unity 版本 `2023.1.22f1`；本環境沒有可用 Unity Editor／授權。
- 使用者先前回報 V9五項、V11七項、雙樓梯、晶片只取得一次、PVC PASS，僅是 Phase 1 基準，新提交不可沿用 PASS。
- 《高科技回收分類工廠全景.png》及指定 V11 試玩截圖**尚未直接取得**；未逐圖比對，未以生成圖片冒充實際畫面。

| 階段 | 程式實作 | Unity 驗收 |
| --- | --- | --- |
| 2.1 高位入口、局部牆洞、連續設備 | 已實作 | NOT RUN |
| 2.2 統一流程、型別映射、去重 | 已實作 | NOT RUN |
| 2.3 三類加工、3→1、出口收集 | 已實作 | NOT RUN |
| 2.4 支柱、管線、回歸與報告 | 已實作支柱／管線，新增18項測試並遷移過時斷言 | NOT RUN；實際回歸驗收未完成 |

## A. 原架構問題與整合

原 ReferenceFactory 的 packages／bFeedPackets 各自循環，BuildOutfeedPallets 初始化成品與投入無關。Presentation 另建 C3_SortingSimulation 推進動畫及 CompletedCycles；Workshop 根據週期推測材料增加統計；EcoConveyorSorter Trigger 又能獨立加計數及銷毀。三套路徑無法證明同一份原料真的運輸、加工、輸出。

現在 `C3RecyclingLine` 統一管理唯一 ID、類型快照、CurrentRouteSegment、TargetMachine、State History、Counted／ProcessingComplete／ProductCreated、BatchId 與成品。`C3RecyclingLineGeometry` 生成設備並呈現指定的壓頭／推桿狀態；`C3ConveyorSection` 綁定真實 START／END、Renderer 及帶面 Collider。

Presentation 保留啟動燈光／音效／Device 訂閱；CompletedCycles 改讀正式批次數。ReferenceFactory 保留倉體、地坪、PVC、原工作站及公用管線，停止生成兩組循環原料、舊設備及預置成品。Workshop 保留取料／檢測／武器，移除依假週期增加統計。八件舊設備 OBJ 資產與 GUID 保留，但不再實例化；舊 SortingSimulation 整個停用並銷毀。

Phase 1 的157個舊結構 leaf、53個舊裝飾碰撞路徑仍用精確 allowlist 清理；不擴大停用任意 Collider，原上層工作桌／配電箱、互動及 Trigger 保留。

## B. 完整物料路線與設備

下列均為**來源設計值，非 Unity 世界 Bounds 量測**，單位 m。設備由原 Presentation 的 Awake 建立；進入 Play 後於 `C3_P2_RecyclingLine` 檢查，不是編輯模式預置的另一套設備。

高位入口 `(1.2,7,12.5)` → `(-.9,7,12.5)` → `(-3.8,7,12.5)` → `(-3.8,7,16.85)` → `(-4.8,7,16.85)` → `(-6.2,5.8,16.85)` → 主帶終點 `(-19.5,5.8,16.85)`。前五段寬1.10、主帶寬1.52，方向依箭頭。轉彎後先水平離開上一帶寬，再下降約40.6°，避免直接穿入舊帶面。主帶來源 Y=5.62→5.80。

原 `C_Wall_4_Lintel` Collider 世界 Y=3.5..10，原 Transform 不動，只改局部 center Y=-1.75、size Y=3，留下 Y=3.5..6.5。新增三個同姿態可見 Cube／BoxCollider 代理：

| 代理 | 中心 | 完整尺寸 |
| --- | --- | --- |
| P2_WALL_HIGH_NEAR | (-.3,7.3,10.825) | (.15,1.6,1.65) |
| P2_WALL_HIGH_FAR | (-.3,7.3,14.175) | (.15,1.6,1.65) |
| P2_WALL_HIGH_HEADER | (-.3,9.05,12.5) | (.15,1.9,5) |

物料淨開口 Y=6.5..8.1、Z=11.65..13.35，附安全邊框；保留人員過樑下緣 Y=3.5、地面門洞、PVC、B_Door_Floor 及 B_Safety。Scene 只改該 Collider／父 child list，增加15個 YAML文件、三個可見實體代理。

| 類型 | 機體中心 | 主帶分流點 | 上部料斗接口 | 加工床中心 | 正式出口 | 收集中心 |
| --- | --- | --- | --- | --- | --- | --- |
| Paper | (-17.9,.55,14.8) | (-16.7,5.8,16.85) | (-16.7,4.45,15.6) | (-17.9,2.4,15.2) | (-17.9,1.25,12.75) | (-17.9,1.25,9.1) |
| Metal | (-13,.55,14.8) | (-11.8,5.8,16.85) | (-11.8,4.45,15.6) | (-13,2.4,15.2) | (-13,1.25,12.75) | (-13,1.25,9.1) |
| Plastic | (-8.1,.55,14.8) | (-6.9,5.8,16.85) | (-6.9,4.45,15.6) | (-8.1,2.4,15.2) | (-8.1,1.25,12.75) | (-8.1,1.25,9.1) |

令機體中心 X=x，每台實際接續為：

1. `(x+1.2,5.8,16.85)` 水平出主帶至 `(x+1.2,5.8,15.8)`，寬1.25。
2. 上方重力溜槽至 `(x+1.2,4.45,15.6)`，寬1.25；機內陡槽至 `(x+1.2,2.4,15.3)`，寬.90，約81°，沿相同水平向下降，從壓頭側面進料。
3. 寬.90的水平接料床至 `(x+1.2,2.4,15.0)`；再沿真實 PROCESS_BED 分到 `(x-.32,2.4,15.0)`、`(x,2.4,15.0)`、`(x+.32,2.4,15.0)`。床寬3.20、深1.00、頂Y2.40。
4. 成品在 `(x,2.4,15.2)` 形成，水平出料床至 `(x,2.4,14.7)`，機內斜帶至 `(x,1.25,12.75)` 正式出口，寬1.55。
5. 獨立下帶至 `(x,1.25,9.8)`，寬1.55；寬2.40收集床至 `(x-.65,1.25,9.1)`、`(x,1.25,9.1)`、`(x+.65,1.25,9.1)`，依批次逐格放置。

主帶前護邊在三個分流點各留1.40m實體缺口；壓床內短接／出料床不放橫切原料的側板。機殼中空，保留入口／出口；外露長段具有護邊、滾輪、鋼架。有限帶面長度與端點一致，不讓上一平帶延伸成陡槽上方的懸挑台階。

姿態在接點.35m內平滑轉向，段內跟隨帶面法線。SphereCast／BoxCast 用相鄰真實帶面 Collider 決定接觸高度，世界位移限制原料4.5m/s、成品2m/s，避免陡槽邊緣跳位。這些原生查詢尚待 Editor 實測。

完整表見 [V11_Phase2_Topology.csv](V11_Phase2_Topology.csv)：30個設備段、9條原料床位、9條成品收集路線。basis 明標 SOURCE_DESIGN_NOT_UNITY_MEASUREMENT；方向 Start→End，Y 是表面而非物料中心。

## C. 型別、加工、計數與生命週期

Enum 保留 Metal=0、Plastic=1、Electronics=2，附加 Paper=3。機台映射 Paper0／Metal1／Plastic2，舊 Runtime 接口 Plastic0／Metal1／Paper2。Electronics 拒絕進三分類線，電子快取／量子武器仍獨立。來源搜尋未發現場景／Prefab 序列化引用 EcoRecycleCargo script GUID；仍保留原 enum 值及 meta 供外部內容相容。

每個可視原料球為1投入單位，三個同類單位實際到床後才加工2.5秒。維持同一 GO 運至加工床，壓頭按 Processing 進度下壓／回升，原料同步變形；完成才準備紙捆／金屬壓塊／塑膠捆。成品保留唯一 BatchId 與三個 SourceMaterialIds。3→1是投料批次規則，不聲稱模型體積等於質量／密度；本體.52×.42×.50，含束帶運輸包絡.52×.43×.51。

Runtime 原子驗證批次、原料ID、唯一性、型別及容量後才計數、顯示成品並發出一次 BatchCompleted。交易拒絕時保留單一隱藏待提交成品及 LastBlockedReason，不重生或重複加數；生成成品前先檢查實際輸出帶面，避免缺失帶面時每次重試留下新的隱藏物件。分類只解鎖原取料點，不自動 AddItem，不改彈藥／快捷欄。

`Spawned → Feeding → MainBelt → Identified → Diverting → MachineInput → Processing → OutputReady → Outfeeding → Collected`

- AcceptedUnits = WaitingUnits + ProcessingUnits + ProcessedUnits。
- ProcessedUnits = 3 × ProcessedBatches。
- CollectedProducts ≤ OutputProducts ≤ ProcessedBatches。
- WaitingUnits 含未釋放及運輸／床位等待；FedUnits 為已上帶單位。
- OutputProducts 只在越過 MACHINE_OUTPUT 後增加；CollectedProducts 只在到達收集格後增加。

每機最多9單位／3捆，總27原料／9成品；三床位預留、主線至少1m參數間距、有背壓。內建 useFiniteTestSupply 只供九件，每類三件，使用場景 GUID 的新 ID；**這是 C 側有限演示，不是正式 B 區供料或通關**。

正式接入接口為 `P2_B_HIGH_INFEED_INTERFACE` Transform＋TryAcceptCargo：同一場景、唯一ID、支援類型、materialUnits=1、入口+Y.12附近.15m內、世界scale=1、小型可見模型、無啟用的非Trigger Collider或動態Rigidbody。接管同一GO；拒絕不修改／銷毀上游。C_Main 的 EcoConveyorSorter 只委派接口，無管理器的其他場景保留明確映射的舊相容路徑。未建立虛構 B 生成器；新增測試臨時建立原生 Trigger／kinematic Rigidbody 驗證交接。

Dormant／Starting 不輸送、不加工、不正式加數；Running 正常處理；Completed 只排空既有物料、拒收新料，不改原任務出口。Time.timeScale=0不前進，停用再啟用不重播供料／重建設備。新增設備／物料場景擁有，材質共用並在 OnDestroy 釋放，Update 不建 Material。

同一持久 Runtime receipt ledger 跨場景重載保留已完成ID；C_Main仍依原規則重置本關解鎖計數，背包／彈藥／快捷欄保留。ledger上限4095單位，滿時拒收、不淘汰去重；未完成物料可由上游重試。沒有新增跨程式重啟存檔承諾。

## D. 二樓走道、支柱與管線

下表 Bounds 是來源推算，原生 Bounds／角色／鏡頭均未驗收。

| 結構 | Phase 1 | Phase 2 | 理由 |
| --- | --- | --- | --- |
| 六根 V11_EDGE_SUPPORTS | X=-24.10／-.65；Z14.5／17.5／20.9；截面.14 | X=-24.32／-.43；Z13.75／17.95／21.20；截面.18 | 平台外側接點，新增平台底懸臂梁／斜撐 |
| 十三根倉體裝飾柱 | 西X=-24.53、東-.45；截面.30×.32 | 西-24.70、東-.12；截面.18×.22 | 嵌牆避開長廊，仍無實體Collider |
| 十三踢腳板 | 舊柱位，.36×.48×.39 | 新柱位，.22×.48×.26 | 避免模型伸入走道 |
| 七根屋頂橫樑 | X中心-12.5、長24 | 中心-12.41、長24.58 | 接移動後柱位及吊架 |
| 五條供應管 | 基準Y7.60；外側X=-23.7／-1.38 | 基準Y8.80；外側X=-24.55／-.15 | 最低垂直吊夾端點Y6.89→8.09，避頭部／高位橋 |

支柱Y=.56..4.32，梁撐不穿頂Y4.50平台。來源推算西柱X Bounds=-24.41..-24.23、東=-.52..-.34；裝飾柱向走道內緣=-24.61／-.21。

中央管舊端點 `(-6,4.28,15.94)→(-5.7,5.6,16.52)→(-3.9,5.98,17.1)`，改 `(-5.8,4.28,15.94)→(-5.8,8.65,15.94)→(-3.9,9.2,17.1)` 避轉接帶。高位橋三組屋頂吊架，主帶四組 X=-8.5／-10.5／-15.4／-19.3、Z=16.85±.88、底Y5.8頂11.45，避開分流口並連到Z16屋樑；B側加懸臂連到C屋樑，PVC／東梯下無新增落地柱。

逐件68筆新舊中心／尺寸與新增支撐見 [V11_Phase2_SupportChanges.csv](V11_Phase2_SupportChanges.csv)。NEW舊值空白；旋轉圓管尺寸不是世界AABB。

原 CharacterController 半徑.30、站高1.80、skin .08、步高.30、坡限45、趴高.75、雙22階樓梯、14段實體護欄、工作桌／晶片位置與ID保留。上層站姿頭頂約Y6.30。測試使用原 TickMovement→CharacterController.Move 走雙梯、長廊、後平台、趴姿及防墜，另查原生 CapsuleCast 頭部淨空；不縮小角色、不放寬原.01m斷言。

## E. 測試與實際結果

```text
Unity Compilation: NOT RUN
Unity PlayMode: NOT RUN
Manual Gameplay: NOT RUN
```

### 已執行的來源檢查

| 檢查 | 結果及界線 |
| --- | --- |
| git diff --check | PASS；僅空白格式 |
| Tree-sitter AST | PASS；110檔、910方法、122個IEnumerator的yield／return來源檢查；不是型別／語意編譯 |
| Scene YAML／新局部引用 | PASS；2168→2183 ID，僅牆Collider／父child list改動；不是Unity匯入 |
| 相容／保護比對 | PASS；原meta、Prefab、asmdef、Packages、ProjectSettings、玩家／零件／裝置／解謎、背包武器原方法、樓梯平台護欄builder保留 |
| 舊碰撞清單 | PASS；157結構leaf、53舊裝飾路徑可解析，未誤納上層工作站 |
| 新meta | PASS；7個新資產成對且GUID唯一 |
| 測試／反射來源 | PASS；保留5／7名稱，新增18項，檢查成員／方法／參數型別；無Ignore／Explicit／不相容NonParallelizable；不是Unity反射執行結果 |
| 原料數值路線審查 | PASS，僅限新帶面／護邊／機殼／吊架／開口代理的來源模型：489取樣無預測>1.1mm穿入，.05s位移約≤.225m；不涵蓋完整Unity世界、成品原生接觸、角色或實際Bounds |

### Phase 2 PlayMode（逐項）

| 測試 | 驗證設計 | 結果 |
| --- | --- | --- |
| ElevatedInfeed | 實際高位接口／帶面尺寸與材質 | NOT RUN |
| WallOpeningClearance | 原生SphereCast上洞、牆體／PVC保留、地面雙向行走 | NOT RUN |
| ContinuousConveyorRoute | 30段Renderer／Collider、端點Raycast與原生接點接觸 | NOT RUN |
| IngressIdentityAndTriggerDedup | 原生Trigger交接同一GO，重複不計數 | NOT RUN |
| CargoFollowsRoute | 同一ID／GO、歷史、真實接觸、沿途穿入與壓頭避讓 | NOT RUN |
| PaperSorting | 紙類實到正確機床、成品及舊統計映射 | NOT RUN |
| MetalSorting | 金屬實到正確機床、成品及舊統計映射 | NOT RUN |
| PlasticSorting | 塑膠實到正確機床、成品及舊統計映射 | NOT RUN |
| ProcessingState | 不足三份不加工／不提前加數，三份到床才開始 | NOT RUN |
| MachineOutput | 可見壓頭位移、加工前無成品、成品在機內形成 | NOT RUN |
| OutputConveyor | 同一成品移動、實際包絡接觸／ComputePenetration、三出口到收集 | NOT RUN |
| MaterialAccounting | 9→3、逐tick守恆、重播／Trigger去重、事件次數、背包彈藥不自增 | NOT RUN |
| ActivationState | Dormant／Starting、暫停、Completed排空拒收 | NOT RUN |
| ReloadSafety | 真重載、單設備根、重啟與收據不重播 | NOT RUN |
| UpperWalkwayClearance | 原角色站姿／頭部、東上西下 | NOT RUN |
| ChipAndGuardRegression | 西上取一晶片東下、站趴防墜、PVC雙向 | NOT RUN |
| ProneWalkwayClearance | 原角色趴姿走東／後／西長廊 | NOT RUN |
| Regression | 零件、解謎、取料、工作台、武器及跨場景背包彈藥 | NOT RUN |

### V9／V11 及輔助回歸

| Suite／測試 | 本分支結果 |
| --- | --- |
| V9 ReplacementStairsHaveMatchingPhysicalTreadsOnBothSides | NOT RUN |
| V9 BReceivingBeltActuallyExistsOnCFrontFaceAndMeetsMainHighBelt | NOT RUN |
| V9 ReplacementDeckWorldBoundsMatchBothStairExitsAndRearBay | NOT RUN |
| V9 PlayerCanClimbRightStairAndReachRearMaintenanceBridge | NOT RUN |
| V9 PriorFloorStripeIsPassable | NOT RUN |
| V11 WalkACollectExactlyOneChipAndReturnDownB | NOT RUN |
| V11 WalkBThenRearConnectionAndDescendA | NOT RUN |
| V11 GroundBCDoorApproachRemainsOpenUnderTheEastFlight | NOT RUN |
| V11 EveryGuardStopsStandingAndProneCapsulesIncludingCorners | NOT RUN |
| V11 WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions | NOT RUN |
| V11 LegacyCleanupPreservesFloorTriggersAndSurvivesSceneReload | NOT RUN |
| V11 OriginalPartsConsoleSortingBenchesAndWeaponStillOperate | NOT RUN |
| M6FlowTests.FactoryStartupAndThreeWasteTypesUseBoundedVisualPool | NOT RUN |
| C3VisualFPSIntegrationTests.ReferenceFactoryBuildsThreeSortersWithoutChangingPuzzle | NOT RUN |
| Unity Compilation | NOT RUN |
| Unity PlayMode | NOT RUN |
| Manual Gameplay／Profiler | NOT RUN |

V9低位OBJ名稱／尺寸與V11最後的舊feed Bounds斷言，替換為30段真實設備Renderer／Collider、Raycast、Overlap接點驗證；其餘四個V9、六個V11測試本體保留。有限帶面端點Raycast向內取樣1mm避浮點盒邊，仍比對原端點並保持.01m容差，接點另查原生重疊。M6舊waste/feedMarks/sortedMarkers反射改查正式物料／批次；VisualFPS預置18件假原料與V8設備斷言改查實際線、工作站／PVC／機體碰撞。共用原V11持久Runtime暫停／替身／還原流程，保護原背包、輸入、Time、Cursor、視角偏好與事件訂閱。

`C3V7CZoneBoundaryAndAisleTests` 仍要求封閉 V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER、V8_B_To_C_ContinuousRubber及舊V8_MAIN_STAIR_CONTINUOUS_RAMP。這是已知歷史規格衝突，未修改、未Ignore、未執行；不是已證實的新Phase 2 FAIL，也不能宣稱Run All PASS。其餘專案測試亦NOT RUN。

## F. 修改檔案與 GitHub

相對Phase 1共26檔（含7新meta），以下均在 `Assets/_Modules_/`：

- `02_Levels/Level03/Scenes/C_Main.unity`：局部高位牆洞代理。
- `02_Levels/Level03/Scripts/C3ConveyorSection.cs`、`C3RecyclingLineGeometry.cs`、`C3RecyclingLine.cs`，各含新meta。
- 同Scripts：`C3FactoryPresentation.cs`、`C3ReferenceFactoryEnvironment.cs`、`C3V3Circulation.cs`、`EcoRecycleCargo.cs`、`EcoConveyorSorter.cs`、`EcoAdventureRuntime.cs`、`EcoFactoryWorkshop.cs`。
- `05_Testing/PlayMode/C3V11Phase2RecyclingLineTests.cs`與新meta；`C3V9VisibleIndustrialRouteTests.cs`、`C3V11SafeCirculationTests.cs`、`M6FlowTests.cs`、`C3VisualFPSIntegrationTests.cs`。
- `07_Docs/DevelopmentReports/V11_Phase2_Report.md`、`V11_Phase2_Topology.csv`、`V11_Phase2_SupportChanges.csv`，各含新meta。

不納入.vscode、Library、Temp、Obj或本機檢查產物；Phase 1報告不改。里程碑提交：

- `fd0c6c061acbe3919561483ad0e48b2dc387619c` — feat(v11-p2): rebuild elevated B-side material infeed
- `f9890ab222c5e6a37d8e687ba9b14b10a2c0f097` — feat(v11-p2): unify recycling cargo flow and sorting
- `7b6f95e9574a1d781732b5c1152d2497f67c591f` — feat(v11-p2): implement processing and three output conveyors
- `30b17cb3bf64cedd0a3708b8444b3b7be61c56d9` — fix(v11-p2): align mezzanine supports and clear walking routes
- `f5b9d43510117ba806184713dcc3770e5c0503a9` — fix(v11-p2): clear material handoffs and finite belt contacts


另以 `test(v11-p2): add recycling line integration coverage` 提交最終測試／報告；完整最新SHA、新Draft PR URL見交付訊息及PR commits（報告無法內嵌其自身提交SHA）。新PR Base必須是Phase 1分支，標明依賴#2，維持Draft等待ChatGPT審查與使用者驗收。

## G. 未解決項目與使用者驗收

`B→C Full Level Transition: BLOCKED — Level02 scene unavailable`

完整Level02/B_Main.unity缺少，只完成C側接口、有限演示及人員通道保護；沒有虛構B通關條件／任務進度／玩家傳送。

其餘待驗收：Unity編譯與反射實際執行、30段世界Bounds／接點、陡槽與成品有限帶面接觸、站趴／相機淨空、材質／機械外觀、指定圖片接近程度、FPS／背包／武器及效能。來源推算不能排除Unity匯入、原生物理或鏡頭下的問題。四階段程式與測試已實作，實際遊戲驗收尚未完成。

1. 關閉Unity，GitHub Desktop Fetch／Pull，切換 **feature/v11-phase2-recycling-line**，確認最新交付SHA。本機.vscode不需提交此PR。
2. Unity 2023.1.22f1等待編譯／匯入，先檢查Console紅字；保留完整訊息，不以按Exit Safe Mode當作修復成功。
3. Test Runner→PlayMode：Phase 2的18項、V9的5項、V11的7項，再跑上表兩項輔助回歸，匯出XML；歷史V7衝突另記。
4. C_Main進Play，Dormant無正式物料／成品；實收原三零件、完成控制台解謎並啟動。
5. 跟隨高位九件演示料，每類三件，同一路線／正確分流，三份到床才壓縮；各成一件紙捆／金屬塊／塑膠捆，由各自機內出口到收集床，久候不再無限生。查看Materials／Products／收據，分類不直接加背包材料。
6. 暫停／恢復；完成任務只排空既有料。重載確認一套設備、背包彈藥快捷欄保留、舊已完成ID不重播；新場景九個新演示ID屬新批投入。
7. 實走西上東下、東上西下、後平台、維修工作桌及轉角；Z趴走長廊、測護欄防墜、晶片只加一個、PVC可見雙向通行。再驗WASD、視角、左射／右瞄、Q/E、R、TAB、1～5、原取料／工作台／武器。
8. 提供Console、XML、必要Game／Scene View與異常位置供審查修正。驗收前維持Draft，不合併。

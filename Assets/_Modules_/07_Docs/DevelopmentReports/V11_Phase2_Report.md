# V11 Phase 2 — Recycling line

基準：`ed26bfc9e281737e96421be149f7b539eb79326c`。Phase 1 的五項 V9、七項 V11 與樓梯／晶片／PVC 實測 PASS 為使用者回報；新提交須重新驗證。

分支 `feature/v11-phase2-recycling-line`，Base `feature/v11-factory-rebuild`，依賴尚未合併的 Draft PR #2；不修改或合併 PR #2／main。

參考圖《高科技回收分類工廠全景.png》及 V11 試玩截圖尚未直接取得，沒有逐圖比對。依文字要求及既有場景來源實作；Unity 世界 Bounds 尚待 Editor 實測。

## 里程碑

| 階段 | 實作 | Unity 驗收 |
| --- | --- | --- |
| 2.1 高位入口／局部牆洞／完整機械路線 | 已實作 | NOT RUN |
| 2.2 統一物料狀態與資料相容 | 已實作運輸／識別／輸入佇列；加工交易在 2.3 | NOT RUN |
| 2.3 三類加工／守恆／出口 | 已實作 | NOT RUN |
| 2.4 支柱／管線／回歸 | 支柱／管線已實作；測試待補 | NOT RUN |

## 2.1 設備

高位接口 (1.2,7,12.5) → (-.9,7,12.5) → (-3.8,7,12.5) → (-3.8,7,16.85) → (-6.2,5.8,16.85)，主帶終點 (-19.5,5.8,16.85)。在 2.2 將轉接段移至 X=-3.8，下降段改為 26.565°，並保留主帶到第一個 Plastic 分流口的 1.9m 辨識區。三機沿用 Paper X=-17.9、Metal=-13、Plastic=-8.1。

每段具有實際 START／END Transform、共用模型姿態的實體帶面、側護邊、滾輪與鋼架。交接處縮短側板避免直角側板橫切物料路線；高架跨東梯使用屋頂吊架。各機為中空機殼，保留真實進料／前方出口，沒有整個實心 Cube 穿料動畫。

原 C_Wall_4_Lintel 的 BoxCollider 由世界 Y=3.5..10 改為 Y=3.5..6.5；原 Transform 不變，只改局部 center／size。新增三個具可見模型的實體代理補回 Y=6.5..8.1 的 Z=10..11.65／13.35..15 側帶與 Y=8.1..10 全上緣。留下專用開口 Y=6.5..8.1、Z=11.65..13.35，保留原人員洞過樑下緣 Y=3.5、PVC、地板及 B_Safety。

退役 C3_SortingSimulation 的獨立循環與 ReferenceFactory 的 packages／bFeedPackets、預置成品及舊設備生成。八個舊設備 OBJ 保留資產及 GUID但不再實例化。Presentation 保留原啟動燈光／音效與 DeviceState；ReferenceFactory 保留倉體、PVC、原工作站及公用管線。

設備節點見同目錄 `V11_Phase2_Topology.csv`；該表是設計／來源座標，不能當作 Unity 實際查詢結果。

## 2.2 單一物料流程

`C3RecyclingLine` 是唯一狀態與正式批次計數權責。每份投入具有 ID、不可混淆的類型快照、1 單位、完整 State History、路段、目標機及加工／輸出旗標。相同 GO 沿原料路徑到加工床；以真實設備端點及帶面法線計算姿態，轉角／坡度交接使用共用法線分角保持端點位置連續。主線保持 .5m 排隊間距，機台三個床位有預留／背壓，沒有空床就留在分流閘口排隊。

`EcoRecycleCargo.Kind` 明訂舊 Metal=0、Plastic=1、Electronics=2，Paper 附加為 3。對應機台為 Paper0／Metal1／Plastic2，舊資料介面為 Plastic0／Metal1／Paper2；不支援 Electronics 進入此工業線，不把它冒充紙類。電子回收材料仍來自原快取／工作台。

`EcoConveyorSorter` 在 C_Main 只把符合高位接口契約的同一個物料交給權威流程，不自行加計數、銷毀已接管物料或重複加工。沒有此流程的其他關卡保留明確 enum 映射的舊介面。Workshop 不再依 CompletedCycles % 3 推測類型並重複增加統計；Presentation 的 CompletedCycles 改讀正式批次數。

內建測試供料只有 9 件（每類 3 件）、每次新場景不同 ID，且明示是 C 側有限演示，不是 B 通關。每機最多接收 9 件／3 捆，滿載時拒絕並保留上游物料，不持續無限生成。外部 TryAcceptCargo 只接受位於高位接口、1 單位、小型標準姿態、沒有啟用實體碰撞或動態 Rigidbody 的物料；拒絕時不修改上游物件。Dormant／Starting 不運輸、不加工；Completed 僅排空既有物料，不接新料。停用／重啟不重新播種，暫停時不前進。

## 2.3 加工、成品與守恆

每個可視原料球代表 1 個投入單位，必須 3 個同類單位實際抵達該機的三個加工床位，才啟動 2.5 秒壓縮週期。壓頭依加工進度下壓／回升，原料同步變形；加工完成後才準備對應紙捆／金屬壓塊／塑膠捆。成品保留唯一 BatchId 與三個 SourceMaterialIds；這是 3→1 製造轉換，不是用不同物件假裝同一件原料。

Runtime 以原料 ID 與批次 ID 原子提交、拒絕重複及錯誤類型，然後才顯示成品並發布一次 BatchCompleted。分類只解鎖原取料點，沒有 AddItem 自動放入背包。原料記錄完整經過 Spawned→Feeding→MainBelt→Identified→Diverting→MachineInput→Processing→OutputReady→Outfeeding→Collected。成品在原加工床形成，經水平出料床、機內斜帶、真實前方出口、各自下帶與收集床，沒有跳至出口。滿載每機最多 3 捆，有明確背壓，沒有循環無限生成。

統計定義：AcceptedUnits = WaitingUnits + ProcessingUnits + ProcessedUnits；ProcessedUnits = 3 × ProcessedBatches；CollectedProducts ≤ OutputProducts ≤ ProcessedBatches。WaitingUnits 包含尚未釋放的 Spawned 以及運輸／機台等待；FedUnits 為已正式釋放上帶的單位。OutputProducts 僅在越過真正 MACHINE_OUTPUT 節點後增加；CollectedProducts 僅在到達對應收集格後增加。

Receipt ledger 在同一個持久 Runtime 的場景重載間保留，拒絕已完成上游 ID 重播；未完成物料由上游重試，沒有偽造已完成收據。C_Main 重載仍沿用原規則重置本關材料解鎖計數，不清背包／彈藥／快捷欄。ledger 上限 4095 單位，容量不足拒收而不淘汰去重紀錄。此為本次執行期間的防重播，不宣稱已有跨程式重啟的存檔系統。

## 2.4 支柱、管線與碰撞

六根 V11_EDGE_SUPPORTS 由 X=-24.10／-.65 移到 -24.32／-.43，Z=14.5／17.5／20.9 改到平台接點 13.75／17.95／21.20，截面 .14 改 .18m。柱子 Y=.56..4.32，新增可見的懸臂梁與斜撐，最高不超過平台底面 4.32，沒有把柱頭穿進 Y=4.5 的走道。西柱 X Bounds=-24.41..-24.23、東柱=-.52..-.34，均在平台外緣外側。

十三根全高倉體裝飾柱改嵌牆位置：西 X=-24.53→-24.70、東 -.45→-.12；截面 .30×.32 改 .18×.22。柱身朝走道的內緣分別 -24.61／-.21，退到走道外；踢腳板同步移動縮窄。它們仍是無實體 Collider 的倉體裝飾，真正支承為上述有碰撞的柱梁，沒有新增隱形牆。

外側供應管線 X=-23.70→-24.55、X=-1.38→-.15；全五條基準 Y=7.60→8.80。最低吊夾底面約 Y=8.06（含 .055 厚度），高於站姿頭頂 6.30。中央抽氣管改先在 (-6,15.94) 垂直升到 Y=8.65，再回到 (-3.9,9.20,17.10)，避開 X=-3.8..-6.2 的轉接帶。逐件位置／尺寸見 `V11_Phase2_SupportChanges.csv`；均是來源設計值，仍須 Unity Bounds／角色實測。

原 CharacterController（半徑 .30、站高 1.80、skin .08、步高 .30、坡限45）、兩座22階、14段實體防墜護欄、工作桌／晶片位置和物品ID、原控制台／背包／武器玩法均未改動。新高架帶與吊架在站姿頭部之外，回歸將沿用原角色的 TickMovement／CharacterController.Move 驗證，並檢查趴姿與轉角；不能以此靜態尺寸直接宣稱通行PASS。

## 驗證狀態（本提交）

AST 語法與 Scene YAML 結構檢查已執行，非 Unity 編譯。Unity Compilation: NOT RUN；Unity PlayMode: NOT RUN；Manual Gameplay: NOT RUN。本環境沒有可用 Unity Editor。

`B→C Full Level Transition: BLOCKED — Level02 scene unavailable`。只完成 C 側高位物料接口，不建立假的 B 通關狀態；後續有限測試供料不代表正式 B 關卡整合。

# V11 Phase 2 — Recycling line

基準：`ed26bfc9e281737e96421be149f7b539eb79326c`。Phase 1 的五項 V9、七項 V11 與樓梯／晶片／PVC 實測 PASS 為使用者回報；新提交須重新驗證。

分支 `feature/v11-phase2-recycling-line`，Base `feature/v11-factory-rebuild`，依賴尚未合併的 Draft PR #2；不修改或合併 PR #2／main。

參考圖《高科技回收分類工廠全景.png》及 V11 試玩截圖尚未直接取得，沒有逐圖比對。依文字要求及既有場景來源實作；Unity 世界 Bounds 尚待 Editor 實測。

## 里程碑

| 階段 | 實作 | Unity 驗收 |
| --- | --- | --- |
| 2.1 高位入口／局部牆洞／完整機械路線 | 已實作 | NOT RUN |
| 2.2 統一物料狀態與資料相容 | 已實作運輸／識別／輸入佇列；加工交易在 2.3 | NOT RUN |
| 2.3 三類加工／守恆／出口 | 待實作 | NOT RUN |
| 2.4 支柱／管線／回歸 | 待實作 | NOT RUN |

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

## 驗證狀態

AST 語法與 Scene YAML 結構檢查已執行，非 Unity 編譯。Unity Compilation: NOT RUN；Unity PlayMode: NOT RUN；Manual Gameplay: NOT RUN。本環境沒有可用 Unity Editor。

`B→C Full Level Transition: BLOCKED — Level02 scene unavailable`。只完成 C 側高位物料接口，不建立假的 B 通關狀態；後續有限測試供料不代表正式 B 關卡整合。

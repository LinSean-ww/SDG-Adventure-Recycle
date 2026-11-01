# V11 工廠視覺升級 — 實作與待驗收報告

- 分支：`feature/v11-factory-visual-upgrade`
- 基準與目前 HEAD：`6bafde862ee1e235e19fa613eb7f9b7cfd4f070b`。
- 使用既有隔離 checkout，沒有建立 worktree、Commit、Push 或操作 PR。
- 依使用者本次提供的 GitHub 確認：Phase 2 Fast-forward 已完成；PR #3 仍為 Open / Draft，Head 為 `feature/v11-phase2-recycling-line`，SHA 為 `6bafde862ee1e235e19fa613eb7f9b7cfd4f070b`。本次沒有重新 Fast-forward，也沒有查寫或操作 PR。
- 使用者提供的 M6 11/11、第一人稱、WASD、樓梯 PASS 均屬修改前基準。
- 本次是程序化立體美術與修補程式；沒有 Unity 2023.1.22f1，所有 Unity Compilation / EditMode / PlayMode / 人工視覺驗收均 **NOT RUN**。

## 素材與實際可用工具

完整 ZIP 已解壓在 checkout 外的 `/workspace/attachments/v11-visual-references`，18 張 PNG 均以完整解碼確認可讀。已查看三張主要完整原圖、目前遊戲截圖，並查看既有三張繁中分類標牌。

- `01_Four_Workstations/00_Original_Four_Station_Sheet.png`
- `01_Four_Workstations/01_ResourceProcessingConsole.png`
- `01_Four_Workstations/02_RecycledMaterialInspection.png`
- `01_Four_Workstations/03_QuantumWeaponAssembly.png`
- `01_Four_Workstations/04_MaintenanceWorkbench.png`
- `02_Recycled_Assets/Style_A_Original.png`
- `02_Recycled_Assets/Style_A_01_Worker.png`
- `02_Recycled_Assets/Style_A_02_Circuit_Board.png`
- `02_Recycled_Assets/Style_A_03_Plastic_Waste.png`
- `02_Recycled_Assets/Style_A_04_Metal_Scrap.png`
- `02_Recycled_Assets/Style_A_05_Quantum_Weapon.png`
- `02_Recycled_Assets/Style_B_Original.png`
- `02_Recycled_Assets/Style_B_01_Worker.png`
- `02_Recycled_Assets/Style_B_02_Circuit_Board.png`
- `02_Recycled_Assets/Style_B_03_Plastic_Waste.png`
- `02_Recycled_Assets/Style_B_04_Compressed_Metal_Bale.png`
- `02_Recycled_Assets/Style_B_05_Quantum_Weapon.png`
- `03_Current_Unity_Screenshot.png`

Blender 4.3.2 成功讀取原 fk.fbx：一個 BODYBONE armature、16 個骨骼節點、1308 頂點 people mesh、people/eyes 材質，沒有綁定 action。此探測不代表 Unity Humanoid Avatar 或動畫驗收。Blender 回報家目錄 extension cache 唯讀，但 FBX 匯入與探測完成。

## A — 玩家來源、朝向與立體工人（實作完成，品質／動作待驗收）

靜態來源：M2_Player prefab 的 fk 根有 -90° Y 視覺旋轉；原 C3BoneController 保留匯入 rest pose，idle 只有小幅 X 擺動，不能把 T-pose 自動變成自然垂臂。`M3_FirstPersonShadow` 是 fk 下另一個白色 SkinnedMeshRenderer，原來只投影；PlayerPerspectiveRig 第三人稱會把原 ShadowsOnly 轉為 On，令該替身可見。這能解釋白色 T-pose 的來源路徑；沒有對使用者截圖中的運行時物件進行遠端實機探測。

新增 C3WorkerAppearance，在 C3FirstPersonController 原 perspective.Bind 之前安裝。保留 fk／骨骼／FBX，只停用其舊 Renderer；新視覺是有深度的程序化關節層級，包含安全帽、反光背心、深色衣褲、手套、工具腰包、膝墊、靴子及凸起回收箭頭。模型正面統一 +Z，依實際速度調整視覺子根，不改 CharacterController 根。使用原 PlayerPerspectiveRig 隱藏／顯示新模型，不新增 Camera 或玩家控制器。

站立、走／跑擺肢、蹲下屈膝、趴下與頭部回正已寫入程序化姿勢。這不是新製作的高品質 SkinnedMesh/Humanoid 動畫庫：沒有宣稱已有 mocap、完整手指／臉部、腳底 IK、武器持握 IK 或照片級服裝。姿態銜接、腳底穿地、攝影機、遮擋與陰影均待 Unity 調整。

本次審查修復保存 fk Renderer 的原 enabled 與 shadowCastingMode。停用時立即隱藏新根；以存活玩家的 rig 執行下一幀恢復，先檢查玩家、場景與是否已重新啟用，避免場景卸載時恢復正在銷毀的外觀。整個玩家停用時，改借既有 persistent runtime 作 coroutine scheduler，等待玩家回到 active；只排程存活檢查，沒有呼叫其背包／獎勵 API，沒有建立額外 runner。若玩家已被銷毀／場景卸載，直接退出。單獨移除時先移除新根的快取、隱藏並解除所有自有材質引用，再延遲 Destroy 幾何並釋放調色盤。重新啟用只重用原根，不重建模型。

已先向使用者回報共享快取問題與最小方案。PlayerPerspectiveRig 只新增 `SetBodyRendererBaseline` 與 `RemoveBodyRenderers`，共 45 行：同步存活 Renderer 的基準快取／當前視角狀態，並移除被淘汰的子樹快取。原 Bind、ApplyMode、相機碰撞修正及移動方法均未改；沒有強迫重新 Bind 相機。原 C3FirstPersonController 仍僅一行安裝 hook，與前次審查輸入相同。C3BoneController、SharedFirstPersonController、原 prefab 與 fk.fbx 未改。

## B — 三分類機標牌（靜態幾何核對，原生面向待驗收）

原 AtWorld 以 Quaternion.identity、bothSides=false 建牌。MakeFace 將唯一可讀面放在 local +Z .038，且旋轉 180°。若 Unity Quad 的原生法線是 -Z，舊設定會令可讀面朝 +Z；牌面世界 Z=12.858，機器前板最小 Z=12.85，牌面還略埋在前板。這個面向推導依賴原生 Mesh，不能當作已執行 Unity 的結果。

保留前次三機標牌世界 Y=180° 修正，沒有再改動其幾何或共用 MakeFace。根旋轉 180° 乘上 ReadableFront 的 local 180° 等價 360°；以原生法線 -Z 為前提，世界法線仍為 -Z，世界 U／V 軸分別 +X／+Y，沒有左右鏡像。local z=.038 經根旋轉後令面位於 Z=12.782，前板外側約 68mm。共用 C3BakedSign Shader 明確 Cull Back、ZWrite On、ZTest LEqual，會剔除背面，因此位置正確不代表面向已經通過。

PaperHall／MetalHall／PlasticHall 的 PNG 已解碼查看，字樣為紙類／金屬／塑膠；Texture、Shader、GUID 不變。新增測試讀取 Unity 實際 Mesh.normals、vertices、uv，核對兩層旋轉、世界法線、UV 軸、前板間距及 -Z approach 的原生 Physics.Raycast 遮擋，並保留無有效標牌 Collider 的檢查。此環境無 Unity，沒有觀測原生 Quad 法線，也沒有實際看見標牌；以上自動化與玩家通道可讀性均待 Windows 驗收。

## C — 原四站 3D 美術（程序化近似，待視覺／功能驗收）

C3WorkstationVisualUpgrade 在原四個物件下掛接新的視覺根：

| 原物件 | 新立體細節 |
| --- | --- |
| M4_PuzzleConsole | 三組監控畫面、立體按鈕／拉桿、紅色急停、通風柵、外接管線、櫃體 |
| Secondary_RecyclingInspectionWorkbench_F | 掃描橫架／鏡頭、托盤、PCB、塑膠樣本、檢測螢幕 |
| Hidden_WeaponAssemblyBench_F | 雙機械臂與夾爪、固定座、綠能槍展示模型、控制螢幕 |
| UpperChipMaintenanceBench | 立體工具板、扳手／螺絲起子／鉗子、工具箱、PCB、發光工作燈外殼 |

共通抽屜、把手、支腳、黃黑警示、掉漆細節與凸起回收箭頭。舊控制台的額外 ReferenceFactory 外殼生成移除，避免兩個重疊外殼。有效 Collider 的原構件 Renderer 保留可見；其他舊呈現可隱藏，但 gameplay 元件／Collider 不停用。新的裝飾碰撞在建立當下即停用，延後 Destroy；實際是否不遮擋互動由新增原生射線測試驗收。

本次將借用與自建標牌分開管理。借用的 ControlCenter／Inspection／Assembly 在搬動任何牌之前保存原 parent、local/world 位置與旋轉、scale、sibling；移除元件時先恢復 parent／姿態，再依原 sibling 排序，然後才刪除自建根。借用材質完全不加入釋放清單。自建的維修站牌與其材質才屬於本元件；刪除時先停用根、解除 Renderer 引用，再釋放其材質及自有調色盤。原有物理 Collider 與互動身分保留。

真正的 F 路徑：控制台／任務零件由 C3PlayerInteractor 處理；檢測台、組裝台及材料箱由 EcoAdventureRuntime.InteractWorkshop 處理。維修桌沒有獨立 C3Interactable 或 EcoWorkshopInteractable；保留桌面與附近控制晶片拾取，不新增維修任務或獎勵。這是中低細節程序化 3D，不宣稱與參考圖同等寫實。原零件、維修桌位置、互動權限、啟動與 UI 均保留，仍需玩家繞看及實際 F 互動驗收。

## D — PCB、廢料、成品浮雕（不增加正式計數）

- PCB 使用立體基板、晶片、插座、電容、散熱片、走線及焊點，放在原 CircuitMaterial 箱與工作站上，不是第十件正式原料。
- 三個原料籠內原本的純色 Cube／Cylinder 改用既有 Paper_FoldedCarton、Metal_CrushedCan、Plastic_CrushedBottle Body/Details 立體 OBJ。都是 DECOR_ONLY，沒有 EcoRecycleCargo 身分或供料呼叫。
- 原正式 15 種原料模型、九進三出、材料 ID、原料包絡與共用材質流程保留。
- 金屬／塑膠成品仍使用原立體 Shell＋Front＋束帶。其 Front 原 Z=-.250..-.247058，可能埋入封閉外殼面；正面浮雕整體前移 4mm 至 -.254..-.251058。保留拓樸、UV、法線、原 GUID，仍在正式 ProductSize(.52,.43,.51) 包絡內。
- 未把候選完整概念圖貼成角色／工作站／武器平面；沒有新增假成品或獎勵。

## E — 一套綠色量子槍（視覺替換，功能待回歸）

C3VisualBuilder.QuantumRifle 供組裝台展示與 EcoQuantumWeaponView 使用同一造型：回收金屬機匣、槍托、握把、彈匣、雙綠能管、銅管、金屬箍、瞄具、能源槍口及回收箭頭。後者內部的光與短暫射線改綠色；音效、muzzle 位置、可見狀態、ADS、後座及近牆避讓方法保留。

本次 QuantumRifle 新增 ShadowCastingMode 參數：第一人稱明確傳 Off，組裝台預設 On，沒有複製第二套造型。元件移除時先停用自有 rifle、清空其 Renderer 材質引用，再 Destroy rifle／調色盤；不釋放組裝台的另一個 owner。裝飾 Collider 仍於建立時停用並排程刪除。新增測試比較兩者部件清單、陰影、Collider 及移除後材質所有權；尚未 Unity 實測。

上一輪 EcoAdventureRuntime 的 BuildHeldModel 武器造型分支修正（4 行新增／12 行刪除）本輪原樣保留。提交前核對另確認兩處原有 Material 缺口：Shoot 每發光束及 AddGunPart 非武器手持物都 new Material，但刪除 GameObject 沒有明確釋放該 Material。僅新增 C3OwnedVisualMaterial，讓各 single-slot Renderer 的專屬 Material 隨同一 GameObject 清理；OnDestroy 先解除仍屬於自己的材質引用，再延遲 Destroy Material，避免提早釋放或透過 renderer.material getter 產生額外實例。已正確的 C3VisualBuilder／EcoQuantumWeaponView 所有權與 Dispose 不重做。

本輪正式 Shoot 光束起／終色由青色改為綠色 (0.2,1,0.25)／(0.1,0.65,0.18)。原起終點、85m 命中射線、15 傷害、0.18s 射速與光束壽命、彈匣／能源及輸入規則保留；沒有改接 OnShot callback 或再生第二組光束。其他射擊判定、R、TAB、1～5、獎勵防重複、ADS／第三人稱位置更新及跨場景方法保留。現有 20 項視覺測試、共享 Renderer 快取及工人程式已檢查，本輪未修改。所有 Unity 驗證仍為 NOT RUN；切換武器→空手→其他手持物→武器、連續射擊的 Material 數量與場景銷毀清理待 Windows 驗收。持握手部、精細焊縫與高精模型未完成；不能宣稱新增了完整第三人稱裝備骨架。

**另發現並修復的視覺接線缺口：** 原本搜尋 Assets／Packages，EcoQuantumWeaponView 的 AttachTo 沒有正式呼叫點；MonoScript GUID 沒有場景／Prefab 綁定。正式持槍仍由 EcoAdventureRuntime.BuildHeldModel 建立 RecycledHousing／EnergyCoil 等舊造型，單獨建立視覺元件的測試無法驗證正式持槍。因此已先回報來源證據與最小接線 Diff，再僅替換該方法的武器造型分支：AddComponent<EcoQuantumWeaponView>、AttachTo 原 camera、SetVisible(true)、恢復原 hip offset，沒有重寫 runtime 或修改射擊／背包／獎勵方法。

陰影測試已改走正式 inventory/quick slot → BuildHeldModel → 取得真實 weaponModel，而不是自行安裝視覺元件；核對共用部件、Off／On、無有效 Collider、第一／第三人稱 parent、模型重建與自有材質釋放。checkout 外 `ProductionWeaponWiring.patch` 提供上一輪這個入口的實際 Diff。原 runtime 仍負責 ADS／射擊與特效；本輪只將其既有 Shoot 光束改綠，沒有改接新的 OnShot／UpdateViewmodel callback，也沒有新增近牆避讓行為。正式持槍尺寸、各視角、ADS、射擊、重載與場景切換仍待 Unity 驗收。

## F — 拾取物與探索提示（程式實作，待原生驗證）

EcoEnergyPickup 的條件／數值／補能方法完全未改；僅將原粗略外觀改為瓶身／瓶頸／瓶蓋／標籤、罐體／捲邊／拉環及分層廢報紙。原 Trigger 尺寸不改。

本次 C3SalvageAppearance 保存 V11_SalvageArt_Visual 與附屬 hint。停用／重新啟用重用同一個根；移除時先停用 hint 並顯式 ReleaseVisuals，再停用幾何、解除 Renderer 材質引用，最後延遲 Destroy 根並釋放調色盤。保留 PlasticBottle／MetalCan／WastePaper 的 5／8／3、手持武器門檻、滿彈匣保留與 once-only；正式 EcoEnergyPickup 沒有變更。

C3ProximityHint 保留 3.6m 漸淡、低速脈動、Physics.RaycastNonAlloc 及固定 32 hits buffer，飽和時保守不可見。以 DefaultExecutionOrder(250) 在 default-order 視覺 owner 的 Start 之後捕捉 Renderer，避免材料箱的新 PCB／瓶罐漏掉提示；新增來源數量對照測試。不新建 Light，材料箱仍借用原一盞。Configure 在第一次改值之前保存 enabled／intensity；每次寫入前比對上次自有值，發現其他系統新值就放棄該欄位，停用／移除只恢復仍由本元件控制的欄位。色彩、range、shadows 等未改動。

舊方式會克隆 source 材質並覆寫 source 的全域 PropertyBlock，無法安全還原與 concurrent writer 共存：Unity MPB API 不提供列舉／移除單一屬性，per-material block 又有獨立優先級。本次以 C3HintVisuals 隔離：一次保存各 source Renderer 的 sharedMaterials、global MPB 及每個 material index 的 MPB，建立使用原 sharedMesh／bones 的自有 additive overlay。Source 材質與所有 MPB 從未寫入，因此停用／刪除毋須把過期快照寫回，原始狀態與運作期間外部新值均保留。

Overlay 使用 Resources/V11Visual/C3HintOverlay.shader，ZTest LEqual、ZWrite Off、Cull Back、Off shadows，不含 Collider；每個 owner 只有一個自有 Material，_EMISSION 關鍵字控制實際 shader 輸出。Update 只更新自有 MPB／Renderer，不建立 Material、Light 或 GameObject。清理時先隱藏自有 Renderer、解除自有材質引用，再延遲 Destroy 幾何／Material；不釋放任何 source Material 或 sharedMesh。附加 draw calls／填充率與透明層順序需 Profiler 和畫面驗收。

C3CollectiblePart 也改用同一隔離機制，保留原自有 NearbyPartGlow Light；OnDisable／OnEnable 先清 cue，下次 Update 必定重算距離／LOS，移除造成 stale glow 的提前 return。單獨移除時清理 overlay 與自有 beacon，source 引用維持不變。CanInteract／Interact 與原進度、獎勵方法逐位元保留。既有 M6 近距離發光測試保持原斷言；若 LOS 揭露原幾何阻擋，必須保留失敗證據調查，不得放寬或 Ignore。

## G — 做舊與色調（視覺設定，待光照驗收）

複用既有 ArtFull 的 RustedSteel／DarkSteel／OilDust 貼圖，另存至 V11Visual Resources 供程序化材質使用，原圖及 meta 不變。新櫃體、槍體及裝備採鏽蝕材質與掉漆幾何；新增 16 處薄油污／輪痕與避開 B/C 門的牆面鏽水痕，無有效 Collider。降低 ReferenceFactory 的水泥／地板亮度與環境光，保留自發光關鍵畫面、安全牌和原廠燈。

沒有修改 C3FactoryDecay 的原事件／燈光程式、原場景碰撞／樓梯／走道／B/D 門。新表面細節屬補充做舊，未製作倒塌結構。新亮度需與原視覺回歸、螢幕可讀性、真實目標裝置效能一起驗收。

## 靜態檢查與保護

- C# 語法解析；不是 Unity 型別編譯。
- 原 M6、VisualFPS、Phase2、SafeCirculation 套件未修改；C3V11VisualUpgradeTests 擴充為 20 項 UnityTest，均 NOT RUN。
- C_Main、M2_Player prefab、原 fk、原 GUID/meta、正式九進三出、獎勵與能量方法不變；EcoAdventureRuntime 的例外僅限已說明的武器視覺建立、光束顏色與臨時 Material 所有權。
- C3FirstPersonController 唯一變更為視覺安裝 hook；C3RecyclingLineGeometry 唯一變更為三標牌旋轉。
- OBJ 保留原 UV／法線／三角索引，偏移後不超過成品代理。
- 新資產都有 meta；所有 meta GUID 唯一。新程式沒有新增動態 PointLight、獎勵、供料或玩家 Move 呼叫。
- git diff --check；工作樹未暫存，沒有 Commit／Push。

### 新增測試的實際範圍與隔離

20 項涵蓋：工人重複第一／第三人稱與停用重啟、移除後舊快照恢復／重裝、inactive 玩家移除工人再恢復、依實際 CharacterController 速度轉身；四站原 Collider／無新增 cargo；控制台真實 FindTarget → 原 input callback／Update → 開 UI／解謎／確認啟動；檢測與組裝的真實 InteractWorkshop 射線派送、缺料失敗、材料箱領取、精確扣一份及防重複；維修桌附近晶片的原任務拾取；三分類牌實際 Mesh／UV／遮擋；LOS、全域／索引 MPB concurrent writer、Light 還原／外部寫值、任務提示停用／重啟／拾取／單獨移除；借用標牌歸還；salvage 幾何清理與 5／8／3 門檻；正式持槍建立／共用武器陰影／重建清理；兩輪完整場景重載及 hint Material 數量。

測試沿用 C3V11Phase2RecyclingLineTests → C3V11SafeCirculationTests 的已存在隔離 fixture：暫停並脫離原 persistent inventory 的 sceneLoaded 訂閱，另建測試 inventory，結束／失敗時清理測試場景並恢復原 singleton、訂閱、背包、quick slots、PlayerPrefs、input context、cursor、timeScale。新增臨時 GameObject 先停用／Destroy，等待清理後才釋放借用的測試 Material。未修改既有 fixture 與 M6 11 項。

測試不是 OS 鍵盤／滑鼠模擬：任務互動以既有 callback 排隊後執行原 Update，工作站執行 F 原本呼叫的 ray dispatcher，兩者都必須先命中真實 gameplay Collider。解謎選項的新增驗證呼叫原 UI handler，真實 UI Raycast／ScrollRect 仍由未改動的 M6 驗證。組裝測試以正式 receipt API 配置上游已處理材料，再從原材料箱派送；額外一份 fixture 材料只用於辨別是否精確扣一份。完整輸送／九進三出由原 Phase 2 suite 與人工驗收負責，不宣稱此新測試已跑完整產線。

## Windows Unity 2023.1.22f1 驗收（全部待執行）

1. 等待編譯／Shader 與資產匯入；記錄所有 Console 訊息。先執行 20 項 C3V11VisualUpgradeTests，保留 XML 與 Editor.log；以上全部待執行。
2. 執行完整 M6FlowTests 11 項，尤其兩個 UI Raycast、出生點樓板、控制台與 B/D 門、重開／返回／再進入、近距離拾取提示。
3. 執行 C3VisualFPSIntegrationTests、C3V11Phase2RecyclingLineTests、C3V11SafeCirculationTests、C3V11RecyclingArtTests、現有 M2／M7 玩家與相機回歸。
4. 第一人稱看不到自己的白色 T-pose；第三人稱繞看完整工人；各方向走／跑，確認外觀面向實際速度。站立、Ctrl 蹲下、Z 趴下、姿態恢復、Q/E、上下雙梯、靠牆切換視角，檢查穿地、相機與陰影。
5. 控制台、檢測台、組裝台以真正 F 與原距離限制互動；維修桌維持非獨立 F 任務、驗證附近晶片。收三件、錯解／重試／正解／啟動／完成畫面；檢測台不送材料、組裝依原條件扣料一次。
6. 三台中文標牌由 -Z 正面與斜角查看，不鏡像、不被前板／輸送帶遮住；保留截圖。
7. 九原料→三正式成品，核對金屬／塑膠浮雕、束帶、壓縮包絡、收集／收據／防重複。籠內廢料不得變正式供料。
8. 三種地面廢料、材料箱與任務物件：遠近、遮牆、拾取消失、滿彈匣保留、未裝備不補能；Profiler 檢查每幀沒有新建材質或光源。
9. 左鍵、右鍵 ADS、R、TAB、1～5 與場景切換，確認原槍與獎勵持久性；新槍口對齊、近牆避讓與畫面占比需人工調整。
10. 觀察降低亮度後的材質、螢幕可讀性與 FPS；重載至少兩輪，沒有重複工作站、提示光或殘留材質。
11. 逐一單獨移除 worker／hint／collectible／workstation／salvage 視覺元件；檢查舊身體視角快取、原標牌、原材質與 MPB、新幾何／Light／Material 清理。帶遮擋遠近停用／重啟提示，檢查外部 indexed/global MPB 與 Light 寫值保留。
12. 在 1280×720、1920×1080、866×838、640×360 檢查 UI、四站與標牌；原 M6 11/11 是改動前基準，新版尚無 11/11 驗收結果。

## 不應宣稱完成的項目

沒有新製作或原生驗證的 Humanoid Avatar／高品質動畫／腳底 IK／手指與持槍 IK，也沒有照片級角色或武器。程序化造型與新姿態尚未在 Unity 看過；分階段程式實作完成不代表美術品質驗收完成。所有正式遊戲驗收仍待 Windows Unity 結果。

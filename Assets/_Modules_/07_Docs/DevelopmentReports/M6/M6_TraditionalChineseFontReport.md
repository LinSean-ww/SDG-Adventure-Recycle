# SDG Adventure M6｜Windows Build 繁體中文字型修復

日期：2026-10-24（Asia/Taipei）  
範圍：僅 `SDG Adventure_M6_TEST`。正式母專案、原始 C3 與 `ONLYfactory.fbx` 均未修改。Windows Player 的實際中文字畫面仍待使用者人工驗收。

## 1. 問題定位

原 M6 UI 全部使用 Unity Legacy IMGUI：`GUI.skin`、`GUIStyle` 與 `OnGUI` 均未指定專案自帶字型。選關入口、任務 HUD、互動提示、控制台及結算文字在 Editor Play Mode 可藉 Editor 字型顯示；使用者實測 Windows Player 中繁中文字成方框，英文與數字正常。修復前 `Assets/` 沒有 `.ttf`／`.otf` 字型資產。這與 Player 無可用繁中 IMGUI 預設字型的現象一致；單靠原 Build 成功不能證明字形顯示。

共用字型現在由 `Resources.Load<Font>("Fonts/NotoSansCJKtc-Regular")` 取得；每個相關 `OnGUI` 在建立／使用 `GUIStyle` 之前將它套用到 `GUI.skin` 的 label、button、box、window、textField、textArea、toggle。HUD 和互動提示的快取樣式在首次套用後才建立。未修改 UI 文字、位置、字號、玩法或操作。

## 2. 字型來源與授權

- 字型：**Noto Sans CJK TC Regular**，臺灣繁體中文靜態 OTF。來源為 [Noto CJK 官方儲存庫的字型檔](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf)；[官方下載指引](https://github.com/notofonts/noto-cjk/blob/main/Sans/README.md)列出臺灣繁中版本及單一字重 OTF。
- 授權：[Noto CJK Sans/LICENSE](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)，**SIL Open Font License 1.1**。原授權文字允許字型與軟體一同封裝、嵌入及散布；本輪未修改字型。完整授權檔放在 `Assets/StreamingAssets/FontLicenses/NotoSansCJK-OFL.txt`，Windows Build 的 `StreamingAssets/FontLicenses/` 內亦保留同檔。
- 下載字型 SHA-256：`dce08bd4fd91aa8aa76ed8fea4b694c2dfb8550f67871e326843212ddbeb88b4`，16,435,884 bytes。授權檔 SHA-256：`6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`，4,301 bytes。來源為官方 `main` 分支於本輪下載時的內容，以上雜湊固定本次交付版本。

未使用 Windows 系統字型，也未要求 Player 從使用者電腦安裝字型；未新增 Unity 套件。

## 3. 新增與修改檔案

| 類別 | 檔案／用途 |
|---|---|
| 字型資產 | `Assets/Resources/Fonts/NotoSansCJKtc-Regular.otf` 及 Unity 產生的 `.meta`；`Assets/Resources.meta`、`Assets/Resources/Fonts.meta`。`Resources` 確保字型納入 Player。 |
| 隨 Build 授權 | `Assets/StreamingAssets/FontLicenses/NotoSansCJK-OFL.txt` 及目錄／檔案 `.meta`。 |
| 共用 IMGUI | 新增 `Assets/Shared/Scripts/CjkGuiFont.cs` 與 `.meta`，集中載入並套用 `GUI.skin` 字型。 |
| 選關入口 | 修改 `Assets/Core/ModuleSystem/ModuleCoordinator.cs` 的 `OnGUI`，只增加共用字型套用。 |
| C3 UI | 修改測試副本中的 `C3TreasureHud.cs`、`C3PlayerInteractor.cs`、`C3PuzzleConsole.cs`、`C3CompletionScreen.cs`，各在 `OnGUI` 套用同一字型；來源 `.meta`／GUID 保留。 |
| 驗證 | 新增 `Assets/Editor/M6FontValidation.cs`、`Assets/Tests/PlayMode/M6FontTests.cs` 及 `.meta`；本報告與 `DevelopmentReports/M6/Font*` 日誌／XML。 |
| Build | 重新產生 `DevelopmentReports/M6/WindowsBuild/`。 |

未更動 `C_Main.unity`、四個 C3 Prefab、`ONLYfactory.fbx`、七個原材質、Graphics/Quality、M6 光照更新呼叫、控制台位置或 UI 文案／版面。
`FontFixStaticValidation.txt` 再核對原始 C3 M6 清單 98 檔雜湊差異 0、移植來源 GUID 32 個差異 0、場景與 FBX 與來源相同，且原光照更新呼叫仍在。

## 4. 驗證結果

| 項目 | 狀態 | 證據 |
|---|---|---|
| Unity 字型匯入、Resources 載入 | PASS | `FontValidationFinal.log`：OTF 匯入為動態 `Font`，`Resources.Load` 指向同資產。 |
| 五處 UI 文字字形覆蓋 | PASS | `M6FontValidation` 掃描選關、HUD、互動、解謎、結算腳本字串；216 個不同字元（含繁中與標點）`Font.HasCharacter` 缺字 0，且五處均呼叫共用字型。 |
| 英文、數字與代表性繁中 | PASS | `M6FontTests` 於 PlayMode 從 `Resources` 載入並檢查代表性字元；包含在 3/3 測試內。 |
| 原 M6 關卡流程與光照回歸 | PASS | `FontFixPlayModeResults.xml`：**3 TOTAL／3 PASS／0 FAIL／0 SKIPPED**；包含原流程及視覺回歸測試。 |
| Unity Editor 場景／依賴檢查 | PASS | `FontFixEditorValidate.log`：21 項檢查、0 FAIL。 |
| Windows x64 Build | PASS | `FontFixBuildFinal.log`：Succeeded，**0 errors、0 warnings**。 |
| 字型與授權納入 Build | PASS | 首次字型 Build 日誌列出 `NotoSansCJKtc-Regular.otf` 約 15.7 MB；最終 Build 的 `SDGAdventure_M6_TEST_Data/resources.assets` 為 16,436,594 bytes，內含字型名稱；`SDGAdventure_M6_TEST_Data/StreamingAssets/FontLicenses/NotoSansCJK-OFL.txt` 存在且 SHA-256 與來源授權檔一致。 |
| Windows Player 實際繁中顯示 | **待人工驗收** | 編譯、字形覆蓋與打包可自動確認；實際 Game 視窗是否仍有方框，必須由使用者檢查。 |

首次匯入時 Unity 的中間編譯階段曾在新共用腳本尚未匯入前短暫回報找不到 `SDGAdventure.UI`；同次匯入後重編成功。之後獨立的 `FontValidationFinal.log`、PlayMode 及最終 Build 均無 C# error。完整早期日誌保留供追溯，最終狀態以上表為準。

## 5. Windows Player 人工驗收

1. 啟動 `DevelopmentReports/M6/WindowsBuild/SDGAdventure_M6_TEST.exe`，確認選關標題、說明與按鈕中英文、數字、繁中及 `｜` 正常，沒有方框。
2. 進入 C3：按 P 展開任務 HUD，確認目標、三零件名稱與提示；看向可互動物件，確認 E 鍵提示中的繁中正常。
3. 取得三零件，開啟控制台，檢查線索、三步選項、錯誤回饋、重試與啟動確認的中文字及 `→`、`／` 等符號。
4. 通關後檢查結算數字、SDG 12 教育文字、重新開始與返回選關按鈕；依序重開、返回、再次進入，確認字型及功能持續正常。
5. 同時留意字形是否過小、被裁切、首次顯示是否延遲，以及 Unity Player 日誌或畫面錯誤；若有問題，記錄截圖與解析度交回測試副本處理。

本輪只完成技術修復及自動驗證，**不宣告 Windows Player 中文畫面人工 PASS**；等待使用者與 ChatGPT 審查。

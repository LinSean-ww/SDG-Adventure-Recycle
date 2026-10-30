# SDG Adventure｜模組驗收規範

## 自動檢查

1. Unity 2023.1.22f1 可開啟與編譯；asmdef 引用完整，無新增 C# Error。
2. 所有原有正式資產的 `.meta` 與 GUID 保持一致；Scene、Prefab、FBX、Material、Script 無 Missing 或非預期引用。
3. Build Settings 含開發入口及已註冊正式關卡；測試場景不混入正式關卡清單。
4. 執行 EditMode 與 PlayMode；至少驗證載入、未完成防護、一次性結算、重開、返回、再次進入。
5. C3 回歸包含角色移動／跳躍、骨骼、第一人稱遮擋、三零件收集、解謎、設備啟動及完成流程。SharedPlayer 在自己的測試場景驗證，不強制掛到 C3。

## 人工檢查

在 Game 視窗以真實鍵鼠操作 WASD、滑鼠俯仰、跳躍、E 互動、任務與結算 UI。目視檢查第一人稱自遮擋、角色外觀／陰影、材質、燈光、碰撞、音量及不同解析度。自動化 PASS 不等於人工 PASS。

## 記錄方式

每項標註 PASS、FAIL、PARTIAL、BLOCKED、NOT RUN 或 MANUAL TEST REQUIRED，附 Unity 日誌／Test Runner XML 與簡要問題位置。發現路徑、GUID 或資料遺失風險時，先停下受影響搬移，從隔離副本的外部備份回復。

## 測試輸出位置

Editor 診斷工具與 PlayMode 截圖、文字、CSV、工具備份均經由 `SDGAdventure.Testing.ReportOutputPaths` 寫到 Unity 專案外。預設位置是「Unity 專案資料夾的同層」`<專案資料夾名稱>_TestOutputs`，例如隔離副本預設為 `SDG Adventure_M4B_Isolated_20261027_TestOutputs`；不用指定使用者帳號或固定磁碟。

要自訂位置，**啟動 Unity 前**設定環境變數 `SDG_ADVENTURE_TEST_OUTPUT_ROOT` 為絕對路徑。設定值若在任何 Unity 專案或備份內、是專案的上層，或使用相對路徑，工具會明確報錯，避免把產物寫回 Assets、專案根層或安全備份。Unity Test Runner 的批次 XML 與 Editor 日誌另以 `-testResults`、`-logFile` 指向相同的外部測試輸出資料夾。新一次測試可使用獨立子資料夾避免覆蓋舊證據。

歷史 `DevelopmentReports` 是外部封存，不是現行工具輸出目錄；執行後的 Unity 專案根層不應產生 `DevelopmentReports`。

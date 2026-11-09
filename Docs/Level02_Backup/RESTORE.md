# Level02 Git 備份與還原

此分支是 Level02 的**部分備份**，不是已驗證的完整 Unity 還原。Level02 原始素材的再散布權利尚未確認，因此模型、貼圖、材質、音效、Shader 和其他來源檔沒有放進 Git。Level04 已有的素材缺漏另見 `Docs/Level04_Backup/RESTORE.md`；Level01 也有既有排除。不要在缺件時把場景載入成功或 Git push 成功視為完整還原。

## 版本與內容

- Unity Editor：2023.1.22f1；Built-in Render Pipeline。
- 分支：`backup/level02-integrated-20261109`；本文件對應的備份 commit 可在 checkout 後以 `git log -1 -- Docs/Level02_Backup/RESTORE.md` 查詢，實際 checkout 版本以 `git rev-parse HEAD` 為準。基底 Level04 備份為 `613d196766c30b58bbef22fcb308b26a1bfad317`。
- 場景：`Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity`；其 GUID 為 `2e19d486964e4ddcbfd319027885ebe3`，與 Level04 場景不同。
- 套件及版本以 `Packages/manifest.json`、`Packages/packages-lock.json` 為準，包含 Input System 1.7.0、uGUI、TextMeshPro；母專案另含 AI Navigation。
- 此次保留第二關場景、關卡腳本及其 meta、共用玩家輸入／互動及 Headlamp Action、開發選關、Build Settings、任務所需的快捷欄顯示協調與 Level02 測試。
- 第三方 `Future Pad Door - FREE` 在來源專案有示範資產，但本次匯入場景的直接 GUID 引用未發現指向該素材；不得因此推定其再散布授權。

## 未納入的素材

`Local_Asset_Manifest.csv` 記錄本次保留在本機的 80 個來源及再散布授權待確認檔案，包括 `ONLYfactory.fbx`、星星箱及普通箱模型、工業門、平台、偵測器、工廠地板、貼圖、材質、Shader 與 `FactorySounds`。每列記錄路徑、原 GUID、SHA-256、大小、是否直接出現在場景 GUID 引用、來源專案相對路徑與內容比對結果；「非直接引用」不代表一定不需要，可能由材質、模型或 Resources 間接使用。

上述來源路徑只表示原專案的本機位置，不代表已取得再散布權；目前沒有可驗證的公開來源或版本資訊，請向原作者逐項確認。各組員須依適用條款合法取得素材。部分 `.meta` 另行納入 Git 以保留引用 GUID，並非授權證明。

## 乾淨還原順序

1. Clone 此儲存庫及備份分支，確認 commit；先不要以缺少素材的狀態開啟 Unity，以免編輯器重新處理孤立 meta。
2. 向關卡原作者確認每項來源、版本和再散布／團隊使用權；取得合法副本，依 CSV `Path` 放回，核對 SHA-256 和 `.meta` 中的 GUID。已提供的 meta 需與相應原始資產配對；若權利不允許提供某項，需由團隊決定合法替代及受控重綁，並重新驗證。
3. 依 Level04、Level01 的既有還原文件處理其缺件；不得用第二關素材覆蓋其他關卡。比對 GUID 唯一性與場景引用。
4. 以 Unity 2023.1.22f1 開啟，允許套件依 lock 檔還原，檢查 Console、Missing Script／Prefab／Material／Shader。進入 `Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity` 後選第二關。
5. 核對頭燈、互動鍵重綁、地圖與快捷欄、場景切換和既有關卡相容性；必要時執行 Level02 PlayMode 測試及受影響共用輸入回歸。

## 驗證界線

使用者回報原工作區的 Level02 人工與相關回歸驗收 53/53；本次備份不重新聲稱執行該測試。未在完整合法素材的乾淨 Clone 中驗證 Unity 開啟、編譯、缺失引用或遊玩。Level02 本次沒有需使用 LFS 的大型檔案；既有 Level04 LFS 缺件及其下載驗證不包含在本次結果。

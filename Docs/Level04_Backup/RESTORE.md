# Level04 私人備份還原說明

本分支是已驗收專案的部分 Git 備份，並非完整可開啟及遊玩的 Unity 專案。部分素材是否允許透過 GitHub 再散布尚未確認，因此沒有上傳；請勿把此 checkout 當成完整 Level04。本機完整備份另行保存。

## 版本與取得

- Unity Editor：2023.1.22f1；AI Navigation：1.1.7（見 Packages/manifest.json）。
- 本分支保留團隊關卡 C#、Level04 場景、必要共享程式與測試。
- Noto Sans TC Regular 使用來源附帶的 SIL OFL 1.1，字型與原 OFL.txt 和 README.txt 一起保留。
- Local_Asset_Manifest.csv 列出未納入 Git 的 Level04 資料夾、模型、Prefab、貼圖、材質、特效、NavMesh 與 .meta；GUID 及 SHA-256 供比對，不代表取得或再散布授權。清單某些資料可能由 Unity 重建，須逐項驗證。
- Level01 原有 .gitignore 排除 Nicrom、language 與 EmojiOne；這些本機素材也不在本次備份，且本次不改動第一關。

## 合法取得與還原

1. 請第四關原作者提供各素材的原供應者、套件版本及適用授權；每位組員依條款合法取得。不可從沒有權利提供的連結下載或再散布。尚未取得可驗證來源的項目見 Local_Asset_Manifest.csv；此文件不虛構下載網址。
2. 將各素材按清單中的相對路徑安裝至 Assets；必要時由原作者在其授權允許的範圍提供原始 .meta。先比對清單 GUID、內容與 SHA-256，並掃描 GUID 衝突。不得用任意新 GUID 假裝修復。若無法合法提供 .meta，需由團隊決定合法的 Unity 受控重綁流程並重新驗證場景。
3. 安裝可合法取得的原素材後，確認原作者產生的 NavMesh 及其 meta 是否允許保存；否則用同版本設定重新烘焙，並確認場景引用。
4. 以 Unity 2023.1.22f1 執行編譯、Missing Script/Prefab/Material/Shader 檢查，驗證 Level01、Level03、Level04 與跨場景武器、彈藥及背包。首次導入可能需要重新建立 Library。
5. 如取得許可且確需加入大型貼圖，.gitattributes 已設定 Plasma_SMG_Mat_Normal_OpenGL.png 的 LFS 規則；該檔目前未包含，也尚未驗證 LFS 物件上傳或下載。

## 已知原素材缺失

來源素材中雷電材質 b85bb668bf945fe47a123c232f435529、輪胎貼圖 642136540fef5884ea2f1140a6858b9b、液體 Cubemap 05d53dddd55afef4c884503fa332502c、吊車貼圖 cf83d416a473b404eaafec6e459c5a2c 及材質腳本 d0353a89b1f911e48b9e16bdc9f2e058 尚待原作者確認。不得以虛構 GUID 當作修復。

上傳完成與還原驗證是不同事項；截至本文件建立時，未在不含本機素材的乾淨 checkout 驗證可執行性。

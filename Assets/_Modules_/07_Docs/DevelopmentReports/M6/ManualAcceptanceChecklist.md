# M6 Unity 人工驗收清單

狀態：**待使用者與 ChatGPT 驗收；以下均未標 PASS。** 專案僅使用 `SDG Adventure_M6_TEST`，Unity 2023.1.22f1。

1. 在 Unity 開啟 `Assets/Scenes/M6_DevelopmentSelection.unity`，確認 Build Settings 第 0 場是此入口、第 1 場是 C3，Console 沒有編譯 Error。
2. Play，確認入口顯示「M6 開發驗證選關入口」與非正式 UI 說明；游標可見，可點「進入 C3_TreasurePuzzle」。
3. 載入 C3 後在 Hierarchy／Inspector 確認只有一個 C3 Player、Main Camera、AudioListener；無 Missing Script、Missing Prefab 或材質。檢查原 FBX、工廠佈局、控制台位置及碰撞未改。
4. 點選 Game 視窗，確認 WASD、滑鼠視角、Left Shift 跑、Left Ctrl 蹲、Space 跳；ESC 解鎖游標，點 Game 視窗可再鎖定。
5. 確認準星與 E 提示正常；按 P 開／關任務表。未收齊前按 E 操作控制台不會通關。
6. 分別尋找並以 E 取得保險絲、控制晶片、過濾模組；HUD 顯示 3/3，重複互動不增加件數。
7. 在控制台輸入錯誤順序並提交，確認不通關；重試後選「廢棄物分類 → 資源回收 → 污染處理」，確認解謎成功但尚未通關。
8. 關閉面板後再次對準控制台按 E，確認啟動設備；檢查螢幕轉綠、點光源、結算畫面、SDG 12 文案、3/3、提交次數與遊玩時間。此時 E、P、ESC、WASD、滑鼠不應讓角色操作或破壞結算。
9. 在結算畫面按「重新開始本關卡」，確認重載的是 C3；零件回到 0/3、謎題與設備未完成、結算畫面消失，游標與控制正常。
10. 再次完成 C3，按「返回 M6 開發驗證選關入口」；確認入口顯示最近通關結果，游標可見、時間比例為 1，沒有 C3 Player／Camera 遺留。
11. 再次由入口進入同一關，確認零件、謎題與完成通知不殘留；再次通關時僅回報一次。
12. 以 640×360 與一般螢幕尺寸檢查控制台、任務表與結算畫面文字、捲動和兩個按鈕；留意材質、光照、碰撞、音量及 Console Error。
13. 視需要啟動 `DevelopmentReports/M6/WindowsBuild/SDGAdventure_M6_TEST.exe` 重複入口 → C3 → 通關 → 返回流程；這是獨立執行檔的人工驗收，不由 Build 成功代替。

請分別記錄每步 PASS／FAIL、Unity Console 錯誤、截圖與環境。若發現問題，只在測試副本修復並重新驗證；正式母專案保持唯讀。

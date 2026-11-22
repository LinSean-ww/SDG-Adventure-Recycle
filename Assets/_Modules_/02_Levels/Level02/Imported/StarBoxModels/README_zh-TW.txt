Unity 2023.1.22f1 箱子模型
========================
NormalBox.obj：一般紙箱。
StarBox.obj：同尺寸紙箱，其中一面有黃色五角星。
每個模型為 1 x 1 x 1 單位，Y 軸朝上，原點位於底部中心。
每個模型 12 個三角形、24 個面頂點；四張共用 512 x 512 PNG 貼圖。
星星為單面貼圖，不是凸起物件。其他五面沒有星星。
這是依參考圖製作的簡化遊戲模型，不是參考圖片的自動立體重建。

匯入方式
1. 解壓縮，將完整 StarBoxModels 資料夾拖入 Unity Project 的 Assets。
   不要只拖入 OBJ；PNG、MTL 和 Editor 資料夾也要保留。
2. 等候 Unity 編譯完成。
3. 上方選單 Tools > Star Box > Create Box Prefabs。
4. 打開 StarBoxModels/Prefabs。
5. 將 NormalBox.prefab 或 StarBox.prefab 拖入 Hierarchy/Scene。
6. 放在地板頂面，Scale 保持 (1,1,1)。
7. 按 Play：預製物件已附 Box Collider 和 Rigidbody，會受重力影響。
   地板也需要 Collider。

支援 Built-in / URP。工具會依目前管線建立適合的材質。
未提供 HDRP 設定。若只使用 OBJ，可自行建立材質並指派同名貼圖。
工具再次執行會更新本套件的 Materials 與 Prefabs。
模型本身不包含第一人稱、拿取旋轉、隨機生成或過關計數程式。
NormalBox 與 StarBox 是不同預製物件，日後可由生成器配置 95 / 5 個。
目前完成網格、貼圖與檔案結構檢查，尚未在 Unity Editor 內實測。

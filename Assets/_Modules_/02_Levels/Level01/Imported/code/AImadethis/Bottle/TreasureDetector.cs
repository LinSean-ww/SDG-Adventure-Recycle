using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class TreasureDetector : MonoBehaviour
{
    [Header("UI 設定")]
    public TextMeshProUGUI distanceText; // 距離與任務進度提示框

    [Header("互動與浮起設定")]
    public float interactDistance = 2f; // 互動距離門檻 (預設2公尺)
    public float floatSpeed = 2f;         // 浮起速度
    public float targetHeight = 1.5f;     // 指定浮起高度 (Y=1.5)
    public float waitBeforeDestroy = 5.0f; // 浮起後等待消失的時間

    [Header("外部腳本連結 (請將掛有 scanball 的球體拖進來)")]
    public scanball scanBallScript;       // 用來同步掃描球的冷卻與觸發狀態

    [Header("順序寶物清單 (請依序將 1~10 個物件拖入)")]
    public List<Transform> targetBottles = new List<Transform>();

    [Header("紀錄設定")]
    private Vector3 recordedPosition;
    private List<float> distanceHistory = new List<float>(); // 儲存最多 3 筆距離紀錄
    private const int maxHistoryCount = 3;

    public int CollectedCount => currentIndex;
    public int TotalCount => targetBottles.Count;
    public event System.Action ProgressChanged;
    private int currentIndex = 0;          // 目前要探測的寶物索引 (0 代表第 1 個)
    private bool isAnimating = false;      // 是否正在進行浮起動畫
    public bool IsAnimating => isAnimating;

    void Start()
    {
        if (scanBallScript == null)
        {
            scanBallScript = FindFirstObjectByType<scanball>();
        }

        UpdateDistanceUI();
    }

    void Update()
    {
        // 1. 當按下 F 鍵時，嘗試挖起目前順序的物件
        if (Level01Input.CanUseTools && Level01Input.PrimaryPressed)
        {
            TryDigUpBottle();
        }

        // 2. 即時更新 UI 顯示
        UpdateDistanceUI();
    }

    // 提供給 scanball 查詢：根據當前已找完的數量，計算出爐的即時冷卻時間
    public float GetCurrentCooldown()
    {
        if (scanBallScript == null) return 10f;
        float baseCooldown = scanBallScript.baseCooldownTime;
        // 每找到一個減去初始秒數的 10% (currentIndex 代表已找到的數量)
        float reducedCooldown = baseCooldown - (baseCooldown * 0.10f * currentIndex);
        // 確保冷卻時間不會小於 0.5 秒
        return Mathf.Max(0.5f, reducedCooldown);
    }

    // 當 scanball 每次成功觸發掃描時，由 scanball 呼叫此方法來新增距離紀錄
    public void TriggerScanRecord()
    {
        recordedPosition = transform.position;
        RecordNewDistance();
    }

    // 計算並新增一筆距離紀錄到清單中（最多保留 3 筆）
    void RecordNewDistance()
    {
        if (currentIndex < targetBottles.Count && targetBottles[currentIndex] != null)
        {
            Transform currentBottle = targetBottles[currentIndex];
            Vector3 bottlePos = currentBottle.position;

            // 嚴格忽視 Y 軸，只計算 X 與 Z 平面距離
            float flatDistance = Mathf.Sqrt(Mathf.Pow(bottlePos.x - recordedPosition.x, 2) + Mathf.Pow(bottlePos.z - recordedPosition.z, 2));

            // 將新距離加入紀錄清單
            distanceHistory.Add(flatDistance);

            // 如果超過 3 筆，移除最舊的一筆（保持最多 3 筆）
            if (distanceHistory.Count > maxHistoryCount)
            {
                distanceHistory.RemoveAt(0);
            }
        }
    }

    // 嘗試挖起物件
    void TryDigUpBottle()
    {
        if (isAnimating) return;

        if (currentIndex >= targetBottles.Count)
        {
            Debug.Log("所有寶物都已經挖完了！");
            return;
        }

        Transform target = targetBottles[currentIndex];
        if (target == null) return;

        // 計算人物與當前目標在 X-Z 平面的實際距離
        Vector3 myPos = transform.position;
        Vector3 bottlePos = target.position;
        float flatDist = Mathf.Sqrt(Mathf.Pow(myPos.x - bottlePos.x, 2) + Mathf.Pow(myPos.z - bottlePos.z, 2));

        // 檢查距離是否小於 interactDistance (2公尺)
        if (flatDist > interactDistance)
        {
            Debug.Log($"【無法挖寶】距離太遠！XZ 平面實際距離為 {flatDist:F2}M (需小於 {interactDistance}M)");
            return;
        }

        // 啟動平滑浮起與計時消失協程
        StartCoroutine(FloatAndDisappearSequence(target));
    }

    // 協程：從地板慢慢浮起 -> 等待 5 秒 -> 消失 -> 計入進度
    IEnumerator FloatAndDisappearSequence(Transform targetBottle)
    {
        isAnimating = true;

        Vector3 startPos = targetBottle.position;
        Vector3 endPos = new Vector3(startPos.x, targetHeight, startPos.z);

        float journey = 0f;
        while (journey < 1f)
        {
            journey += Time.deltaTime * floatSpeed;
            targetBottle.position = Vector3.Lerp(startPos, endPos, journey);
            yield return null;
        }
        targetBottle.position = endPos;

        yield return new WaitForSeconds(waitBeforeDestroy);

        targetBottle.gameObject.SetActive(false);

        currentIndex++;
        ProgressChanged?.Invoke();
        distanceHistory.Clear(); // 清空舊距離，準備迎接下一個目標
        isAnimating = false;

        Debug.Log($"寶物已回收！目前進度：{currentIndex}/{targetBottles.Count}");
    }

    void UpdateDistanceUI()
    {
        if (distanceText == null) return;

        int totalCount = targetBottles.Count;
        string displayText = $"<b>回收進度: {currentIndex}/{totalCount}</b>\n";

        displayText += "<b>距離紀錄:</b>\n";

        if (currentIndex >= totalCount)
        {
            displayText += "已經找完\n去門口旁的機器互動";
        }
        else if (distanceHistory.Count == 0)
        {
            displayText += "尚未進行掃描 (" + Level01Input.Binding("ScanAim") + ")";
        }
        else
        {
            for (int i = 0; i < distanceHistory.Count; i++)
            {
                displayText += $"{i + 1}: {distanceHistory[i]:F2}M\n";
            }
            displayText += $"距離小於{interactDistance:0.#}M可按{Level01Input.Binding("Fire")}挖掘";
        }

        distanceText.text = displayText;
    }
    // 提供給外部（如 DoorInteraction）檢查是否所有寶物都已找完
    public bool IsAllTreasuresFound()
    {
        return currentIndex >= targetBottles.Count;
    }
}

using UnityEngine;
using TMPro;
using System.Collections;

public class scanball : MonoBehaviour
{
    [Header("大小設定")]
    public Vector3 smallScale = new Vector3(0.01f, 0.01f, 0.01f);
    public Vector3 largeScale = new Vector3(10f, 10f, 10f);
    public float scaleSpeed = 5f;

    [Header("透明度設定")]
    [Range(0f, 255f)]
    public float originalAlpha = 150f;
    public float fadeSpeed = 2f;

    [Header("冷卻時間設定")]
    public float baseCooldownTime = 10f; // 初始基準冷卻時間 (10秒)
    public TextMeshProUGUI cooldownText;

    [Header("挖寶偵測器連結")]
    public TreasureDetector treasureDetector; // 用來取得即時進度與動態冷卻時間

    private float lastUseTime = -10f;
    private bool isRunning = false;
    public bool IsRunning => isRunning;
    private Renderer sphereRenderer;

    void Start()
    {
        sphereRenderer = GetComponent<Renderer>();
        transform.localScale = smallScale;
        SetAlpha(originalAlpha / 255f);

        if (cooldownText != null)
        {
            cooldownText.text = "";
        }
    }

    void Update()
    {
        // 取得隨著進度減少後的動態冷卻時間
        float currentCooldown = GetCurrentCooldown();

        // 處理冷卻時間的 UI 顯示
        float timeSinceLastUse = Time.time - lastUseTime;
        if (timeSinceLastUse < currentCooldown)
        {
            float remainingTime = currentCooldown - timeSinceLastUse;
            if (cooldownText != null)
            {
                cooldownText.text = $"冷卻中:{remainingTime:F1}秒";
            }
        }
        else if (cooldownText != null && !isRunning)
        {
            cooldownText.text = "掃描冷卻完畢";
        }

        // 按下 Q 鍵觸發
        if (Level01Input.CanUseTools && Level01Input.SecondaryPressed)
        {
            if (isRunning) return;
            if (Time.time < lastUseTime + currentCooldown) return;

            StartCoroutine(PlayScanEffectSequence());
        }
    }

    // 向 TreasureDetector 詢問當前的動態冷卻時間
    float GetCurrentCooldown()
    {
        if (treasureDetector != null)
        {
            return treasureDetector.GetCurrentCooldown();
        }
        return baseCooldownTime;
    }

    IEnumerator PlayScanEffectSequence()
    {
        isRunning = true;
        if (cooldownText != null) cooldownText.text = "掃描中";

        // 同步通知偵測器記錄當前座標與距離
        if (treasureDetector != null)
        {
            treasureDetector.TriggerScanRecord();
        }

        // 1. 放大至設定的極限大小
        while (Vector3.Distance(transform.localScale, largeScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, largeScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }
        transform.localScale = largeScale;

        // 2. 顏色慢慢淡化
        Color color = sphereRenderer.material.color;
        while (color.a > 0.01f)
        {
            color.a -= Time.deltaTime * fadeSpeed;
            color.a = Mathf.Max(0f, color.a);
            SetAlpha(color.a);
            yield return null;
        }
        SetAlpha(0f);

        // 3. 縮小回 0.01
        while (Vector3.Distance(transform.localScale, smallScale) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, smallScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }
        transform.localScale = smallScale;

        // 4. 恢復原本透明度
        SetAlpha(originalAlpha / 255f);

        isRunning = false;
        lastUseTime = Time.time;
    }

    void SetAlpha(float alpha01)
    {
        if (sphereRenderer != null)
        {
            Color c = sphereRenderer.material.color;
            c.a = alpha01;
            sphereRenderer.material.color = c;
        }
    }
}

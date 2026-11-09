
using UnityEngine;
using UnityEngine.UI;

public class RecycleHouse : MonoBehaviour
{
    [Header("看板文字")]
    public Text questionText;

    [Header("通關畫面")]
    public GameObject winPanel;

    private int currentQuestion = 0;

    private string feedback = "";
    public int CompletedQuestions => currentQuestion;
    public int TotalQuestions => answers.Length;
    public string Feedback => feedback;
    public event System.Action ProgressChanged;

    private RecycleItemType[] answers =
    {
        RecycleItemType.Brick,
        RecycleItemType.Tire,
        RecycleItemType.GlassBottle,
        RecycleItemType.Disc,
        RecycleItemType.Motherboard
    };

    private string[] questions =
    {
        "哪一種廢棄材料經破碎處理後，可作為設備基座或地面鋪設材料？",
        "哪一種廢棄物經處理後，可製成再生橡膠材料？",
        "哪一種材料可經破碎與熔融後，再製成新的再生玻璃原料？",
        "哪一種物品含有可再利用的塑膠與金屬鍍膜材料？",
        "哪一種電子廢棄物中含有可回收的金屬與電路材料？"
    };

    void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        UpdateBoard();
    }

    void UpdateBoard()
    {
        if (questionText == null)
        {
            Debug.LogError("RecycleHouse：沒有指定 Question Text！");
            return;
        }

        if (currentQuestion >= questions.Length)
        {
            questionText.text =
                "五題全部完成！\n資源循環任務成功！";

            return;
        }

        questionText.text =
            "第 " + (currentQuestion + 1) + " / 5 題\n" +
            questions[currentQuestion] +
            "\n" + feedback;
    }


    public bool TrySubmit(RecycleItem item)
    {
        if (item == null || currentQuestion >= answers.Length)
            return false;

        Debug.Log("提交物品：" + item.itemType);

        Debug.Log("目前題目：" + (currentQuestion + 1));

        // 判斷答案是否正確
        bool isCorrect =
            item.itemType == answers[currentQuestion];

        if (isCorrect)
        {
            Debug.Log("答案正確！");

            // 正確物品回收，從場景移除
            Destroy(item.gameObject);

            feedback = "分類正確！回收成功！";
        }
        else
        {
            Debug.LogWarning("答案錯誤！物品退回地面。");

            // 不刪除物品，交給玩家控制程式放下
            feedback = "分類錯誤！請繼續下一題。";
        }

        // 不論答對或答錯，都進入下一題
        currentQuestion++;

        UpdateBoard();
        ProgressChanged?.Invoke();

        Debug.Log("目前完成題數：" + currentQuestion);

        // 五題全部完成
        if (currentQuestion >= answers.Length)
        {
            Debug.Log("恭喜完成任務!");

            if (winPanel != null)
            {
                winPanel.SetActive(true);
            }
        }

        // 回傳是否答對
        return isCorrect;
    }
}


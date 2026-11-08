using UnityEngine;
using System.Collections;

public class DoorInteraction : MonoBehaviour
{
    [Header("物件參照設定")]
    public TreasureDetector treasureDetector; // 拖曳掛載 TreasureDetector 的物件
    public Transform doorObject;              // 拖曳你的大門本體 (ABDoor)
    public Transform finalItem;               // 拖曳投入動畫用的電池物件 (Enrge)
    public Transform insertSlotTarget;        // 投遞進去時的起始位置（可選）

    [Header("面板切換設定")]
    public Transform powerLowPlane;           // 拖曳低電量平面 (powerlow)
    public Transform powerHighPlane;          // 拖曳高電量平面 (powerhigh)

    [Header("互動與開門設定")]
    public float interactDistance = 3.0f;     // 玩家與控制器的互動距離
    public float openDelay = 2.0f;            // 投入後的延遲開門時間 (2秒)
    public float openMoveDistance = 5.0f;     // 門向下移動的距離
    public float openSpeed = 2.0f;            // 門移動的速度

    [Header("電池投入動畫設定")]
    public float itemMoveDistance = 5.0f;     // 電池向下移動的距離
    public float itemMoveSpeed = 3.0f;        // 電池移動的速度

    private bool isOpenTriggered = false;     // 是否已經觸發過開門
    public bool IsBusy { get; private set; }
    public bool IsOpen { get; private set; }

    void Update()
    {
        // 當玩家按下 E 鍵時，嘗試與門禁控制器互動
        if (Level01Input.InteractPressed && !Level01Input.IsLookingAtGate())
        {
            TryInteractWithDoor();
        }
    }

    void TryInteractWithDoor()
    {
        if (isOpenTriggered) return;

        if (treasureDetector == null)
        {
            Debug.Log("【門禁系統】未連結 TreasureDetector！");
            return;
        }

        Transform player = treasureDetector.transform;
        float dist = Vector3.Distance(player.position, transform.position);

        if (!treasureDetector.IsAllTreasuresFound() && dist < interactDistance)
        {
            Debug.Log("【門禁系統】還沒找完所有寶物，無法啟動控制器！");
            return;
        }

        if (dist > interactDistance)
        {
            return;
        }

        StartCoroutine(InsertItemAndOpenDoorSequence());
    }

    IEnumerator InsertItemAndOpenDoorSequence()
    {
        isOpenTriggered = true;
        IsBusy = true;
        Debug.Log("【門禁系統】電力系統上線！");

        // 1. 互換 powerlow 與 powerhigh 的位置
        if (powerLowPlane != null && powerHighPlane != null)
        {
            Vector3 tempPos = powerLowPlane.position;
            powerLowPlane.position = powerHighPlane.position;
            powerHighPlane.position = tempPos;
            Debug.Log("【門禁系統】電力顯示已切換為高電量！");
        }

        // 2. 處理物品投入動畫：顯示電池並向下移動
        if (finalItem != null)
        {
            if (insertSlotTarget != null)
            {
                finalItem.position = insertSlotTarget.position;
            }

            // 將電池設為顯示
            finalItem.gameObject.SetActive(true);

            // 執行電池向下移動的動畫
            yield return StartCoroutine(MoveItemDownRoutine(finalItem));

            // 移動完畢後隱藏電池
            finalItem.gameObject.SetActive(false);
        }

        Debug.Log($"【門禁系統】物品已投入，將在 {openDelay} 秒後開啟大門...");
        yield return new WaitForSeconds(openDelay);

        // 3. 開啟大門
        if (doorObject != null)
        {
            yield return StartCoroutine(MoveDoorDownRoutine());
            IsOpen = true;
        }
        else
        {
            Debug.LogWarning("【門禁系統】未指定大門物件 (ABDoor)！");
        }
        IsBusy = false;
    }

    // 讓電池平滑向下移動指定距離的協程
    IEnumerator MoveItemDownRoutine(Transform itemTransform)
    {
        Vector3 startPos = itemTransform.position;
        Vector3 endPos = startPos - new Vector3(0, itemMoveDistance, 0);

        float journey = 0f;
        while (journey < 1f)
        {
            journey += Time.deltaTime * itemMoveSpeed;
            itemTransform.position = Vector3.Lerp(startPos, endPos, journey);
            yield return null;
        }
        itemTransform.position = endPos;
    }

    IEnumerator MoveDoorDownRoutine()
    {
        Vector3 startPos = doorObject.position;
        Vector3 endPos = startPos - new Vector3(0, openMoveDistance, 0);

        float journey = 0f;
        while (journey < 1f)
        {
            journey += Time.deltaTime * openSpeed;
            doorObject.position = Vector3.Lerp(startPos, endPos, journey); // 修正為小寫的 position
            yield return null;
        }
        doorObject.position = endPos;
        Debug.Log("【門禁系統】大門已完全開啟！");
    }
}

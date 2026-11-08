using UnityEngine;

public class FenceDoor : MonoBehaviour
{
    [Header("互動設定")]
    public float interactionRange = 3f;     // 可以互動的最大距離
    public KeyCode interactKey = KeyCode.E; // 互動按鍵（預設為 E）

    [Header("狀態")]
    public bool FenceisInteracted = false;       // 記錄是否已經被互動過

    private Transform playerCamera;         // 玩家攝影機的位置
    private bool DoorOpen = false;

    public Transform RDF;
    public Transform LDF;

    void Start()
    {
        if(RDF == null)
        {
            Debug.Log("lossRDF");
        }

        if (LDF == null)
        {
            Debug.Log("lossLDF");
        }

        //reset touch
        FenceisInteracted = false;

        DoorOpen = false;

        // 自動尋找場景中的主攝影機
        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        if (playerCamera == null) return;

        // 1. 計算攝影機與這個物件之間的距離
        float distance = Vector3.Distance(transform.position, playerCamera.position);

        // 2. 檢查是否小於等於互動距離
        if (distance <= interactionRange)
        {
            // 3. 從攝影機正中央發射一條射線，檢查視線是否有對準它
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit hit;

            // 發射射線（長度為 interactionRange）
            // 如果射線打到的目標剛好是「這個物件本身」
            // 4. 檢查玩家是否有按下指定的互動按鍵（例如 E）
            if (Physics.Raycast(ray, out hit, interactionRange) && 
                hit.transform == transform && 
                Input.GetKeyDown(interactKey))
            {
                FenceisInteracted = true; // 將值設為 true，其他物件可以來讀取
                Debug.Log("成功與Door互動！");
                // 這裡可以寫你想要觸發的事件

                if (DoorOpen == false)
                {
                    Debug.Log("dick");
                    for (int i = 0;i <= 60; i++)
                    {
                        Debug.Log("do" + i + "times");
                        RDF.localEulerAngles = new Vector3(-90, i , 0);
                        LDF.localEulerAngles = new Vector3(-90, -i , 0);
                        DoorOpen = true;
                    }
                }
                else if (DoorOpen == true)
                {
                    for (int i = 60; i >= 0; i--)
                    {
                        Debug.Log("do" + i + "times");
                        RDF.localEulerAngles = new Vector3(-90, i , 0);
                        LDF.localEulerAngles = new Vector3(-90, -i , 0);
                        DoorOpen = false;
                    }
                }
            }
            else
            {
                FenceisInteracted = false;
            }
        }
    }
}
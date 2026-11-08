using UnityEngine;

using System.Collections; // 必須引入這行才能使用協程 (Coroutine)



public class NewDoor : MonoBehaviour

{

    [Header("互動設定")]

    public float interactionRange = 3f;      // 可以互動的最大距離

    public KeyCode interactKey = KeyCode.E;  // 互動按鍵（預設為 E）



    [Header("狀態")]

    public bool FenceisInteracted = false;        // 記錄是否已經被互動過



    private Transform playerCamera;          // 玩家攝影機的位置

    private bool DoorOpen = false;

    private bool isMoving = false;           // 用來判斷門是否正在轉動中（避免重複觸發）
    public bool IsMoving => isMoving;



    public Transform RDF;

    public Transform LDF;



    // 設定關閉與開啟時的角度

    private Vector3 closedRDFRot = new Vector3(-90, 0, 0);

    private Vector3 openRDFRot = new Vector3(-90, 60, 0);



    private Vector3 closedLDFRot = new Vector3(-90, 0, 0);

    private Vector3 openLDFRot = new Vector3(-90, -60, 0);



    public float rotationSpeed = 5f;         // 門旋轉的速度



    void Start()

    {

        if (RDF == null) Debug.Log("lossRDF");

        if (LDF == null) Debug.Log("lossLDF");



        FenceisInteracted = false;



        if (Camera.main != null)

        {

            playerCamera = Camera.main.transform;

        }

    }



    void Update()

    {

        if (playerCamera == null) return;



        float distance = Vector3.Distance(transform.position, playerCamera.position);



        if (distance <= interactionRange)

        {

            Ray ray = new Ray(playerCamera.position, playerCamera.forward);

            RaycastHit hit;



            if (Physics.Raycast(ray, out hit, interactionRange) &&

                hit.transform == transform &&

                Level01Input.InteractPressed)

            {

                FenceisInteracted = true;

                Debug.Log("成功與Door互動！");



                // 如果門目前沒有在移動，才執行開關門動作

                if (!isMoving)

                {

                    if (DoorOpen == false)

                    {

                        // 啟動開門協程

                        StartCoroutine(RotateDoors(openRDFRot, openLDFRot));

                        DoorOpen = true;

                    }

                    else

                    {

                        // 啟動關門協程

                        StartCoroutine(RotateDoors(closedRDFRot, closedLDFRot));

                        DoorOpen = false;

                    }

                }

            }

        }

        else

        {

            FenceisInteracted = false;

        }

    }



    // 建立一個協程來處理平滑旋轉

    IEnumerator RotateDoors(Vector3 targetRDFRot, Vector3 targetLDFRot)

    {

        isMoving = true; // 標記為正在移動



        Quaternion startRDF = RDF.localRotation;

        Quaternion startLDF = LDF.localRotation;



        Quaternion endRDF = Quaternion.Euler(targetRDFRot);

        Quaternion endLDF = Quaternion.Euler(targetLDFRot);



        float elapsedTime = 0f;

        float duration = 1f; // 轉動過程花費的時間（秒）



        while (elapsedTime < duration)

        {

            elapsedTime += Time.deltaTime;

            float t = elapsedTime / duration;



            // 使用 Slerp 進行平滑差值旋轉

            RDF.localRotation = Quaternion.Slerp(startRDF, endRDF, t);

            LDF.localRotation = Quaternion.Slerp(startLDF, endLDF, t);



            yield return null; // 等待下一個影格

        }



        // 確保最後精準對齊目標角度

        RDF.localRotation = endRDF;

        LDF.localRotation = endLDF;



        isMoving = false; // 移動結束

    }

}

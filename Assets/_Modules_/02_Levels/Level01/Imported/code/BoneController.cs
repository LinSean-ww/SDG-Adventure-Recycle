using UnityEngine;

public class BoneController : MonoBehaviour
{
    // 宣告要控制的骨骼 Transform
    private Transform LHTB;
    private Transform RHTB;
    private Transform LLTB;
    private Transform RLTB;

    private PlayerMove playerMove;

    void Start()
    {
        // 根據你的階層結構（參考圖片：BODYBONE -> downbone -> topbone -> LHB -> LtopHand）
        // 逐層尋找對應的骨骼物件
        LHTB = transform.Find("BODYBONE/downbone/topbone/LtopHand");
        RHTB = transform.Find("BODYBONE/downbone/topbone/RtopHand");
        LLTB = transform.Find("BODYBONE/Ltopleg");
        RLTB = transform.Find("BODYBONE/Rtopleg");

        if (LHTB == null)
        {
            Debug.LogError("找不到LHTB，請檢查路徑是否正確！");
        }
        if (RHTB == null)
        {
            Debug.LogError("找不到RHTB，請檢查路徑是否正確！");
        }
        if (LLTB == null)
        {
            Debug.LogError("找不到LLTB，請檢查路徑是否正確！");
        }
        if (RLTB == null)
        {
            Debug.LogError("找不到RLTB，請檢查路徑是否正確！");
        }

        playerMove = GetComponent<PlayerMove>();
    }

    void Update()
    {
        
    }
    void LateUpdate()
    {
        bool Wkeep = Input.GetKey(KeyCode.W);
        bool Akeep = Input.GetKey(KeyCode.A);
        bool Skeep = Input.GetKey(KeyCode.S);
        bool Dkeep = Input.GetKey(KeyCode.D);
        bool Run = Input.GetKey(KeyCode.LeftShift);
        float codrun = playerMove.run;

        if (Input.GetKey(KeyCode.LeftShift) == true)
        {
            codrun = playerMove.run;
        }
        else
        {
            codrun = 1f;
        }


        if (Wkeep == false && Akeep == false && Skeep == false && Dkeep == false)
        {
            if (LHTB != null)
            {
                // 範例：使用 Sin 函數讓手臂在本地軸向產生左右擺動的效果
                float angle = Mathf.Sin(Time.time * 2.5f) * 10f; // 擺動頻率與幅度

                // 修改骨骼的區域旋轉角度 (Local Euler Angles)
                LHTB.localEulerAngles = new Vector3(0, angle - 170, 0);
            }
            if (RHTB != null)
            {
                // 範例：使用 Sin 函數讓手臂在本地軸向產生左右擺動的效果
                float angle = Mathf.Sin(Time.time * 2.5f) * 10f; // 擺動頻率與幅度

                // 修改骨骼的區域旋轉角度 (Local Euler Angles)
                RHTB.localEulerAngles = new Vector3(0, -angle + 170, 0);
            }
            if (LLTB != null)
            {
                // reset feet
                LLTB.localEulerAngles = new Vector3(90, 0, -180);
            }
            if (RLTB != null)
            {
                // reset feet
                RLTB.localEulerAngles = new Vector3(90, 0, -180);
            }
        }
        else if(Wkeep == true || Akeep == true || Skeep == true || Dkeep == true)
        {
            if (LHTB != null)
            {
                //使用 Sin 函數讓手臂在本地軸向產生前後擺動的效果
                float angle = Mathf.Sin(Time.time * 11f * codrun) * 45f; //擺動頻率與幅度

                // 修改骨骼的區域旋轉角度 (Local Euler Angles)
                LHTB.localEulerAngles = new Vector3(angle - 180, 0, 0);
            }
            if (RHTB != null)
            {
                //使用 Sin 函數讓手臂在本地軸向產生前後擺動的效果
                float angle = Mathf.Sin(Time.time * 11f * codrun) * 45f; //擺動頻率與幅度

                // 修改骨骼的區域旋轉角度 (Local Euler Angles)
                RHTB.localEulerAngles = new Vector3(-angle - 180, 0, 0);
            }
            if (LLTB != null)
            {
                //使用 Sin 函數讓手臂在本地軸向產生前後擺動的效果
                float angle = Mathf.Sin(Time.time * 11f * codrun) * 45f; //擺動頻率與幅度

                //修改骨骼的區域旋轉角度 (Local Euler Angles)
                LLTB.localEulerAngles = new Vector3(-angle + 90, 0, -180);
            }
            if (RLTB != null)
            {
                //使用 Sin 函數讓手臂在本地軸向產生前後擺動的效果
                float angle = Mathf.Sin(Time.time * 11f * codrun) * 45f; //擺動頻率與幅度

                //修改骨骼的區域旋轉角度 (Local Euler Angles)
                RLTB.localEulerAngles = new Vector3(angle + 90, 0, -180);
            }
        }
    }
}
using UnityEngine;
public class FPSLimiter : MonoBehaviour
{
    void Start()
    {
        // 将最大帧率限制为 60 帧
        Application.targetFrameRate = 60;
        // 若要不限制帧率可设为 -1
    }
}

using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("視角設定")]
    public float mouseSensitivity = 2.0f;
    private float cameraPitch = 0.0f;

    // 目標角色（用來跟著左右轉身）
    public Transform playerBody;
    public Transform Head;

    void Start()
    {
        // 鎖定游標
        Cursor.lockState = CursorLockMode.Locked;

        if (Head == null)
        {
            Debug.LogError("找不到Head，請檢查路徑是否正確！");
        }
    }

    void Update()
    {
        // 取得滑鼠數值
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 1. 上下旋轉攝影機（抬頭 60 度 [-60]，低頭 48 度 [48]）
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -60f, 48f);
        transform.localRotation = Quaternion.Euler(cameraPitch, 0, 0);

        // 2. 左右旋轉角色本體
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
        if (Head != null)
        {
            Vector3 headEuler = Head.localEulerAngles;
            Head.localEulerAngles = new Vector3(cameraPitch, headEuler.y, headEuler.z);
        }
    }
}
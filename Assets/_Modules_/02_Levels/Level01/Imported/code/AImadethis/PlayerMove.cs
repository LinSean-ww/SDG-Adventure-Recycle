using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;

    [Header("移動設定")]
    public float playerSpeed = 5.0f;
    public float jumpHeight = 1.5f;
    public float gravityValue = -23.0f;
    public float run = 2f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float codrun = 1;
        //跑步狀態
        
        if (Input.GetKey(KeyCode.LeftShift) == true)
        {
            codrun = run;
        }
        else
        {
            codrun = 1f;
        }

        //貼地偵測
        if (controller.isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f;
        }

        //WASD 移動
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        //跳躍
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravityValue);
        }

        //重力計算
        playerVelocity.y += gravityValue * Time.deltaTime;

        //執行移動
        Vector3 totalMove = move * (playerSpeed * codrun) + new Vector3(0, playerVelocity.y, 0);
        controller.Move(totalMove * Time.deltaTime);
    }
}
using UnityEngine;

namespace C3TreasurePuzzle
{
    // 原始 BoneController 以 WASD 判斷動作，碰到牆仍會播放走路，且固定 Euler 角會覆蓋 FBX 原始姿勢。
    // 此版本只控制 fk 的四肢骨骼，以 CharacterController 的實際水平速度切換待機與走路。
    public sealed class C3BoneController : MonoBehaviour
    {
        [SerializeField] private float idleArmAngle = 4f;
        [SerializeField] private float walkArmAngle = 28f;
        [SerializeField] private float walkLegAngle = 30f;
        [SerializeField] private float walkSpeed = 10f;
        [SerializeField] private float blendSpeed = 7f;
        [SerializeField] private float movingThreshold = 0.15f;

        private CharacterController motor;
        private Transform leftArm, rightArm, leftLeg, rightLeg;
        private Quaternion leftArmRest, rightArmRest, leftLegRest, rightLegRest;
        private float walkWeight;
        private float phase;
        private float airborneBlend;

        public bool HasRequiredBones => leftArm && rightArm && leftLeg && rightLeg;

        private void Awake()
        {
            motor = GetComponentInParent<CharacterController>();
            if (!TryResolveBones())
            {
                Debug.LogError("C3BoneController: fk 四肢骨骼路徑不完整。", this);
                enabled = false;
            }
            else if (!motor)
            {
                Debug.LogError("C3BoneController: 找不到 C3 玩家 CharacterController。", this);
                enabled = false;
            }
        }

        public bool TryResolveBones()
        {
            leftArm = transform.Find("BODYBONE/downbone/topbone/LtopHand");
            rightArm = transform.Find("BODYBONE/downbone/topbone/RtopHand");
            leftLeg = transform.Find("BODYBONE/Ltopleg");
            rightLeg = transform.Find("BODYBONE/Rtopleg");
            if (!HasRequiredBones) return false;
            leftArmRest = leftArm.localRotation;
            rightArmRest = rightArm.localRotation;
            leftLegRest = leftLeg.localRotation;
            rightLegRest = rightLeg.localRotation;
            return true;
        }

        private void LateUpdate()
        {
            if (!motor || !HasRequiredBones) return;
            Vector3 velocity = motor.velocity;
            velocity.y = 0f;
            float planarSpeed = velocity.magnitude;
            float targetWeight = planarSpeed > movingThreshold ? 1f : 0f;
            walkWeight = Mathf.MoveTowards(walkWeight, targetWeight, blendSpeed * Time.deltaTime);
            if (walkWeight > 0f) phase += Time.deltaTime * walkSpeed;

            float idle = Mathf.Sin(Time.time * 2.5f) * idleArmAngle * (1f - walkWeight);
            bool airborne = !motor.isGrounded;
            airborneBlend = Mathf.MoveTowards(airborneBlend, airborne ? 1f : 0f,
                Time.deltaTime * 6f);
            float stride = Mathf.Sin(phase) * walkWeight * (1f - airborneBlend);
            float jumpLegBend = airborneBlend * 14f;
            // 所有旋轉都相對於匯入模型的初始 localRotation，避免把骨骼硬設為固定 Euler 角。
            leftArm.localRotation = leftArmRest * Quaternion.Euler(stride * walkArmAngle + idle, 0f, 0f);
            rightArm.localRotation = rightArmRest * Quaternion.Euler(-stride * walkArmAngle - idle, 0f, 0f);
            leftLeg.localRotation = leftLegRest * Quaternion.Euler(-stride * walkLegAngle + jumpLegBend, 0f, 0f);
            rightLeg.localRotation = rightLegRest * Quaternion.Euler(stride * walkLegAngle + jumpLegBend, 0f, 0f);
        }
    }
}

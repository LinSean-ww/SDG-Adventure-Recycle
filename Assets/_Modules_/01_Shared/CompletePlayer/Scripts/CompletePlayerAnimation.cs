using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.CompletePlayer
{
    // Animates the original FK skeleton without changing the imported FBX or camera rig.
    public sealed class CompletePlayerAnimation : MonoBehaviour
    {
        [SerializeField] private float movingThreshold = 0.15f;
        [SerializeField] private float walkStride = 26f;
        [SerializeField] private float runStride = 39f;
        [SerializeField] private float idleArmSway = 3f;
        [SerializeField] private float blendRate = 7f;

        private CharacterController motor;
        private SharedFirstPersonController player;
        private Transform leftArm, rightArm, leftLeg, rightLeg;
        private Quaternion leftArmRest, rightArmRest, leftLegRest, rightLegRest;
        private float strideWeight, phase, airborneBlend;

        public bool HasRequiredBones => leftArm && rightArm && leftLeg && rightLeg;
        public float StrideWeight => strideWeight;

        private void Awake()
        {
            motor = GetComponentInParent<CharacterController>();
            player = GetComponentInParent<SharedFirstPersonController>();
            if (!motor || !player || !TryResolveBones())
            {
                Debug.LogError("CompletePlayerAnimation requires the SharedPlayer motor and FK arm/leg bones.", this);
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
            if (!HasRequiredBones || !motor) return;
            Vector3 velocity = motor.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float target = speed > movingThreshold ? 1f : 0f;
            strideWeight = Mathf.MoveTowards(strideWeight, target, blendRate * Time.deltaTime);
            if (strideWeight > 0f) phase += Time.deltaTime * Mathf.Lerp(7f, 13f, Mathf.InverseLerp(2f, 7f, speed));
            float angle = Mathf.Lerp(walkStride, runStride, Mathf.InverseLerp(4f, 7f, speed));
            bool airborne = !player.IsGrounded;
            airborneBlend = Mathf.MoveTowards(airborneBlend, airborne ? 1f : 0f,
                Time.deltaTime * 6f);
            float swing = Mathf.Sin(phase) * angle * strideWeight * (1f - airborneBlend);
            float jumpLegBend = airborneBlend * 14f;
            float idle = Mathf.Sin(Time.time * 2.4f) * idleArmSway * (1f - strideWeight);
            leftArm.localRotation = leftArmRest * Quaternion.Euler(swing + idle, 0f, 0f);
            rightArm.localRotation = rightArmRest * Quaternion.Euler(-swing - idle, 0f, 0f);
            leftLeg.localRotation = leftLegRest * Quaternion.Euler(-swing + jumpLegBend, 0f, 0f);
            rightLeg.localRotation = rightLegRest * Quaternion.Euler(swing + jumpLegBend, 0f, 0f);
        }
    }
}

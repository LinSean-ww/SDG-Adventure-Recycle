using UnityEngine;
using UnityEngine.InputSystem;

namespace SDGAdventure.SharedPlayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SharedFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform viewTransform;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.1f;
        [SerializeField] private float standingEyeHeight = 1.5f;
        [SerializeField] private float crouchingEyeHeight = 0.85f;
        [SerializeField] private float stanceSpeed = 3f;
        [SerializeField] private float crouchingStepOffset = 0.05f;
        [SerializeField] private float maxLookAngle = 80f;
        [SerializeField] private float degreesPerMousePixel = 0.08f;

        private readonly Collider[] clearanceHits = new Collider[32];
        private CharacterController motor;
        private PlayerInputService input;
        private float verticalSpeed;
        private Vector3 externalHorizontalVelocity;
        private float externalControlLock;
        private bool externalImpulseActive;
        public bool IsExternallyDisplaced => externalImpulseActive;

        public void ApplyExternalImpulse(Vector3 velocity, float controlLockSeconds)
        {
            externalHorizontalVelocity = Vector3.ProjectOnPlane(velocity, transform.up);
            verticalSpeed = Mathf.Max(0f, Vector3.Dot(velocity, transform.up));
            externalControlLock = Mathf.Max(0f, controlLockSeconds);
            externalImpulseActive = true;
            jumpQueued = false;
        }

        public void ClearExternalImpulse()
        {
            externalHorizontalVelocity = Vector3.zero;
            externalControlLock = 0f;
            externalImpulseActive = false;
            verticalSpeed = 0f;
            jumpQueued = false;
        }
        private float pitch;
        private float standingStepOffset;
        private bool jumpQueued;
        [Header("Eco FPS additions")]
        [SerializeField] private float proneHeight = 0.75f;
        [SerializeField] private float proneEyeHeight = 0.48f;
        [SerializeField] private float proneSpeed = 1.05f;
        [SerializeField] private float leanOffset = 0.21f;
        [SerializeField] private float leanDegrees = 13f;
        private bool prone;
        private float lean;
        private PlayerPerspectiveRig perspective;

        public bool IsCrouching { get; private set; }
        public bool IsProne => prone;
        public float CurrentSpeed { get; private set; }
        public float CurrentHeight => motor == null ? 0f : motor.height;
        public float StandingHeight => standingHeight;
        public bool IsGrounded => motor != null &&
            (motor.isGrounded || (verticalSpeed <= 0f && ProbeGround()));

        public void SetViewTransform(Transform value) => viewTransform = value;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            standingStepOffset = motor.stepOffset;
            if (viewTransform == null)
            {
                Camera child = GetComponentInChildren<Camera>();
                if (child != null) viewTransform = child.transform;
            }
            standingHeight = Mathf.Max(standingHeight, motor.radius * 2f);
            crouchingHeight = Mathf.Clamp(crouchingHeight, motor.radius * 2f, standingHeight);
            motor.height = standingHeight;
            motor.center = Vector3.up * (standingHeight * 0.5f);
            if (viewTransform != null)
            {
                Vector3 eye = viewTransform.localPosition;
                eye.y = standingEyeHeight;
                viewTransform.localPosition = eye;
                pitch = Mathf.DeltaAngle(0f, viewTransform.localEulerAngles.x);
            }
            perspective = GetComponent<PlayerPerspectiveRig>();
            if (perspective == null) perspective = gameObject.AddComponent<PlayerPerspectiveRig>();
            perspective.Bind(viewTransform);
        }

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input != null) { input.JumpPressed += QueueJump; input.PronePressed += ToggleProne; }
        }

        private void OnDisable()
        {
            if (input != null) { input.JumpPressed -= QueueJump; input.PronePressed -= ToggleProne; }
            jumpQueued = false;
        }

        private void QueueJump() => jumpQueued = true;

        private void Update()
        {
            if (input == null || input.Context != PlayerInputContext.Gameplay || viewTransform == null)
            {
                jumpQueued = false;
                return;
            }

            if (PlayerCursor.LockState == CursorLockMode.Locked &&
                !(GetComponent<PlayerInteractor>()?.SuppressLook ?? false))
            {
                Vector2 look = input.Look * (degreesPerMousePixel * input.Keybindings.Settings.mouseSensitivity);
                if (perspective != null && perspective.IsThirdPerson)
                {
                    perspective.Orbit(look.x, look.y, -maxLookAngle, maxLookAngle);
                    pitch = perspective.OrbitPitch;
                }
                else
                {
                    transform.Rotate(0f, look.x, 0f);
                    pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
                    viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
            }

            bool acceptsInput = PlayerCursor.LockState == CursorLockMode.Locked;
            Vector2 movement = acceptsInput ? input.Move : Vector2.zero;
            TickMotor(movement, acceptsInput && input.SprintActive, acceptsInput && input.CrouchActive,
                acceptsInput && jumpQueued, Time.deltaTime);
            jumpQueued = false;
        }

        // Public so level-specific movement rules and deterministic tests can share the same motor step.
        public void TickMotor(Vector2 movement, bool sprint, bool crouch, bool jump, float deltaTime)
        {
            if (deltaTime <= 0f || motor == null) return;
            bool grounded = IsGrounded;
            if (externalImpulseActive)
            {
                externalControlLock = Mathf.Max(0f, externalControlLock - deltaTime);
                movement = Vector2.zero;
                jump = false;
                if (verticalSpeed > 0f) grounded = false;
            }
            bool blockedFromStanding = !prone && !crouch &&
                motor.height < standingHeight - 0.01f && !CanStand();
            float target = prone ? Mathf.Clamp(proneHeight, motor.radius * 2f, crouchingHeight) :
                blockedFromStanding ? Mathf.Min(motor.height, crouchingHeight) :
                crouch ? crouchingHeight : standingHeight;
            float height = Mathf.MoveTowards(motor.height, target, stanceSpeed * deltaTime);
            if (Mathf.Abs(height - motor.height) > 0.0001f)
            {
                motor.height = height;
                motor.center = Vector3.up * (height * 0.5f);
            }
            IsCrouching = crouch || height < standingHeight - 0.01f;
            float stepOffset = IsCrouching ? Mathf.Min(crouchingStepOffset, height - motor.radius * 2f)
                : standingStepOffset;
            stepOffset = Mathf.Max(0f, stepOffset);
            if (Mathf.Abs(motor.stepOffset - stepOffset) > 0.0001f)
                motor.stepOffset = stepOffset;
            if (viewTransform != null)
            {
                float fraction = Mathf.InverseLerp(crouchingHeight, standingHeight, height);
                Vector3 eye = viewTransform.localPosition;
                eye.y = height < crouchingHeight
                    ? Mathf.Lerp(proneEyeHeight, crouchingEyeHeight, Mathf.InverseLerp(Mathf.Clamp(proneHeight, motor.radius * 2f, crouchingHeight), crouchingHeight, height))
                    : Mathf.Lerp(crouchingEyeHeight, standingEyeHeight, fraction);
                if (perspective != null) perspective.SetEyeHeight(eye.y);
                if (perspective == null || !perspective.IsThirdPerson)
                    viewTransform.localPosition = eye;
            }

            movement = Vector2.ClampMagnitude(movement, 1f);
            CurrentSpeed = prone ? proneSpeed :
                IsCrouching ? crouchSpeed : sprint ? runSpeed : walkSpeed;
            Vector3 travel = perspective != null && perspective.IsThirdPerson
                ? perspective.GetThirdPersonTravel(movement)
                : (transform.right * movement.x + transform.forward * movement.y);
            if (perspective != null) perspective.FaceTravel(travel, deltaTime);
            Vector3 horizontal = travel * CurrentSpeed + externalHorizontalVelocity;
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jump && !IsCrouching && grounded)
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalSpeed -= gravity * deltaTime;
            CollisionFlags collision = motor.Move((horizontal + Vector3.up * verticalSpeed) * deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
                if (externalImpulseActive && externalControlLock <= 0f)
                { externalImpulseActive = false; externalHorizontalVelocity = Vector3.zero; }
            }
        }

        private void ToggleProne()
        {
            if (PlayerCursor.LockState != CursorLockMode.Locked) return;
            if (!prone) prone = true;
            else if (CanStand()) prone = false;
        }

        private void LateUpdate()
        {
            if (viewTransform == null) return;
            if (perspective != null && perspective.IsThirdPerson) { lean = 0f; return; }
            Keyboard keyboard = Keyboard.current;
            bool allowed = keyboard != null && input != null &&
                input.Context == PlayerInputContext.Gameplay &&
                PlayerCursor.LockState == CursorLockMode.Locked;
            float target = 0f;
            if (allowed)
            {
                if (input.LeanLeftActive) target = -1f;
                if (input.LeanRightActive) target = 1f;
            }
            if (target != 0f)
            {
                Collider[] results = Physics.OverlapSphere(
                    viewTransform.position + transform.right * (target * leanOffset),
                    0.10f, ~0, QueryTriggerInteraction.Ignore);
                foreach (Collider col in results)
                    if (!col.transform.IsChildOf(transform))
                    {
                        target = 0f;
                        break;
                    }
            }
            lean = Mathf.MoveTowards(lean, target, Time.deltaTime * 5.5f);
            Vector3 position = viewTransform.localPosition;
            position.x = lean * leanOffset;
            viewTransform.localPosition = position;
            viewTransform.localRotation = Quaternion.Euler(pitch, 0f, -lean * leanDegrees);
        }

        public bool CanStand()
        {
            if (motor == null) return false;
            if (motor.height >= standingHeight - 0.01f) return true;
            float radius = Mathf.Max(0.01f, motor.radius - motor.skinWidth);
            Vector3 up = transform.up;
            Vector3 lower = transform.position + up * (motor.height - radius);
            Vector3 upper = transform.position + up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, clearanceHits,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (clearanceHits[i] != motor && !clearanceHits[i].transform.IsChildOf(transform))
                    return false;
            return true;
        }

        private bool ProbeGround()
        {
            float radius = Mathf.Max(0.05f, motor.radius - motor.skinWidth);
            Vector3 origin = transform.position + transform.up * (motor.radius + 0.05f);
            if (!Physics.SphereCast(origin, radius, -transform.up, out RaycastHit hit,
                    motor.skinWidth + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider != motor &&
                Vector3.Dot(hit.normal, transform.up) >= Mathf.Cos(motor.slopeLimit * Mathf.Deg2Rad);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            ClearExternalImpulse();
            motor.enabled = true;
        }
    }
}

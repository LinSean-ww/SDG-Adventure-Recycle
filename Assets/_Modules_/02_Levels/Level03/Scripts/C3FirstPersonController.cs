using UnityEngine;
using UnityEngine.InputSystem;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class C3FirstPersonController : MonoBehaviour
    {
        [Header("視角")]
        [SerializeField] private Transform viewTransform;
        [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2f;
        [SerializeField] private Transform headBone;
        [SerializeField] private float minLookAngle = -60f;
        [SerializeField] private float maxLookAngle = 48f;

        [Header("移動")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField] private bool allowJump = true;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;

        [Header("蹲下")]
        [SerializeField, Min(0.6f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.6f)] private float crouchHeight = 1.1f;
        [SerializeField, Min(0.1f)] private float standingEyeHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float crouchEyeHeight = 0.85f;
        [SerializeField, Min(0.1f)] private float stanceSpeed = 3f;

        private CharacterController characterController;
        private float verticalSpeed;
        private float lookPitch;
        private float currentMoveSpeed;
        private bool isCrouching;
        [Header("新 FPS 操作")]
        [SerializeField] private float proneHeight = 0.75f;
        [SerializeField] private float proneEyeHeight = 0.48f;
        [SerializeField] private float proneSpeed = 1.05f;
        [SerializeField] private float leanOffset = 0.21f;
        [SerializeField] private float leanAngle = 13f;
        private bool isProne;
        private float leanAmount;
        private Quaternion headRestLocalRotation;
        private readonly Collider[] clearanceHits = new Collider[32];
        private PlayerInputService sharedInput;
        private bool jumpQueued;
        private PlayerPerspectiveRig perspective;

        public void SetViewTransform(Transform value) => viewTransform = value;
        public void UseSharedInput(PlayerInputService service)
        {
            if (sharedInput != null) sharedInput.JumpPressed -= QueueJump;
            sharedInput = service;
            if (isActiveAndEnabled && sharedInput != null) sharedInput.JumpPressed += QueueJump;
        }

        private void OnEnable()
        {
            if (sharedInput != null) sharedInput.JumpPressed += QueueJump;
        }

        private void QueueJump() => jumpQueued = true;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            // F8 diagnostics identify the actual collider blocking the production aisle.
            if (!GetComponent<C3MovementCollisionProbe>())
                gameObject.AddComponent<C3MovementCollisionProbe>();
            if (viewTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) viewTransform = childCamera.transform;
            }
            standingHeight = Mathf.Max(standingHeight, characterController.radius * 2f);
            crouchHeight = Mathf.Clamp(crouchHeight, characterController.radius * 2f, standingHeight);
            characterController.height = standingHeight;
            characterController.center = Vector3.up * (standingHeight * 0.5f);
            if (viewTransform != null)
                viewTransform.localPosition = new Vector3(viewTransform.localPosition.x, standingEyeHeight, viewTransform.localPosition.z);
            if (viewTransform != null) lookPitch = Mathf.DeltaAngle(0f, viewTransform.localEulerAngles.x);
            if (headBone == null)
                headBone = transform.Find("fk/BODYBONE/downbone/topbone/head");
            if (headBone != null) headRestLocalRotation = headBone.localRotation;
            // C3 starts in first-person FPS; avoid the unanimated T-pose
            // third-person avatar covering the sorting machines on entry.
            PlayerViewPreference.Mode = PlayerViewMode.FirstPerson;
            perspective = GetComponent<PlayerPerspectiveRig>();
            if (perspective == null) perspective = gameObject.AddComponent<PlayerPerspectiveRig>();
            C3WorkerAppearance.Install(gameObject);
            perspective.Bind(viewTransform);
        }

        private void Start()
        {
            // 遊戲開始先鎖定滑鼠；ESC 放開，點擊遊戲畫面可重新鎖定。
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (sharedInput != null)
            {
                if (sharedInput.Context != PlayerInputContext.Gameplay ||
                    C3PuzzleConsole.BlocksGameplayInputThisFrame || C3CompletionScreen.BlocksGameplayInputThisFrame)
                {
                    jumpQueued = false;
                    return;
                }
                bool acceptsSharedInput = Cursor.lockState == CursorLockMode.Locked && viewTransform != null;
                if (acceptsSharedInput) ReadProneKey();
                if (acceptsSharedInput)
                    ApplyLook(sharedInput.Look * (0.08f * sharedInput.Keybindings.Settings.mouseSensitivity));
                TickMovement(acceptsSharedInput ? sharedInput.Move : Vector2.zero,
                    acceptsSharedInput && sharedInput.SprintActive,
                    acceptsSharedInput && sharedInput.CrouchActive,
                    acceptsSharedInput && jumpQueued, Time.deltaTime);
                jumpQueued = false;
                return;
            }
            if (C3PuzzleConsole.BlocksGameplayInputThisFrame || C3CompletionScreen.BlocksGameplayInputThisFrame)
            {
                TickMovement(Vector2.zero, false, false, false, Time.deltaTime);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 解鎖滑鼠時停用水平與視角輸入，但重力仍持續作用。
            bool acceptsInput = Cursor.lockState == CursorLockMode.Locked && viewTransform != null;
            if (acceptsInput) ReadProneKey();
            if (acceptsInput)
                ApplyLook(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));

            Vector2 input = acceptsInput
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
                : Vector2.zero;
            TickMovement(input,
                acceptsInput && Input.GetKey(KeyCode.LeftShift),
                acceptsInput && Input.GetKey(KeyCode.LeftControl),
                acceptsInput && Input.GetKeyDown(KeyCode.Space),
                Time.deltaTime);
        }

        private void ApplyLook(Vector2 mouseDelta)
        {
            // 沿用 CameraLook 的靈敏度與非對稱俯仰限制；玩家根物件是唯一的左右轉向來源。
            if (perspective != null && perspective.IsThirdPerson)
            {
                perspective.Orbit(mouseDelta.x * mouseSensitivity, mouseDelta.y * mouseSensitivity,
                    minLookAngle, maxLookAngle);
                lookPitch = perspective.OrbitPitch;
            }
            else
            {
                transform.Rotate(0f, mouseDelta.x * mouseSensitivity, 0f);
                lookPitch = Mathf.Clamp(lookPitch - mouseDelta.y * mouseSensitivity,
                    minLookAngle, maxLookAngle);
                viewTransform.localRotation = Quaternion.Euler(lookPitch, 0f, 0f);
            }
        }

        private void LateUpdate()
        {
            UpdateLean();
            if (headBone == null || headBone.parent == null) return;
            if (perspective != null && perspective.IsThirdPerson)
            {
                headBone.rotation = headBone.parent.rotation * headRestLocalRotation;
                return;
            }
            // head 在已旋轉的 fk 骨架內；以玩家世界右軸施加一次俯仰，保留 FBX 的原始 localRotation。
            // Camera 不掛在 head 下，避免骨骼動畫再帶動 Camera 造成抖動或重複旋轉。
            headBone.rotation = Quaternion.AngleAxis(lookPitch, transform.right) *
                (headBone.parent.rotation * headRestLocalRotation);
        }

        // Input is collected in Update; keeping the movement step separate also permits
        // PlayMode tests to exercise the real controller without synthetic OS key events.
        private void TickMovement(Vector2 input, bool sprintHeld, bool crouchHeld, bool jumpPressed, float deltaTime)
        {
            bool blockedFromStanding = !isProne && !crouchHeld &&
                characterController.height < standingHeight - 0.01f && !CanStand();
            float targetHeight = isProne
                ? Mathf.Clamp(proneHeight, characterController.radius * 2f, crouchHeight)
                : crouchHeld || blockedFromStanding ? crouchHeight : standingHeight;
            float height = Mathf.MoveTowards(characterController.height, targetHeight, stanceSpeed * deltaTime);
            characterController.height = height;
            characterController.center = Vector3.up * (height * 0.5f);
            isCrouching = crouchHeld || height < standingHeight - 0.01f;
            if (viewTransform != null)
            {
                float eye = isProne ? proneEyeHeight : Mathf.Lerp(crouchEyeHeight, standingEyeHeight,
                    Mathf.InverseLerp(crouchHeight, standingHeight, height));
                Vector3 position = viewTransform.localPosition;
                if (perspective != null) perspective.SetEyeHeight(eye);
                if (perspective == null || !perspective.IsThirdPerson)
                {
                    position.y = Mathf.MoveTowards(position.y, eye, stanceSpeed * deltaTime);
                    viewTransform.localPosition = position;
                }
            }

            input = Vector2.ClampMagnitude(input, 1f);
            currentMoveSpeed = isProne ? proneSpeed : isCrouching ? crouchSpeed : sprintHeld ? runSpeed : moveSpeed;
            Vector3 travel = perspective != null && perspective.IsThirdPerson
                ? perspective.GetThirdPersonTravel(input)
                : (transform.right * input.x + transform.forward * input.y);
            if (perspective != null) perspective.FaceTravel(travel, deltaTime);
            Vector3 horizontal = travel * currentMoveSpeed;

            // Keep gravity active even when the cursor is unlocked.
            if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jumpPressed && allowJump && !isCrouching && characterController.isGrounded)
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalSpeed -= gravity * deltaTime;

            CollisionFlags collision = characterController.Move((horizontal + Vector3.up * verticalSpeed) * deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        }

        private void ReadProneKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.zKey.wasPressedThisFrame) return;
            if (isProne)
            {
                if (CanStand()) isProne = false;
                // Remain prone if the standing clearance capsule is blocked.
            }
            else
            {
                isProne = true;
            }
        }

        private void UpdateLean()
        {
            if (viewTransform == null) return;
            if (perspective != null && perspective.IsThirdPerson) { leanAmount = 0f; return; }
            Keyboard keyboard = Keyboard.current;
            bool enabledInput = keyboard != null &&
                Cursor.lockState == CursorLockMode.Locked &&
                (sharedInput == null || sharedInput.Context == PlayerInputContext.Gameplay) &&
                !C3PuzzleConsole.BlocksGameplayInputThisFrame &&
                !C3CompletionScreen.BlocksGameplayInputThisFrame;
            float target = 0f;
            if (enabledInput)
            {
                if (keyboard.qKey.isPressed) target = -1f;
                if (keyboard.eKey.isPressed) target = 1f;
            }
            if (target != 0f)
            {
                Vector3 candidate = viewTransform.position + transform.right * (target * leanOffset);
                Collider[] hits = Physics.OverlapSphere(candidate, 0.10f,
                    ~0, QueryTriggerInteraction.Ignore);
                foreach (Collider hit in hits)
                    if (!hit.transform.IsChildOf(transform))
                    {
                        target = 0f; // Do not lean into walls or machinery.
                        break;
                    }
            }
            leanAmount = Mathf.MoveTowards(leanAmount, target, Time.deltaTime * 5.5f);
            Vector3 local = viewTransform.localPosition;
            local.x = leanAmount * leanOffset;
            viewTransform.localPosition = local;
            viewTransform.localRotation = Quaternion.Euler(lookPitch, 0f, -leanAmount * leanAngle);
        }

        private bool CanStand()
        {
            float radius = characterController.radius;
            Vector3 up = transform.up;
            Vector3 bottom = transform.position + up * radius;
            Vector3 top = transform.position + up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, clearanceHits,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (clearanceHits[i] != characterController &&
                    !clearanceHits[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private void OnDisable()
        {
            if (sharedInput != null) sharedInput.JumpPressed -= QueueJump;
            jumpQueued = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

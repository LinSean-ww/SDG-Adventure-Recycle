using UnityEngine;
using UnityEngine.Rendering;

namespace SDGAdventure.SharedPlayer
{
    public enum PlayerViewMode { FirstPerson = 0, ThirdPerson = 1 }

    // Intentionally separate from keybinding JSON/checksum: existing saved controls are preserved.
    public static class PlayerViewPreference
    {
        public const string Key = "SDGAdventure.Player.ViewMode.v1";
        public static PlayerViewMode Mode
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1 ? PlayerViewMode.ThirdPerson : PlayerViewMode.FirstPerson;
            set
            {
                PlayerPrefs.SetInt(Key, value == PlayerViewMode.ThirdPerson ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }

    // Camera only: no additional Camera or CharacterController is created.
    // Execution order puts collision correction after controllers and procedural animation.
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class PlayerPerspectiveRig : MonoBehaviour
    {
        [Header("Third-person camera")]
        [SerializeField, Range(1f, 6f)] private float followDistance = 3.1f;
        [SerializeField, Range(0f, 1.2f)] private float shoulderOffset = 0.42f;
        [SerializeField, Range(0.08f, 0.5f)] private float collisionRadius = 0.23f;
        [SerializeField, Min(0f)] private float smoothTime = 0.07f;
        [SerializeField, Min(0f)] private float turnSpeed = 580f;
        [SerializeField] private LayerMask cameraCollisionLayers = ~0;

        private Transform view;
        private Camera cam;
        private CharacterController capsule;
        private Renderer[] bodyRenderers;
        private ShadowCastingMode[] originalCast;
        private bool[] originalEnabled;
        private PlayerViewMode appliedMode = (PlayerViewMode)(-1);
        private float orbitYaw;
        private float orbitPitch;
        private float eyeHeight = 1.5f;
        private Vector3 originalEyeOffset;
        private Vector3 followVelocity;
        private readonly RaycastHit[] collisionHits = new RaycastHit[48];
        private bool collisionLastFrame;
        private bool snapNext;

        public bool IsThirdPerson => PlayerViewPreference.Mode == PlayerViewMode.ThirdPerson;
        public float TurnSpeed => turnSpeed;
        public Vector3 CameraFlatForward => Quaternion.Euler(0f, orbitYaw, 0f) * Vector3.forward;
        public Vector3 CameraFlatRight => Quaternion.Euler(0f, orbitYaw, 0f) * Vector3.right;
        public float OrbitPitch => orbitPitch;

        public void Bind(Transform cameraTransform)
        {
            if (cameraTransform == null) return;
            if (view == cameraTransform) return;
            view = cameraTransform;
            cam = view.GetComponent<Camera>();
            capsule = GetComponent<CharacterController>();
            originalEyeOffset = view.localPosition;
            eyeHeight = originalEyeOffset.y;
            orbitYaw = transform.eulerAngles.y;
            orbitPitch = Mathf.DeltaAngle(0f, view.localEulerAngles.x);
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            originalCast = new ShadowCastingMode[bodyRenderers.Length];
            originalEnabled = new bool[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                originalCast[i] = bodyRenderers[i].shadowCastingMode;
                originalEnabled[i] = bodyRenderers[i].enabled;
            }
            appliedMode = (PlayerViewMode)(-1);
            ApplyMode();
        }

        public void SetEyeHeight(float height) => eyeHeight = height;

        // Visual replacements must update the saved baseline as well as the live
        // renderer, otherwise the next view toggle reapplies a stale snapshot.
        // This never rebinds/moves the camera or changes player pose/preferences.
        public void SetBodyRendererBaseline(Renderer renderer, bool enabledValue, ShadowCastingMode casting)
        {
            if (!renderer) return;
            if (bodyRenderers != null)
            {
                int index = System.Array.IndexOf(bodyRenderers, renderer);
                if (index < 0)
                {
                    index = bodyRenderers.Length;
                    System.Array.Resize(ref bodyRenderers, index + 1);
                    System.Array.Resize(ref originalEnabled, index + 1);
                    System.Array.Resize(ref originalCast, index + 1);
                    bodyRenderers[index] = renderer;
                }
                originalEnabled[index] = enabledValue;
                originalCast[index] = casting;
            }
            renderer.enabled = enabledValue;
            renderer.shadowCastingMode = view == null ? casting : IsThirdPerson
                ? casting == ShadowCastingMode.ShadowsOnly ? ShadowCastingMode.On : casting
                : ShadowCastingMode.ShadowsOnly;
        }

        // Drop only the outgoing visual subtree. Keep all surviving baselines;
        // repeated add/remove cycles must not grow a cache of dead renderers.
        public void RemoveBodyRenderers(Transform visualRoot)
        {
            if (!visualRoot || bodyRenderers == null) return;
            int kept = 0;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer renderer = bodyRenderers[i];
                if (!renderer || renderer.transform.IsChildOf(visualRoot)) continue;
                bodyRenderers[kept] = renderer;
                originalEnabled[kept] = originalEnabled[i];
                originalCast[kept++] = originalCast[i];
            }
            System.Array.Resize(ref bodyRenderers, kept);
            System.Array.Resize(ref originalEnabled, kept);
            System.Array.Resize(ref originalCast, kept);
        }

        public void Orbit(float yawDelta, float pitchDelta, float minPitch, float maxPitch)
        {
            if (!IsThirdPerson) return;
            orbitYaw = Mathf.Repeat(orbitYaw + yawDelta, 360f);
            orbitPitch = Mathf.Clamp(orbitPitch - pitchDelta, minPitch, maxPitch);
        }

        // Character faces actual travel direction in third-person, not the free-look cursor.
        public Vector3 GetThirdPersonTravel(Vector2 movement)
        {
            return Vector3.ClampMagnitude(CameraFlatRight * movement.x +
                                          CameraFlatForward * movement.y, 1f);
        }

        public void FaceTravel(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (!IsThirdPerson || direction.sqrMagnitude < 0.0004f) return;
            Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                desired, turnSpeed * dt);
        }

        public void ApplyMode()
        {
            if (view == null) return;
            PlayerViewMode mode = PlayerViewPreference.Mode;
            if (mode == appliedMode) return;
            if (mode == PlayerViewMode.ThirdPerson)
            {
                orbitYaw = transform.eulerAngles.y;
                orbitPitch = Mathf.DeltaAngle(0f, view.localEulerAngles.x);
                followVelocity = Vector3.zero;
                collisionLastFrame = false;
                snapNext = true;
            }
            else
            {
                // Restore a valid local first-person pose without moving the CharacterController.
                transform.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
                view.localPosition = new Vector3(originalEyeOffset.x, eyeHeight, originalEyeOffset.z);
                view.localRotation = Quaternion.Euler(orbitPitch, 0f, 0f);
                followVelocity = Vector3.zero;
            }
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer renderer = bodyRenderers[i];
                if (renderer == null) continue;
                renderer.enabled = originalEnabled[i];
                renderer.shadowCastingMode = mode == PlayerViewMode.FirstPerson
                    ? ShadowCastingMode.ShadowsOnly
                    : originalCast[i] == ShadowCastingMode.ShadowsOnly
                        ? ShadowCastingMode.On : originalCast[i];
            }
            appliedMode = mode;
        }

        private void LateUpdate()
        {
            ApplyMode();
            if (!IsThirdPerson || view == null) return;
            Quaternion aim = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 focus = transform.position + Vector3.up * eyeHeight;
            Vector3 shoulder = aim * Vector3.right * shoulderOffset;
            Vector3 target = focus + shoulder - aim * Vector3.forward * followDistance;
            Vector3 segment = target - focus;
            float distance = segment.magnitude;
            bool blocked = false;
            if (distance > 0.001f)
            {
                int count = Physics.SphereCastNonAlloc(focus, collisionRadius,
                    segment / distance, collisionHits, distance, cameraCollisionLayers,
                    QueryTriggerInteraction.Ignore);
                float nearest = distance;
                for (int i = 0; i < count; i++)
                {
                    Collider collider = collisionHits[i].collider;
                    if (!collider || collider.transform.IsChildOf(transform)) continue;
                    if (collisionHits[i].distance < nearest)
                    {
                        blocked = true;
                        nearest = collisionHits[i].distance;
                    }
                }
                if (blocked) target = focus + segment.normalized * Mathf.Max(0.05f, nearest - 0.08f);
            }
            // Never smooth through walls when the target is suddenly occluded.
            view.position = snapNext || blocked || collisionLastFrame || smoothTime <= 0f
                ? target
                : Vector3.SmoothDamp(view.position, target, ref followVelocity,
                    smoothTime, Mathf.Infinity, Time.deltaTime);
            if (blocked) followVelocity = Vector3.zero;
            collisionLastFrame = blocked;
            snapNext = false;
            view.rotation = aim;
        }
    }
}

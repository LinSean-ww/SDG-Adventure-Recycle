using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SDGAdventure.SharedPlayer
{
    [DefaultExecutionOrder(350)]
    public sealed class Level02BoxCarryController : MonoBehaviour, IPlayerInteractionExtension
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactionDistance = 4.5f;
        [SerializeField, Min(0.5f)] private float boxPickupDistance = 3.5f;
        [SerializeField] private LayerMask raycastLayers = ~0;
        [SerializeField, Min(0.5f)] private float boxHoldDistance = 1.3f;
        [SerializeField, Min(0.05f)] private float boxRotationDegreesPerPixel = 0.45f;
        [SerializeField, Min(1f)] private float boxCarrySpeed = 12f;
        [SerializeField, Min(90f)] private float boxTurnSpeed = 1200f;
        [SerializeField, Min(0.5f)] private float maxBoxCarryDistance = 2.6f;
        [SerializeField, Min(0f)] private float pickupDistanceGrace = 0.35f;
        [SerializeField, Min(0f)] private float boxObstructionBreakDelay = 0.1f;

        private static readonly Vector2[] AimOffsets =
        {
            Vector2.zero, new Vector2(0f, 0.06f), new Vector2(-0.06f, 0f),
            new Vector2(0.06f, 0f), new Vector2(0f, -0.06f)
        };

        private readonly Collider[] dropHits = new Collider[64];
        private readonly RaycastHit[] carryCastHits = new RaycastHit[64];
        private readonly List<IgnoredBoxPair> ignoredBoxPairs = new List<IgnoredBoxPair>();
        private readonly HashSet<Collider> otherBoxColliders = new HashSet<Collider>();

        private struct IgnoredBoxPair
        {
            public Collider carried;
            public Collider other;
            public bool previouslyIgnored;
        }
        private PlayerInputService input;
        private PlayerHudCanvas hud;
        private IPlayerInteractable target;
        private Rigidbody boxTarget;
        private Rigidbody heldBox;
        private Quaternion heldRotationOffset;
        private Vector3 heldCenterLocal;
        private Vector3 heldDesiredPosition;
        private Quaternion heldDesiredRotation;
        private BoxCollider heldShape;
        private Collider[] heldColliders;
        private bool[] previousPlayerCollisionIgnored;
        private CharacterController playerCollider;
        private bool previousKinematic;
        private bool previousGravity;
        private bool previousDetectCollisions;
        private RigidbodyInterpolation previousInterpolation;
        private CollisionDetectionMode previousCollisionMode;
        private AudioClip boxDropSound;
        private float pickedUpAt;
        private float blockedByBoxFor;
        private bool escapedBoxPile;
        private Vector3 pickupPosition;
        private Quaternion pickupRotation;

        public string Prompt { get; private set; }
        public IPlayerInteractable CurrentTarget => target;
        public Rigidbody HeldBox => heldBox;
        public bool SuppressLook => IsRotatingHeldBox;
        public bool IsRotatingHeldBox => heldBox != null && input != null &&
            input.Context == PlayerInputContext.Gameplay &&
            PlayerCursor.LockState == CursorLockMode.Locked &&
            Mouse.current != null && Mouse.current.rightButton.isPressed;
        public void SetCamera(Camera value) => playerCamera = value;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            playerCollider = GetComponent<CharacterController>();
            hud = GetComponent<PlayerHudCanvas>();
            if (hud == null) hud = gameObject.AddComponent<PlayerHudCanvas>();
            hud.Build();
            boxDropSound = Resources.Load<AudioClip>("FactorySounds/BoxDrop");
        }

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input != null) GetComponent<PlayerInteractor>().Extension = this;
        }

        private void OnDisable()
        {
            var sharedInteractor = GetComponent<PlayerInteractor>();
            if (sharedInteractor != null && ReferenceEquals(sharedInteractor.Extension, this)) sharedInteractor.Extension = null;
            ReleaseHeldBox(false);
            target = null;
            boxTarget = null;
        }

        private void Update()
        {
            RefreshTarget();
            string key = input != null && input.Keybindings != null
                ? input.Keybindings.BindingDisplay("Gameplay", "Interact", 0) : "F";
            string prompt = heldBox != null ? "放下箱子  |  按住右鍵移動滑鼠旋轉" : boxTarget != null ? "拿起箱子" :
                target != null ? target.Prompt : "";
            Prompt = string.IsNullOrEmpty(prompt) ? "" : prompt + "  [" + key + "]";
        }

        private void LateUpdate()
        {
            if (heldBox == null || playerCamera == null) return;
            if (IsRotatingHeldBox)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * boxRotationDegreesPerPixel;
                heldRotationOffset = Quaternion.AngleAxis(delta.x, Vector3.up) *
                    Quaternion.AngleAxis(-delta.y, Vector3.right) * heldRotationOffset;
            }
            heldDesiredRotation = GetViewRotation() * heldRotationOffset;
            heldDesiredPosition = GetHoldPosition(heldDesiredRotation);
        }

        private void FixedUpdate()
        {
            if (heldBox == null) return;
            Vector3 playerCenter = transform.position + Vector3.up;
            if (Time.time - pickedUpAt >= pickupDistanceGrace)
            {
                if ((ShapeCenter(heldBox.position, heldBox.rotation) - playerCenter).sqrMagnitude >
                    maxBoxCarryDistance * maxBoxCarryDistance)
                {
                    ReleaseHeldBox(true);
                    return;
                }
                blockedByBoxFor = BoxBlocksCarry(playerCenter)
                    ? blockedByBoxFor + Time.fixedDeltaTime : 0f;
                if (blockedByBoxFor >= boxObstructionBreakDelay)
                {
                    ReleaseHeldBox(true);
                    return;
                }
            }
            MoveHeldBox();
        }

        private bool BoxBlocksCarry(Vector3 playerCenter)
        {
            Vector3 towardBox = ShapeCenter(heldBox.position, heldBox.rotation) - playerCenter;
            float distance = towardBox.magnitude;
            if (distance < 0.1f) return false;
            int count = Physics.RaycastNonAlloc(playerCenter, towardBox / distance,
                carryCastHits, distance - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = carryCastHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(heldBox.transform)) continue;
                Rigidbody body = hit.attachedRigidbody;
                if (body == null) body = hit.GetComponentInParent<Rigidbody>();
                if (IsBoxBody(body)) return true;
            }
            return count == carryCastHits.Length;
        }

        private void RefreshTarget()
        {
            target = null;
            boxTarget = null;
            if (heldBox != null || input == null || input.Context != PlayerInputContext.Gameplay || playerCamera == null ||
                PlayerCursor.LockState != CursorLockMode.Locked) return;
            FindAimTargets(out target, out boxTarget);
        }

        public IPlayerInteractable FindTarget()
        {
            FindAimTargets(out IPlayerInteractable found, out _);
            return found;
        }

        private void FindAimTargets(out IPlayerInteractable found, out Rigidbody box)
        {
            found = null;
            box = null;
            if (playerCamera == null) return;
            PlayerPerspectiveRig rig = GetComponent<PlayerPerspectiveRig>();
            bool third = rig != null && rig.IsThirdPerson;
            float maxRayDistance = third ? interactionDistance + 4.5f : interactionDistance;
            for (int aim = 0; aim < AimOffsets.Length; aim++)
            {
                Vector2 offset = AimOffsets[aim];
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(
                    0.5f + offset.x, 0.5f + offset.y, 0f));
                RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, raycastLayers,
                    QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    if (third && Vector3.Distance(transform.position + Vector3.up, hit.point) >
                        interactionDistance) break;
                    Rigidbody body = hit.collider.attachedRigidbody;
                    if (body == null) body = hit.collider.GetComponentInParent<Rigidbody>();
                    if (IsPickableBox(body))
                    {
                        float boxReach = third
                            ? Vector3.Distance(transform.position + Vector3.up, hit.point)
                            : hit.distance;
                        if (boxReach <= boxPickupDistance) { box = body; return; }
                        break;
                    }
                    if (aim == 0)
                    {
                        MonoBehaviour[] components = hit.collider.GetComponentsInParent<MonoBehaviour>();
                        foreach (MonoBehaviour component in components)
                            if (component is IPlayerInteractable candidate && component.isActiveAndEnabled &&
                                candidate.CanInteract(gameObject)) { found = candidate; return; }
                    }
                    break; // This ray is blocked by a wall or another object.
                }
            }
        }

        private static bool IsPickableBox(Rigidbody body)
        {
            return body != null && !body.isKinematic && IsBoxBody(body);
        }

        private static bool IsBoxBody(Rigidbody body)
        {
            if (body == null) return false;
            // Unity's imported OBJ can have its named root above the Rigidbody,
            // or a named mesh below it. Check both sides of the physics object.
            for (Transform parent = body.transform; parent != null; parent = parent.parent)
                if (IsBoxName(parent.name)) return true;
            foreach (Transform child in body.GetComponentsInChildren<Transform>(true))
                if (IsBoxName(child.name)) return true;
            foreach (MeshFilter mesh in body.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.sharedMesh != null && IsBoxName(mesh.sharedMesh.name)) return true;
            return false;
        }

        private static bool IsBoxName(string name)
        {
            return name == "StarBox" || name.StartsWith("StarBox (", StringComparison.Ordinal) ||
                   name == "NormalBox" || name.StartsWith("NormalBox (", StringComparison.Ordinal);
        }

        private void OnInteractPressed() => TryInteract();

        public bool TryInteract()
        {
            if (input == null || input.Context != PlayerInputContext.Gameplay ||
                PlayerCursor.LockState != CursorLockMode.Locked) return false;
            if (heldBox != null) { TryDropBox(); return true; }
            RefreshTarget();
            if (boxTarget != null) return TryPickUpBox(boxTarget);
            return false;
        }

        private bool TryPickUpBox(Rigidbody body)
        {
            if (!IsPickableBox(body)) return false;
            Collider[] colliders = body.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return false;
            heldBox = body;
            Quaternion initialRotation = body.rotation;
            heldRotationOffset = Quaternion.Inverse(GetViewRotation()) * initialRotation;
            heldCenterLocal = Vector3.up * 0.5f;
            foreach (Collider collider in colliders)
                if (collider.enabled && !collider.isTrigger)
                {
                    heldCenterLocal = body.transform.InverseTransformPoint(collider.bounds.center);
                    if (collider is BoxCollider box) heldShape = box;
                    break;
                }
            if (heldShape == null)
            {
                heldBox = null;
                return false;
            }
            heldColliders = colliders;
            previousPlayerCollisionIgnored = new bool[colliders.Length];
            pickupPosition = body.position;
            pickupRotation = body.rotation;
            previousKinematic = body.isKinematic;
            previousGravity = body.useGravity;
            previousDetectCollisions = body.detectCollisions;
            previousInterpolation = body.interpolation;
            previousCollisionMode = body.collisionDetectionMode;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (playerCollider == null || colliders[i] == null) continue;
                previousPlayerCollisionIgnored[i] = Physics.GetIgnoreCollision(
                    playerCollider, colliders[i]);
                Physics.IgnoreCollision(playerCollider, colliders[i], true);
            }
            IgnoreOtherBoxContacts();
            escapedBoxPile = IsClearOfOtherBoxes(body.position, body.rotation);
            if (escapedBoxPile) RestoreOtherBoxContacts();
            heldDesiredRotation = initialRotation;
            heldDesiredPosition = GetHoldPosition(initialRotation);
            pickedUpAt = Time.time;
            blockedByBoxFor = 0f;
            target = null;
            boxTarget = null;
            return true;
        }

        private Quaternion GetViewRotation()
        {
            return Quaternion.LookRotation(-playerCamera.transform.forward, playerCamera.transform.up);
        }

        private Vector3 GetHoldPosition(Quaternion rotation)
        {
            PlayerPerspectiveRig rig = GetComponent<PlayerPerspectiveRig>();
            bool third = rig != null && rig.IsThirdPerson;
            float distance = third ? Mathf.Max(boxHoldDistance,
                Vector3.Distance(playerCamera.transform.position,
                    transform.position + Vector3.up * 1.25f) + 1.1f) : boxHoldDistance;
            Vector3 center = playerCamera.transform.position +
                playerCamera.transform.forward * distance;
            Vector3 localOffset = Vector3.Scale(heldCenterLocal, heldBox.transform.lossyScale);
            return center - rotation * localOffset;
        }

        private bool TryDropBox()
        {
            ReleaseHeldBox(true);
            return true;
        }

        private void IgnoreOtherBoxContacts()
        {
            ignoredBoxPairs.Clear();
            otherBoxColliders.Clear();
            foreach (Rigidbody otherBody in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (otherBody == heldBox || !IsPickableBox(otherBody)) continue;
                foreach (Collider other in otherBody.GetComponentsInChildren<Collider>())
                {
                    if (other == null || !other.enabled || other.isTrigger) continue;
                    otherBoxColliders.Add(other);
                    foreach (Collider carried in heldColliders)
                    {
                        if (carried == null || !carried.enabled || carried.isTrigger) continue;
                        bool wasIgnored = Physics.GetIgnoreCollision(carried, other);
                        ignoredBoxPairs.Add(new IgnoredBoxPair
                        {
                            carried = carried,
                            other = other,
                            previouslyIgnored = wasIgnored
                        });
                        if (!wasIgnored) Physics.IgnoreCollision(carried, other, true);
                    }
                }
            }
        }

        private void RestoreOtherBoxContacts()
        {
            foreach (IgnoredBoxPair pair in ignoredBoxPairs)
                if (pair.carried != null && pair.other != null)
                    Physics.IgnoreCollision(pair.carried, pair.other, pair.previouslyIgnored);
            ignoredBoxPairs.Clear();
            otherBoxColliders.Clear();
        }

        private void MoveHeldBox()
        {
            Rigidbody body = heldBox;
            Quaternion rotation = Quaternion.RotateTowards(body.rotation, heldDesiredRotation,
                boxTurnSpeed * Time.fixedDeltaTime);
            Vector3 position = Vector3.MoveTowards(body.position, heldDesiredPosition,
                boxCarrySpeed * Time.fixedDeltaTime);
            Vector3 fromCenter = ShapeCenter(body.position, body.rotation);
            Vector3 toCenter = ShapeCenter(position, rotation);
            Vector3 travel = toCenter - fromCenter;
            if (travel.sqrMagnitude > 0.000001f)
            {
                float length = travel.magnitude;
                int count = Physics.BoxCastNonAlloc(fromCenter, ShapeHalfExtents(),
                    travel / length, carryCastHits, body.rotation, length, ~0,
                    QueryTriggerInteraction.Ignore);
                float nearest = length;
                for (int i = 0; i < count; i++)
                {
                    if (!BlocksCarriedBox(carryCastHits[i].collider)) continue;
                    nearest = Mathf.Min(nearest, carryCastHits[i].distance);
                }
                if (nearest < length)
                    position = Vector3.Lerp(body.position, position,
                        Mathf.Clamp01((nearest - 0.02f) / length));
            }

            if (!PoseIsClear(position, rotation))
            {
                if (PoseIsClear(position, body.rotation)) rotation = body.rotation;
                else if (PoseIsClear(body.position, rotation)) position = body.position;
                else { position = body.position; rotation = body.rotation; }
            }
            if (!escapedBoxPile && IsClearOfOtherBoxes(position, rotation))
            {
                escapedBoxPile = true;
                RestoreOtherBoxContacts();
            }
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        private Vector3 ShapeCenter(Vector3 position, Quaternion rotation)
        {
            Vector3 scaled = Vector3.Scale(heldCenterLocal, heldBox.transform.lossyScale);
            return position + rotation * scaled;
        }

        private Vector3 ShapeHalfExtents()
        {
            Vector3 scale = heldShape.transform.lossyScale;
            // An inflated query box overlaps platform rims even when the real
            // collider is clear, blocking both the carry cast and pose check.
            // Keep a small world-space skin so resting contacts can separate.
            Vector3 half = Vector3.Scale(heldShape.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Vector3.Max(half - Vector3.one * 0.005f, Vector3.one * 0.001f);
        }

        private bool BlocksCarriedBox(Collider collider)
        {
            return collider != null && !collider.transform.IsChildOf(heldBox.transform) &&
                !collider.transform.IsChildOf(transform) &&
                !(otherBoxColliders.Contains(collider) && !escapedBoxPile);
        }

        private bool IsClearOfOtherBoxes(Vector3 position, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(ShapeCenter(position, rotation),
                ShapeHalfExtents(), dropHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == dropHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (otherBoxColliders.Contains(dropHits[i])) return false;
            return true;
        }

        private bool PoseIsClear(Vector3 position, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(ShapeCenter(position, rotation),
                ShapeHalfExtents(), dropHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == dropHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (BlocksCarriedBox(dropHits[i])) return false;
            return true;
        }

        private void ReleaseHeldBox(bool playSound)
        {
            if (heldBox == null) return;
            Rigidbody body = heldBox;
            if (!escapedBoxPile && !IsClearOfOtherBoxes(body.position, body.rotation))
            {
                // A cancelled pickup returns to its stable starting pose before
                // box-to-box contacts are restored.
                body.position = pickupPosition;
                body.rotation = pickupRotation;
            }
            Vector3 releasePosition = body.position;
            body.isKinematic = previousKinematic;
            body.useGravity = previousGravity;
            body.detectCollisions = previousDetectCollisions;
            body.collisionDetectionMode = previousCollisionMode;
            body.interpolation = previousInterpolation;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            for (int i = 0; i < heldColliders.Length; i++)
                if (playerCollider != null && heldColliders[i] != null)
                    Physics.IgnoreCollision(playerCollider, heldColliders[i],
                        previousPlayerCollisionIgnored[i]);
            RestoreOtherBoxContacts();
            escapedBoxPile = false;
            heldBox = null;
            heldShape = null;
            heldColliders = null;
            previousPlayerCollisionIgnored = null;
            if (playSound && boxDropSound != null)
                AudioSource.PlayClipAtPoint(boxDropSound, releasePosition, 1f);
        }
    }
}
